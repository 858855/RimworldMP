
// Mirrors the limits of SteamPeerStream (src/Multiplayer.Foundation.RimWorld/SteamPeerStream.cs) without Steam:
// one message per write, a bounded reliable send buffer, a pump that moves at most 8 messages per frame,
// a 1 MiB receive queue that kills the connection when it overflows, and an orderly close that still
// delivers queued messages (CloseConnection with linger), while an error drops them. Real Steam stays untested.
sealed class SteamLikeLink : IDisposable
{
    public const int SendBufferBytes = 512 * 1024;
    public const int ReceiveQueueBytes = 1024 * 1024;
    public const int MessagesPerPump = 8;
    private readonly Direction aToB = new(), bToA = new();
    private readonly CancellationTokenSource stop = new();
    private readonly Task pump;

    public SteamLikeLink()
    {
        A = new Endpoint(bToA, aToB);
        B = new Endpoint(aToB, bToA);
        pump = Task.Run(PumpAsync);
    }

    public Stream A { get; }
    public Stream B { get; }
    public int MaxSendBufferBytes => Math.Max(aToB.MaxInTransit, bToA.MaxInTransit);
    public int MaxReceiveQueueBytes => Math.Max(aToB.MaxQueued, bToA.MaxQueued);
    public string? Failure => aToB.Failure ?? bToA.Failure;

    private async Task PumpAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            aToB.Pump(); bToA.Pump();
            try { await Task.Delay(16, stop.Token); } catch (OperationCanceledException) { }
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        aToB.Close(null); bToA.Close(null);
        pump.Wait(TimeSpan.FromSeconds(2));
    }

    private sealed class Direction
    {
        private readonly object gate = new();
        private readonly Queue<byte[]> inTransit = new(), received = new();
        private readonly SemaphoreSlim available = new(0);
        private int inTransitBytes, queuedBytes;
        private bool finishing;
        private byte[]? current;
        private int position;
        public bool Closed;
        public string? Failure;
        public int MaxInTransit, MaxQueued;

        public void Send(byte[] buffer, int offset, int count)
        {
            lock (gate)
            {
                if (Closed || finishing) throw new ObjectDisposedException("SteamLikeLink");
                if (inTransitBytes + count > SendBufferBytes)
                {
                    Close("Steam send failed: k_EResultLimitExceeded");
                    throw new IOException(Failure);
                }
                inTransit.Enqueue(buffer.AsSpan(offset, count).ToArray());
                inTransitBytes += count;
                MaxInTransit = Math.Max(MaxInTransit, inTransitBytes);
            }
        }

        public void Pump()
        {
            lock (gate)
            {
                for (int i = 0; i < MessagesPerPump && inTransit.Count > 0 && !Closed; i++)
                {
                    byte[] message = inTransit.Dequeue();
                    inTransitBytes -= message.Length;
                    if (queuedBytes + message.Length > ReceiveQueueBytes) { Close("Steam receive queue full."); return; }
                    received.Enqueue(message);
                    queuedBytes += message.Length;
                    MaxQueued = Math.Max(MaxQueued, queuedBytes);
                    available.Release();
                }
                if (finishing && inTransit.Count == 0) Close(null);
            }
        }

        /// <summary>Orderly close with linger: deliver what is already in transit, then end the stream.</summary>
        public void Finish()
        {
            lock (gate)
            {
                if (Closed) return;
                finishing = true;
                if (inTransit.Count == 0) Close(null);
            }
        }

        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            while (true)
            {
                lock (gate)
                {
                    if (current == null && received.Count > 0) { current = received.Dequeue(); position = 0; }
                    if (current != null)
                    {
                        int size = Math.Min(count, current.Length - position);
                        Buffer.BlockCopy(current, position, buffer, offset, size);
                        position += size; queuedBytes -= size;
                        if (position == current.Length) current = null;
                        return size;
                    }
                    if (Closed) return 0;
                }
                await available.WaitAsync(token);
            }
        }

        public void Close(string? reason)
        {
            lock (gate)
            {
                if (Closed) return;
                Closed = true; Failure ??= reason;
                available.Release();
            }
        }
    }

    private sealed class Endpoint : Stream
    {
        private readonly Direction incoming, outgoing;
        public Endpoint(Direction incoming, Direction outgoing) { this.incoming = incoming; this.outgoing = outgoing; }
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => incoming.ReadAsync(buffer, offset, count, token);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { outgoing.Send(buffer, offset, count); return Task.CompletedTask; }
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, default).GetAwaiter().GetResult();
        public override void Write(byte[] buffer, int offset, int count) => outgoing.Send(buffer, offset, count);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { incoming.Close(null); outgoing.Finish(); }
            base.Dispose(disposing);
        }
    }
}
