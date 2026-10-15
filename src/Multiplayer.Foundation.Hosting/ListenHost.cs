using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Multiplayer.Foundation.Server;

namespace Multiplayer.Foundation.Hosting
{
    /// <summary>Runs the same authoritative lobby server inside a client process.</summary>
    public sealed class ListenHost : IAsyncDisposable
    {
        private readonly FoundationServer server;
        private int stopped;

        private ListenHost(FoundationServer server, LobbyClient client, string bindAddress)
        {
            this.server = server;
            Client = client;
            BindAddress = bindAddress;
        }

        public LobbyClient Client { get; }
        public string BindAddress { get; }
        /// <summary>Debug: this host's server lets it start the shared setup alone.</summary>
        public bool AllowSoloStart { get; private set; }
        public int Port => server.BoundPort;
        public bool AcceptsIPv4 => server.AcceptsIPv4;
        public bool AcceptsIPv6 => server.AcceptsIPv6;
        public bool AttachConnection(NetworkConnection connection, PlayerKey? verifiedKey = null) =>
            server.AttachConnection(connection, verifiedKey);

        public static async Task<ListenHost> StartAsync(string bindAddress, int port, string playerName,
            string lobbyName, byte capacity, PlayerKey? playerKey = null, bool allowSoloStart = false)
        {
            if (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 40 || playerName.Any(char.IsControl))
                throw new ArgumentException("Player name must have 1-40 characters.", nameof(playerName));
            if (string.IsNullOrWhiteSpace(lobbyName) || lobbyName.Length > 40 || lobbyName.Any(char.IsControl))
                throw new ArgumentException("Lobby name must have 1-40 characters.", nameof(lobbyName));
            if (capacity < 2 || capacity > 64)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            var config = new HostConfig { BindAddress = bindAddress, Port = port,
                MaxPlayersPerLobby = capacity, AllowSoloStart = allowSoloStart };
            var server = new FoundationServer(config);
            LobbyClient? client = null;
            try
            {
                await server.StartAsync().ConfigureAwait(false);
                client = await LobbyClient.ConnectAsync(server.LoopbackAddress.ToString(), server.BoundPort, playerName,
                    TimeSpan.FromSeconds(5), CancellationToken.None, playerKey).ConfigureAwait(false);
                await client.SendAsync(MessageType.CreateLobby, 2,
                    LobbyPayloads.Create(lobbyName, capacity)).ConfigureAwait(false);
                return new ListenHost(server, client, bindAddress) { AllowSoloStart = allowSoloStart };
            }
            catch
            {
                client?.Dispose();
                await server.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0) return;
            try { await Client.DisconnectAsync().ConfigureAwait(false); }
            finally { await server.DisposeAsync().ConfigureAwait(false); }
        }

        public ValueTask DisposeAsync() => new ValueTask(StopAsync());
    }
}
