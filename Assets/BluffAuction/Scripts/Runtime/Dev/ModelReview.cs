using System.Collections;
using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.Dev
{
    /// <summary>
    /// 개발용 모형 점검 (-scenario models): 경매장 아래 빈 촬영장에 손님을 한 명씩 앉혀
    /// 전신·얼굴·감정 표현·손 모양·경매품을 고르게 비춰 찍는다.
    /// </summary>
    public static class ModelReview
    {
        static readonly Vector3 Origin = new Vector3(0, -50f, 0);

        public static IEnumerator Run(GameApp app, CameraRig rig, PostFx fx)
        {
            AutoShots.Log("models start");
            app.Ui.SetScreen(null);
            rig.enabled = false;
            var studio = BuildStudio();
            var cam = rig.transform;
            fx.SetAperture(16f);

            var looks = new List<CharacterLook>();
            looks.AddRange(CharacterLook.Guests);
            looks.AddRange(CharacterLook.Players);
            looks.Add(CharacterLook.Auctioneer);
            foreach (var look in looks)
            {
                var g = Spawn(studio, look);
                yield return new WaitForSeconds(0.4f);
                Aim(cam, fx, Origin + new Vector3(0.75f, 1.15f, 1.75f), Origin + new Vector3(0, 0.78f, 0));
                yield return AutoShots.Capture("full_" + look.Id);
                var head = g.Head.position + Vector3.up * 0.12f;
                Aim(cam, fx, head + new Vector3(0.16f, 0.02f, 0.62f), head);
                yield return AutoShots.Capture("head_" + look.Id);
                Object.Destroy(g.gameObject);
            }

            // 감정 표현: 카메라를 바라보는 사람에게
            var emoter = Spawn(studio, CharacterLook.Guests[2]);
            var viewer = Origin + new Vector3(0.0f, 1.2f, 1.6f);
            foreach (var e in new[] { Gesture.Wave, Gesture.ThumbsUp, Gesture.MiddleFinger, Gesture.Peace, Gesture.Facepalm, Gesture.Angry, Gesture.Mock, Gesture.Clap, Gesture.Shrug, Gesture.Laugh, Gesture.Bid, Gesture.Cheer })
            {
                emoter.LookAt(viewer);
                emoter.Emote(e, viewer);
                Aim(cam, fx, viewer + new Vector3(0.25f, -0.05f, 0f), Origin + new Vector3(0, 0.95f, 0));
                yield return new WaitForSeconds(0.9f);
                yield return AutoShots.Capture("emote_" + e);
                yield return new WaitForSeconds(1.4f);
            }
            Object.Destroy(emoter.gameObject);

            // 손 모양 (왼손을 앞으로 들어 가까이)
            var hander = Spawn(studio, CharacterLook.Guests[0]);
            hander.ShoulderL.localRotation = Quaternion.Euler(-80, 20, 0);
            foreach (HandPose p in System.Enum.GetValues(typeof(HandPose)))
            {
                hander.GloveL.SetPose(p, true);
                hander.enabled = false;
                hander.ShoulderL.localRotation = Quaternion.Euler(-80, 20, 0);
                hander.ElbowL.localRotation = Quaternion.Euler(-60, 0, 0);
                yield return null;
                var h = hander.HandL.position + hander.HandL.up * -0.05f;
                Aim(cam, fx, h + new Vector3(0.05f, 0.08f, 0.32f), h);
                yield return AutoShots.Capture("hand_" + p);
            }
            Object.Destroy(hander.gameObject);

            // 경매품 (회전판 없이, 고르게)
            for (int i = 0; i < ItemModels.Count; i++)
            {
                var item = ItemModels.Build(i, studio.transform);
                item.transform.localPosition = new Vector3(0, 0.6f, 0);
                var b = Bounds(item);
                float size = Mathf.Max(0.12f, b.extents.magnitude);
                // 플레이어는 -Z 쪽에 앉으므로 물건의 앞면도 -Z
                Aim(cam, fx, b.center + new Vector3(0.35f, 0.25f, -1f).normalized * size * 3.2f, b.center);
                yield return new WaitForSeconds(0.2f);
                yield return AutoShots.Capture("item_" + i);
                Object.Destroy(item);
            }
            AutoShots.Log("models done");
            Application.Quit();
        }

        /// <summary>-scenario custom: 꾸미기 화면의 부위마다 한 장, 무작위 몇 번.</summary>
        public static IEnumerator Customize(GameApp app)
        {
            yield return new WaitForSeconds(2f);
            app.GoCustomize(() => app.GoTitle());
            yield return new WaitForSeconds(1.2f);
            var cz = UI.CustomizeScreen.Current;
            for (int i = 0; i < UI.CustomizeScreen.CategoryCount; i++)
            {
                cz.DevSelect(i);
                yield return new WaitForSeconds(0.9f);
                yield return AutoShots.Capture("cz_cat" + i);
            }
            for (int i = 0; i < 4; i++)
            {
                cz.DevRandom();
                cz.DevSelect(i % 2 == 0 ? 0 : 5);
                yield return new WaitForSeconds(1.2f);
                yield return AutoShots.Capture("cz_random" + i);
            }
            Application.Quit();
        }

        /// <summary>-scenario social: 한 판을 자동으로 두며 채팅·감정 표현·던지기·바퀴·능력 표시·직접 넣은 그림을 찍는다.</summary>
        public static IEnumerator Social(GameApp app)
        {
            yield return new WaitForSeconds(2.5f);
            var last = new LastSetup { Name = "Player", Rivals = 4, Difficulty = 1, Items = 2, BidSeconds = 24, Extend = 3, Loan = true };
            app.Session.Autopilot = true;
            app.StartGame(GameApp.BuildSetup(last));
            int myItem = Core.AuctionEngine.ItemId(0, 0);
            ItemImages.Store(myItem, TestPicture());
            bool presented = false;
            app.Session.EventShown += e =>
            {
                AutoShots.Log(e.ToString());
                if (e.Type == Core.EventType.LotPresented && e.Item == myItem) presented = true;
            };
            float t0 = Time.unscaledTime;
            while (app.Session.View.Phase != Core.Phase.Bidding && Time.unscaledTime - t0 < 60f) yield return null;
            yield return new WaitForSeconds(2f);
            yield return AutoShots.Capture("bidding");

            SocialHub.I.SendChat("이거 진짜예요? 너무 비싼 것 같은데…");
            yield return new WaitForSeconds(0.6f);
            yield return AutoShots.Capture("chat");
            app.GameScreen.DevOpenChat();
            yield return new WaitForSeconds(0.4f);
            yield return AutoShots.Capture("chat_open");
            UI.UiRoot.I.Root.panel.focusController.focusedElement?.Blur();
            app.GameScreen.DevCloseChat();
            yield return new WaitForSeconds(0.3f);

            // Enter 로 채팅 열기: 여러 번, 서로 다른 때에 눌러도 매번 열리는지 (예전에는 50ms 마다 봐서 자주 놓쳤다)
            int opened = 0, tries = 0;
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.11f + i * 0.037f);
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb == null) break;
                tries++;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return new WaitForSeconds(0.12f);
                if (app.GameScreen.DevChatOpen) opened++;
                app.GameScreen.DevCloseChat();
            }
            AutoShots.Log("enter opened chat " + opened + "/" + tries);
            yield return new WaitForSeconds(0.4f);

            // 감정 표현 바퀴 (보이기만)
            app.GameScreen.DevShowWheel(true, 7);
            yield return new WaitForSeconds(0.3f);
            yield return AutoShots.Capture("wheel");
            app.GameScreen.DevShowWheel(false, -1);

            // 1번 손님에게 가운데 손가락 → 그 손님 쪽을 본다 (반응도 찍는다)
            var a = app.Rig.AnglesTo(app.Stage.SeatHead(1));
            app.Rig.LookTowards(a.x, a.y);
            yield return new WaitForSeconds(0.8f);
            SocialHub.I.SendEmote(Gesture.MiddleFinger, 1);
            yield return new WaitForSeconds(1.0f);
            yield return AutoShots.Capture("emote_mine");
            yield return new WaitForSeconds(1.6f);
            yield return AutoShots.Capture("emote_reaction");

            // 2번 손님에게 사과 던지기
            var b = app.Rig.AnglesTo(app.Stage.SeatHead(2));
            app.Rig.LookTowards(b.x, b.y);
            yield return new WaitForSeconds(0.8f);
            SocialHub.I.SendThrow(1, 2, b.x, b.y, 1f);
            yield return new WaitForSeconds(0.18f);
            yield return AutoShots.Capture("throw_fly");
            yield return new WaitForSeconds(0.5f);
            yield return AutoShots.Capture("throw_hit");
            yield return new WaitForSeconds(1.8f);
            yield return AutoShots.Capture("throw_back");

            // 옆자리(1번)를 말아 쥔 신문지로
            var c = app.Rig.AnglesTo(app.Stage.SeatHead(1));
            app.Rig.LookTowards(c.x, c.y);
            app.Throwables.DevPickMelee(0);
            yield return new WaitForSeconds(0.9f);
            yield return AutoShots.Capture("melee_hold");
            // 클릭과 같은 길: 조준점 아래 사람 (1번을 보고 있다)
            app.Throwables.DevSwing();
            yield return new WaitForSeconds(0.14f);
            yield return AutoShots.Capture("melee_swing");
            yield return new WaitForSeconds(0.35f);
            yield return AutoShots.Capture("melee_hit");
            yield return new WaitForSeconds(1.2f);
            yield return AutoShots.Capture("melee_back");
            AutoShots.Log("in reach 0-1: " + app.Throwables.InReach(0, 1) + " 0-2: " + app.Throwables.InReach(0, 2));

            app.Rig.LookTowards(0, -12);
            // 능력 표시가 나올 때까지 (최대 25초)
            t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 25f)
            {
                yield return new WaitForSeconds(1f);
                if (app.GameScreen != null && HasAbilityStrip(app)) break;
            }
            yield return AutoShots.Capture("abilities");

            // 내 물건(그림 넣은 것)이 나올 때까지
            t0 = Time.unscaledTime;
            while (!presented && Time.unscaledTime - t0 < 240f && app.Session.Running) yield return null;
            yield return new WaitForSeconds(2.2f);
            app.Rig.LookTowards(0, -8);
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("picture");
            app.Rig.LookTowards(0, -40);
            yield return new WaitForSeconds(0.8f);
            yield return AutoShots.Capture("picture_card");
            AutoShots.Log("social done");
            Application.Quit();
        }

        /// <summary>-scenario table: 1인칭으로 탁자 위 물건을 하나씩 가까이 + 맞는 효과.</summary>
        public static IEnumerator Table(GameApp app)
        {
            yield return new WaitForSeconds(2.5f);
            var last = new LastSetup { Name = "Player", Rivals = 4, Difficulty = 1, Items = 2, BidSeconds = 40, Extend = 3, Loan = true };
            app.Session.Autopilot = true;
            app.StartGame(GameApp.BuildSetup(last));
            float t0 = Time.unscaledTime;
            while (app.Session.View.Phase != Core.Phase.Bidding && Time.unscaledTime - t0 < 60f) yield return null;
            yield return new WaitForSeconds(1f);
            var kit = app.TableKit;
            var targets = new List<KeyValuePair<string, Vector3>>();
            for (int i = 0; i < 3; i++) targets.Add(new KeyValuePair<string, Vector3>("tool" + i, kit.Tools[i].transform.position + Vector3.up * 0.05f));
            var props = app.Throwables.DevPropPositions();
            for (int i = 0; i < props.Count; i++) targets.Add(new KeyValuePair<string, Vector3>("prop" + i, props[i]));
            app.Rig.DevFov = 24f;
            foreach (var t in targets)
            {
                var a = app.Rig.AnglesTo(t.Value);
                app.Rig.LookTowards(a.x, a.y);
                yield return new WaitForSeconds(0.9f);
                yield return AutoShots.Capture("near_" + t.Key);
            }
            // 넓게: 왼쪽 도구 줄, 오른쪽 패들·장부·코인
            app.Rig.DevFov = 40f;
            foreach (var look in new[] { new Vector2(-38, -50), new Vector2(34, -48), new Vector2(60, -40) })
            {
                app.Rig.LookTowards(look.x, look.y);
                yield return new WaitForSeconds(0.9f);
                yield return AutoShots.Capture("wide_" + look.x);
            }
            app.Rig.DevFov = 0f;
            // 맞는 효과: 2번 손님에게 토마토
            var h = app.Rig.AnglesTo(app.Stage.SeatHead(2));
            app.Rig.LookTowards(h.x, h.y);
            yield return new WaitForSeconds(0.8f);
            SocialHub.I.SendThrow(3, 2, h.x, h.y, 1f);
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.12f);
                yield return AutoShots.Capture("hit" + i);
            }
            // 내가 맞을 때 (2번이 나에게 사과)
            var m = new Net.ThrowMsg { Seat = 2, Kind = 1, Target = app.Stage.LocalSeat, Power = 1f, Seed = 3 };
            SocialHub.I.OnThrow(m);
            for (int i = 0; i < 6; i++)
            {
                yield return new WaitForSeconds(0.15f);
                yield return AutoShots.Capture("me_hit" + i);
            }
            // 토마토를 내가 맞을 때
            yield return new WaitForSeconds(2f);
            SocialHub.I.OnThrow(new Net.ThrowMsg { Seat = 3, Kind = 3, Target = app.Stage.LocalSeat, Power = 1f, Seed = 5 });
            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForSeconds(0.2f);
                yield return AutoShots.Capture("me_tomato" + i);
            }

            // 근접 무기: 조준점으로 고르기 (옆자리 · 건너편 · 허공) → 클릭과 같은 길로 휘두르기
            yield return new WaitForSeconds(1.5f);
            app.Throwables.DevPickMelee(0);
            int n = app.Stage.Guests.Count;
            foreach (int seat in new[] { 1, 2, -1 })
            {
                if (seat >= 0) { var a = app.Rig.AnglesTo(app.Stage.SeatHead(seat)); app.Rig.LookTowards(a.x, a.y); }
                else app.Rig.LookTowards(0f, 25f);
                yield return new WaitForSeconds(0.9f);
                bool ok;
                int aim = app.Throwables.MeleeAim(out ok);
                AutoShots.Log("melee aim at seat " + seat + " -> " + aim + " in reach " + ok + " (seats " + n + ")");
                yield return AutoShots.Capture("aim_" + (seat >= 0 ? seat.ToString() : "air"));
            }
            var near = app.Rig.AnglesTo(app.Stage.SeatHead(n - 1));
            app.Rig.LookTowards(near.x, near.y);
            yield return new WaitForSeconds(0.9f);
            app.Throwables.DevSwing();
            yield return new WaitForSeconds(0.25f);
            yield return AutoShots.Capture("swing_hit");
            Application.Quit();
        }

        static bool HasAbilityStrip(GameApp app)
        {
            var strip = UI.UiRoot.I.Root.Q(null, "ability-strip");
            return strip != null && strip.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.Flex;
        }

        /// <summary>시험용 그림: 노을 하늘 위 웃는 얼굴.</summary>
        static byte[] TestPicture()
        {
            const int W = 320, H = 240;
            var t = new Texture2D(W, H, TextureFormat.RGB24, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = Color.Lerp(new Color(0.95f, 0.55f, 0.3f), new Color(0.25f, 0.2f, 0.55f), y / (float)H);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(W * 0.5f, H * 0.5f));
                    if (d < 80) c = new Color(1f, 0.85f, 0.2f);
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(W * 0.5f - 28, H * 0.5f + 22)) < 10) c = Color.black;
                    if (Vector2.Distance(new Vector2(x, y), new Vector2(W * 0.5f + 28, H * 0.5f + 22)) < 10) c = Color.black;
                    if (d > 40 && d < 52 && y < H * 0.5f - 6) c = Color.black;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            var jpg = t.EncodeToJPG(85);
            Object.Destroy(t);
            return jpg;
        }

        static Guest Spawn(GameObject studio, CharacterLook look)
        {
            var g = GuestBuilder.Build(studio.transform, look, Stage.SeatColors[1], look.Id == "auctioneer");
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.LookAt(Origin + new Vector3(0.5f, 1.1f, 2f));
            return g;
        }

        static void Aim(Transform cam, PostFx fx, Vector3 from, Vector3 at)
        {
            cam.position = from;
            cam.rotation = Quaternion.LookRotation(at - from, Vector3.up);
            var c = cam.GetComponentInChildren<Camera>();
            if (c != null) c.fieldOfView = 32f;
            fx.SetFocus(Vector3.Distance(from, at));
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static GameObject BuildStudio()
        {
            var go = new GameObject("ModelStudio");
            go.transform.position = Origin;
            var b = new ModelBuilder();
            int back = b.M("back", ArtLibrary.I.Tinted("VelvetPlain", new Color(0.18f, 0.06f, 0.07f)));
            int floor = b.M("floor", ArtLibrary.I.Tinted("VelvetPlain", new Color(0.12f, 0.08f, 0.07f)));
            int seat = b.M("Mahogany");
            b.Kit.Box(back, new Vector3(0, 1.5f, -1.2f), new Vector3(6f, 3f, 0.05f));
            b.Kit.Box(floor, new Vector3(0, -0.01f, 0), new Vector3(6f, 0.02f, 6f));
            b.Kit.Cylinder(seat, 0.2f, 0f, 0.48f, 24, true);
            b.Build("Set", go.transform);
            Light(go, new Vector3(-1.2f, 2.4f, 2.2f), new Color(1f, 0.9f, 0.78f), 9f, 6f);
            Light(go, new Vector3(1.6f, 1.4f, 1.8f), new Color(0.7f, 0.75f, 0.9f), 3f, 6f);
            Light(go, new Vector3(0.4f, 2.2f, -1f), new Color(1f, 0.7f, 0.5f), 5f, 4f);
            return go;
        }

        static void Light(GameObject parent, Vector3 local, Color c, float intensity, float range)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent.transform, false);
            l.transform.localPosition = local;
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.Soft;
        }
    }
}
