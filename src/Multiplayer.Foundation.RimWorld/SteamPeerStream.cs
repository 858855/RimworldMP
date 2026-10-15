using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;
using Steamworks;

namespace Multiplayer.Foundation.RimWorld
{
    // Steam messages carry complete Foundation frames; ReadAsync still exposes a byte stream.
    internal sealed class SteamPeerStream : Stream
    {
        private const int MaxQueuedBytes = 1024 * 1024;
        private readonly HSteamNetConnection handle;
        private readonly Queue<byte[]> received = new Queue<byte[]>();
        private readonly SemaphoreSlim available = new SemaphoreSlim(0);
        private readonly object gate = new object();
        private readonly TaskCompletionSource<bool> connected =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private byte[]? current;
        private int position;
        private int queuedBytes;
        private bool closed;

        internal SteamPeerStream(HSteamNetConnection handle) { this.handle = handle; }
        internal HSteamNetConnection Handle => handle;
        internal Task Connected => connected.Task;
        public override bool CanRead => !closed;
        public override bool CanWrite => !closed;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        internal void MarkConnected() => connected.TrySetResult(true);

        internal void Pump()
        {
            if (closed) return;
            var pointers = new IntPtr[8];
            int count = SteamNetworkingSockets.ReceiveMessagesOnConnection(handle, pointers, pointers.Length);
            if (count < 0) { CloseWithError("Steam receive failed."); return; }
            for (int i = 0; i < count; i++)
            {
                try
                {
                    SteamNetworkingMessage_t message = SteamNetworkingMessage_t.FromIntPtr(pointers[i]);
                    if (message.m_cbSize < FrameCodec.MinimumBodyLength + 4 ||
                        message.m_cbSize > FrameCodec.MaximumBodyLength + 4)
                    { CloseWithError("Invalid Steam frame length."); continue; }
                    byte[] bytes = new byte[message.m_cbSize];
                    Marshal.Copy(message.m_pData, bytes, 0, bytes.Length);
                    lock (gate)
                    {
                        if (closed) continue;
                        if (queuedBytes + bytes.Length > MaxQueuedBytes)
                        { CloseWithError("Steam receive queue full."); continue; }
                        received.Enqueue(bytes);
                        queuedBytes += bytes.Length;
                        available.Release();
                    }
                }
                finally { SteamNetworkingMessage_t.Release(pointers[i]); }
            }
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ValidateBuffer(buffer, offset, count);
            if (count == 0) return 0;
            while (true)
            {
                lock (gate)
                {
                    if (current == null && received.Count > 0)
                    { current = received.Dequeue(); position = 0; }
                    if (current != null)
                    {
                        int size = Math.Min(count, current.Length - position);
                        Buffer.BlockCopy(current, position, buffer, offset, size);
                        position += size;
                        queuedBytes -= size;
                        if (position == current.Length) current = null;
                        return size;
                    }
                    if (closed) return 0;
                }
                await available.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            ValidateBuffer(buffer, offset, count);
            cancellationToken.ThrowIfCancellationRequested();
            if (closed) throw new ObjectDisposedException(nameof(SteamPeerStream));
            if (!connected.Task.IsCompletedSuccessfully) throw new IOException("Steam connection is not ready.");
            if (count > FrameCodec.MaximumBodyLength + 4) throw new IOException("Steam frame too large.");
            IntPtr native = Marshal.AllocHGlobal(count);
            try
            {
                Marshal.Copy(buffer, offset, native, count);
                EResult result = SteamNetworkingSockets.SendMessageToConnection(handle, native, (uint)count,
                    Constants.k_nSteamNetworkingSend_Reliable, out _);
                if (result != EResult.k_EResultOK) throw new IOException("Steam send failed: " + result);
            }
            finally { Marshal.FreeHGlobal(native); }
            return Task.CompletedTask;
        }

        internal void CloseWithError(string reason)
        {
            lock (gate)
            {
                if (closed) return;
                closed = true;
                connected.TrySetException(new IOException(reason));
                available.Release();
            }
            SteamNetworkingSockets.CloseConnection(handle, 0, reason, false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) CloseWithError("Steam connection closed.");
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        private static void ValidateBuffer(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }
    }
}
