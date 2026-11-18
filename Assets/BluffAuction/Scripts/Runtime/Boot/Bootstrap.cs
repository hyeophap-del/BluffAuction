using System.Collections;
using System.Collections.Generic;
using BluffAuction.Characters;
using BluffAuction.Core;
using BluffAuction.Dev;
using BluffAuction.Game;
using BluffAuction.World;
using UnityEngine;
using EventType = BluffAuction.Core.EventType;

namespace BluffAuction.Boot
{
    /// <summary>
    /// 게임 시작점 (Main 씬의 유일한 오브젝트). 경매장·카메라·무대·게임 흐름을 만들고 타이틀을 띄운다.
    /// 개발용: -autoshots &lt;폴더&gt; -scenario tour|play 로 화면 촬영 / 자동 한 판.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public Hall Hall;
        public Stage Stage;
        public CameraRig Rig;
        public PostFx Fx;
        public GameApp App;

        void Start()
        {
            Application.targetFrameRate = -1;
            Platform.Steam.Init();
            var world = new GameObject("World").transform;
            Hall = Hall.Build(world);
            Fx = PostFx.Create(world);
            Rig = CameraRig.Create(world, Fx);
            Stage = Stage.Create(world, Hall);
            App = GameApp.Create(world, Hall, Stage, Rig, Fx);
            // Steam: 초대 수락은 어느 화면에서든 받고, 연동 전에 딴 업적도 올린다
            Platform.Steam.JoinRequested += App.AcceptSteamInvite;
            Platform.Steam.SyncAchievements(SaveData.I.Stats.Achievements);
            StartCoroutine(Warm());
        }

        void Update() { Platform.Steam.Tick(); }

        void OnApplicationQuit()
        {
            // 방을 먼저 정리해야 작별 메시지가 Steam 소켓으로 나간다 (Steam 을 끈 뒤에는 보낼 수 없다)
            if (Net.NetRoom.I != null) Net.NetRoom.I.Leave();
            Platform.Steam.Shutdown();
        }

        IEnumerator Warm()
        {
            App.GoTitle();
            yield return null;
            Hall.Probe.RenderProbe();
            Characters.PortraitStudio.RenderAll(this);
            yield return null;
            // 초대를 받아 게임이 켜졌으면 (+connect_lobby) 그 방으로
            var invite = Platform.Steam.TakePendingLobby();
            if (invite != 0 && !AutoShots.Requested) App.AcceptSteamInvite(invite);
            if (!AutoShots.Requested) yield break;
            if (AutoShots.Scenario == "tour") StartCoroutine(ShotTour());
            else if (AutoShots.Scenario == "quick") StartCoroutine(Quick());
            else if (AutoShots.Scenario == "models") StartCoroutine(ModelReview.Run(App, Rig, Fx));
            else if (AutoShots.Scenario == "custom") StartCoroutine(ModelReview.Customize(App));
            else if (AutoShots.Scenario == "social") StartCoroutine(ModelReview.Social(App));
            else if (AutoShots.Scenario == "table") StartCoroutine(ModelReview.Table(App));
            else if (AutoShots.Scenario == "store") StartCoroutine(StoreShots.Run(App));
            else if (AutoShots.Scenario == "achicons") StartCoroutine(StoreShots.AchievementIcons());
            else if (AutoShots.Scenario == "steam") StartCoroutine(SteamCheck.Run(App));
            else if (AutoShots.Scenario == "nethost") StartCoroutine(NetHost());
            else if (AutoShots.Scenario == "netjoin") StartCoroutine(NetJoin());
            else StartCoroutine(AutoPlay());
        }

        // ═════════════════ 온라인 시험 (같은 컴퓨터에서 둘을 띄운다) ═════════════════

        IEnumerator NetHost()
        {
            SaveData.I.Last.Name = "HostTester";
            var room = Net.NetRoom.I;
            var err = room.HostRoom(Net.TcpTransport.DefaultPort);
            AutoShots.Log("host: " + (err ?? "ok"));
            if (err != null) { Application.Quit(); yield break; }
            App.GoLobby();
            room.AddAI("baron");
            room.AddAI("velvet");
            var r = room.Lobby.Rules;
            r.Items = 2;
            r.BidSeconds = 15;
            r.Extend = 3;
            room.SetRules(r);
            float t0 = Time.unscaledTime;
            string why;
            while (!room.CanStart(out why) || room.Lobby.Slots.Count < 4)
            {
                if (Time.unscaledTime - t0 > 90f) { AutoShots.Log("host: nobody joined"); Application.Quit(); yield break; }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            yield return AutoShots.Capture("host_lobby");
            App.Session.Autopilot = true;
            var hub = Game.SocialHub.I;
            hub.ChatShown += (s, t) => AutoShots.Log("H chat seat=" + s + " " + t);
            hub.EmoteShown += (s, g, tg) => AutoShots.Log("H emote seat=" + s + " " + g + " -> " + tg);
            hub.HitShown += (a, b) => AutoShots.Log("H hit " + a + " -> " + b);
            room.StartGame();
            App.Session.EventShown += e => AutoShots.Log("H " + e);
            StartCoroutine(LogVoice("host"));
            yield return WatchGame("host");
        }

        IEnumerator LogVoice(string tag)
        {
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSeconds(8f);
                AutoShots.Log(tag + " voice packets in: " + Game.SocialHub.I.VoicePacketsIn);
            }
        }

        IEnumerator NetJoin()
        {
            SaveData.I.Last.Name = "GuestTester";
            SaveData.I.Last.Look = 1;
            // 꾸민 생김새가 방장에게 그대로 가는지
            var look = CharacterLook.Players[2].Clone();
            look.Id = "custom";
            look.Hat = HatStyle.Crown;
            look.HairStyle = HairStyle.Curly;
            look.Hair = new Color(0.9f, 0.3f, 0.5f);
            look.Outfit = Outfit.Cloak;
            SaveData.I.Last.Custom = look;
            var room = Net.NetRoom.I;
            string err = "x";
            if (Net.NetRoom.SteamIpTest)
            {
                // Steam 소켓 전송 계층으로 (연결은 비동기: 방 정보가 오면 붙은 것)
                yield return new WaitForSeconds(5f);
                err = room.JoinSteamIp("127.0.0.1:" + Net.SteamTransport.TestPort);
                float tw = Time.unscaledTime;
                while (err == null && room.Lobby.Slots.Count == 0 && room.IsClient && Time.unscaledTime - tw < 20f) yield return null;
                if (err == null && room.Lobby.Slots.Count == 0) err = "no lobby over steam sockets";
                AutoShots.Log("join (steam sockets): " + (err ?? "ok") + " in " + (Time.unscaledTime - tw).ToString("0.0") + "s");
            }
            else
            {
                for (int i = 0; i < 40 && err != null; i++)
                {
                    err = room.Join("127.0.0.1", Net.TcpTransport.DefaultPort);
                    if (err != null) yield return new WaitForSeconds(0.5f);
                }
                AutoShots.Log("join: " + (err ?? "ok"));
            }
            if (err != null) { Application.Quit(); yield break; }
            App.Session.Autopilot = true;
            yield return new WaitForSeconds(1.5f);
            room.SetReady(true);
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("join_lobby");
            float t0 = Time.unscaledTime;
            while (!room.InGame)
            {
                if (Time.unscaledTime - t0 > 60f || !room.Active) { AutoShots.Log("join: game never started"); Application.Quit(); yield break; }
                yield return null;
            }
            App.Session.EventShown += e => AutoShots.Log("C " + e);
            StartCoroutine(JoinSocial());
            yield return WatchGame("join");
        }

        /// <summary>참가자: 채팅·시험 목소리·감정 표현·던지기를 방장에게 보낸다.</summary>
        IEnumerator JoinSocial()
        {
            var hub = Game.SocialHub.I;
            hub.ChatShown += (s, t) => AutoShots.Log("C chat seat=" + s + " " + t);
            yield return new WaitForSeconds(14f);
            hub.SendChat("방장님 안녕하세요! 이 물건 진짜예요?");
            yield return new WaitForSeconds(1f);
            hub.Mic.MicOn = true;
            yield return new WaitForSeconds(4f);
            hub.Mic.MicOn = false;
            hub.SendEmote(Gesture.Wave, 0);
            yield return new WaitForSeconds(1.5f);
            hub.SendThrow(1, 0, 0, 0, 1f);
            yield return new WaitForSeconds(0.5f);
            yield return AutoShots.Capture("join_social");
            AutoShots.Log("join social sent");
        }

        IEnumerator WatchGame(string tag)
        {
            float t0 = Time.unscaledTime;
            float next = t0 + 6f;
            while (App.Session.Running && App.Session.View.Phase != Phase.Results)
            {
                if (Time.unscaledTime > next)
                {
                    next += 20f;
                    yield return AutoShots.Capture(tag + "_t" + Mathf.RoundToInt(Time.unscaledTime - t0));
                }
                if (Time.unscaledTime - t0 > 400f) { AutoShots.Log(tag + ": timeout"); break; }
                yield return null;
            }
            AutoShots.Log(tag + ": phase " + App.Session.View.Phase + " running " + App.Session.Running);
            yield return new WaitForSeconds(5f);
            yield return AutoShots.Capture(tag + "_results");
            if (App.Session.View != null && App.Session.View.Seats.Length > 0)
                for (int i = 0; i < App.Session.View.SeatCount; i++)
                {
                    var s = App.Session.View.Seats[i];
                    AutoShots.Log(tag + " seat " + i + " " + s.Name + " rank " + s.Rank + " score " + s.Score);
                }
            yield return new WaitForSeconds(3f);
            Application.Quit();
        }

        /// <summary>빠른 화면 확인: 초상화 저장 + 메뉴 화면들 촬영.</summary>
        IEnumerator Quick()
        {
            yield return new WaitForSeconds(2.5f);
            DumpPortraits();
            yield return AutoShots.Capture("title");
            App.GoSetup();
            yield return new WaitForSeconds(1.2f);
            yield return AutoShots.Capture("setup");
            App.GoOnline();
            yield return new WaitForSeconds(1.0f);
            yield return AutoShots.Capture("online");
            App.GoSettings(() => App.GoTitle());
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("settings");
            App.GoHowTo(() => App.GoTitle());
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("howto");
            App.GoRecords(() => App.GoTitle());
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("records");
            Application.Quit();
        }

        /// <summary>진단: 같은 렌더 요청 방식으로 경매장을 RT 에 찍어 본다 (초상화 문제 가르기).</summary>
        void ProbeRender()
        {
            var go = new GameObject("ProbeCam");
            go.transform.position = new Vector3(0, 1.6f, -3f);
            go.transform.LookAt(new Vector3(0, 1f, 0));
            var cam = go.AddComponent<Camera>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            cam.targetTexture = rt;
            cam.enabled = false;
            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            Save(rt, "probe_request.png");
            var req2 = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            bool ok2 = UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req2);
            AutoShots.Log("single camera request supported " + ok2);
            if (ok2)
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req2);
                Save(rt, "probe_single.png");
            }
            Destroy(go);
        }

        static void Save(RenderTexture rt, string file)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            var c = tex.GetPixel(rt.width / 2, rt.height / 2);
            AutoShots.Log(file + " center " + c);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(AutoShots.Dir, file), tex.EncodeToPNG());
            Destroy(tex);
        }

        void DumpPortraits()
        {
            ProbeRender();
            AutoShots.Log("portraits ready " + Characters.PortraitStudio.Ready);
            foreach (var id in new[] { "baron", "velvet", "p_gold" })
            {
                var tex = Characters.PortraitStudio.Get(id);
                if (tex == null) { AutoShots.Log("portrait " + id + " missing"); continue; }
                AutoShots.Log("portrait " + id + " center " + tex.GetPixel(tex.width / 2, tex.height / 2));
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(AutoShots.Dir, "portrait_" + id + ".png"), tex.EncodeToPNG());
            }
        }

        // ═════════════════ 자동 한 판 (시험) ═════════════════

        readonly Queue<string> pendingShots = new Queue<string>();
        int lots;

        IEnumerator AutoPlay()
        {
            AutoShots.Log("autoplay start " + Screen.width + "x" + Screen.height + " quality " + QualitySettings.GetQualityLevel());
            yield return new WaitForSeconds(2.5f);
            yield return AutoShots.Capture("title");
            App.GoSetup();
            yield return new WaitForSeconds(1.0f);
            yield return AutoShots.Capture("setup");
            var last = new LastSetup { Name = "Player", Rivals = 4, Difficulty = 1, Items = 2, BidSeconds = 16, Extend = 4, Loan = true };
            var setup = GameApp.BuildSetup(last);
            App.Session.Autopilot = true;
            App.StartGame(setup);
            App.Session.EventShown += OnAutoEvent;
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("registration");
            float t0 = Time.unscaledTime;
            float nextPeriodic = t0 + 15f;
            while (App.Session.Running && App.Session.View.Phase != Phase.Results)
            {
                if (pendingShots.Count > 0) yield return AutoShots.Capture(pendingShots.Dequeue());
                if (Time.unscaledTime > nextPeriodic)
                {
                    nextPeriodic += 15f;
                    yield return AutoShots.Capture("t" + Mathf.RoundToInt(Time.unscaledTime - t0));
                }
                if (Time.unscaledTime - t0 > 600f) { AutoShots.Log("timeout"); break; }
                yield return null;
            }
            yield return new WaitForSeconds(2.5f);
            yield return AutoShots.Capture("results_a");
            yield return new WaitForSeconds(4f);
            yield return AutoShots.Capture("results_b");
            AutoShots.Log("autoplay done");
            Application.Quit();
        }

        void OnAutoEvent(GameEvent e)
        {
            AutoShots.Log(e.ToString());
            switch (e.Type)
            {
                case EventType.LotPresented:
                    lots++;
                    if (lots <= 2) StartCoroutine(Later(0.9f, "present" + lots));
                    if (lots == 1) StartCoroutine(LookTour());
                    break;
                case EventType.BiddingOpened:
                    if (lots <= 2) StartCoroutine(Later(4.5f, "bidding" + lots));
                    break;
                case EventType.Sold:
                    if (lots <= 3) StartCoroutine(Later(0.9f, "sold" + lots));
                    break;
                case EventType.NoBids:
                    StartCoroutine(Later(0.5f, "nobids" + lots));
                    break;
                case EventType.RevealStarted:
                    StartCoroutine(Later(3.5f, "reveal_a"));
                    StartCoroutine(Later(9f, "reveal_b"));
                    break;
            }
        }

        /// <summary>1인칭으로 둘러보며 찍기 (탁자 위 물건·가격판·상대 얼굴).</summary>
        IEnumerator LookTour()
        {
            yield return new WaitForSecondsRealtime(5f);
            var looks = new[] { new Vector2(0, -46), new Vector2(-38, -48), new Vector2(34, -46), new Vector2(0, 24), new Vector2(-70, -4), new Vector2(70, -4), new Vector2(0, -12) };
            string[] names = { "look_down", "look_left_tools", "look_right_paddle", "look_board", "look_left", "look_right", "look_center" };
            for (int i = 0; i < looks.Length; i++)
            {
                Rig.LookTowards(looks[i].x, looks[i].y);
                yield return new WaitForSecondsRealtime(0.9f);
                pendingShots.Enqueue(names[i]);
                yield return new WaitForSecondsRealtime(0.4f);
            }
        }

        IEnumerator Later(float t, string shot)
        {
            yield return new WaitForSecondsRealtime(t);
            pendingShots.Enqueue(shot);
        }

        // ═════════════════ 촬영 여행 (모형·구도 확인) ═════════════════

        IEnumerator ShotTour()
        {
            AutoShots.Log("tour start " + Screen.width + "x" + Screen.height + " quality " + QualitySettings.GetQualityLevel());
            var looks = new List<CharacterLook> { CharacterLook.Players[0] };
            var colors = new List<int> { 0 };
            for (int i = 0; i < 5; i++)
            {
                looks.Add(CharacterLook.Guests[i]);
                colors.Add(i + 1);
            }
            App.Ui.SetScreen(null);
            Stage.Seat(looks, colors);
            Stage.ShowItem(1);
            Stage.AllLookAt(Stage.ItemPoint);
            yield return new WaitForSeconds(1.5f);
            foreach (var s in new[] { Shot.Seat, Shot.Item, Shot.Wide, Shot.Auctioneer, Shot.Register, Shot.Reveal })
            {
                Rig.Cut(s);
                if (s == Shot.Auctioneer) Stage.Auctioneer.Play(Gesture.Gavel);
                yield return new WaitForSeconds(0.6f);
                yield return AutoShots.Capture(s.ToString().ToLowerInvariant());
            }
            for (int k = 1; k < 6; k++)
            {
                Rig.Cut(Shot.Bidder, Stage.SeatWorld(k));
                Stage.Guests[k].Play(Gesture.Bid);
                yield return new WaitForSeconds(0.5f);
                yield return AutoShots.Capture("bidder" + k);
            }
            for (int i = 0; i < 12; i++)
            {
                Stage.ShowItem(i);
                Rig.Cut(Shot.Item);
                yield return new WaitForSeconds(1.0f);
                yield return AutoShots.Capture("item" + i);
            }
            AutoShots.Log("tour done");
            Application.Quit();
        }
    }
}
