using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BluffAuction.Art;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.Table;
using BluffAuction.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace BluffAuction.Dev
{
    /// <summary>
    /// -scenario store: Steam 상점·라이브러리 그림을 게임 화면으로 만든다 (경매장 장면 + 로고).
    /// 장면은 2배로 그려 줄이고, 로고는 UI Toolkit 패널(WorldPanel)로 그 위에 얹어 게임과 같은 글꼴을 쓴다.
    /// -storeposes &lt;파일&gt; 이 있으면 그 구도로 찍는다 (한 줄: 이름 w h 배치 언어 px py pz lx ly lz fov).
    /// 배치: left · center · bottom · none(글자 없음) · logo(투명 배경에 로고만).
    /// </summary>
    public static class StoreShots
    {
        sealed class Spec
        {
            public string Name, Layout, Lang;
            public int W, H;
            public Vector3 Pos, Look;
            public float Fov;
            public RenderTexture Scene;
        }

        // 탁자 가운데가 원점, 내 자리(0)가 -Z, 경매사가 +Z 쪽에 선다
        static readonly string[] Defaults =
        {
            // 가로 캡슐: 물건·경매사가 화면 오른쪽 2/3 지점에 오게 왼쪽 앞으로 비스듬히 (가격판은 화면 위로 빠진다)
            "header          920  430 left   en   0.35 1.7 -2.6   -0.85 1.2 0.5   40",
            "header_ko       920  430 left   ko   0.35 1.7 -2.6   -0.85 1.2 0.5   40",
            "small           462  174 center en   0.0 1.9 -3.0    0.0 1.25 0.8   32",
            "small_ko        462  174 center ko   0.0 1.9 -3.0    0.0 1.25 0.8   32",
            "main           1232  706 left   en   0.35 1.7 -2.6   -0.85 1.2 0.5   40",
            "main_ko        1232  706 left   ko   0.35 1.7 -2.6   -0.85 1.2 0.5   40",
            // 세로 캡슐: 탁자 끝 눈높이에서 물건 너머 경매사 (로고는 아래)
            "vertical        748  896 bottom en   0.0 1.3 -1.45   0.0 1.15 1.6   54",
            "vertical_ko     748  896 bottom ko   0.0 1.3 -1.45   0.0 1.15 1.6   54",
            "library_capsule 600  900 bottom en   0.0 1.3 -1.45   0.0 1.15 1.6   56",
            "library_capsule_ko 600 900 bottom ko 0.0 1.3 -1.45   0.0 1.15 1.6   56",
            // 라이브러리 배경: 낮게 잡아야 가격판이 경매사 얼굴을 가리지 않는다
            "library_hero   3840 1240 none   en   0.0 1.6 -3.5    0.0 1.3 0.8   30",
            "page_background 1438 810 none   en   0.0 2.6 -4.6    0.0 1.0 0.8   44",
            "library_logo   1280  720 logo   en   0 0 0  0 0 1  40",
            "library_logo_ko 1280 720 logo   ko   0 0 0  0 0 1  40",
        };

        public static IEnumerator Run(GameApp app)
        {
            yield return new WaitForSeconds(2.5f);
            // 탁자 위 카드·가격판 글자도 그 언어로 (-storelang en|ko, 그 언어 그림만 찍는다)
            string lang = Arg("-storelang") ?? "en";
            Loc.L.Set(lang == "ko" ? Loc.Lang.Korean : Loc.Lang.English);
            var last = new LastSetup { Name = "Player", Rivals = 4, Difficulty = 1, Items = 2, BidSeconds = 40, Extend = 3, Loan = true };
            app.Session.Autopilot = true;
            app.StartGame(GameApp.BuildSetup(last));
            float t0 = Time.unscaledTime;
            while (app.Session.View.Phase != Core.Phase.Bidding && Time.unscaledTime - t0 < 60f) yield return null;
            yield return new WaitForSeconds(4f);

            // 글자·이름표는 그림에 넣지 않는다, 흐림·잡티도 끈다
            UiRoot.I.Root.style.display = DisplayStyle.None;
            if (app.Fx != null)
            {
                if (app.Fx.Dof != null) app.Fx.Dof.active = false;
                if (app.Fx.Grain != null) app.Fx.Grain.active = false;
                if (app.Fx.Chroma != null) app.Fx.Chroma.active = false;
                // 작은 썸네일에서도 보이게 조금 밝게
                if (app.Fx.Color != null) app.Fx.Color.postExposure.value += 0.3f;
            }
            // 내 자리 손님(1인칭이라 머리가 없다)은 빼고, 탁자 위 내 물건들만 남긴다
            var st = app.Stage;
            if (st.LocalSeat < st.Guests.Count) st.Guests[st.LocalSeat].gameObject.SetActive(false);
            // 손님들이 패들을 번쩍 (경매가 한창인 장면)
            for (int k = 0; k < st.Guests.Count; k++)
                if (k != st.LocalSeat) st.Guests[k].Play(k % 2 == 1 ? Gesture.BigBid : Gesture.Bid, 2.4f);
            yield return new WaitForSeconds(0.55f);

            var specs = Load().FindAll(s => s.Lang == lang);
            var cam = MakeCamera(app.Rig.Cam);
            foreach (var s in specs)
                if (s.Layout != "logo") s.Scene = RenderScene(cam, s);
            UnityEngine.Object.Destroy(cam.gameObject);

            string outDir = Path.Combine(AutoShots.Dir, "store");
            Directory.CreateDirectory(outDir);
            bool raw = Arg("-storeraw") != null;
            foreach (var s in specs)
            {
                if (raw && s.Scene != null) SaveRT(s.Scene, Path.Combine(outDir, s.Name + "_raw.png"));
                yield return Compose(s, Path.Combine(outDir, s.Name + ".png"));
                if (s.Scene != null) s.Scene.Release();
            }
            AutoShots.Log("store done " + specs.Count);
            Application.Quit();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>
        /// -scenario achicons: Steam 업적 아이콘 (달성 · 미달성, 256 · 64 px) → &lt;폴더&gt;/achievements/.
        /// 버건디 바탕에 금테 메달 + 업적마다 다른 무늬. 미달성은 같은 그림을 회색으로.
        /// </summary>
        public static IEnumerator AchievementIcons()
        {
            yield return new WaitForSeconds(1.5f);
            UiRoot.I.Root.style.display = DisplayStyle.None;
            string outDir = Path.Combine(AutoShots.Dir, "achievements");
            Directory.CreateDirectory(outDir);
            // id → 무늬 (USS 아이콘 클래스, 또는 글자)
            var marks = new[]
            {
                new[] { "first_win", "", "★" }, new[] { "bargain", "ico-tag", "" }, new[] { "bluff", "ico-eye", "" }, new[] { "collector", "ico-paddle", "" },
                new[] { "debt", "", "◆" }, new[] { "hard", "ico-hammer", "" }, new[] { "full", "", "8" }, new[] { "spender", "", "G" },
            };
            foreach (var m in marks)
                foreach (bool got in new[] { true, false })
                {
                    string name = "ACH_" + m[0].ToUpperInvariant() + (got ? "" : "_locked");
                    yield return Medal(m[1], m[2], got, Path.Combine(outDir, name));
                }
            AutoShots.Log("achievement icons done " + marks.Length * 2);
            Application.Quit();
        }

        static IEnumerator Medal(string iconClass, string glyph, bool got, string basePath)
        {
            const int S = 256;
            var gold = got ? new Color(0.95f, 0.78f, 0.42f) : new Color(0.5f, 0.5f, 0.5f);
            var panel = new WorldPanel(null, "Medal", S, S, got ? new Color(0.17f, 0.05f, 0.06f) : new Color(0.1f, 0.1f, 0.1f));
            var root = panel.Root;
            root.style.width = S;
            root.style.height = S;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            var medal = new VisualElement();
            medal.style.width = 216;
            medal.style.height = 216;
            float r = 108f;
            medal.style.borderTopLeftRadius = r; medal.style.borderTopRightRadius = r; medal.style.borderBottomLeftRadius = r; medal.style.borderBottomRightRadius = r;
            medal.style.backgroundColor = got ? new Color(0.42f, 0.09f, 0.12f) : new Color(0.2f, 0.2f, 0.2f);
            float bw = 12f;
            medal.style.borderTopWidth = bw; medal.style.borderBottomWidth = bw; medal.style.borderLeftWidth = bw; medal.style.borderRightWidth = bw;
            medal.style.borderTopColor = gold; medal.style.borderBottomColor = gold; medal.style.borderLeftColor = gold; medal.style.borderRightColor = gold;
            medal.style.alignItems = Align.Center;
            medal.style.justifyContent = Justify.Center;
            if (!string.IsNullOrEmpty(iconClass))
            {
                var ico = new VisualElement();
                ico.AddToClassList(iconClass);
                ico.style.width = 132;
                ico.style.height = 132;
                ico.style.unityBackgroundImageTintColor = gold;
                medal.Add(ico);
            }
            else
            {
                var l = new Label(glyph);
                l.AddToClassList("f-serif");
                l.style.fontSize = glyph == "★" || glyph == "◆" ? 128 : 136;
                l.style.color = gold;
                l.style.unityTextAlign = TextAnchor.MiddleCenter;
                l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
                l.style.marginTop = glyph == "★" || glyph == "◆" ? -6 : -10;
                medal.Add(l);
            }
            root.Add(medal);
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var big = ReadBack(panel.RT);
            File.WriteAllBytes(basePath + ".png", big.EncodeToPNG());
            // 64 px: 256 → 128 → 64 로 반씩 줄여 계단 없이
            var half = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var quarter = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(panel.RT, half);
            Graphics.Blit(half, quarter);
            var small = ReadBack(quarter);
            File.WriteAllBytes(basePath + "_64.png", small.EncodeToPNG());
            RenderTexture.active = null;
            half.Release();
            quarter.Release();
            UnityEngine.Object.Destroy(big);
            UnityEngine.Object.Destroy(small);
            panel.Destroy();
        }

        static List<Spec> Load()
        {
            string[] lines = Defaults;
            string file = Arg("-storeposes");
            if (file != null && File.Exists(file)) lines = File.ReadAllLines(file);
            var list = new List<Spec>();
            foreach (var line in lines)
            {
                var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 12 || p[0].StartsWith("#")) continue;
                Func<int, float> f = i => float.Parse(p[i], CultureInfo.InvariantCulture);
                list.Add(new Spec
                {
                    Name = p[0], W = int.Parse(p[1]), H = int.Parse(p[2]), Layout = p[3], Lang = p[4],
                    Pos = new Vector3(f(5), f(6), f(7)), Look = new Vector3(f(8), f(9), f(10)), Fov = f(11)
                });
            }
            return list;
        }

        static Camera MakeCamera(Camera main)
        {
            var go = new GameObject("StoreCam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(main);
            cam.enabled = false;
            cam.targetTexture = null;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            data.requiresDepthTexture = true;
            return cam;
        }

        /// <summary>장면을 2배(큰 그림은 1배)로 그려 최종 크기로 줄인다.</summary>
        static RenderTexture RenderScene(Camera cam, Spec s)
        {
            int ss = Mathf.Max(s.W, s.H) * 2 <= 4096 ? 2 : 1;
            var big = new RenderTexture(s.W * ss, s.H * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Bilinear };
            big.Create();
            cam.transform.position = s.Pos;
            cam.transform.rotation = Quaternion.LookRotation(s.Look - s.Pos, Vector3.up);
            cam.fieldOfView = s.Fov;
            cam.aspect = s.W / (float)s.H;
            cam.targetTexture = big;
            var req = new RenderPipeline.StandardRequest { destination = big };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            cam.targetTexture = null;
            if (ss == 1) return big;
            var small = new RenderTexture(s.W, s.H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            small.Create();
            // 2:1 쌍선형 = 네 칸 평균
            Graphics.Blit(big, small);
            big.Release();
            return small;
        }

        static IEnumerator Compose(Spec s, string path)
        {
            bool logoOnly = s.Layout == "logo";
            var panel = new WorldPanel(null, "Store_" + s.Name, s.W, s.H, logoOnly ? new Color(0, 0, 0, 0) : Color.black);
            var root = panel.Root;
            root.style.width = s.W;
            root.style.height = s.H;
            Texture2D shade = null, scene = null;
            if (s.Scene != null)
            {
                // RenderTexture 를 바로 배경으로 쓰면 sRGB 를 두 번 풀어 어두워진다 → 보통 텍스처로 옮겨 쓴다
                scene = ReadBack(s.Scene);
                var bg = Fill();
                bg.style.backgroundImage = scene;
                root.Add(bg);
            }
            if (s.Layout == "left" || s.Layout == "bottom" || s.Layout == "center")
            {
                shade = Shade(s.Layout);
                var sh = Fill();
                sh.style.backgroundImage = shade;
                root.Add(sh);
            }
            if (s.Layout != "none") root.Add(Logo(s));

            // 패널은 다음 프레임에 그려진다
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = panel.RT;
            var tex = new Texture2D(s.W, s.H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, s.W, s.H), 0, 0);
            RenderTexture.active = prev;
            if (logoOnly) Unpremultiply(tex);
            else Opaque(tex);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            if (shade != null) UnityEngine.Object.Destroy(shade);
            if (scene != null) UnityEngine.Object.Destroy(scene);
            panel.Destroy();
            AutoShots.Log("store " + s.Name + " " + s.W + "x" + s.H);
        }

        static Texture2D ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev;
            Opaque(tex);
            return tex;
        }

        static void SaveRT(RenderTexture rt, string path)
        {
            var tex = ReadBack(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
        }

        static VisualElement Fill()
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
            e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            return e;
        }

        /// <summary>로고 뒤를 어둡게 (왼쪽 · 아래 · 가운데 띠).</summary>
        static Texture2D Shade(string layout)
        {
            const int N = 256;
            bool horizontal = layout == "left";
            var t = new Texture2D(horizontal ? N : 1, horizontal ? 1 : N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var ink = new Color(0.03f, 0.012f, 0.01f);
            for (int i = 0; i < N; i++)
            {
                float u = i / (N - 1f), a;
                // (Mathf.SmoothStep 은 값 사이를 잇는 함수라 0~1 로 먼저 줄여 넣는다)
                if (layout == "left") a = 0.86f * Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, u / 0.62f), 1.3f);
                else if (layout == "bottom") a = 0.92f * Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, u / 0.5f), 1.2f);   // 텍스처 v=0 이 아래
                else a = 0.55f * Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.3f, 2));
                var c = new Color(ink.r, ink.g, ink.b, a);
                if (horizontal) t.SetPixel(i, 0, c); else t.SetPixel(0, i, c);
            }
            t.Apply();
            return t;
        }

        static VisualElement Logo(Spec s)
        {
            bool ko = s.Lang == "ko";
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.flexDirection = FlexDirection.Column;
            // 글자 크기의 기준 (그림 높이·너비 중 작은 쪽에 맞춘다)
            float u;
            switch (s.Layout)
            {
                case "left":
                    u = Mathf.Min(s.H * 0.62f, s.W * 0.42f);
                    box.style.left = s.W * 0.055f; box.style.top = 0; box.style.bottom = 0;
                    box.style.justifyContent = Justify.Center;
                    box.style.alignItems = Align.FlexStart;
                    break;
                case "bottom":
                    u = s.W * 0.62f;
                    box.style.left = 0; box.style.right = 0; box.style.bottom = s.H * 0.06f;
                    box.style.alignItems = Align.Center;
                    break;
                case "logo":
                    // Steam 라이브러리 로고는 캔버스를 거의 채우는 편이 좋다
                    u = Mathf.Min(s.H * 0.95f, s.W * 0.78f);
                    box.style.left = 0; box.style.right = 0; box.style.top = 0; box.style.bottom = 0;
                    box.style.justifyContent = Justify.Center;
                    box.style.alignItems = Align.Center;
                    break;
                default:   // center
                    u = Mathf.Min(s.H * 0.9f, s.W * 0.5f);
                    box.style.left = 0; box.style.right = 0; box.style.top = 0; box.style.bottom = 0;
                    box.style.justifyContent = Justify.Center;
                    box.style.alignItems = Align.Center;
                    break;
            }
            bool stacked = s.Layout != "center";
            var align = s.Layout == "left" ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            if (ko)
            {
                box.Add(Title("허풍 경매장", u * 0.3f, true, align));
                box.Add(Rule(u, s.Layout));
                box.Add(Title("BLUFF AUCTION", u * 0.105f, false, align, 0.32f));
            }
            else
            {
                if (stacked)
                {
                    box.Add(Title("BLUFF", u * 0.3f, false, align));
                    var a = Title("AUCTION", u * 0.3f, false, align);
                    a.style.marginTop = -u * 0.07f;
                    box.Add(a);
                }
                else box.Add(Title("BLUFF AUCTION", u * 0.25f, false, align));
                box.Add(Rule(u, s.Layout));
                // 작은 캡슐에는 부제를 넣지 않는다 (너무 작아 읽히지 않는다)
                if (stacked) box.Add(Sub("BID · BLUFF · BONK", u * 0.07f, align));
            }
            return box;
        }

        static Label Title(string text, float size, bool hangul, TextAnchor align, float spacing = 0.06f)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            if (hangul) { if (ArtLibrary.I.UiFontBold != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(ArtLibrary.I.UiFontBold); }
            else l.AddToClassList("f-serif");
            l.style.fontSize = size;
            l.style.letterSpacing = size * spacing;
            l.style.color = new Color(0.96f, 0.8f, 0.48f);
            l.style.unityTextAlign = align;
            l.style.unityTextOutlineWidth = Mathf.Clamp(size * 0.025f, 1f, 4f);
            l.style.unityTextOutlineColor = new Color(0.16f, 0.06f, 0.02f);
            l.style.textShadow = new TextShadow { offset = new Vector2(0, size * 0.05f), blurRadius = size * 0.16f, color = new Color(0, 0, 0, 0.9f) };
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.marginLeft = 0; l.style.marginRight = 0;
            return l;
        }

        static Label Sub(string text, float size, TextAnchor align)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.AddToClassList("f-display");
            l.style.fontSize = size;
            l.style.letterSpacing = size * 0.35f;
            l.style.color = new Color(0.98f, 0.92f, 0.78f);
            l.style.unityTextAlign = align;
            l.style.textShadow = new TextShadow { offset = new Vector2(0, size * 0.08f), blurRadius = size * 0.3f, color = new Color(0, 0, 0, 0.95f) };
            return l;
        }

        static VisualElement Rule(float u, string layout)
        {
            var d = new VisualElement { pickingMode = PickingMode.Ignore };
            d.AddToClassList("divider");
            d.style.width = u * 0.95f;
            d.style.height = Mathf.Max(10f, u * 0.06f);
            d.style.marginTop = u * 0.015f;
            d.style.marginBottom = u * 0.02f;
            if (layout == "left") d.style.alignSelf = Align.FlexStart;
            return d;
        }

        /// <summary>투명 배경 로고: 패널은 색에 알파를 곱해 그리므로 PNG 로 쓰기 전에 되돌린다.</summary>
        static void Unpremultiply(Texture2D t)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) { px[i] = new Color32(0, 0, 0, 0); continue; }
                if (c.a == 255) continue;
                float k = 255f / c.a;
                px[i] = new Color32((byte)Mathf.Min(255, c.r * k), (byte)Mathf.Min(255, c.g * k), (byte)Mathf.Min(255, c.b * k), c.a);
            }
            t.SetPixels32(px);
            t.Apply();
        }

        static void Opaque(Texture2D t)
        {
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = 255;
            t.SetPixels32(px);
            t.Apply();
        }
    }
}
