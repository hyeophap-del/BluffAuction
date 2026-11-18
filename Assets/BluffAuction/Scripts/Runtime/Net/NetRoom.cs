using System;
using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.UI;
using UnityEngine;

namespace BluffAuction.Net
{
    /// <summary>
    /// 온라인 방. 방장은 대기실(사람·AI 자리, 규칙)을 관리하고 게임을 시작하면 규칙 엔진을 직접 돌려
    /// 좌석마다 그 좌석이 볼 수 있는 상태·사건만 보낸다(비공개 가치는 방장 컴퓨터 밖으로 나가지 않는다).
    /// 참가자는 명령만 보내고 받은 상태를 보여 준다. 게임 중 나간 사람의 자리는 AI 가 이어받는다.
    /// </summary>
    public sealed class NetRoom : MonoBehaviour
    {
        public static NetRoom I { get; private set; }
        public const int MaxSeats = GameRules.MaxSeats;

        public bool IsHost { get; private set; }
        public bool IsClient { get; private set; }
        public bool Active { get { return IsHost || IsClient; } }
        public bool InGame { get; private set; }
        public LobbyMsg Lobby = new LobbyMsg();
        public int MyPeerId { get; private set; }
        public event Action LobbyChanged;

        ITransport transport;
        LanDiscovery discovery;
        readonly Dictionary<int, int> seatOfPeer = new Dictionary<int, int>();
        readonly Dictionary<int, int> peerOfSeat = new Dictionary<int, int>();
        readonly Dictionary<int, int> sentVersion = new Dictionary<int, int>();
        readonly Dictionary<int, float> sentAt = new Dictionary<int, float>();
        readonly Dictionary<int, GameView> seatViews = new Dictionary<int, GameView>();
        readonly HashSet<int> greeted = new HashSet<int>();
        GameSession session;

        public static NetRoom Create(Transform parent)
        {
            var go = new GameObject("NetRoom");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<NetRoom>();
            return I;
        }

        GameApp App { get { return GameApp.I; } }

        // ═════════════════ 방장 ═════════════════

        /// <summary>시험: 방장이 Steam 소켓 IP 포트도 열고, 참가자는 그리로 붙는다 (같은 계정 두 창으로 Steam 전송 계층을 시험).</summary>
        public static bool SteamIpTest { get { return Array.IndexOf(Environment.GetCommandLineArgs(), "-steamnet") >= 0; } }

        public string HostRoom(int port)
        {
            Leave();
            // 같은 네트워크·IP(TCP) 와 Steam 친구(릴레이) 를 한 방에서 같이 받는다. 한쪽만 되면 그쪽만.
            ITransport tcp = null, steam = null;
            string tcpErr = null;
            try { tcp = TcpTransport.Host(port); }
            catch (Exception e) { tcpErr = e.Message; }
            if (Platform.Steam.Ready)
            {
                try { steam = SteamTransport.Host(SteamIpTest); }
                catch (Exception e) { Debug.LogWarning("[Net] steam host failed: " + e.Message); }
            }
            if (tcp == null && steam == null) return tcpErr;
            transport = tcp != null && steam != null ? new MultiTransport(tcp, steam) : (tcp ?? steam);
            IsHost = true;
            MyPeerId = 0;
            var last = SaveData.I.Last;
            Lobby = new LobbyMsg { Rules = CloneRules(last), RoomName = MyName() };
            Lobby.Slots.Add(new LobbySlot { Name = Lobby.RoomName, Look = last.Look, LookJson = SaveData.MyLook.ToJson(), IsHost = true, PeerId = 0, Ready = true });
            transport.PeerConnected += OnPeerConnected;
            transport.PeerDisconnected += OnPeerDisconnected;
            transport.Received += OnReceived;
            if (tcp != null) discovery = LanDiscovery.Advertise(port, () => Lobby.RoomName + "|" + Lobby.Slots.Count + "/" + MaxSeats + "|" + (InGame ? 1 : 0));
            if (steam != null)
            {
                SteamLobbyState = 1;
                Platform.SteamLobby.Create(MaxSeats, Lobby.RoomName, Wire.GameVersion, (lobby, err) =>
                {
                    if (!IsHost) { Platform.SteamLobby.Leave(); return; }
                    SteamLobbyState = lobby != 0 ? 2 : -1;
                    if (err != null) Debug.LogWarning("[Net] steam lobby failed: " + err);
                    Changed(false);
                });
            }
            Changed();
            return null;
        }

        /// <summary>Steam 로비: 0 없음 · 1 만드는 중 · 2 친구가 들어올 수 있음 · -1 실패.</summary>
        public int SteamLobbyState { get; private set; }

        /// <summary>Steam 로비 정보와 친구 목록 상태를 방 상태에 맞춘다.</summary>
        void SyncSteam()
        {
            if (!Platform.Steam.Ready) return;
            int humans = 0;
            foreach (var s in Lobby.Slots) if (!s.IsAI) humans++;
            ulong lobby = Platform.SteamLobby.Current;
            if (IsHost && lobby != 0)
            {
                Platform.SteamLobby.SetData(Platform.SteamLobby.KeyPlayers, Lobby.Slots.Count + "/" + MaxSeats);
                Platform.SteamLobby.SetData(Platform.SteamLobby.KeyInGame, InGame ? "1" : "0");
                Platform.SteamLobby.SetJoinable(!InGame && Lobby.Slots.Count < MaxSeats);
            }
            string status = InGame ? L.T("rp_online", humans) : L.T("rp_lobby", Lobby.Slots.Count, MaxSeats);
            Platform.Steam.Presence(status, InGame ? 0 : lobby, humans);
        }

        static LastSetup CloneRules(LastSetup l)
        {
            return JsonUtility.FromJson<LastSetup>(JsonUtility.ToJson(l));
        }

        void OnPeerConnected(int peer) { }

        void OnPeerDisconnected(int peer)
        {
            greeted.Remove(peer);
            if (InGame)
            {
                int seat;
                if (seatOfPeer.TryGetValue(peer, out seat))
                {
                    seatOfPeer.Remove(peer);
                    peerOfSeat.Remove(seat);
                    if (session != null) session.ReplaceWithAI(seat);
                    UiRoot.I.Toast(L.T("net_left_ai", SeatNameOf(seat)), "toast-alert", 4f);
                }
            }
            int idx = Lobby.Slots.FindIndex(s => s.PeerId == peer);
            if (idx >= 0)
            {
                var slot = Lobby.Slots[idx];
                if (InGame)
                {
                    slot.IsAI = true;
                    slot.Profile = AiProfiles.All[idx % AiProfiles.All.Length].Id;
                    slot.PeerId = -1;
                    slot.Name = L.T("ai_" + slot.Profile);
                }
                else Lobby.Slots.RemoveAt(idx);
                if (!InGame) UiRoot.I.Toast(L.T("net_player_left", slot.Name), null, 3f);
            }
            Changed();
        }

        string SeatNameOf(int seat)
        {
            var v = session != null ? session.View : null;
            return v != null && seat < v.SeatCount ? v.Seats[seat].Name : "?";
        }

        public void AddAI(string profile)
        {
            if (!IsHost || InGame || Lobby.Slots.Count >= MaxSeats) return;
            if (string.IsNullOrEmpty(profile))
            {
                var ids = AiProfiles.Ids();
                foreach (var s in Lobby.Slots) if (s.IsAI) ids.Remove(s.Profile);
                profile = ids.Count > 0 ? ids[UnityEngine.Random.Range(0, ids.Count)] : AiProfiles.All[0].Id;
            }
            Lobby.Slots.Add(new LobbySlot { Name = L.T("ai_" + profile), IsAI = true, Profile = profile, Ready = true, PeerId = -1 });
            Changed();
        }

        public void RemoveSlot(int index)
        {
            if (!IsHost || InGame || index <= 0 || index >= Lobby.Slots.Count) return;
            var s = Lobby.Slots[index];
            if (!s.IsAI && s.PeerId > 0)
            {
                Send(s.PeerId, Msg.Bye, new ByeMsg { Reason = "kicked" });
                transport.Kick(s.PeerId);
            }
            Lobby.Slots.RemoveAt(index);
            Changed();
        }

        public void SetRules(LastSetup r)
        {
            if (!IsHost || InGame) return;
            Lobby.Rules = CloneRules(r);
            Changed();
        }

        public bool CanStart(out string why)
        {
            why = null;
            if (Lobby.Slots.Count < GameRules.MinPlayers) { why = L.T("net_need_players", GameRules.MinPlayers); return false; }
            foreach (var s in Lobby.Slots)
                if (!s.IsAI && !s.IsHost && !s.Ready) { why = L.T("net_wait_ready"); return false; }
            return true;
        }

        public void StartGame()
        {
            string why;
            if (!IsHost || InGame || !CanStart(out why)) return;
            var setup = GameApp.BuildSetup(Lobby.Rules);
            setup.Seats.Clear();
            seatOfPeer.Clear();
            peerOfSeat.Clear();
            sentVersion.Clear();
            sentAt.Clear();
            seatViews.Clear();
            for (int i = 0; i < Lobby.Slots.Count; i++)
            {
                var s = Lobby.Slots[i];
                var info = new SeatInfo { Name = s.Name, IsAI = s.IsAI, AiProfile = s.IsAI ? s.Profile : "", ColorIndex = i };
                info.CharacterId = s.IsAI ? AiProfiles.Get(s.Profile).CharacterId : s.Look;
                info.LookJson = s.IsAI ? "" : (s.LookJson ?? "");
                setup.Seats.Add(info);
                if (s.IsHost) setup.LocalSeat = i;
                else if (!s.IsAI)
                {
                    seatOfPeer[s.PeerId] = i;
                    peerOfSeat[i] = s.PeerId;
                }
            }
            InGame = true;
            foreach (var kv in peerOfSeat) Send(kv.Value, Msg.Start, new StartMsg { Seat = kv.Key, Seats = setup.Seats });
            session = App.Session;
            session.RemoteSeats.Clear();
            foreach (var kv in peerOfSeat) session.RemoteSeats.Add(kv.Key);
            session.RawPumped -= OnPumped;
            session.RawPumped += OnPumped;
            App.StartGame(setup);
            Changed();
        }

        /// <summary>방장: 좌석마다 걸러진 상태·사건을 보낸다 (바뀌었을 때 + 1초마다 시간 맞추기).</summary>
        void OnPumped(List<GameEvent> raw)
        {
            var e = session.Engine;
            if (e == null) return;
            float now = Time.unscaledTime;
            foreach (var kv in peerOfSeat)
            {
                int seat = kv.Key, peer = kv.Value;
                var list = new List<GameEvent>();
                foreach (var ev in raw)
                {
                    GameEvent shown;
                    if (e.TryFilterEvent(ev, seat, out shown)) list.Add(shown);
                }
                int sv;
                float st;
                bool changed = !sentVersion.TryGetValue(seat, out sv) || sv != e.Version;
                bool stale = !sentAt.TryGetValue(seat, out st) || now - st > 1f;
                if (!changed && !stale && list.Count == 0) continue;
                GameView view;
                if (!seatViews.TryGetValue(seat, out view)) seatViews[seat] = view = new GameView();
                e.BuildView(seat, view);
                Send(peer, Msg.View, view);
                if (list.Count > 0) Send(peer, Msg.Event, new EventsMsg { Events = list });
                sentVersion[seat] = e.Version;
                sentAt[seat] = now;
            }
        }

        /// <summary>결과 뒤 모두 대기실로.</summary>
        public void BackToLobby()
        {
            if (!IsHost) return;
            InGame = false;
            if (session != null) session.RawPumped -= OnPumped;
            // 나갔던 사람 자리(AI 로 바뀐 자리)는 대기실에서 정리
            Lobby.Slots.RemoveAll(s => s.IsAI && s.PeerId == -1 && s.Name.Length == 0);
            foreach (var s in Lobby.Slots) if (!s.IsAI && !s.IsHost) s.Ready = false;
            transport.Broadcast(Wire.Pack(Msg.ToLobby, null));
            Changed();
            App.GoLobby();
        }

        // ═════════════════ 참가자 ═════════════════

        public string Join(string address, int port)
        {
            return JoinWith(() => TcpTransport.Connect(address, port));
        }

        /// <summary>Steam 로비(초대·친구 방 목록)로 들어가 방장에게 Steam 릴레이로 붙는다. 실패하면 failed(이유).</summary>
        public void JoinSteamLobby(ulong lobby, Action<string> failed)
        {
            Leave();
            Platform.SteamLobby.Join(lobby, (owner, err) =>
            {
                if (err == null)
                {
                    string ver = Platform.SteamLobby.GetData(lobby, Platform.SteamLobby.KeyVersion);
                    if (ver != Wire.GameVersion) err = "version " + ver + " != " + Wire.GameVersion;
                    else if (Platform.SteamLobby.GetData(lobby, Platform.SteamLobby.KeyInGame) == "1") err = "in game";
                }
                if (err == null) err = JoinWith(() => SteamTransport.Connect(owner), true);
                if (err != null)
                {
                    Platform.SteamLobby.Leave();
                    if (failed != null) failed(err);
                    return;
                }
                App.GoLobby();
            });
        }

        /// <summary>시험용: Steam 소켓으로 IP 에 붙는다 (-steamnet).</summary>
        public string JoinSteamIp(string address)
        {
            return JoinWith(() => SteamTransport.ConnectIp(address));
        }

        string JoinWith(Func<ITransport> connect, bool keepSteamLobby = false)
        {
            if (keepSteamLobby) Shutdown(false);
            else Leave();
            try { transport = connect(); }
            catch (Exception e) { return e.Message; }
            IsClient = true;
            transport.Received += OnReceived;
            transport.Closed += OnClosed;
            var last = SaveData.I.Last;
            Send(0, Msg.Hello, new HelloMsg { Name = MyName(), Look = last.Look, LookJson = SaveData.MyLook.ToJson(), Version = Wire.GameVersion });
            return null;
        }

        /// <summary>내 이름: 직접 쓴 이름 → Steam 이름 → Player.</summary>
        static string MyName()
        {
            var n = SaveData.I.Last.Name;
            if (!string.IsNullOrWhiteSpace(n)) return n.Trim();
            if (Platform.Steam.Ready && !string.IsNullOrWhiteSpace(Platform.Steam.PersonaName)) return Platform.Steam.PersonaName.Trim();
            return "Player";
        }

        public void SetReady(bool ready)
        {
            if (IsClient) Send(0, Msg.Ready, new ReadyMsg { Ready = ready });
        }

        void OnClosed(string why)
        {
            bool wasClient = IsClient;
            Shutdown();
            if (wasClient)
            {
                App.QuitToMenu();
                UiRoot.I.Toast(L.T("net_closed") + (string.IsNullOrEmpty(why) ? "" : " (" + why + ")"), "toast-alert", 5f);
            }
        }

        void SendCmd(int kind, int a, string text, Registration reg)
        {
            Send(0, Msg.Cmd, new CmdMsg { Kind = kind, A = a, Text = text, Reg = reg });
        }

        // ═════════════════ 받기 ═════════════════

        void OnReceived(int peer, byte[] data)
        {
            var type = Wire.TypeOf(data);
            try
            {
                if (IsHost) HostReceive(peer, type, data);
                else ClientReceive(type, data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] bad message " + type + ": " + e.Message);
            }
        }

        void HostReceive(int peer, Msg type, byte[] data)
        {
            switch (type)
            {
                case Msg.Hello:
                {
                    var h = Wire.Unpack<HelloMsg>(data);
                    if (h.Version != Wire.GameVersion) { Refuse(peer, "version " + h.Version + " != " + Wire.GameVersion); return; }
                    if (InGame) { Refuse(peer, "in game"); return; }
                    if (Lobby.Slots.Count >= MaxSeats) { Refuse(peer, "full"); return; }
                    string name = AuctionEngine.Sanitize(h.Name);
                    if (name.Length == 0) name = "Player";
                    if (name.Length > 16) name = name.Substring(0, 16);
                    // 받은 생김새는 범위를 다시 확인해 저장 (이상하면 기본 생김새)
                    var look = h.LookJson != null && h.LookJson.Length < 8000 ? Characters.CharacterLook.FromJson(h.LookJson) : null;
                    Lobby.Slots.Add(new LobbySlot { Name = name, Look = Mathf.Clamp(h.Look, 0, 3), LookJson = look != null ? look.ToJson() : "", PeerId = peer });
                    greeted.Add(peer);
                    Send(peer, Msg.Welcome, new WelcomeMsg { PeerId = peer });
                    UiRoot.I.Toast(L.T("net_player_joined", name), null, 3f);
                    Changed();
                    break;
                }
                case Msg.Ready:
                {
                    var r = Wire.Unpack<ReadyMsg>(data);
                    var s = Lobby.Slots.Find(x => x.PeerId == peer);
                    if (s != null) { s.Ready = r.Ready; Changed(); }
                    break;
                }
                case Msg.Cmd:
                {
                    int seat;
                    if (!InGame || session == null || !seatOfPeer.TryGetValue(peer, out seat)) return;
                    var c = Wire.Unpack<CmdMsg>(data);
                    var res = session.Apply(seat, c.Kind, c.A, c.Text, c.Reg);
                    if (res != Result.Ok) Send(peer, Msg.CmdResult, new CmdResultMsg { Kind = c.Kind, Code = (int)res });
                    break;
                }
                // ── 어울리기: 보낸 사람 좌석은 방장이 채운다 (꾸며 낼 수 없게)
                case Msg.Chat:
                {
                    int seat = SeatOf(peer);
                    if (seat < 0 || !RateOk(peer, 0.35f)) return;
                    var c = Wire.Unpack<ChatMsg>(data);
                    c.Seat = seat;
                    c.Text = SocialHub.Clean(c.Text);
                    if (c.Text.Length == 0) return;
                    Relay(peer, Wire.Pack(Msg.Chat, c));
                    SocialHub.I.OnChat(c);
                    break;
                }
                case Msg.Emote:
                {
                    int seat = SeatOf(peer);
                    if (seat < 0 || !RateOk(peer + 1000, 0.6f)) return;
                    var m = Wire.Unpack<EmoteMsg>(data);
                    m.Seat = seat;
                    Relay(peer, Wire.Pack(Msg.Emote, m));
                    SocialHub.I.OnEmote(m);
                    break;
                }
                case Msg.Throw:
                {
                    int seat = SeatOf(peer);
                    if (seat < 0 || !RateOk(peer + 2000, 0.4f)) return;
                    var m = Wire.Unpack<ThrowMsg>(data);
                    m.Seat = seat;
                    m.Power = Mathf.Clamp(m.Power, 0.3f, 1.5f);
                    Relay(peer, Wire.Pack(Msg.Throw, m));
                    SocialHub.I.OnThrow(m);
                    break;
                }
                case Msg.Melee:
                {
                    int seat = SeatOf(peer);
                    if (seat < 0 || !RateOk(peer + 3000, 0.4f)) return;
                    var m = Wire.Unpack<MeleeMsg>(data);
                    m.Seat = seat;
                    Relay(peer, Wire.Pack(Msg.Melee, m));
                    SocialHub.I.OnMelee(m);
                    break;
                }
                case Msg.Voice:
                {
                    int seat = SeatOf(peer);
                    if (seat < 0 || data.Length < 3 || data.Length > 3 + Adpcm.FrameBytes * 8) return;
                    data[1] = (byte)seat;
                    Relay(peer, data);
                    SocialHub.I.OnVoice(seat, data);
                    break;
                }
                case Msg.ItemImage:
                {
                    int seat;
                    if (!InGame || !seatOfPeer.TryGetValue(peer, out seat)) return;
                    var m = Wire.Unpack<ItemImageMsg>(data);
                    if (AuctionEngine.SellerOf(m.Item) != seat || m.Data == null || m.Data.Length > 900000) return;
                    Relay(peer, Wire.Pack(Msg.ItemImage, m));
                    SocialHub.I.OnItemImage(m);
                    break;
                }
            }
        }

        // ═════════════════ 어울리기 ═════════════════

        readonly Dictionary<int, float> lastSocial = new Dictionary<int, float>();

        bool RateOk(int key, float gap)
        {
            float t;
            if (lastSocial.TryGetValue(key, out t) && Time.unscaledTime - t < gap) return false;
            lastSocial[key] = Time.unscaledTime;
            return true;
        }

        /// <summary>연결 번호의 좌석 (게임 중에는 좌석, 대기실에서는 자리 순서).</summary>
        int SeatOf(int peer)
        {
            int seat;
            if (InGame && seatOfPeer.TryGetValue(peer, out seat)) return seat;
            return Lobby.Slots.FindIndex(s => s.PeerId == peer);
        }

        /// <summary>내 좌석 (게임 중) 또는 대기실 자리 순서.</summary>
        public int MySeat
        {
            get
            {
                if (InGame && App.Session != null && App.Session.Running) return App.Session.LocalSeat;
                return IsHost ? Lobby.Slots.FindIndex(s => s.IsHost) : Lobby.Slots.FindIndex(s => s.PeerId == MyPeerId);
            }
        }

        public void SendSocial(Msg type, object payload)
        {
            if (transport == null) return;
            if (IsHost) Relay(-1, Wire.Pack(type, payload));
            else Send(0, type, payload);
        }

        public void SendRaw(byte[] data)
        {
            if (transport == null) return;
            if (IsHost) Relay(-1, data);
            else transport.Send(0, data);
        }

        void Relay(int except, byte[] data)
        {
            foreach (var p in greeted) if (p != except) transport.Send(p, data);
        }

        void Refuse(int peer, string why)
        {
            Send(peer, Msg.Bye, new ByeMsg { Reason = why });
            transport.Kick(peer);
        }

        void ClientReceive(Msg type, byte[] data)
        {
            switch (type)
            {
                case Msg.Welcome:
                    MyPeerId = Wire.Unpack<WelcomeMsg>(data).PeerId;
                    break;
                case Msg.Lobby:
                    Lobby = Wire.Unpack<LobbyMsg>(data);
                    if (!InGame && !(UiRoot.I.Current is VisualElementTag)) App.GoLobby();
                    Changed(false);
                    break;
                case Msg.Start:
                {
                    var s = Wire.Unpack<StartMsg>(data);
                    InGame = true;
                    App.StartClientGame(s.Seats, s.Seat);
                    App.Session.SendCommand = SendCmd;
                    SyncSteam();
                    break;
                }
                case Msg.View:
                    if (InGame) App.Session.ReceiveView(Wire.Unpack<GameView>(data));
                    break;
                case Msg.Event:
                    if (InGame) App.Session.ReceiveEvents(Wire.Unpack<EventsMsg>(data).Events);
                    break;
                case Msg.CmdResult:
                {
                    var r = Wire.Unpack<CmdResultMsg>(data);
                    UiRoot.I.Toast(L.T("net_cmd_refused") + " (" + (Result)r.Code + ")", "toast-alert", 2.5f);
                    break;
                }
                case Msg.ToLobby:
                    InGame = false;
                    App.GoLobby();
                    SyncSteam();
                    break;
                case Msg.Bye:
                {
                    var b = Wire.Unpack<ByeMsg>(data);
                    OnClosed(b.Reason);
                    break;
                }
                case Msg.Chat: SocialHub.I.OnChat(Wire.Unpack<ChatMsg>(data)); break;
                case Msg.Emote: SocialHub.I.OnEmote(Wire.Unpack<EmoteMsg>(data)); break;
                case Msg.Throw: SocialHub.I.OnThrow(Wire.Unpack<ThrowMsg>(data)); break;
                case Msg.Melee: SocialHub.I.OnMelee(Wire.Unpack<MeleeMsg>(data)); break;
                case Msg.Voice: if (data.Length >= 3) SocialHub.I.OnVoice(data[1], data); break;
                case Msg.ItemImage: SocialHub.I.OnItemImage(Wire.Unpack<ItemImageMsg>(data)); break;
            }
        }

        // ═════════════════ 공통 ═════════════════

        void Send(int peer, Msg type, object payload)
        {
            if (transport != null) transport.Send(peer, Wire.Pack(type, payload));
        }

        void Changed(bool broadcast = true)
        {
            if (IsHost && broadcast && transport != null)
            {
                var data = Wire.Pack(Msg.Lobby, Lobby);
                foreach (var p in greeted) transport.Send(p, data);
            }
            SyncSteam();
            if (LobbyChanged != null) LobbyChanged();
        }

        public void Leave()
        {
            if (transport != null && IsHost && transport.Active) transport.Broadcast(Wire.Pack(Msg.Bye, new ByeMsg { Reason = "host left" }));
            Shutdown();
        }

        void Shutdown(bool leaveSteamLobby = true)
        {
            if (leaveSteamLobby && Platform.SteamLobby.InLobby)
            {
                Platform.SteamLobby.Leave();
                Platform.Steam.Presence(L.T("rp_menu"));
            }
            SteamLobbyState = 0;
            if (session != null) session.RawPumped -= OnPumped;
            session = null;
            if (discovery != null) discovery.Dispose();
            discovery = null;
            if (transport != null) transport.Dispose();
            transport = null;
            IsHost = false;
            IsClient = false;
            InGame = false;
            greeted.Clear();
            seatOfPeer.Clear();
            peerOfSeat.Clear();
        }

        void Update()
        {
            if (transport != null) transport.Poll();
            if (discovery != null) discovery.Tick();
        }

        void OnApplicationQuit() { Leave(); }

        /// <summary>대기실 화면 표시용 표지 (화면을 다시 만들지 않기 위해).</summary>
        public sealed class VisualElementTag : UnityEngine.UIElements.VisualElement { }
    }
}
