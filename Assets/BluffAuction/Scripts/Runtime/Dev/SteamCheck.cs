using System.Collections;
using BluffAuction.Game;
using BluffAuction.Net;
using BluffAuction.Platform;
using BluffAuction.UI;
using UnityEngine;

namespace BluffAuction.Dev
{
    /// <summary>
    /// -scenario steam: Steam 연동 확인 (Steam 이 켜져 있어야 한다). 초기화 · 업적 왕복(켰다가 되돌림) · 친구 목록 ·
    /// 방 만들기 → Steam 로비 · 초대 창을 기록하고 찍는다. 이미 딴 업적은 건드리지 않는다.
    /// </summary>
    public static class SteamCheck
    {
        public static IEnumerator Run(GameApp app)
        {
            yield return new WaitForSeconds(2f);
            AutoShots.Log("steam ready " + Steam.Ready + " · test app " + Steam.IsTestApp + " · " + (Steam.Ready ? Steam.PersonaName + " (" + Steam.MySteamId + ")" : Steam.InitError));
            if (!Steam.Ready)
            {
                app.GoOnline();
                yield return new WaitForSeconds(1.2f);
                yield return AutoShots.Capture("steam_off_online");
                Application.Quit();
                yield break;
            }

            // 업적: 통계는 Steam 이 알아서 내려 주므로 잠깐 기다린다
            string r = "";
            for (int i = 0; i < 20; i++)
            {
                r = Steam.TestAchievementRoundTrip("ACH_WIN_ONE_GAME");
                if (!r.Contains("not found")) break;
                yield return new WaitForSeconds(0.5f);
            }
            AutoShots.Log("achievement round trip: " + r);

            var friends = SteamLobby.Friends();
            AutoShots.Log("friends " + friends.Count + " · online " + friends.FindAll(f => f.Online).Count + " · playing " + friends.FindAll(f => f.Playing).Count);

            app.GoOnline();
            yield return new WaitForSeconds(2.5f);
            yield return AutoShots.Capture("steam_online");

            var room = NetRoom.I;
            var err = room.HostRoom(TcpTransport.DefaultPort);
            AutoShots.Log("host: " + (err ?? "ok"));
            if (err != null) { Application.Quit(); yield break; }
            app.GoLobby();
            float t0 = Time.unscaledTime;
            while (room.SteamLobbyState == 1 && Time.unscaledTime - t0 < 15f) yield return null;
            ulong lobby = SteamLobby.Current;
            AutoShots.Log("steam lobby state " + room.SteamLobbyState + " id " + lobby + " in " + (Time.unscaledTime - t0).ToString("0.0") + "s"
                + " · ver=" + SteamLobby.GetData(lobby, SteamLobby.KeyVersion) + " name=" + SteamLobby.GetData(lobby, SteamLobby.KeyName)
                + " players=" + SteamLobby.GetData(lobby, SteamLobby.KeyPlayers) + " ingame=" + SteamLobby.GetData(lobby, SteamLobby.KeyInGame)
                + " · overlay " + SteamLobby.OverlayAvailable);
            room.AddAI(null);
            yield return new WaitForSeconds(1f);
            AutoShots.Log("after AI: players=" + SteamLobby.GetData(lobby, SteamLobby.KeyPlayers));
            yield return AutoShots.Capture("steam_lobby");
            SteamInvitePanel.Open();
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("steam_invite");
            UiRoot.I.CloseTop();

            room.Leave();
            yield return new WaitForSeconds(0.5f);
            AutoShots.Log("left: lobby " + SteamLobby.Current);
            app.GoTitle();
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
    }
}
