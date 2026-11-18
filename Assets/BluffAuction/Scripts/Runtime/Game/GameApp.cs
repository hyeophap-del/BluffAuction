using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Core;
using BluffAuction.Loc;
using BluffAuction.UI;
using BluffAuction.World;
using UnityEngine;

namespace BluffAuction.Game
{
    /// <summary>
    /// 게임 전체 흐름: 타이틀 → 새 게임 → 한 판(등록·경매·공개) → 결과 → 다시/메뉴.
    /// 경매장·카메라·UI·소리·진행을 한곳에서 잇는다.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }
        public Hall Hall;
        public Stage Stage;
        public CameraRig Rig;
        public PostFx Fx;
        public UiRoot Ui;
        public AudioManager Audio;
        public GameSession Session;
        public Presenter Presenter;
        public GameScreen GameScreen;
        public Table.TableKit TableKit;
        public Table.Throwables Throwables;
        public SocialHub Social;
        GameSetup lastSetup;

        public static GameApp Create(Transform parent, Hall hall, Stage stage, CameraRig rig, PostFx fx)
        {
            var go = new GameObject("GameApp");
            go.transform.SetParent(parent, false);
            var app = go.AddComponent<GameApp>();
            I = app;
            app.Hall = hall;
            app.Stage = stage;
            app.Rig = rig;
            app.Fx = fx;
            app.Audio = AudioManager.Create(go.transform);
            app.Ui = UiRoot.Create(go.transform);
            app.Session = go.AddComponent<GameSession>();
            app.Presenter = go.AddComponent<Presenter>();
            app.Presenter.Init(app);
            Net.NetRoom.Create(go.transform);
            app.Social = SocialHub.Create(app);
            app.ApplySettings();
            return app;
        }

        // ═════════════════ 설정 적용 ═════════════════

        public void ApplySettings()
        {
            var s = SaveData.I.Settings;
            L.Set(s.Language >= 0 ? (Lang)s.Language : L.FromSystem());
            QualitySettings.SetQualityLevel(Mathf.Clamp(s.Quality, 0, QualitySettings.names.Length - 1), true);
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            if (Fx != null) Fx.SetQuality(s.Dof && s.Quality > 0, s.Grain, true);
            if (Rig != null) Rig.Intensity = s.CameraMotion;
            if (Ui != null) Ui.ApplyScale();
            Session.AiSpeed = s.AiSpeed;
        }

        public void ApplyDisplay()
        {
            var s = SaveData.I.Settings;
            var mode = s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            int w = s.ResWidth > 0 ? s.ResWidth : Screen.currentResolution.width;
            int h = s.ResHeight > 0 ? s.ResHeight : Screen.currentResolution.height;
            if (!s.Fullscreen && s.ResWidth <= 0)
            {
                w = Mathf.RoundToInt(Screen.currentResolution.width * 0.8f);
                h = Mathf.RoundToInt(Screen.currentResolution.height * 0.8f);
            }
            Screen.SetResolution(w, h, mode);
        }

        // ═════════════════ 화면 이동 ═════════════════

        public void GoTitle()
        {
            if (Net.NetRoom.I != null && Net.NetRoom.I.Active) Net.NetRoom.I.Leave();
            if (Social != null) { Social.ClearMutes(); Social.SetMicTest(false); }
            Session.End();
            Presenter.Detach();
            GameScreen = null;
            EndFirstPerson();
            SeatAmbient();
            Rig.Go(Shot.Menu, 2f);
            Audio.PlayMusic("bgm_menu");
            Audio.PlayAmbience("amb_room");
            Ui.SetScreen(new TitleScreen(this).Root);
            Platform.Steam.Presence(L.T("rp_menu"));
        }

        /// <summary>Steam 초대 수락 (오버레이·친구 목록 「게임 참가」·켤 때 받은 초대): 하던 판이 있으면 묻고 그 방으로 간다.</summary>
        public void AcceptSteamInvite(ulong lobby)
        {
            if (lobby == 0) return;
            System.Action go = () =>
            {
                QuitToMenu();
                Ui.Toast(L.T("net_connecting"), null, 3f);
                Net.NetRoom.I.JoinSteamLobby(lobby, err => Ui.Toast(L.T("net_connect_failed", err), "toast-alert", 5f));
            };
            bool busy = Session.Running || (Net.NetRoom.I != null && Net.NetRoom.I.Active);
            if (busy) Ui.Confirm(L.T("steam_invite_confirm"), L.T("steam_invite_confirm_body"), L.T("steam_join"), go, L.T("cancel"));
            else go();
        }

        /// <summary>메뉴 배경: 손님 몇 명이 앉아 있고 물건이 돈다.</summary>
        void SeatAmbient()
        {
            var looks = new List<CharacterLook> { SaveData.MyLook };
            var colors = new List<int> { 0 };
            var ids = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            var rng = new System.Random();
            for (int i = 0; i < 5; i++)
            {
                int j = rng.Next(ids.Count);
                looks.Add(CharacterLook.Guests[ids[j]]);
                colors.Add(i + 1);
                ids.RemoveAt(j);
            }
            Stage.Seat(looks, colors);
            Stage.Guests[0].gameObject.SetActive(false);
            Stage.ShowItem(rng.Next(ItemCount));
            Stage.AllLookAt(Stage.ItemPoint);
        }

        const int ItemCount = 12;

        /// <summary>좌석의 생김새: 사람은 꾸민 생김새(없으면 기본 넷 중 하나), AI 는 성격마다 정해진 것.</summary>
        public static CharacterLook LookOf(SeatInfo s)
        {
            if (s.IsAI && string.IsNullOrEmpty(s.LookJson)) return CharacterLook.Guests[((s.CharacterId % CharacterLook.Guests.Length) + CharacterLook.Guests.Length) % CharacterLook.Guests.Length];
            var custom = CharacterLook.FromJson(s.LookJson);
            return custom ?? CharacterLook.Players[((s.CharacterId % CharacterLook.Players.Length) + CharacterLook.Players.Length) % CharacterLook.Players.Length];
        }

        public static CharacterLook LookOf(Net.LobbySlot s)
        {
            if (s.IsAI) return CharacterLook.Guests[AiProfiles.Get(s.Profile).CharacterId];
            return CharacterLook.FromJson(s.LookJson) ?? CharacterLook.Players[Mathf.Clamp(s.Look, 0, CharacterLook.Players.Length - 1)];
        }

        /// <summary>한 판 동안 1인칭: 내 자리 눈높이에서 고개를 돌려 보고, 앞 탁자에 카드·장부·도구를 놓는다.</summary>
        void BeginFirstPerson()
        {
            EndFirstPerson();
            float seatR = Hall.SeatR;
            Rig.Eye = new Vector3(0, 1.36f, -seatR + 0.13f);
            Rig.BaseYaw = 0f;
            Rig.TableRadiusNow = Hall.TableR;
            var s = SaveData.I.Settings;
            Rig.Sensitivity = s.Sensitivity;
            Rig.InvertY = s.InvertY;
            Rig.LookTowards(0f, -14f, true);
            Rig.FirstPerson = true;
            TableKit = Table.TableKit.Create(this);
            Throwables = Table.Throwables.Create(this);
            var interactor = Rig.GetComponent<Table.Interactor>();
            if (interactor == null) interactor = Rig.gameObject.AddComponent<Table.Interactor>();
            interactor.Cam = Rig.Cam;
            interactor.LookMode = () => Rig.MouseLook;
            interactor.Enabled = true;
        }

        void EndFirstPerson()
        {
            if (Rig != null)
            {
                Rig.FirstPerson = false;
                Rig.MouseLook = false;
                var interactor = Rig.GetComponent<Table.Interactor>();
                if (interactor != null) interactor.Enabled = false;
            }
            if (TableKit != null) Destroy(TableKit.gameObject);
            TableKit = null;
            if (Throwables != null) Destroy(Throwables.gameObject);
            Throwables = null;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        public void GoSetup() { Ui.SetScreen(new SetupScreen(this).Root); }

        public void GoOnline() { Ui.SetScreen(new OnlineScreen(this).Root); }

        /// <summary>온라인 대기실 (방장·참가자). 한 판이 끝난 뒤에도 여기로 돌아온다.</summary>
        public void GoLobby()
        {
            if (Ui.Current is Net.NetRoom.VisualElementTag) return;
            Session.End();
            Presenter.Detach();
            GameScreen = null;
            EndFirstPerson();
            Rig.Go(Shot.Wide, 1.5f);
            Audio.PlayMusic("bgm_menu");
            Ui.SetScreen(new LobbyScreen(this).Root);
        }

        string seatedLobby = "";

        /// <summary>대기실 사람들을 탁자에 앉혀 보여 준다.</summary>
        public void SeatLobby()
        {
            var room = Net.NetRoom.I;
            if (room == null) return;
            var looks = new List<CharacterLook>();
            var colors = new List<int>();
            int local = 0;
            string key = "";
            for (int i = 0; i < room.Lobby.Slots.Count; i++)
            {
                var s = room.Lobby.Slots[i];
                looks.Add(LookOf(s));
                colors.Add(i);
                if ((room.IsHost && s.IsHost) || (!room.IsHost && s.PeerId == room.MyPeerId)) local = i;
                key += looks[i].Key + ",";
            }
            key += local;
            if (key == seatedLobby || looks.Count == 0) return;
            seatedLobby = key;
            Stage.Seat(looks, colors, local);
            Stage.HideItem();
            Stage.AllLookAt(Stage.ItemPoint);
        }

        /// <summary>온라인 참가자: 방장이 시작한 판에 들어간다 (엔진 없이 받은 상태를 보여 준다).</summary>
        public void StartClientGame(List<SeatInfo> seats, int seat)
        {
            var setup = new GameSetup { Seats = seats, LocalSeat = seat };
            var looks = new List<CharacterLook>();
            var colors = new List<int>();
            foreach (var s in seats)
            {
                looks.Add(LookOf(s));
                colors.Add(s.ColorIndex);
            }
            Presenter.Detach();
            seatedLobby = "";
            Stage.Seat(looks, colors, seat);
            Stage.HideItem();
            ItemImages.Clear();
            Social.ResetSeats();
            Session.BeginClient(setup);
            BeginFirstPerson();
            GameScreen = new GameScreen(this);
            Ui.SetScreen(GameScreen.Root);
            Presenter.Attach(Session, GameScreen);
            Audio.PlayMusic("bgm_auction", 2f);
            SaveData.I.Stats.Games++;
            SaveData.Save();
        }

        public void GoSettings(System.Action back) { Ui.SetScreen(new SettingsScreen(this, back).Root); }

        public void GoHowTo(System.Action back) { Ui.SetScreen(new HowToScreen(back).Root); }

        public void GoCustomize(System.Action back) { Ui.SetScreen(new CustomizeScreen(this, back).Root); }

        public void GoRecords(System.Action back) { Ui.SetScreen(new RecordsScreen(back).Root); }

        // ═════════════════ 한 판 ═════════════════

        public void StartGame(GameSetup setup)
        {
            lastSetup = setup;
            var looks = new List<CharacterLook>();
            var colors = new List<int>();
            for (int i = 0; i < setup.Seats.Count; i++)
            {
                var s = setup.Seats[i];
                looks.Add(LookOf(s));
                colors.Add(s.ColorIndex);
            }
            seatedLobby = "";
            Stage.Seat(looks, colors, setup.LocalSeat);
            Stage.HideItem();
            ItemImages.Clear();
            Social.ResetSeats();
            Presenter.Detach();
            Session.Begin(setup);
            BeginFirstPerson();
            GameScreen = new GameScreen(this);
            Ui.SetScreen(GameScreen.Root);
            Presenter.Attach(Session, GameScreen);
            Session.Go();
            Audio.PlayMusic("bgm_auction", 2f);
            SaveData.I.Stats.Games++;
            SaveData.Save();
            // 온라인 방장이면 NetRoom 이 곧 온라인 상태로 덮어쓴다
            Platform.Steam.Presence(L.T("rp_solo"));
        }

        public void Rematch()
        {
            if (lastSetup == null) { GoSetup(); return; }
            var s = lastSetup;
            s.Seed = 0;
            StartGame(s);
        }

        public bool HasLastSetup { get { return lastSetup != null; } }

        public void QuitToMenu()
        {
            Session.Paused = false;
            GoTitle();
        }

        /// <summary>메뉴 설정으로 한 판의 GameSetup 을 만든다.</summary>
        public static GameSetup BuildSetup(LastSetup last)
        {
            var setup = new GameSetup();
            var r = setup.Rules;
            r.ItemsPerPlayer = Mathf.Clamp(last.Items, 2, 4);
            r.BidSeconds = Mathf.Clamp(last.BidSeconds, 10, 120);
            r.ExtendSeconds = Mathf.Clamp(last.Extend, 0, 15);
            r.BlindMode = last.Blind;
            r.LoanAmount = last.Loan ? 10000 : 0;
            setup.Difficulty = (Difficulty)Mathf.Clamp(last.Difficulty, 0, 2);
            // 이름을 안 쓰면 'Player' (AI 가 여러 판에 걸쳐 같은 사람으로 기억하는 열쇠이기도 하다)
            string name = string.IsNullOrWhiteSpace(last.Name) ? L.T("name_placeholder") : last.Name.Trim();
            var mine = last.Custom ?? CharacterLook.DefaultPlayer();
            setup.Seats.Add(new SeatInfo { Name = name, IsAI = false, ColorIndex = 0, CharacterId = last.Look, LookJson = mine.ToJson() });
            var ids = new List<string>(last.RivalIds);
            var all = AiProfiles.Ids();
            var rng = new System.Random();
            while (ids.Count < last.Rivals)
            {
                string pick = all[rng.Next(all.Count)];
                if (!ids.Contains(pick)) ids.Add(pick);
            }
            if (ids.Count > last.Rivals) ids.RemoveRange(last.Rivals, ids.Count - last.Rivals);
            // 자리 순서를 섞는다
            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = ids[i];
                ids[i] = ids[j];
                ids[j] = t;
            }
            for (int i = 0; i < ids.Count; i++)
            {
                var p = AiProfiles.Get(ids[i]);
                setup.Seats.Add(new SeatInfo { Name = L.T("ai_" + p.Id), IsAI = true, AiProfile = p.Id, CharacterId = p.CharacterId, ColorIndex = i + 1 });
            }
            setup.LocalSeat = 0;
            return setup;
        }

        void Update()
        {
            if (GameScreen != null) GameScreen.Tick();
            else if (UiRoot.I != null) UiRoot.I.ToastLayer.style.paddingTop = 390;
        }

        void OnApplicationQuit()
        {
            SaveData.Save();
        }
    }
}
