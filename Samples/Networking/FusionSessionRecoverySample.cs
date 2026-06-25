using System;
using System.Collections.Generic;

namespace PortfolioSamples.Networking
{
    public enum SessionState
    {
        Disconnected,
        Connecting,
        Lobby,
        InGame,
        MigratingHost,
        Rejoining,
        Failed
    }

    public sealed class FusionSessionRecoverySample
    {
        private readonly List<string> _pendingRebinds = new();

        public SessionState State { get; private set; } = SessionState.Disconnected;
        public string LastRoomCode { get; private set; }
        public int LocalPlayerId { get; private set; }

        public void EnterRoom(string roomCode, int localPlayerId)
        {
            LastRoomCode = roomCode;
            LocalPlayerId = localPlayerId;
            State = SessionState.Connecting;
        }

        public void OnRoomJoined()
        {
            State = SessionState.Lobby;
        }

        public void OnGameStarted(IEnumerable<string> networkObjectsToRebind)
        {
            _pendingRebinds.Clear();
            _pendingRebinds.AddRange(networkObjectsToRebind);
            State = SessionState.InGame;
        }

        public void OnHostMigrationStarted()
        {
            if (State != SessionState.InGame)
                return;

            State = SessionState.MigratingHost;
        }

        public void OnHostMigrationCompleted(Action<string> rebindObject)
        {
            if (State != SessionState.MigratingHost)
                return;

            foreach (string objectId in _pendingRebinds)
                rebindObject?.Invoke(objectId);

            State = SessionState.InGame;
        }

        public void OnConnectionLost()
        {
            State = string.IsNullOrEmpty(LastRoomCode) ? SessionState.Failed : SessionState.Rejoining;
        }

        public bool TryBuildRejoinRequest(out string roomCode, out int playerId)
        {
            roomCode = LastRoomCode;
            playerId = LocalPlayerId;
            return State == SessionState.Rejoining && !string.IsNullOrEmpty(roomCode);
        }
    }
}

