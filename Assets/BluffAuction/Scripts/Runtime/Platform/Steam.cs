using System;
using System.Collections.Generic;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace BluffAuction.Platform
{
    /// <summary>
    /// Steam 연동 (Steamworks.NET, Packages/com.rlabrecque.steamworks.net). Steam 이 꺼져 있거나 실패하면 Ready=false 로
    /// 조용히 넘어가고, 게임은 Steam 없이도 그대로 돈다 (같은 네트워크·IP 온라인, 게임 안 업적).
    /// 앱 번호를 받기 전에는 Valve 시험 앱 Spacewar(480)로 돌린다: 그 앱에 있는 업적·통계 이름으로 바꿔 시험한다.
    /// </summary>
    public static class Steam
    {
        /// <summary>Steamworks 앱 번호. 등록하면 바꾼다 (BuildScript 가 480 일 때만 steam_appid.txt 를 실행 파일 옆에 둔다).</summary>
        public const uint AppId = 480;
        public static bool IsTestApp { get { return AppId == 480; } }

        public static bool Ready { get; private set; }
        public static string InitError { get; private set; }
        public static string PersonaName { get; private set; }
        public static ulong MySteamId { get; private set; }

        /// <summary>초대 수락·친구 목록 「게임 참가」로 들어온 Steam 로비. 받는 쪽이 없으면 보관했다가 TakePendingLobby 로 꺼낸다.</summary>
        public static event Action<ulong> JoinRequested;
        static ulong pendingLobby;

#if STEAMWORKS_NET
        static Callback<GameLobbyJoinRequested_t> lobbyJoinCb;
        static Callback<GameRichPresenceJoinRequested_t> presenceJoinCb;
#endif

        public static void Init()
        {
            InitError = "";
            PersonaName = "";
#if STEAMWORKS_NET
            try
            {
                // 정식 앱은 Steam 밖에서 바로 켜면 Steam 으로 다시 띄운다 (steam_appid.txt 가 있으면 그냥 넘어간다)
                if (!IsTestApp && SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
                {
                    Application.Quit();
                    return;
                }
                string err;
                var res = SteamAPI.InitEx(out err);
                Ready = res == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
                if (!Ready)
                {
                    InitError = res + (string.IsNullOrEmpty(err) ? "" : " " + err);
                    Log("init failed: " + InitError);
                    return;
                }
                PersonaName = SteamFriends.GetPersonaName() ?? "";
                MySteamId = SteamUser.GetSteamID().m_SteamID;
                // P2P 릴레이 경로를 미리 잡아 둔다 (방에 들어갈 때 덜 기다리게)
                SteamNetworkingUtils.InitRelayNetworkAccess();
                lobbyJoinCb = Callback<GameLobbyJoinRequested_t>.Create(e => Request(e.m_steamIDLobby.m_SteamID));
                presenceJoinCb = Callback<GameRichPresenceJoinRequested_t>.Create(e => Request(LobbyFromConnect(e.m_rgchConnect)));
                pendingLobby = LobbyFromArgs(Environment.GetCommandLineArgs());
                Log("ready: " + PersonaName + " · app " + SteamUtils.GetAppID().m_AppId + (pendingLobby != 0 ? " · invite " + pendingLobby : ""));
            }
            catch (Exception e)
            {
                // steam_api64.dll 이 없거나 Steam 이 꺼져 있으면 여기로 온다
                Ready = false;
                InitError = e.GetType().Name + ": " + e.Message;
                Log("init failed: " + InitError);
            }
#else
            InitError = "STEAMWORKS_NET off";
#endif
        }

        public static void Tick()
        {
#if STEAMWORKS_NET
            if (Ready) SteamAPI.RunCallbacks();
#endif
        }

        public static void Shutdown()
        {
#if STEAMWORKS_NET
            if (Ready)
            {
                SteamLobby.Leave();
                SteamAPI.Shutdown();
            }
#endif
            Ready = false;
        }

        // ═════════════════ 초대 ═════════════════

        static void Request(ulong lobby)
        {
            if (lobby == 0) return;
            Log("join requested " + lobby);
            if (JoinRequested != null) JoinRequested(lobby);
            else pendingLobby = lobby;
        }

        /// <summary>게임을 켤 때 받은 초대(+connect_lobby). 타이틀이 뜬 뒤 한 번 꺼낸다.</summary>
        public static ulong TakePendingLobby()
        {
            var l = pendingLobby;
            pendingLobby = 0;
            return l;
        }

        static ulong LobbyFromArgs(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                ulong id;
                if (args[i] == "+connect_lobby" && ulong.TryParse(args[i + 1], out id)) return id;
            }
            return 0;
        }

        static ulong LobbyFromConnect(string connect)
        {
            return string.IsNullOrEmpty(connect) ? 0 : LobbyFromArgs(connect.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        // ═════════════════ 업적 · 통계 ═════════════════

        /// <summary>게임 안 업적 id → Steam 업적 이름. 시험 앱(Spacewar)에는 그 앱에 있는 것만 잇는다.</summary>
        static string AchievementName(string id)
        {
            if (IsTestApp) return id == "first_win" ? "ACH_WIN_ONE_GAME" : null;
            return "ACH_" + id.ToUpperInvariant();
        }

        static string StatName(string stat)
        {
            if (IsTestApp) return stat == "games" ? "NumGames" : (stat == "wins" ? "NumWins" : null);
            return stat;
        }

        /// <summary>자동 시험은 진짜 Steam 계정의 업적·통계를 건드리지 않는다.</summary>
        static bool Blocked { get { return !Ready || Dev.AutoShots.Requested; } }

        public static void Unlock(string id)
        {
            var name = AchievementName(id);
#if STEAMWORKS_NET
            if (Blocked || name == null) { Log("achievement " + id + " (not sent)"); return; }
            bool ok = SteamUserStats.SetAchievement(name);
            SteamUserStats.StoreStats();
            Log("achievement " + id + " -> " + name + " " + ok);
#else
            Log("achievement " + id);
#endif
        }

        /// <summary>한 판이 끝날 때: 판 수 +1, 이겼으면 승리 수 +1 (Steam 쪽 값에 더한다 — 여러 컴퓨터에서 해도 합쳐진다).</summary>
        public static void AddGameResult(bool won)
        {
#if STEAMWORKS_NET
            if (Blocked) return;
            Increment(StatName("games"));
            if (won) Increment(StatName("wins"));
            SteamUserStats.StoreStats();
#endif
        }

#if STEAMWORKS_NET
        static void Increment(string stat)
        {
            int cur;
            if (stat != null && SteamUserStats.GetStat(stat, out cur)) SteamUserStats.SetStat(stat, cur + 1);
        }
#endif

        /// <summary>시작할 때: Steam 연동 전에 게임 안에서 딴 업적도 Steam 에 올린다.</summary>
        public static void SyncAchievements(IEnumerable<string> ids)
        {
#if STEAMWORKS_NET
            if (Blocked) return;
            bool any = false;
            foreach (var id in ids)
            {
                var name = AchievementName(id);
                bool got;
                if (name == null || !SteamUserStats.GetAchievement(name, out got) || got) continue;
                any |= SteamUserStats.SetAchievement(name);
            }
            if (any) SteamUserStats.StoreStats();
#endif
        }

        /// <summary>시험용: 업적 하나를 켰다가 원래대로 돌린다 (이미 딴 업적은 건드리지 않는다). 결과를 글로 돌려준다.</summary>
        public static string TestAchievementRoundTrip(string steamName)
        {
#if STEAMWORKS_NET
            if (!Ready) return "not ready";
            bool before;
            if (!SteamUserStats.GetAchievement(steamName, out before)) return steamName + ": not found (stats not loaded yet?)";
            if (before) return steamName + ": already unlocked on this account (left as is)";
            bool set = SteamUserStats.SetAchievement(steamName);
            bool after;
            SteamUserStats.GetAchievement(steamName, out after);
            bool cleared = SteamUserStats.ClearAchievement(steamName);
            bool stored = SteamUserStats.StoreStats();
            return steamName + ": set " + set + " -> achieved " + after + ", cleared " + cleared + ", stored " + stored;
#else
            return "STEAMWORKS_NET off";
#endif
        }

        // ═════════════════ 친구 목록 상태 ═════════════════

        /// <summary>친구 목록에 보이는 상태. lobby 가 있으면 친구가 「게임 참가」로 바로 들어올 수 있다.</summary>
        public static void Presence(string status, ulong lobby = 0, int players = 0)
        {
#if STEAMWORKS_NET
            if (!Ready) return;
            SteamFriends.SetRichPresence("status", status ?? "");
            SteamFriends.SetRichPresence("connect", lobby != 0 ? "+connect_lobby " + lobby : "");
            // 같은 방 친구들을 친구 목록에서 한데 묶는다
            SteamFriends.SetRichPresence("steam_player_group", lobby != 0 ? lobby.ToString() : "");
            SteamFriends.SetRichPresence("steam_player_group_size", lobby != 0 ? players.ToString() : "");
#endif
        }

        internal static void Log(string line)
        {
            Debug.Log("[Steam] " + line);
            Dev.AutoShots.Log("steam: " + line);
        }
    }
}
