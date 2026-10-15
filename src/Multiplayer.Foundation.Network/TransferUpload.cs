using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    /// <summary>
    /// Sends one <see cref="TransferSource"/> and paces chunks by receiver acknowledgements (XFER-001).
    /// Acks and aborts arrive on the caller's message loop and are handed in with <see cref="OnAck"/> and <see cref="OnAbort"/>.
    /// </summary>
    public sealed class TransferUpload
    {
        private readonly TransferSource source;
        private readonly Func<WireMessage, Task> send;
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly object gate = new object();
        private string? abortReason;

        public TransferUpload(TransferSource source, Func<WireMessage, Task> send)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.send = send ?? throw new ArgumentNullException(nameof(send));
        }

        public TransferHeader Header => source.Header;

        public void OnAck(Guid receiver, uint received)
        {
            lock (gate) source.Acknowledge(receiver, received);
            signal.Release();
        }

        public void OnAbort(string reason)
        {
            lock (gate) abortReason ??= reason;
            signal.Release();
        }

        /// <summary>Sends Begin and all chunks; completes when every receiver confirmed the last chunk.</summary>
        public async Task RunAsync(TimeSpan stallTimeout, CancellationToken cancellationToken = default)
        {
            await send(new WireMessage(MessageType.TransferBegin, 0, TransferPayloads.Begin(source.Header))).ConfigureAwait(false);
            while (true)
            {
                lock (gate)
                {
                    if (abortReason != null) throw new IOException("Transfer aborted: " + abortReason);
                    if (source.IsComplete) return;
                }
                while (true)
                {
                    byte[] payload;
                    lock (gate) { if (!source.TryTakeChunk(out _, out payload)) break; }
                    await send(new WireMessage(MessageType.TransferChunk, 0, payload)).ConfigureAwait(false);
                }
                if (!await signal.WaitAsync(stallTimeout, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        await send(new WireMessage(MessageType.TransferAbort, 0,
                            TransferPayloads.Abort(source.Header.TransferId, "Keine Bestätigung vom Empfänger."))).ConfigureAwait(false);
                    }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { }
                    throw new TimeoutException("Transfer stalled: no acknowledgement.");
                }
            }
        }
    }
}
