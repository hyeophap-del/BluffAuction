using System;
using System.Collections.Generic;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace BluffAuction.Platform
{
    /// <summary>
    /// Steam 로비 = 방 주소. 방장이 친구 전용 로비를 만들면 친구는 초대·친구 목록 「게임 참가」·온라인 화면의 친구 방 목록으로
    /// 들어온다. 실제 게임 메시지는 로비가 아니라 Net.SteamTransport(Steam 릴레이)로 로비 주인에게 직접 오간다.
    /// </summary>
    public static class SteamLobby
    {
        public const string KeyGame = "bluff", KeyVersion = "ver", KeyName = "name", KeyPlayers = "players", KeyInGame = "ingame";

        public static ulong Current { get; private set; }
        public static bool InLobby { get { return Current != 0; } }

        public struct Friend
        {
            public ulong Id;
            public string Name;
            public bool Online;
            /// <summary>이 게임을 하는 중.</summary>
            public bool Playing;
            /// <summary>들어갈 수 있는 로비 (없으면 0).</summary>
            public ulong Lobby;
        }

#if STEAMWORKS_NET
        static CallResult<LobbyCreated_t> createCall;
        static CallResult<LobbyEnter_t> joinCall;
#endif

        /// <summary>방장: 친구 전용 로비를 만든다. done(로비, 오류).</summary>
        public static void Create(int maxMembers, string roomName, string version, Action<ulong, string> done)
        {
#if STEAMWORKS_NET
            if (!Steam.Ready) { done(0, "steam off"); return; }
            Leave();
            if (createCall == null) createCall = CallResult<LobbyCreated_t>.Create();
            createCall.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers), (r, ioFail) =>
            {
                if (ioFail || r.m_eResult != EResult.k_EResultOK) { done(0, ioFail ? "io" : r.m_eResult.ToString()); return; }
                Current = r.m_ulSteamIDLobby;
                var id = new CSteamID(Current);
                SteamMatchmaking.SetLobbyData(id, KeyGame, "1");
                SteamMatchmaking.SetLobbyData(id, KeyVersion, version);
                SteamMatchmaking.SetLobbyData(id, KeyName, roomName ?? "");
                Steam.Log("lobby created " + Current);
                done(Current, null);
            });
#else
            done(0, "steam off");
#endif
        }

        /// <summary>참가자: 로비에 들어가 주인(방장)의 SteamID 를 얻는다. done(주인, 오류).</summary>
        public static void Join(ulong lobby, Action<ulong, string> done)
        {
#if STEAMWORKS_NET
            if (!Steam.Ready) { done(0, "steam off"); return; }
            Leave();
            if (joinCall == null) joinCall = CallResult<LobbyEnter_t>.Create();
            joinCall.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobby)), (r, ioFail) =>
            {
                if (ioFail || r.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    done(0, ioFail ? "io" : ((EChatRoomEnterResponse)r.m_EChatRoomEnterResponse).ToString());
                    return;
                }
                Current = r.m_ulSteamIDLobby;
                ulong owner = SteamMatchmaking.GetLobbyOwner(new CSteamID(Current)).m_SteamID;
                Steam.Log("lobby joined " + Current + " owner " + owner);
                if (owner == Steam.MySteamId) { Leave(); done(0, "own room"); return; }
                done(owner, null);
            });
#else
            done(0, "steam off");
#endif
        }

        public static void Leave()
        {
#if STEAMWORKS_NET
            if (Current != 0 && Steam.Ready) SteamMatchmaking.LeaveLobby(new CSteamID(Current));
#endif
            Current = 0;
        }

        /// <summary>방장: 로비 정보 (인원·게임 중) 를 갱신한다.</summary>
        public static void SetData(string key, string value)
        {
#if STEAMWORKS_NET
            if (Current != 0 && Steam.Ready) SteamMatchmaking.SetLobbyData(new CSteamID(Current), key, value ?? "");
#endif
        }

        public static string GetData(ulong lobby, string key)
        {
#if STEAMWORKS_NET
            if (lobby != 0 && Steam.Ready) return SteamMatchmaking.GetLobbyData(new CSteamID(lobby), key) ?? "";
#endif
            return "";
        }

        /// <summary>방장: 게임이 시작되면 새로 들어올 수 없게.</summary>
        public static void SetJoinable(bool joinable)
        {
#if STEAMWORKS_NET
            if (Current != 0 && Steam.Ready) SteamMatchmaking.SetLobbyJoinable(new CSteamID(Current), joinable);
#endif
        }

        public static bool Invite(ulong friend)
        {
#if STEAMWORKS_NET
            if (Current == 0 || !Steam.Ready) return false;
            return SteamMatchmaking.InviteUserToLobby(new CSteamID(Current), new CSteamID(friend));
#else
            return false;
#endif
        }

        public static bool OverlayAvailable
        {
            get
            {
#if STEAMWORKS_NET
                return Steam.Ready && SteamUtils.IsOverlayEnabled();
#else
                return false;
#endif
            }
        }

        /// <summary>Steam 오버레이의 친구 초대 창 (오버레이가 꺼져 있으면 아무 일도 없다).</summary>
        public static bool OpenInviteOverlay()
        {
#if STEAMWORKS_NET
            if (Current == 0 || !Steam.Ready || !SteamUtils.IsOverlayEnabled()) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(Current));
            return true;
#else
            return false;
#endif
        }

        /// <summary>친구 목록 (이 게임 중인 친구 → 온라인 → 오프라인 순, 이름순).</summary>
        public static List<Friend> Friends()
        {
            var list = new List<Friend>();
#if STEAMWORKS_NET
            if (!Steam.Ready) return list;
            uint app = SteamUtils.GetAppID().m_AppId;
            int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < n; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var f = new Friend
                {
                    Id = id.m_SteamID,
                    Name = SteamFriends.GetFriendPersonaName(id) ?? "?",
                    Online = SteamFriends.GetFriendPersonaState(id) != EPersonaState.k_EPersonaStateOffline
                };
                FriendGameInfo_t game;
                if (SteamFriends.GetFriendGamePlayed(id, out game) && game.m_gameID.AppID().m_AppId == app)
                {
                    f.Playing = true;
                    if (game.m_steamIDLobby.IsValid()) f.Lobby = game.m_steamIDLobby.m_SteamID;
                }
                list.Add(f);
            }
            list.Sort((a, b) =>
            {
                int ra = a.Playing ? 0 : (a.Online ? 1 : 2), rb = b.Playing ? 0 : (b.Online ? 1 : 2);
                return ra != rb ? ra.CompareTo(rb) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
#endif
            return list;
        }
    }
}
