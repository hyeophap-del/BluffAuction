using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    // ═════════════════ 온라인: 방 만들기 / 참가하기 ═════════════════

    public sealed class OnlineScreen
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        readonly LanDiscovery lan;
        readonly VisualElement roomList, steamList;
        readonly Label status;
        readonly TextField address;
        int ticks;

        public OnlineScreen(GameApp app)
        {
            this.app = app;
            Root = Ui.Col("dim-bg");
            Root.style.paddingLeft = 110;
            Root.style.paddingTop = 70;
            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("back"), () => { lan.Dispose(); app.GoTitle(); }, "btn-ghost"));
            var t = Ui.Text(L.T("net_title"), "h2");
            t.style.marginLeft = 20;
            head.Add(t);
            Root.Add(head);

            var body = Ui.Row();
            body.style.alignItems = Align.FlexStart;
            body.style.marginTop = 30;
            Root.Add(body);

            var left = Ui.Col();
            left.Add(TitleScreen.MenuButton(L.T("net_host"), L.T("net_host_sub"), Host));
            var nameRow = Ui.Row("option-row");
            nameRow.style.width = 440;
            nameRow.Add(Ui.Text(L.T("setup_name"), "option-label"));
            var nameField = new TextField { value = SaveData.I.Last.Name, maxLength = 16 };
            nameField.textEdition.placeholder = L.T("name_placeholder");
            nameField.style.width = 280;
            nameField.RegisterValueChangedCallback(e => { SaveData.I.Last.Name = e.newValue; SaveData.Save(); });
            nameRow.Add(nameField);
            left.Add(nameRow);
            var lookRow = Ui.Row("option-row");
            lookRow.style.width = 440;
            lookRow.Add(Ui.Text(L.T("setup_look"), "option-label"), Ui.Div("spacer"), Ui.Portrait(SaveData.MyLook, 56));
            var cz = Ui.Btn(L.T("cz_open"), () => { lan.Dispose(); app.GoCustomize(() => app.GoOnline()); }, "btn-small");
            cz.style.marginLeft = 10;
            lookRow.Add(cz);
            left.Add(lookRow);
            body.Add(left);

            var right = Ui.Col("panel");
            right.style.width = 620;
            right.style.marginLeft = 40;
            right.Add(Ui.Text(L.T("net_join"), "h3"));
            right.Add(Ui.Text(L.T("net_join_sub"), "small", "wrap"));
            // Steam 친구가 연 방 (초대 없이도 바로 들어간다)
            if (Platform.Steam.Ready)
            {
                var sh = Ui.Row();
                sh.Add(Ui.Text(L.T("steam_friends"), "small"), Ui.Div("spacer"), Ui.Text(L.T("steam_on", Platform.Steam.PersonaName), "small"));
                right.Add(sh);
                steamList = Ui.Col();
                steamList.style.minHeight = 50;
                right.Add(steamList);
                right.Add(Ui.Div("divider"));
            }
            else right.Add(Ui.Text(L.T("steam_off"), "small", "wrap"));
            right.Add(Ui.Text(L.T("net_rooms"), "small"));
            roomList = Ui.Col();
            roomList.style.minHeight = 140;
            right.Add(roomList);
            right.Add(Ui.Div("divider"));
            var row = Ui.Row();
            row.Add(Ui.Text(L.T("net_address"), "option-label"));
            address = new TextField { value = PlayerPrefs.GetString("last_address", "127.0.0.1") };
            address.style.width = 300;
            address.style.marginLeft = 10;
            row.Add(address);
            row.Add(Ui.Btn(L.T("net_connect"), () => Join(address.value), "btn-small"));
            right.Add(row);
            status = Ui.Text("", "small", "wrap");
            right.Add(status);
            body.Add(right);

            lan = LanDiscovery.Listen();
            Root.schedule.Execute(RefreshRooms).Every(500);
            Root.RegisterCallback<DetachFromPanelEvent>(e => lan.Dispose());
        }

        void RefreshSteam()
        {
            if (steamList == null) return;
            steamList.Clear();
            int n = 0;
            foreach (var f in Platform.SteamLobby.Friends())
            {
                if (f.Lobby == 0) continue;
                var lobby = f.Lobby;
                var b = Ui.Btn(f.Name + "   ·   " + L.T("steam_join"), () => JoinSteam(lobby), "btn-secondary");
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                steamList.Add(b);
                n++;
            }
            if (n == 0) steamList.Add(Ui.Text(L.T("steam_no_friends"), "small"));
        }

        void JoinSteam(ulong lobby)
        {
            status.text = L.T("net_connecting");
            // 붙으면 NetRoom 이 대기실로 넘긴다 (이 화면이 닫히며 LAN 찾기도 멈춘다)
            NetRoom.I.JoinSteamLobby(lobby, err => status.text = L.T("net_connect_failed", err));
        }

        void RefreshRooms()
        {
            if (ticks++ % 4 == 0) RefreshSteam();
            lan.Tick();
            roomList.Clear();
            if (lan.Rooms.Count == 0)
            {
                roomList.Add(Ui.Text(L.T("net_no_rooms"), "small"));
                return;
            }
            foreach (var r in lan.Rooms.Values)
            {
                var parts = (r.Info ?? "").Split('|');
                string label = (parts.Length > 0 ? parts[0] : "?") + "   " + (parts.Length > 1 ? parts[1] : "") + (parts.Length > 2 && parts[2] == "1" ? "  ▶" : "");
                var room = r;
                var b = Ui.Btn(label + "   (" + r.Address + ")", () => Join(room.Address + ":" + room.Port), "btn-secondary");
                b.style.unityTextAlign = TextAnchor.MiddleLeft;
                roomList.Add(b);
            }
        }

        void Host()
        {
            var err = NetRoom.I.HostRoom(TcpTransport.DefaultPort);
            if (err != null) { status.text = L.T("net_host_failed", err); return; }
            lan.Dispose();
            app.GoLobby();
        }

        void Join(string addr)
        {
            addr = (addr ?? "").Trim();
            int port = TcpTransport.DefaultPort;
            int colon = addr.LastIndexOf(':');
            if (colon > 0)
            {
                int.TryParse(addr.Substring(colon + 1), out port);
                addr = addr.Substring(0, colon);
            }
            if (addr.Length == 0) return;
            status.text = L.T("net_connecting");
            PlayerPrefs.SetString("last_address", addr);
            var err = NetRoom.I.Join(addr, port);
            if (err != null) { status.text = L.T("net_connect_failed", err); return; }
            lan.Dispose();
            app.GoLobby();
        }
    }

    // ═════════════════ 대기실 ═════════════════

    public sealed class LobbyScreen
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        readonly VisualElement slots;
        readonly VisualElement rules;
        readonly Button start, ready, invite;
        readonly Label hint, steamState;
        bool myReady;

        public LobbyScreen(GameApp app)
        {
            this.app = app;
            var room = NetRoom.I;
            Root = new NetRoom.VisualElementTag();
            Root.AddToClassList("dim-bg");
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.paddingLeft = 80;
            Root.style.paddingRight = 80;
            Root.style.paddingTop = 50;
            Root.style.paddingBottom = 40;

            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("net_leave"), () => { room.Leave(); app.GoTitle(); }, "btn-ghost"));
            var t = Ui.Text(L.T("net_lobby"), "h2");
            t.style.marginLeft = 20;
            head.Add(t);
            if (room.IsHost)
            {
                var addrs = TcpTransport.LocalAddresses();
                var a = Ui.Text(L.T("net_host_addr", string.Join(", ", addrs.ToArray())) + "  :" + TcpTransport.DefaultPort, "sub");
                a.style.marginLeft = 30;
                head.Add(a);
            }
            // Steam 친구 초대 (방장, 또는 Steam 로비로 들어온 참가자)
            if (Platform.Steam.Ready)
            {
                head.Add(Ui.Div("spacer"));
                steamState = Ui.Text("", "small");
                steamState.style.marginRight = 12;
                head.Add(steamState);
                invite = Ui.Btn(L.T("steam_invite"), SteamInvitePanel.Open, "btn-small");
                head.Add(invite);
            }
            Root.Add(head);
            if (room.IsHost) Root.Add(Ui.Text(Platform.Steam.Ready ? L.T("steam_lobby_hint", TcpTransport.DefaultPort) : L.T("net_port_hint", TcpTransport.DefaultPort), "small"));

            var body = Ui.Row();
            body.style.flexGrow = 1;
            body.style.alignItems = Align.Stretch;
            body.style.marginTop = 16;
            Root.Add(body);

            var left = Ui.Col("panel");
            left.style.flexGrow = 1;
            left.style.marginRight = 20;
            var lh = Ui.Row();
            lh.Add(Ui.Text(L.T("players"), "h3"), Ui.Div("spacer"));
            if (room.IsHost) lh.Add(Ui.Btn(L.T("net_add_ai"), () => room.AddAI(null), "btn-small"));
            left.Add(lh);
            slots = Ui.Col();
            left.Add(slots);
            left.Add(Ui.Div("spacer"));
            // 대기실 채팅 (Enter) — 마이크도 여기서 켤 수 있다
            var lobbyChat = new ChatBox(i => i >= 0 && i < room.Lobby.Slots.Count ? room.Lobby.Slots[i].Name : "?",
                i => World.Stage.SeatColors[((i % World.Stage.SeatColors.Length) + World.Stage.SeatColors.Length) % World.Stage.SeatColors.Length], true);
            lobbyChat.Root.style.width = Length.Percent(100);
            left.Add(lobbyChat.Root);
            var talk = Ui.Btn(L.T("chat_open"), () => lobbyChat.OpenInput(), "btn-small", "btn-ghost");
            talk.style.alignSelf = Align.FlexStart;
            talk.style.marginTop = 6;
            left.Add(talk);
            body.Add(left);

            var right = Ui.Col("panel");
            right.style.width = 560;
            right.Add(Ui.Text(L.T("setup_rules"), "h3"));
            rules = Ui.Col();
            right.Add(rules);
            right.Add(Ui.Div("spacer"));
            hint = Ui.Text("", "small", "center-text", "wrap");
            right.Add(hint);
            if (room.IsHost)
            {
                start = Ui.Btn(L.T("net_start"), () => room.StartGame(), "btn-big");
                right.Add(start);
            }
            else
            {
                ready = Ui.Btn(L.T("net_ready"), () => { myReady = !myReady; room.SetReady(myReady); Refresh(); }, "btn-big");
                right.Add(ready);
            }
            body.Add(right);

            room.LobbyChanged += Refresh;
            Root.RegisterCallback<DetachFromPanelEvent>(e => room.LobbyChanged -= Refresh);
            BuildRules();
            Refresh();
        }

        void BuildRules()
        {
            var room = NetRoom.I;
            rules.Clear();
            var r = room.Lobby.Rules;
            if (room.IsHost)
            {
                rules.Add(SetupScreen.Stepper(L.T("setup_items"), () => r.Items.ToString(), d => { r.Items = Mathf.Clamp(r.Items + d, 2, 4); room.SetRules(r); }));
                rules.Add(SetupScreen.Stepper(L.T("setup_bidtime"), () => L.T("seconds", r.BidSeconds), d => { r.BidSeconds = Mathf.Clamp(r.BidSeconds + d * 5, 15, 90); room.SetRules(r); }));
                rules.Add(SetupScreen.Stepper(L.T("setup_extend"), () => r.Extend == 0 ? L.T("off") : L.T("seconds", r.Extend), d => { r.Extend = Mathf.Clamp(r.Extend + d, 0, 10); room.SetRules(r); }));
                rules.Add(SetupScreen.Stepper(L.T("setup_difficulty"), () => L.T("diff_" + r.Difficulty), d => { r.Difficulty = Mathf.Clamp(r.Difficulty + d, 0, 2); room.SetRules(r); }));
                rules.Add(SetupScreen.Toggle(L.T("setup_loan"), () => r.Loan, v => { r.Loan = v; room.SetRules(r); }));
                rules.Add(SetupScreen.Toggle(L.T("setup_blind"), () => r.Blind, v => { r.Blind = v; room.SetRules(r); }));
            }
            else
            {
                rules.Add(Ui.Text(L.T("net_rules_by_host"), "small"));
                rules.Add(Line(L.T("setup_items"), r.Items.ToString()));
                rules.Add(Line(L.T("setup_bidtime"), L.T("seconds", r.BidSeconds)));
                rules.Add(Line(L.T("setup_extend"), r.Extend == 0 ? L.T("off") : L.T("seconds", r.Extend)));
                rules.Add(Line(L.T("setup_difficulty"), L.T("diff_" + r.Difficulty)));
                rules.Add(Line(L.T("setup_loan"), r.Loan ? L.T("on") : L.T("off")));
                rules.Add(Line(L.T("setup_blind"), r.Blind ? L.T("on") : L.T("off")));
            }
        }

        static VisualElement Line(string label, string value)
        {
            var row = Ui.Row("option-row");
            row.Add(Ui.Text(label, "option-label"), Ui.Text(value, "num"));
            return row;
        }

        void Refresh()
        {
            var room = NetRoom.I;
            if (!room.IsHost) BuildRules();
            slots.Clear();
            for (int i = 0; i < room.Lobby.Slots.Count; i++)
            {
                var s = room.Lobby.Slots[i];
                int idx = i;
                var row = Ui.Row("player-row");
                row.Add(Ui.Portrait(GameApp.LookOf(s), 54));
                var nc = Ui.Col();
                nc.style.flexGrow = 1;
                nc.style.marginLeft = 10;
                bool me = (room.IsHost && s.IsHost) || (!room.IsHost && s.PeerId == room.MyPeerId);
                nc.Add(Ui.Text(s.Name + (me ? "  ★" : ""), "player-name"));
                string tag = s.IsHost ? L.T("net_host_tag") : (s.IsAI ? "AI" : (s.Ready ? "✓ " + L.T("net_ready") : L.T("net_not_ready")));
                nc.Add(Ui.Text(tag, "player-meta"));
                row.Add(nc);
                if (!s.IsAI && !me && SocialHub.I != null)
                {
                    var mute = new Button();
                    mute.AddToClassList("mute-btn");
                    mute.EnableInClassList("mute-on", SocialHub.I.Muted.Contains(idx));
                    Ui.Wire(mute, () => { SocialHub.I.ToggleMute(idx); Refresh(); }, "ui_toggle");
                    row.Add(mute);
                }
                if (room.IsHost && i > 0) row.Add(Ui.Btn(L.T("net_remove"), () => room.RemoveSlot(idx), "btn-small", "btn-ghost"));
                slots.Add(row);
            }
            if (invite != null)
            {
                bool can = room.IsHost ? room.SteamLobbyState == 2 : Platform.SteamLobby.InLobby;
                invite.SetEnabled(can);
                steamState.text = room.SteamLobbyState == 1 ? L.T("steam_lobby_wait") : (room.SteamLobbyState == -1 ? L.T("steam_lobby_failed") : (can ? L.T("steam_lobby_ready") : ""));
            }
            string why;
            bool ok = room.CanStart(out why);
            hint.text = ok ? "" : why;
            // 참가자: 방 정보가 오기 전 (Steam 릴레이 연결은 몇 초 걸린다)
            if (!room.IsHost && room.Lobby.Slots.Count == 0) hint.text = L.T("net_connecting");
            if (start != null) start.SetEnabled(ok);
            if (ready != null) Ui.SetText(ready, myReady ? "✓ " + L.T("net_ready") : L.T("net_ready"));
            app.SeatLobby();
        }
    }

    // ═════════════════ Steam 친구 초대 ═════════════════

    /// <summary>Steam 친구 목록에서 골라 지금 방(Steam 로비)으로 초대한다. 오버레이가 켜져 있으면 Steam 초대 창도 쓸 수 있다.</summary>
    public static class SteamInvitePanel
    {
        public static void Open()
        {
            var panel = Ui.Col("panel");
            panel.style.width = 600;
            panel.Add(Ui.Text(L.T("steam_invite_title"), "h2"));
            var list = new ScrollView();
            list.style.maxHeight = 440;
            list.style.minHeight = 120;
            list.style.marginTop = 10;
            var friends = Platform.SteamLobby.Friends();
            if (friends.Count == 0) list.Add(Ui.Text(L.T("steam_invite_none"), "small"));
            foreach (var f in friends)
            {
                var row = Ui.Row("player-row");
                var nc = Ui.Col();
                nc.style.flexGrow = 1;
                nc.Add(Ui.Text(f.Name, "player-name"));
                nc.Add(Ui.Text(f.Playing ? L.T("steam_ingame") : (f.Online ? L.T("steam_online") : L.T("steam_offline")), "player-meta"));
                row.Add(nc);
                ulong id = f.Id;
                Button b = null;
                b = Ui.Btn(L.T("steam_invite_send"), () =>
                {
                    if (!Platform.SteamLobby.Invite(id)) return;
                    Ui.SetText(b, L.T("steam_invite_sent"));
                    b.SetEnabled(false);
                }, "btn-small");
                row.Add(b);
                if (!f.Online) row.style.opacity = 0.55f;
                list.Add(row);
            }
            panel.Add(list);
            var bottom = Ui.Row();
            bottom.style.marginTop = 14;
            VisualElement wrap = null;
            var overlay = Ui.Btn(L.T("steam_invite_overlay"), () => Platform.SteamLobby.OpenInviteOverlay(), "btn-small", "btn-ghost");
            overlay.SetEnabled(Platform.SteamLobby.OverlayAvailable);
            bottom.Add(overlay);
            bottom.Add(Ui.Div("spacer"));
            bottom.Add(Ui.Btn(L.T("close"), () => UiRoot.I.Close(wrap), "btn-small"));
            panel.Add(bottom);
            wrap = UiRoot.I.Open(panel);
        }
    }
}
