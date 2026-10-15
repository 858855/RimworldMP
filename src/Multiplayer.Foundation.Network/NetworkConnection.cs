using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    public sealed class NetworkConnection : IDisposable
    {
        private readonly IDisposable owner;
        private readonly Stream stream;
        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim receiveGate = new SemaphoreSlim(1, 1);
        private int closed;

        private NetworkConnection(TcpClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            client.NoDelay = true;
            owner = client;
            stream = client.GetStream();
        }

        private NetworkConnection(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead || !stream.CanWrite)
                throw new ArgumentException("A duplex stream is required.", nameof(stream));
            owner = stream;
        }

        public bool IsClosed => Volatile.Read(ref closed) != 0;

        public static NetworkConnection FromAcceptedClient(TcpClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            return new NetworkConnection(client);
        }

        public static NetworkConnection FromDuplexStream(Stream stream) => new NetworkConnection(stream);

        public static async Task<NetworkConnection> ConnectAsync(string host, int port, TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host is required.", nameof(host));
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            DateTime deadline = DateTime.UtcNow + timeout;
            IPAddress[] addresses = await ResolveAsync(host.Trim(), timeout, cancellationToken).ConfigureAwait(false);
            // Try every resolved address in resolver order (IPv6 and IPv4), sharing the remaining time.
            Exception? lastError = null;
            for (int i = 0; i < addresses.Length; i++)
            {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) break;
                TimeSpan attempt = TimeSpan.FromTicks(remaining.Ticks / (addresses.Length - i));
                try
                {
                    return await ConnectToAsync(addresses[i], port, attempt, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException error) { lastError = error; }
                catch (SocketException error) { lastError = error; }
            }
            if (lastError is SocketException) throw lastError;
            throw new TimeoutException("TCP connection timed out.");
        }

        private static async Task<IPAddress[]> ResolveAsync(string host, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            string literal = host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal)
                ? host.Substring(1, host.Length - 2) : host;
            if (IPAddress.TryParse(literal, out IPAddress? parsed)) return new[] { parsed };
            Task<IPAddress[]> lookup = Dns.GetHostAddressesAsync(host);
            Task delay = Task.Delay(timeout, cancellationToken);
            if (await Task.WhenAny(lookup, delay).ConfigureAwait(false) != lookup)
            {
                Observe(lookup);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Host name lookup timed out.");
            }
            IPAddress[] usable = (await lookup.ConfigureAwait(false))
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork || a.AddressFamily == AddressFamily.InterNetworkV6)
                .ToArray();
            if (usable.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            return usable;
        }

        private static async Task<NetworkConnection> ConnectToAsync(IPAddress address, int port, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var client = new TcpClient(address.AddressFamily);
            try
            {
                Task connect = client.ConnectAsync(address, port);
                Task delay = Task.Delay(timeout, cancellationToken);
                if (await Task.WhenAny(connect, delay).ConfigureAwait(false) != connect)
                {
                    Observe(connect);
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException("TCP connection timed out.");
                }
                await connect.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return new NetworkConnection(client);
            }
            catch
            {
                client.Close();
                throw;
            }
        }

        private static void Observe(Task task) =>
            task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

        public async Task SendAsync(WireMessage message, CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            await sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfClosed();
                await FrameCodec.WriteAsync(stream, message, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException) { Dispose(); throw; }
            catch (ObjectDisposedException) { Dispose(); throw; }
            finally { sendGate.Release(); }
        }

        public async Task<WireMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfClosed();
            await receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfClosed();
                WireMessage? message = await FrameCodec.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
                if (message == null) Dispose();
                return message;
            }
            catch (IOException) { Dispose(); throw; }
            catch (ObjectDisposedException) { Dispose(); throw; }
            finally { receiveGate.Release(); }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref closed, 1) == 0)
                owner.Dispose();
        }

        private void ThrowIfClosed()
        {
            if (IsClosed) throw new ObjectDisposedException(nameof(NetworkConnection));
        }
    }
}
