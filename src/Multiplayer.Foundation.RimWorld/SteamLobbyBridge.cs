using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Multiplayer.Foundation.Hosting;
using Multiplayer.Foundation.Network;
using Multiplayer.Foundation.Protocol;
using Steamworks;
using UnityEngine;

namespace Multiplayer.Foundation.RimWorld
{
    // Steam callbacks and receive polling stay on Unity's main thread.
    internal sealed class SteamLobbyBridge : IDisposable
    {
        private readonly Dictionary<HSteamNetConnection, SteamPeerStream> streams =
            new Dictionary<HSteamNetConnection, SteamPeerStream>();
        private readonly Callback<GameLobbyJoinRequested_t> inviteCallback;
        private readonly Callback<SteamNetConnectionStatusChangedCallback_t> connectionCallback;
        private readonly CallResult<LobbyCreated_t> lobbyCreated;
        private readonly CallResult<LobbyEnter_t> lobbyEntered;
        private readonly GameObject pumpObject;
        private HSteamListenSocket listenSocket = HSteamListenSocket.Invalid;
        private CSteamID lobbyId;
        private ListenHost? host;
        private TaskCompletionSource<CSteamID>? createResult;
        private TaskCompletionSource<CSteamID>? joinResult;
        private bool disposed;

        internal SteamLobbyBridge(Action<CSteamID> invitationReceived)
        {
            if (!SteamAPI.IsSteamRunning() || !SteamUser.GetSteamID().IsValid())
                throw new InvalidOperationException("Steam ist nicht für RimWorld verfügbar.");
            InviteReceived = invitationReceived;
            SteamNetworkingUtils.InitRelayNetworkAccess();
            inviteCallback = Callback<GameLobbyJoinRequested_t>.Create(data => InviteReceived(data.m_steamIDLobby));
            connectionCallback = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnectionChanged);
            lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            lobbyEntered = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);
            pumpObject = new GameObject("Multiplayer Foundation Steam callbacks");
            UnityEngine.Object.DontDestroyOnLoad(pumpObject);
            pumpObject.AddComponent<SteamCallbackPump>().Bridge = this;
        }

        private Action<CSteamID> InviteReceived { get; }
        internal bool IsHosting => host != null && lobbyId.IsValid();
        internal string LobbyCode => lobbyId.IsValid() ? ((ulong)lobbyId).ToString() : "";

        internal async Task StartHostingAsync(ListenHost runningHost, string name, byte capacity)
        {
            if (disposed || host != null || lobbyId.IsValid())
                throw new InvalidOperationException("Steam lobby is already active.");
            host = runningHost;
            listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(0, 0,
                Array.Empty<SteamNetworkingConfigValue_t>());
            if (listenSocket == HSteamListenSocket.Invalid)
            { host = null; throw new IOException("Steam P2P listener could not start."); }
            createResult = new TaskCompletionSource<CSteamID>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, capacity), OnLobbyCreated);
                if (await Task.WhenAny(createResult.Task, Task.Delay(TimeSpan.FromSeconds(20))) != createResult.Task)
                    throw new TimeoutException("Steam lobby creation timed out.");
                CSteamID created = await createResult.Task;
                lobbyId = created;
                SteamMatchmaking.SetLobbyData(created, "foundation_version", WireVersion);
                SteamMatchmaking.SetLobbyData(created, "name", name);
            }
            catch { Leave(); throw; }
            finally { createResult = null; }
        }

        // The Steam lobby tag follows the wire version, so mismatched builds fail before the P2P handshake.
        private static readonly string WireVersion = FrameCodec.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);

        internal async Task<LobbyClient> JoinAsync(CSteamID invitedLobby, string playerName)
        {
            if (disposed || host != null || lobbyId.IsValid())
                throw new InvalidOperationException("Leave the current Steam lobby first.");
            joinResult = new TaskCompletionSource<CSteamID>(TaskCreationOptions.RunContinuationsAsynchronously);
            SteamPeerStream? stream = null;
            try
            {
                lobbyEntered.Set(SteamMatchmaking.JoinLobby(invitedLobby), OnLobbyEntered);
                if (await Task.WhenAny(joinResult.Task, Task.Delay(TimeSpan.FromSeconds(20))) != joinResult.Task)
                    throw new TimeoutException("Steam lobby join timed out.");
                CSteamID joined = await joinResult.Task;
                lobbyId = joined;
                if (SteamMatchmaking.GetLobbyData(joined, "foundation_version") != WireVersion)
                    throw new IOException("Incompatible Steam lobby (different Multiplayer Foundation version).");
                CSteamID owner = SteamMatchmaking.GetLobbyOwner(joined);
                if (!owner.IsValid() || owner == SteamUser.GetSteamID())
                    throw new IOException("Steam lobby host is unavailable.");
                var identity = new SteamNetworkingIdentity();
                identity.SetSteamID(owner);
                HSteamNetConnection handle = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0,
                    Array.Empty<SteamNetworkingConfigValue_t>());
                if (handle == HSteamNetConnection.Invalid)
                    throw new IOException("Steam P2P connection could not start.");
                stream = new SteamPeerStream(handle);
                streams.Add(handle, stream);
                Task finished = await Task.WhenAny(stream.Connected, Task.Delay(TimeSpan.FromSeconds(20)));
                if (finished != stream.Connected) throw new TimeoutException("Steam P2P connection timed out.");
                await stream.Connected;
                // The host verifies this claim against the SteamID Steam authenticated for the connection.
                return await LobbyClient.ConnectOverStreamAsync(stream, playerName, TimeSpan.FromSeconds(10),
                    CancellationToken.None, PlayerKey.Steam((ulong)SteamUser.GetSteamID())).ConfigureAwait(false);
            }
            catch { stream?.Dispose(); Leave(); throw; }
            finally { joinResult = null; }
        }

        internal void OpenInviteDialog()
        {
            if (!IsHosting) throw new InvalidOperationException("No Steam lobby is hosted.");
            SteamFriends.ActivateGameOverlayInviteDialog(lobbyId);
        }

        internal void Leave()
        {
            if (lobbyId.IsValid()) SteamMatchmaking.LeaveLobby(lobbyId);
            lobbyId = default;
            if (listenSocket != HSteamListenSocket.Invalid)
                SteamNetworkingSockets.CloseListenSocket(listenSocket);
            listenSocket = HSteamListenSocket.Invalid;
            foreach (SteamPeerStream stream in streams.Values.ToArray()) stream.Dispose();
            streams.Clear();
            host = null;
        }

        internal void Pump()
        {
            if (disposed) return;
            SteamAPI.RunCallbacks();
            SteamNetworkingSockets.RunCallbacks();
            foreach (SteamPeerStream stream in streams.Values.ToArray()) stream.Pump();
        }

        private void OnLobbyCreated(LobbyCreated_t result, bool failed)
        {
            if (createResult == null && !failed && result.m_eResult == EResult.k_EResultOK)
            { SteamMatchmaking.LeaveLobby((CSteamID)result.m_ulSteamIDLobby); return; }
            if (failed || result.m_eResult != EResult.k_EResultOK)
                createResult?.TrySetException(new IOException("Steam lobby creation failed: " + result.m_eResult));
            else createResult?.TrySetResult((CSteamID)result.m_ulSteamIDLobby);
        }

        private void OnLobbyEntered(LobbyEnter_t result, bool failed)
        {
            if (joinResult == null && !failed && result.m_EChatRoomEnterResponse == 1)
            { SteamMatchmaking.LeaveLobby((CSteamID)result.m_ulSteamIDLobby); return; }
            if (failed || result.m_EChatRoomEnterResponse != 1)
                joinResult?.TrySetException(new IOException("Steam lobby join failed: " + result.m_EChatRoomEnterResponse));
            else joinResult?.TrySetResult((CSteamID)result.m_ulSteamIDLobby);
        }

        private void OnConnectionChanged(SteamNetConnectionStatusChangedCallback_t change)
        {
            HSteamNetConnection handle = change.m_hConn;
            if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting &&
                change.m_info.m_hListenSocket == listenSocket && host != null && lobbyId.IsValid())
            {
                if (streams.ContainsKey(handle)) return;
                CSteamID remote = change.m_info.m_identityRemote.GetSteamID();
                bool member = Enumerable.Range(0, SteamMatchmaking.GetNumLobbyMembers(lobbyId))
                    .Any(index => SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, index) == remote);
                if (!member || SteamNetworkingSockets.AcceptConnection(handle) != EResult.k_EResultOK)
                { SteamNetworkingSockets.CloseConnection(handle, 0, "Not a lobby member.", false); return; }
                var stream = new SteamPeerStream(handle);
                streams.Add(handle, stream);
                // Steam authenticated `remote`; the guest's Hello must claim exactly this SteamID.
                if (!host.AttachConnection(NetworkConnection.FromDuplexStream(stream), PlayerKey.Steam((ulong)remote)))
                    streams.Remove(handle);
            }
            else if (streams.TryGetValue(handle, out SteamPeerStream? stream))
            {
                if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
                    stream.MarkConnected();
                else if (change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
                    change.m_info.m_eState == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
                { stream.CloseWithError("Steam peer disconnected."); streams.Remove(handle); }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            Leave(); disposed = true;
            inviteCallback.Dispose(); connectionCallback.Dispose();
            lobbyCreated.Dispose(); lobbyEntered.Dispose();
            UnityEngine.Object.Destroy(pumpObject);
        }
    }

    public sealed class SteamCallbackPump : MonoBehaviour
    {
        internal SteamLobbyBridge? Bridge;
        private void Update()
        {
            // During shutdown RimWorld closes Steam before this object is destroyed.
            if (!Verse.Steam.SteamManager.Initialized) return;
            try { Bridge?.Pump(); }
            catch (Exception error)
            {
                Debug.LogError("Multiplayer Foundation Steam callback failed: " + error);
                Bridge?.Leave();
            }
        }
        private void OnDestroy() => Bridge?.Dispose();
    }
}
