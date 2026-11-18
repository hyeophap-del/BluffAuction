using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.UI;
using BluffAuction.World;
using UnityEngine;
using UnityEngine.UIElements;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.Table
{
    /// <summary>
    /// 내 자리 앞 탁자 위 물건들 (라이어스 바처럼 고개를 숙여 본다):
    /// 가운데 경매 카드(지금 물건), 오른쪽 장부(내 돈·벌금), 왼쪽 수첩(내 물건의 진짜 가치), 왼쪽 앞 능력 도구 셋(저울·돋보기·가면),
    /// 그리고 탁자 위에 매달린 가격판(모두에게 보이는 현재가·남은 시간). 능력 도구와 패들은 가리켜 누를 수 있다.
    /// </summary>
    public sealed class TableKit : MonoBehaviour
    {
        GameApp app;
        WorldPanel card, ledger, notebook, board;
        readonly List<GameObject> spawned = new List<GameObject>();
        public readonly Interactable[] Tools = new Interactable[3];
        public Interactable PaddleTarget;
        Label cardTag, cardName, cardSeller, cardPhrase, cardPrice, cardStatus;
        VisualElement cardDot;
        Label ledgerCash, ledgerAvail, ledgerPenalty, ledgerLoan;
        VisualElement notebookList;
        Label boardTag, boardPrice, boardWho, boardTime, boardItem;
        VisualElement boardFill;
        int lastNoteVersion = -1;

        GameView V { get { return app.Session.View; } }

        public static TableKit Create(GameApp app)
        {
            var go = new GameObject("TableKit");
            go.transform.SetParent(app.Hall.transform, false);
            var k = go.AddComponent<TableKit>();
            k.app = app;
            k.Build();
            return k;
        }

        // ── 배치: 내 자리에서 본 좌우 x (m), 탁자 가장자리에서 안쪽으로 inset (m). 둥근 가장자리를 따라 앞뒤를 맞춘다
        Vector3 OnTable(float x, float inset, float lift = 0f)
        {
            float r = app.Hall.TableR - inset;
            float z = -Mathf.Sqrt(Mathf.Max(0.01f, r * r - x * x));
            // 가장자리의 나무 테(펠트보다 조금 높다) 위면 그 높이에
            bool onRim = inset < RimWidth;
            return new Vector3(x, Hall.TableHeight + (onRim ? 0.021f : 0.005f) + lift, z);
        }

        /// <summary>탁자 가장자리 나무 테의 폭 (탁자 크기에 따라 같이 커진다).</summary>
        float RimWidth { get { return 0.18f * app.Hall.TableR / Hall.TableRadius; } }

        /// <summary>내 눈 쪽을 향하게 (앞면 -Z 가 나를 본다), tilt 만큼 뒤로 기울인다.</summary>
        Quaternion FacingMe(Vector3 at, float tilt)
        {
            Vector3 eye = app.Rig.Eye;
            Vector3 d = at - eye;
            d.y = 0;
            return Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(tilt, 0, 0);
        }

        void Build()
        {
            var t = transform;
            // ── 경매 카드 (가운데)
            card = new WorldPanel(t, "LotCard", 720, 480, new Color(0.96f, 0.93f, 0.85f, 1f), 0.08f);
            BuildCard(card.Root);
            Stand(card, "CardStand", 0f, 0.31f, new Vector2(0.3f, 0.2f), 28f);

            // ── 장부 (오른쪽): 내 돈
            ledger = new WorldPanel(t, "Ledger", 600, 420, new Color(0.95f, 0.92f, 0.84f, 1f), 0.08f);
            BuildLedger(ledger.Root);
            Stand(ledger, "LedgerStand", 0.42f, 0.42f, new Vector2(0.28f, 0.196f), 34f);
            Coins(OnTable(0.66f, 0.3f));

            // ── 수첩 (왼쪽): 내 물건
            notebook = new WorldPanel(t, "Notebook", 560, 600, new Color(0.93f, 0.89f, 0.8f, 1f), 0.08f);
            BuildNotebook(notebook.Root);
            Stand(notebook, "NotebookStand", -0.38f, 0.36f, new Vector2(0.21f, 0.225f), 40f);

            // ── 능력 도구 셋 (왼쪽 앞, 가죽 깔판 위)
            Tray(-0.41f);
            Tools[0] = Tool("Compare", -0.28f, BuildBalance, () => L.T("ab_compare") + "  " + Count(0) + "\n" + L.T("ab_compare_desc"));
            Tools[1] = Tool("Appraise", -0.41f, BuildLoupe, () => L.T("ab_appraise") + "  " + Count(1) + "\n" + L.T("ab_appraise_desc"));
            Tools[2] = Tool("Fake", -0.54f, BuildMask, () => L.T("ab_fake") + "  " + Count(2) + "\n" + L.T("ab_fake_desc"), 1.3f);

            // ── 촛불 (왼쪽 끝): 도구와 수첩을 비춘다
            // (가장자리 테 위 왼쪽 끝: 도구 뒤에 있으면 역광이 되어 도구가 까맣게 보인다)
            Candle(OnTable(-Mathf.Min(0.8f, (app.Hall.TableR - 0.08f) * 0.84f), 0.08f));

            // ── 내 패들: 손에 들지 않고 탁자 오른쪽 앞에 눕혀 둔다 (입찰하면 들썩)
            AdoptPaddle();

            // ── 가격판 (탁자 위에 매달림, 네 면)
            board = new WorldPanel(t, "Board", 720, 400, new Color(0.06f, 0.035f, 0.03f, 1f), 0.9f);
            BuildBoard(board.Root);
            HangBoard();
        }

        string Count(int k)
        {
            var me = V != null ? V.Me : null;
            if (me == null) return "";
            int n = k == 0 ? me.CompareLeft : (k == 1 ? me.AppraiseLeft : me.FakeLeft);
            return L.T("ab_left", n);
        }

        void Stand(WorldPanel panel, string name, float x, float inset, Vector2 size, float tilt)
        {
            var at = OnTable(x, inset);
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            root.localPosition = at;
            root.localRotation = FacingMe(at, 0);
            spawned.Add(root.gameObject);
            // 황동 받침 (기운 판을 받치는 쐐기)
            var b = new ModelBuilder();
            // 테두리는 무광에 가까운 황동 (위 조명이 모서리에서 번쩍여 번지지 않게)
            int brass = b.M("frame_brass", ArtLibrary.I.Satin("Brass", 0.5f, 0.3f)), wood = b.M("Mahogany");
            b.Kit.RoundedBox(wood, new Vector3(0, 0.008f, 0.02f), new Vector3(size.x + 0.03f, 0.016f, 0.09f), 0.006f, 2);
            b.Kit.Push(new Vector3(0, 0.012f, 0.0f), new Vector3(tilt, 0, 0));
            b.Kit.RoundedBox(brass, new Vector3(0, size.y * 0.5f, 0.006f), new Vector3(size.x + 0.018f, size.y + 0.018f, 0.008f), 0.004f, 2);
            b.Kit.Pop();
            b.Build("Frame", root);
            var face = new GameObject("FacePivot").transform;
            face.SetParent(root, false);
            face.localPosition = new Vector3(0, 0.012f, 0);
            face.localRotation = Quaternion.Euler(tilt, 0, 0);
            var q = panel.Quad(face, "Face", size);
            q.transform.localPosition = new Vector3(0, size.y * 0.5f, 0.001f);
            var col = q.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 0.01f);
        }

        void Coins(Vector3 at)
        {
            var b = new ModelBuilder();
            int gold = b.M("Gold");
            var rnd = new System.Random(7);
            for (int i = 0; i < 9; i++)
            {
                float x = (float)rnd.NextDouble() * 0.01f - 0.005f, z = (float)rnd.NextDouble() * 0.01f - 0.005f;
                b.Kit.Cylinder(gold, 0.017f, i * 0.0042f, 0.004f, 18, true);
                b.Kit.Push(new Vector3(x, 0, z));
                b.Kit.Pop();
            }
            for (int i = 0; i < 5; i++)
            {
                b.Kit.Push(new Vector3(0.045f, 0, 0.012f));
                b.Kit.Cylinder(gold, 0.017f, i * 0.0042f, 0.004f, 18, true);
                b.Kit.Pop();
            }
            var go = b.Build("Coins", transform);
            go.transform.localPosition = at;
            spawned.Add(go);
        }

        const float ToolInset = 0.24f;

        void Tray(float x)
        {
            var at = OnTable(x, ToolInset);
            var b = new ModelBuilder();
            int leather = b.M("Leather"), gold = b.M("gold_satin", ArtLibrary.I.Satin("Gold"));
            b.Kit.RoundedBox(leather, new Vector3(0, 0.003f, 0), new Vector3(0.44f, 0.006f, 0.17f), 0.003f, 2);
            foreach (float s in new[] { -1f, 1f })
                b.Kit.Box(gold, new Vector3(0, 0.0065f, s * 0.072f), new Vector3(0.4f, 0.001f, 0.003f));
            var go = b.Build("ToolTray", transform);
            go.transform.localPosition = at;
            go.transform.localRotation = FacingMe(at, 0);
            spawned.Add(go);
        }

        Light candleLight;
        float candleBase;

        void Candle(Vector3 at)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            // 불꽃은 샹들리에보다 약하게 (눈앞이라 크게 번지지 않게)
            int brass = b.M("brass_satin", ArtLibrary.I.Satin("Brass")), wax = b.M("CandleWax"), flame = b.M("small_flame", ArtLibrary.I.Tinted("Flame", new Color(1f, 0.78f, 0.45f), 3.2f));
            k.Lathe(brass, new[] { P(0, 0), P(0.055f, 0), P(0.06f, 0.008f), P(0.05f, 0.012f), P(0.02f, 0.016f), P(0.016f, 0.03f), P(0.024f, 0.036f), P(0, 0.036f) }, 24);
            k.Tube(brass, MeshKit.Bezier(new Vector3(0.05f, 0.01f, 0), new Vector3(0.085f, 0.02f, 0), new Vector3(0.09f, 0.05f, 0), new Vector3(0.07f, 0.055f, 0), 8), new[] { 0.004f }, 6, false);
            k.Cylinder(wax, 0.017f, 0.036f, 0.13f, 16, true);
            k.Ellipsoid(flame, new Vector3(0, 0.178f, 0), new Vector3(0.007f, 0.016f, 0.007f), 10, 8);
            var go = b.Build("Candle", transform);
            go.transform.localPosition = at;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spawned.Add(go);
            var lgo = new GameObject("CandleLight");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, 0.2f, 0);
            candleLight = lgo.AddComponent<Light>();
            candleLight.type = LightType.Point;
            candleLight.color = new Color(1f, 0.62f, 0.3f);
            candleBase = 1.1f;
            candleLight.intensity = candleBase;
            candleLight.range = 1.1f;
            candleLight.shadows = LightShadows.None;
        }

        // ───────── 내 패들 ─────────

        Transform paddle, paddleRest;
        Vector3 paddleHome;
        Quaternion paddleHomeRot;
        float paddlePop = -1f;
        bool paddleBig;

        void AdoptPaddle()
        {
            var st = app.Stage;
            int me = st.LocalSeat;
            if (me < 0 || me >= st.Guests.Count) return;
            var g = st.Guests[me];
            if (g == null || g.Paddle == null) return;
            // 장부 앞, 탁자 가장자리를 따라 눕힌다: 손잡이는 가운데 쪽, 판은 오른쪽 (판이 테 밖으로 나가지 않게 판 중심을 펠트 위에 잡는다)
            Vector3 disc = OnTable(0.36f, 0.26f, 0.012f), grip = OnTable(0.1f, 0.2f, 0.012f);
            Vector3 dir = disc - grip;
            dir.y = 0;
            dir.Normalize();
            var at = disc - dir * (0.2f * Stage.GuestScale);
            paddleRest = new GameObject("PaddleRest").transform;
            paddleRest.SetParent(transform, false);
            paddleRest.localPosition = at;
            paddleRest.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            spawned.Add(paddleRest.gameObject);
            paddle = g.Paddle;
            g.Paddle = null;
            paddle.SetParent(paddleRest, false);
            paddle.localScale = Vector3.one * Stage.GuestScale;
            // 머리·팔과 함께 그림자만 남겨졌던 것을 되돌린다
            // 패들 앞면은 바로 위 조명을 정면으로 받아 하얗게 번지므로 조금 어둡고 무광으로
            var face = ArtLibrary.I.Tinted("Paint", new Color(0.7f, 0.64f, 0.55f));
            if (face.HasProperty("_Smoothness")) face.SetFloat("_Smoothness", 0.12f);
            foreach (var r in paddle.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name.StartsWith("Paint")) mats[i] = face;
                r.sharedMaterials = mats;
            }
            paddleHome = Vector3.zero;
            paddleHomeRot = Quaternion.Euler(90f, 0, 0);
            paddle.localPosition = paddleHome;
            paddle.localRotation = paddleHomeRot;
            var col = paddleRest.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.02f, 0.17f);
            col.size = new Vector3(0.26f, 0.06f, 0.44f);
            PaddleTarget = paddleRest.gameObject.AddComponent<Interactable>();
            PaddleTarget.HoverLift = 0.01f;
        }

        /// <summary>내가 입찰하면 패들을 번쩍 들었다 내린다.</summary>
        public void PaddlePop(bool big)
        {
            if (paddle == null) return;
            paddlePop = 0f;
            paddleBig = big;
        }

        void Update()
        {
            if (candleLight != null)
            {
                float t = Time.time;
                candleLight.intensity = candleBase * (0.82f + 0.12f * Mathf.PerlinNoise(t * 3.1f, 0.3f) + 0.06f * Mathf.Sin(t * 17f));
            }
            if (paddle != null && paddlePop >= 0f)
            {
                paddlePop += Time.deltaTime / (paddleBig ? 1.1f : 0.8f);
                float u = Mathf.Clamp01(paddlePop);
                // 빨리 올라가고 천천히 내려온다
                float up = u < 0.25f ? Mathf.SmoothStep(0, 1, u / 0.25f) : Mathf.SmoothStep(1, 0, (u - 0.25f) / 0.75f);
                float wave = paddleBig ? Mathf.Sin(paddlePop * 26f) * 10f * up : 0f;
                // 눈앞이라 다 세우면 화면 한쪽을 다 가린다 → 반쯤만 들어 올린다
                paddle.localPosition = paddleHome + new Vector3(0, 0.1f, 0.04f) * up;
                paddle.localRotation = Quaternion.Slerp(paddleHomeRot, Quaternion.Euler(48f, 0, wave), up);
                if (u >= 1f) paddlePop = -1f;
            }
        }

        Interactable Tool(string name, float lateral, System.Action<ModelBuilder> build, System.Func<string> tooltip, float scale = 1.6f)
        {
            var at = OnTable(lateral, ToolInset, 0.006f);
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = at;
            root.transform.localRotation = FacingMe(at, 0);
            var b = new ModelBuilder();
            build(b);
            var model = b.Build("Model", root.transform);
            model.transform.localScale = Vector3.one * scale;
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.08f, 0);
            col.size = new Vector3(0.17f, 0.19f, 0.17f);
            var it = root.AddComponent<Interactable>();
            it.Tooltip = tooltip;
            spawned.Add(root);
            return it;
        }

        // ───────── 능력 도구 모형 ─────────

        /// <summary>속까지 보이는 접시 (바깥 면 + 안쪽 면, 위에서 봐도 비지 않게).</summary>
        static void Dish(MeshKit k, int mat, float r, float depth)
        {
            var outside = new[] { P(0, -depth), P(r * 0.55f, -depth * 0.9f), P(r * 0.9f, -depth * 0.4f), P(r, 0f) };
            k.Lathe(mat, outside, 20);
            var inside = new Vector2[outside.Length];
            for (int i = 0; i < outside.Length; i++) inside[i] = new Vector2(outside[outside.Length - 1 - i].x * 0.94f, outside[outside.Length - 1 - i].y + 0.0015f);
            k.Lathe(mat, inside, 20);
        }

        static void BuildBalance(ModelBuilder b)
        {
            // 시세 비교: 저울 (나무 받침 · 황동 기둥 · 가로대 · 사슬 세 줄에 매달린 접시 둘 · 가운데 바늘)
            var k = b.Kit;
            int brass = b.M("brass_satin", ArtLibrary.I.Satin("Brass", 0.55f, 0.45f)), wood = b.M("Mahogany"), dark = b.M("BlackGloss");
            k.Lathe(wood, new[] { P(0, 0), P(0.036f, 0), P(0.037f, 0.008f), P(0.03f, 0.014f), P(0.016f, 0.018f), P(0, 0.018f) }, 28);
            k.Lathe(brass, new[] { P(0.0105f, 0.018f), P(0.009f, 0.024f), P(0.0055f, 0.028f) }, 14);
            k.Cylinder(brass, 0.0042f, 0.026f, 0.082f, 12, false);
            k.Sphere(brass, new Vector3(0, 0.112f, 0), 0.0075f, 12, 8);
            k.Lathe(brass, new[] { P(0.002f, 0.118f), P(0.0035f, 0.124f), P(0, 0.131f) }, 10);
            // 가로대: 가운데가 두툼한 막대 + 양 끝 고리
            k.Tube(brass, new List<Vector3> { new Vector3(-0.055f, 0.103f, 0), new Vector3(0, 0.106f, 0), new Vector3(0.055f, 0.103f, 0) }, new[] { 0.0022f, 0.0036f, 0.0022f }, 8, true);
            k.Tube(dark, new List<Vector3> { new Vector3(0, 0.104f, 0.004f), new Vector3(0, 0.08f, 0.005f) }, new[] { 0.0012f, 0.0006f }, 5, true);
            foreach (float s in new[] { -1f, 1f })
            {
                var hook = new Vector3(s * 0.055f, 0.1f, 0);
                k.Push(hook + new Vector3(0, 0.002f, 0), new Vector3(90, 0, 0));
                k.Torus(brass, 0.004f, 0.0012f, 10, 4);
                k.Pop();
                var pan = new Vector3(s * 0.055f, 0.046f, 0);
                for (int c = 0; c < 3; c++)
                {
                    float a = c / 3f * Mathf.PI * 2 + 0.5f;
                    k.Tube(brass, new List<Vector3> { hook, pan + new Vector3(Mathf.Cos(a) * 0.021f, 0.002f, Mathf.Sin(a) * 0.021f) }, new[] { 0.0008f }, 4, false);
                }
                k.Push(pan);
                Dish(k, brass, 0.024f, 0.007f);
                k.Pop();
            }
        }

        static void BuildLoupe(ModelBuilder b)
        {
            // 간이감정: 작은 받침에 비스듬히 세운 돋보기 (렌즈가 나를 본다, 손잡이는 앞으로)
            var k = b.Kit;
            int brass = b.M("brass_satin", ArtLibrary.I.Satin("Brass", 0.55f, 0.45f)), glass = b.M("Glass"), wood = b.M("DarkWood"), wood2 = b.M("Mahogany");
            // 받침: 낮은 나무 대 + 렌즈를 받치는 V 자 홈
            k.RoundedBox(wood2, new Vector3(0, 0.006f, 0.012f), new Vector3(0.05f, 0.012f, 0.03f), 0.004f, 2);
            foreach (float s in new[] { -1f, 1f })
                k.Tube(brass, new List<Vector3> { new Vector3(s * 0.016f, 0.012f, 0.016f), new Vector3(s * 0.012f, 0.03f, 0.02f) }, new[] { 0.0018f }, 5, true);
            k.Push(new Vector3(0, 0.06f, 0.012f), new Vector3(-58, 0, 0));
            k.Torus(brass, 0.031f, 0.0045f, 36, 8);
            // 볼록 렌즈 (윗면은 가장자리 → 가운데, 아랫면은 가운데 → 가장자리 순서라야 바깥을 본다)
            k.Lathe(glass, new[] { P(0.03f, 0f), P(0.018f, 0.0022f), P(0, 0.0028f) }, 28);
            k.Lathe(glass, new[] { P(0, -0.0028f), P(0.018f, -0.0022f), P(0.03f, 0f) }, 28);
            // 손잡이: 테 아래쪽에서 앞(나 쪽)으로
            k.Push(new Vector3(0, 0, -0.034f), new Vector3(-90, 0, 0));
            k.Cylinder(brass, 0.0055f, 0f, 0.012f, 12, false);
            k.Lathe(wood, new[] { P(0.0062f, 0.012f), P(0.0072f, 0.03f), P(0.0068f, 0.06f), P(0.005f, 0.066f), P(0, 0.067f) }, 12, true);
            k.Pop();
            k.Pop();
        }

        static void BuildMask(ModelBuilder b)
        {
            // 거짓 통보: 받침대에 꽂아 세운 가면무도회 가면 (보라 칠기 · 금테 · 눈구멍 · 깃털), 앞면이 나를 본다
            var k = b.Kit;
            int mask = b.M("mask", ArtLibrary.I.Tinted("Lacquer", new Color(0.62f, 0.32f, 0.86f))), gold = b.M("gold_satin", ArtLibrary.I.Satin("Gold", 0.6f, 0.5f)),
                stick = b.M("Mahogany"), hole = b.M("Pupil"), feather = b.M("feather", ArtLibrary.I.Tinted("Feather", new Color(0.95f, 0.8f, 0.4f)));
            k.Lathe(stick, new[] { P(0, 0), P(0.022f, 0), P(0.023f, 0.008f), P(0.012f, 0.012f), P(0, 0.012f) }, 20);
            k.Tube(gold, new List<Vector3> { new Vector3(0, 0.01f, 0), new Vector3(0.004f, 0.05f, 0.012f) }, new[] { 0.0026f }, 6, true);
            System.Func<float, Vector2> shape = u => new Vector2(-0.85f + 0.55f * Mathf.Exp(-u * u / 0.03f) + 0.3f * Mathf.Abs(u), 0.7f + 0.45f * Mathf.Pow(Mathf.Abs(u), 2.2f));
            const float w = 0.1f, h = 0.042f, bend = 0.07f;
            // 뒤로 기울여 세워 위 조명이 앞면에 닿게 (앞면은 -Z, X 축 + 회전이라야 얼굴이 위로 든다)
            k.Push(new Vector3(0.004f, 0.066f, 0.02f), new Vector3(34, 0, 6));
            k.CurvedPanel(mask, w, h, bend, shape, 28, 6, 0.004f);
            // 금테: 가장자리를 따라 한 바퀴
            var rim = new List<Vector3>();
            const int N = 28;
            for (int i = 0; i <= N; i++) rim.Add(MaskEdge(i / (float)N * 2 - 1, true, shape, w, h, bend));
            for (int i = N; i >= 0; i--) rim.Add(MaskEdge(i / (float)N * 2 - 1, false, shape, w, h, bend));
            k.Tube(gold, rim, new[] { 0.0016f }, 5, false);
            // 눈구멍 (앞·뒤)
            foreach (float s in new[] { -1f, 1f })
            {
                float ang = s * 0.022f / bend;
                // 앞면 (반지름 bend) 보다 조금 바깥이라야 보인다
                var p = new Vector3(Mathf.Sin(ang) * (bend + 0.0006f), 0.002f, -Mathf.Cos(ang) * (bend + 0.0006f) + bend);
                k.Push(p, new Vector3(0, -s * 18, 0));
                k.Ellipsoid(hole, Vector3.zero, new Vector3(0.011f, 0.0065f, 0.0035f), 14, 6);
                k.Pop();
            }
            // 깃털 셋 (오른쪽 위로 부채꼴)
            for (int i = 0; i < 3; i++)
            {
                k.Push(new Vector3(0.038f, 0.016f, 0.012f), new Vector3(0, 0, -25 + i * 22));
                k.CurvedPanel(feather, 0.012f, 0.045f, 0.2f, u => new Vector2(0f, 1f - 0.75f * u * u), 6, 6, 0.002f);
                k.Pop();
            }
            k.Pop();
        }

        static Vector3 MaskEdge(float u, bool top, System.Func<float, Vector2> shape, float w, float h, float bendR)
        {
            Vector2 range = shape(u);
            float ang = u * w * 0.5f / bendR;
            float y = (top ? range.y : range.x) * h * 0.5f;
            return new Vector3(Mathf.Sin(ang) * bendR, y, -Mathf.Cos(ang) * bendR + bendR - 0.001f);
        }

        /// <summary>도구 위 남은 횟수 표시를 둘 자리 (모형 꼭대기 바로 위).</summary>
        readonly Vector3[] badgeOffset = new Vector3[3];
        readonly bool[] badgeReady = new bool[3];

        public Vector3 ToolBadge(int i)
        {
            var t = Tools[i];
            if (t == null) return Vector3.zero;
            if (!badgeReady[i])
            {
                var rs = t.GetComponentsInChildren<MeshRenderer>();
                Vector3 top = t.transform.position + Vector3.up * 0.13f;
                if (rs.Length > 0)
                {
                    var bnd = rs[0].bounds;
                    foreach (var r in rs) bnd.Encapsulate(r.bounds);
                    // 깃털·사슬 끝까지 재면 너무 높아지므로 도구 몸통 높이 정도로 자른다
                    top = new Vector3(bnd.center.x, Mathf.Min(bnd.max.y + 0.02f, t.transform.position.y + 0.17f), bnd.center.z);
                }
                badgeOffset[i] = top - t.transform.position;
                badgeReady[i] = true;
            }
            return t.transform.position + badgeOffset[i];
        }

        // ───────── 가격판 ─────────

        void HangBoard()
        {
            var root = new GameObject("PriceBoard").transform;
            root.SetParent(transform, false);
            root.localPosition = new Vector3(0, 2.3f, 0);
            spawned.Add(root.gameObject);
            var b = new ModelBuilder();
            var k = b.Kit;
            int brass = b.M("Brass"), wood = b.M("Mahogany"), chain = b.M("GoldDark");
            float w = 0.64f, h = 0.37f, d = (w + 0.05f) * 0.5f + 0.004f;
            k.RoundedBox(wood, Vector3.zero, new Vector3(w + 0.05f, h + 0.05f, w + 0.05f), 0.02f, 2);
            k.RoundedBox(brass, new Vector3(0, h * 0.5f + 0.035f, 0), new Vector3(w + 0.09f, 0.03f, w + 0.09f), 0.012f, 2);
            k.RoundedBox(brass, new Vector3(0, -h * 0.5f - 0.035f, 0), new Vector3(w + 0.09f, 0.03f, w + 0.09f), 0.012f, 2);
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                k.Tube(chain, new List<Vector3> { new Vector3(c.x * w * 0.4f, h * 0.5f + 0.05f, c.y * w * 0.4f), new Vector3(0, 0.95f, 0) }, new[] { 0.003f }, 4, false);
            b.Build("Frame", root);
            for (int i = 0; i < 4; i++)
            {
                var face = new GameObject("Face" + i).transform;
                face.SetParent(root, false);
                face.localRotation = Quaternion.Euler(0, i * 90f, 0);
                var q = board.Quad(face, "Screen", new Vector2(w, h));
                q.transform.localPosition = new Vector3(0, 0, -d);
            }
        }

        // ───────── 패널 내용 ─────────

        void BuildCard(VisualElement r)
        {
            r.style.paddingLeft = r.style.paddingRight = 34;
            r.style.paddingTop = 26;
            r.style.alignItems = Align.Center;
            r.Add(Frame(r, "#8f7240"));
            cardTag = Ui.Text("", "wp-tag");
            cardName = Ui.Text("", "wp-title");
            var sr = Ui.Row();
            cardDot = Ui.Dot(Color.white, 22);
            cardSeller = Ui.Text("", "wp-sub");
            sr.Add(cardDot, cardSeller);
            cardPhrase = Ui.Text("", "wp-quote");
            cardPrice = Ui.Text("", "wp-price");
            cardStatus = Ui.Text("", "wp-sub");
            r.Add(cardTag, cardName, sr, Ui.Div("wp-rule"), cardPhrase, cardPrice, cardStatus);
            // 직접 넣은 그림 (오른쪽 위 작은 사진)
            cardImage = Ui.Div("wp-image");
            cardImage.pickingMode = PickingMode.Ignore;
            cardImage.Show(false);
            r.Add(cardImage);
        }

        VisualElement cardImage;

        void BuildLedger(VisualElement r)
        {
            r.style.paddingLeft = r.style.paddingRight = 30;
            r.style.paddingTop = 22;
            r.Add(Frame(r, "#8f7240"));
            r.Add(Ui.Text(L.T("wallet").ToUpperInvariant(), "wp-tag"));
            ledgerCash = Ui.Text("", "wp-price");
            ledgerAvail = Ui.Text("", "wp-sub");
            ledgerPenalty = Ui.Text("", "wp-small");
            ledgerLoan = Ui.Text("", "wp-small");
            r.Add(ledgerCash, ledgerAvail, Ui.Div("wp-rule"), ledgerPenalty, ledgerLoan);
        }

        void BuildNotebook(VisualElement r)
        {
            r.style.paddingLeft = r.style.paddingRight = 30;
            r.style.paddingTop = 24;
            r.Add(Frame(r, "#6a4a3c"));
            r.Add(Ui.Text(L.T("tk_my_lots").ToUpperInvariant(), "wp-tag"));
            notebookList = Ui.Col();
            r.Add(notebookList);
        }

        void BuildBoard(VisualElement r)
        {
            r.style.alignItems = Align.Center;
            r.style.paddingTop = 18;
            r.Add(Frame(r, "#c9a24a"));
            boardTag = Ui.Text("", "wb-tag");
            boardItem = Ui.Text("", "wb-item");
            boardPrice = Ui.Text("", "wb-price");
            boardWho = Ui.Text("", "wb-who");
            var track = Ui.Div("wb-track");
            boardFill = Ui.Div("wb-fill");
            track.Add(boardFill);
            boardTime = Ui.Text("", "wb-time");
            r.Add(boardTag, boardItem, boardPrice, boardWho, track, boardTime);
        }

        static VisualElement Frame(VisualElement r, string color)
        {
            var f = Ui.Div("wp-frame");
            f.style.borderTopColor = f.style.borderBottomColor = f.style.borderLeftColor = f.style.borderRightColor = Ui.Hex(color);
            f.pickingMode = PickingMode.Ignore;
            return f;
        }

        // ═════════════════ 갱신 ═════════════════

        Color SeatColor(int seat)
        {
            var v = V;
            if (seat < 0 || seat >= v.SeatCount) return Color.gray;
            return Stage.SeatColors[v.Seats[seat].ColorIndex % Stage.SeatColors.Length];
        }

        string Name(int seat)
        {
            var v = V;
            if (seat == -2) return L.T("someone");
            if (seat < 0 || seat >= v.SeatCount) return "";
            return seat == v.Viewer ? L.T("you") : v.Seats[seat].Name;
        }

        public void Refresh()
        {
            var v = V;
            if (v == null || v.Seats.Length == 0) return;
            var it = v.Current;
            int total = v.SeatCount * v.Rounds;
            bool lot = it != null && (v.Phase == Phase.Presenting || v.BiddingOpen || v.Phase == Phase.PriceDrop || v.Phase == Phase.AuctionResult);

            // 경매 카드
            if (lot)
            {
                cardTag.text = (L.T("lot_of", v.AuctionOrder.Count, total) + (v.LeftoverPass ? "  ·  " + L.T("reauction") : "")).ToUpperInvariant();
                cardName.text = GameScreen.ItemName(it);
                cardDot.style.backgroundColor = SeatColor(it.Seller);
                cardSeller.text = L.T("seller") + "  " + Name(it.Seller);
                cardPhrase.text = !string.IsNullOrEmpty(it.CustomPhrase) ? "“" + it.CustomPhrase + "”" : (it.Phrase >= 0 ? "“" + L.T("phrase_" + it.Phrase) + "”" : "");
                cardPrice.text = L.T("start_price") + "  " + L.G(it.InitialStart) + (it.Dropped ? "  →  " + L.G(it.StartPrice) : "");
                cardStatus.text = it.Seller == v.Viewer ? L.T("tk_your_lot", it.MyValue >= 0 ? L.G(it.MyValue) : "") : "";
                var pic = ItemImages.Get(it.Id, v.Viewer);
                cardImage.Show(pic != null);
                if (pic != null) cardImage.style.backgroundImage = Background.FromTexture2D(pic);
            }
            else if (v.Phase == Phase.Reveal && revealItem >= 0 && revealItem < v.Items.Length)
            {
                // 공개 중: 방금 뒤집은 물건과 진짜 가치
                var ri = v.Items[revealItem];
                cardTag.text = L.T("reveal_title").ToUpperInvariant();
                cardName.text = GameScreen.ItemName(ri);
                cardDot.style.backgroundColor = SeatColor(ri.Seller);
                cardSeller.text = L.T("seller") + "  " + Name(ri.Seller);
                cardPhrase.text = revealPaid > 0 ? L.T("reveal_paid") + "  " + L.G(revealPaid) : "";
                cardPrice.text = L.T("reveal_value") + "  " + L.G(revealValue);
                int diff = revealValue - revealPaid;
                cardStatus.text = revealPaid > 0 ? (diff >= 0 ? "+" : "−") + L.G(Mathf.Abs(diff)) : "";
            }
            else
            {
                cardImage.Show(false);
                cardTag.text = v.Phase == Phase.Registration ? L.T("reg_title").ToUpperInvariant() : L.T("title").ToUpperInvariant();
                cardName.text = v.Phase == Phase.Registration ? L.T("tk_register_hint") : "";
                cardSeller.text = "";
                cardDot.style.backgroundColor = new Color(0, 0, 0, 0);
                cardPhrase.text = "";
                cardPrice.text = "";
                cardStatus.text = "";
            }

            // 장부
            var me = v.Me;
            if (me != null)
            {
                ledgerCash.text = L.G(me.Balance);
                ledgerAvail.text = L.T("available") + " " + L.G(v.MyAvailable) + (me.Reserved > 0 ? "   ·   " + L.T("reserved") + " " + L.G(me.Reserved) : "");
                int pen = v.MyPenaltyNow;
                ledgerPenalty.text = pen > 0 ? L.T("penalty_now", L.G(pen)) + "\n" + L.T("penalty_hint", L.G(v.MyUnspentBudget)) : L.T("penalty_none");
                ledgerPenalty.EnableInClassList("wp-red", pen > 0);
                ledgerPenalty.EnableInClassList("wp-green", pen == 0);
                ledgerLoan.text = me.Loan ? L.T("loan_taken") : (v.Rules.LoanAmount > 0 ? L.T("tk_loan_hint", L.G(v.Rules.LoanAmount)) : "");
            }

            // 수첩: 내 물건 (바뀔 때만)
            if (v.Version != lastNoteVersion)
            {
                lastNoteVersion = v.Version;
                notebookList.Clear();
                foreach (var item in v.Items)
                {
                    if (item.Seller != v.Viewer || item.Status == ItemStatus.None) continue;
                    var row = Ui.Col("wp-note");
                    var top = Ui.Row();
                    var nm = Ui.Text(item.Appearance >= 0 ? GameScreen.ItemName(item) : "-", "wp-note-name");
                    nm.style.flexGrow = 1;
                    top.Add(nm, Ui.Text(item.MyValue >= 0 ? L.G(item.MyValue) : "", "wp-note-value"));
                    row.Add(top);
                    row.Add(Ui.Text(NoteStatus(item), "wp-small"));
                    notebookList.Add(row);
                }
                if (notebookList.childCount == 0) notebookList.Add(Ui.Text(L.T("tk_register_hint"), "wp-small"));
            }
        }

        int revealItem = -1, revealValue, revealPaid;

        /// <summary>공개 단계에서 물건 하나를 뒤집으면 경매 카드에 그 물건의 진짜 가치를 적는다.</summary>
        public void ShowReveal(int item, int value, int paid)
        {
            revealItem = item;
            revealValue = value;
            revealPaid = paid;
            Refresh();
            Ui.Pop(cardPrice, 0.2f);
        }

        string NoteStatus(ItemView it)
        {
            switch (it.Status)
            {
                case ItemStatus.Pending: return L.T("start_price") + " " + L.G(it.InitialStart);
                case ItemStatus.Active: return "▶ " + L.T("tk_on_block");
                case ItemStatus.Sold: return L.T("tk_sold_to", Name(it.Winner), L.G(it.SalePrice));
                case ItemStatus.Deferred: return L.T("reauction") + " " + L.G(it.StartPrice);
                case ItemStatus.Unsold: return L.T("unsold");
                case ItemStatus.Returned: return L.T("reveal_returned");
                default: return "";
            }
        }

        /// <summary>매 프레임: 가격판의 시간 막대.</summary>
        public void Tick()
        {
            var v = V;
            if (v == null || v.Seats.Length == 0) return;
            var it = v.Current;
            int ms = app.Session.RemainingMs;
            if (it != null && (v.Phase == Phase.Presenting || v.BiddingOpen || v.Phase == Phase.PriceDrop || v.Phase == Phase.AuctionResult))
            {
                boardTag.text = (L.T("lot_of", v.AuctionOrder.Count, v.SeatCount * v.Rounds) + (v.LeftoverPass ? "  ·  " + L.T("reauction") : "")).ToUpperInvariant();
                boardItem.text = GameScreen.ItemName(it);
                if (it.Status == ItemStatus.Sold)
                {
                    boardPrice.text = L.G(it.SalePrice);
                    boardWho.text = "SOLD · " + Name(it.Winner);
                }
                else if (v.HighestBidder != -1)
                {
                    boardPrice.text = L.G(v.HighestBid);
                    boardWho.text = Name(v.HighestBidder);
                }
                else
                {
                    boardPrice.text = L.G(it.StartPrice);
                    boardWho.text = v.Phase == Phase.PriceDrop ? L.T("nobid_title") : L.T("no_bids_yet");
                }
                float frac = v.DurationMs > 0 ? Mathf.Clamp01(ms / (float)v.DurationMs) : 0f;
                boardFill.style.width = Length.Percent(frac * 100f);
                bool hot = v.BiddingOpen && ms < 5000;
                boardFill.EnableInClassList("wb-fill-hot", hot);
                boardTime.text = v.BiddingOpen || v.Phase == Phase.PriceDrop ? L.T("seconds", Mathf.CeilToInt(ms / 1000f)) : (v.Phase == Phase.Presenting ? L.T("presenting") : "");
                boardTime.EnableInClassList("wb-time-hot", hot);
            }
            else
            {
                boardTag.text = L.T("title").ToUpperInvariant();
                boardItem.text = v.Phase == Phase.Registration ? L.T("reg_title") : (v.Phase == Phase.Reveal ? L.T("reveal_title") : "");
                boardPrice.text = "BLUFF";
                boardWho.text = "AUCTION";
                boardFill.style.width = 0;
                boardTime.text = "";
            }
        }

        void OnDestroy()
        {
            if (card != null) card.Destroy();
            if (ledger != null) ledger.Destroy();
            if (notebook != null) notebook.Destroy();
            if (board != null) board.Destroy();
        }
    }
}
