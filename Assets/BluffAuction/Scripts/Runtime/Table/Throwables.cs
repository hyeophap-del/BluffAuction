using System.Collections;
using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.Net;
using BluffAuction.World;
using UnityEngine;
using UnityEngine.InputSystem;
using static BluffAuction.Art.ModelBuilder;

namespace BluffAuction.Table
{
    /// <summary>
    /// 던질 수 있는 탁자 위 물건 (빵·사과·종이 뭉치·장미·코르크): 가리켜 F(또는 클릭)로 집고, 클릭·F 로 던진다.
    /// 가리킨 사람이 있으면 그 사람에게 포물선으로 날아간다. 던지기는 좌석 기준으로 모두에게 보내고 각자 계산해 그린다.
    /// </summary>
    public sealed class Throwables : MonoBehaviour
    {
        public const int KindCount = 5;
        /// <summary>근접 무기: 말아 쥔 신문 하나 (탁자 위 물건 번호는 MeleeBase).</summary>
        public const int MeleeCount = 1;
        const int MeleeBase = 10;
        /// <summary>근접 무기가 닿는 거리 (내 눈 → 상대 머리). 바로 옆자리는 탁자가 커도 늘 닿는다.</summary>
        public const float Reach = 2.4f;
        const float Speed = 7f;
        GameApp app;
        readonly List<Interactable> props = new List<Interactable>();
        readonly Dictionary<Interactable, int> kindOf = new Dictionary<Interactable, int>();
        Interactable held;
        GameObject heldModel;
        float splatUntil;
        int splatKind = -1;
        const float SoftSplat = 1.4f, HardSplat = 0.6f;
        public bool Holding { get { return held != null; } }
        public float SplatAmount { get { return Mathf.Clamp01((splatUntil - Time.unscaledTime) / (splatKind >= 0 ? SoftSplat : HardSplat)); } }
        /// <summary>얼굴에 맞은 것: -1 딱딱한 것(짧게 어두워짐) · 1 사과(과육 조각) · 3 토마토(씨가 보이는 과즙).</summary>
        public int SplatKind { get { return splatKind; } }

        public static Throwables Create(GameApp app)
        {
            var go = new GameObject("Throwables");
            go.transform.SetParent(app.Hall.transform, false);
            var t = go.AddComponent<Throwables>();
            t.app = app;
            t.BuildColliders();
            t.SpawnProps();
            Interactor.SuppressClick = () => t.Holding;
            return t;
        }

        void OnDestroy()
        {
            if (heldModel != null) Destroy(heldModel);
            Interactor.SuppressClick = null;
        }

        // ── 부딪힐 곳: 탁자 윗면·바닥 (경매장은 원래 충돌체가 없다)
        void BuildColliders()
        {
            float r = app.Hall.TableR;
            var disc = new GameObject("TableTopCollider");
            disc.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "TableTop" };
            const int N = 40;
            var v = new Vector3[N + 1];
            var tris = new int[N * 3];
            v[0] = new Vector3(0, Hall.TableHeight + 0.01f, 0);
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Hall.TableHeight + 0.01f, Mathf.Sin(a) * r);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + (i + 1) % N;
                tris[i * 3 + 2] = 1 + i;
            }
            mesh.vertices = v;
            mesh.triangles = tris;
            disc.AddComponent<MeshCollider>().sharedMesh = mesh;
            disc.AddComponent<MeshOwner>().Mesh = mesh;
            var floor = new GameObject("FloorCollider");
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0, -0.05f, 0);
            floor.AddComponent<BoxCollider>().size = new Vector3(30f, 0.1f, 30f);
        }

        Vector3 OnRim(float x, float inset)
        {
            float rr = app.Hall.TableR - inset;
            return new Vector3(x, Hall.TableHeight + 0.021f, -Mathf.Sqrt(Mathf.Max(0.01f, rr * rr - x * x)));
        }

        void SpawnProps()
        {
            var spots = new[] { new Vector3(-0.17f, 0.06f, 0), new Vector3(0.0f, 0.05f, 1), new Vector3(0.17f, 0.06f, 2), new Vector3(-0.58f, 0.07f, 3), new Vector3(0.64f, 0.07f, 4) };
            foreach (var s in spots)
            {
                int kind = (int)s.z;
                var go = BuildModel(kind, transform);
                go.transform.localPosition = OnRim(s.x, s.y) + Vector3.up * Radius(kind) * 0.8f;
                go.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), kind == 3 ? 90f : 0f);
                var col = go.AddComponent<SphereCollider>();
                col.radius = Radius(kind) * 1.6f;
                col.isTrigger = true;
                var it = go.AddComponent<Interactable>();
                it.HoverLift = 0.01f;
                int k = kind;
                it.Tooltip = () => L.T("throw_" + k) + "\n" + L.T("throw_pick");
                it.OnClick = () => PickUp(it);
                props.Add(it);
                kindOf[it] = kind;
            }
            // 근접 무기: 말아 쥔 신문 하나 — 코르크(테 위)보다 안쪽 펠트 위, 오른쪽으로 뻗게 눕혀 둔다
            {
                var go = BuildWeapon(0, transform);
                float rr = app.Hall.TableR - 0.2f, x = 0.56f;
                var at = new Vector3(x, Hall.TableHeight + 0.028f, -Mathf.Sqrt(Mathf.Max(0.01f, rr * rr - x * x)));
                go.transform.localPosition = at;
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(-at.z, 0, at.x).normalized, Vector3.up) * Quaternion.Euler(90, 0, 0);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.18f, 0);
                col.size = new Vector3(0.06f, 0.36f, 0.06f);
                col.isTrigger = true;
                var it = go.AddComponent<Interactable>();
                it.HoverLift = 0.012f;
                it.Tooltip = () => L.T("melee_0") + "\n" + L.T("melee_pick");
                it.OnClick = () => PickUp(it);
                props.Add(it);
                kindOf[it] = MeleeBase;
            }
        }

        /// <summary>근접 무기 모양: 말아 쥔 신문 (손잡이가 원점, +Y 로 뻗는다).</summary>
        public static GameObject BuildWeapon(int kind, Transform parent)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            var lib = ArtLibrary.I;
            // 살짝 납작하게 만 종이 + 글줄 무늬 + 끝의 겹친 단면 + 가운데 고무줄
            int paper = b.M("paper", lib.Tinted("Cloth", new Color(0.88f, 0.86f, 0.8f))), ink = b.M("ink", lib.Tinted("Cloth", new Color(0.3f, 0.3f, 0.32f))),
                band = b.M("band", lib.Tinted("Lacquer", new Color(0.75f, 0.12f, 0.1f)));
            k.Push(Vector3.zero, Quaternion.identity, new Vector3(1f, 1f, 0.82f));
            k.Lathe(paper, new[] { P(0.022f, 0f), P(0.025f, 0.02f), P(0.026f, 0.17f), P(0.027f, 0.33f), P(0.025f, 0.36f) }, 18, true, true);
            for (int i = 0; i < 8; i++)
                k.Lathe(ink, new[] { P(0.0262f, 0.2f + i * 0.018f), P(0.0265f, 0.207f + i * 0.018f) }, 18, false, false, 150f);
            k.Lathe(ink, new[] { P(0.026f, 0.05f), P(0.0262f, 0.11f) }, 18, false, false, 70f);
            k.Push(new Vector3(0, 0.36f, 0));
            k.Torus(paper, 0.017f, 0.004f, 18, 6);
            k.Torus(paper, 0.009f, 0.003f, 14, 6);
            k.Pop();
            k.Lathe(band, new[] { P(0.0262f, 0.135f), P(0.0275f, 0.14f), P(0.0262f, 0.145f) }, 18);
            k.Pop();
            return b.Build("Newspaper", parent);
        }

        /// <summary>개발용: 탁자 위 물건들의 자리 (가까이 찍기).</summary>
        public List<Vector3> DevPropPositions()
        {
            var l = new List<Vector3>();
            foreach (var p in props) if (p != null) l.Add(p.transform.position);
            return l;
        }

        /// <summary>개발용: 탁자의 근접 무기를 집는다.</summary>
        public void DevPickMelee(int kind)
        {
            foreach (var kv in kindOf) if (kv.Value == MeleeBase + kind && kv.Key.gameObject.activeSelf) { PickUp(kv.Key); return; }
        }

        /// <summary>개발용: 들고 있는 무기로 그 좌석을 때린다.</summary>
        public void DevSwingAt(int seat)
        {
            if (!HoldingMelee) return;
            swingT = 0f;
            SocialHub.I.SendMelee(kindOf[held] - MeleeBase, InReach(app.Stage.LocalSeat, seat) ? seat : -1);
        }

        /// <summary>개발용: 클릭과 같은 길로 휘두른다 (조준점 아래 사람).</summary>
        public void DevSwing() { Swing(); }

        bool HoldingMelee { get { return held != null && kindOf[held] >= MeleeBase; } }
        public bool HoldingWeapon { get { return HoldingMelee; } }
        float swingT = -1f;

        /// <summary>두 좌석이 근접 무기가 닿을 만큼 가까운지 (바로 옆자리이거나 머리 사이가 Reach 안).</summary>
        public bool InReach(int a, int b)
        {
            var st = app.Stage;
            int n = st.Guests.Count;
            if (a < 0 || b < 0 || a >= n || b >= n || a == b) return false;
            int gap = Mathf.Abs(st.Visual(a) - st.Visual(b));
            if (gap == 1 || gap == n - 1) return true;
            return Vector3.Distance(st.SeatHead(a), st.SeatHead(b)) <= Reach;
        }

        /// <summary>근접 무기 조준: 조준점 아래 사람(없으면 -1)과, 그 사람이 닿는 거리인지.</summary>
        public int MeleeAim(out bool inReach)
        {
            int t = AimedSeat();
            inReach = t >= 0 && InReach(app.Stage.LocalSeat, t);
            return t;
        }

        float lastFarToast;

        void Swing()
        {
            if (held == null || swingT >= 0f) return;
            swingT = 0f;
            bool ok;
            int target = MeleeAim(out ok);
            if (target >= 0 && !ok)
            {
                target = -1;
                if (Time.unscaledTime - lastFarToast > 3f)
                {
                    lastFarToast = Time.unscaledTime;
                    UI.UiRoot.I.Toast(L.T("melee_far"), null, 1.8f);
                }
            }
            SocialHub.I.SendMelee(kindOf[held] - MeleeBase, target);
        }

        /// <summary>들고 있던 근접 무기를 제자리에 둔다.</summary>
        void PutBack()
        {
            if (held == null) return;
            Destroy(heldModel);
            heldModel = null;
            held.gameObject.SetActive(true);
            held = null;
            swingT = -1f;
            AudioManager.I.Play("sfx_thud", 0.4f, 1.3f);
        }

        /// <summary>누군가 휘둘렀다: 남이면 왼손에 무기를 쥐고 내려치는 몸짓, 맞으면 효과.</summary>
        public void PlaySwing(MeleeMsg m)
        {
            var st = app.Stage;
            if (m.Seat < 0 || m.Seat >= st.Guests.Count) return;
            bool mine = m.Seat == st.LocalSeat;
            if (!mine)
            {
                var g = st.Guests[m.Seat];
                Vector3 at = m.Target >= 0 && m.Target < st.Guests.Count
                    ? (m.Target == st.LocalSeat ? app.Rig.Cam.transform.position : st.SeatHead(m.Target))
                    : g.Head.position + g.transform.forward * 1.2f;
                g.SwingAt(at);
                if (g.GloveL != null && g.GloveL.Grip != null)
                {
                    var w = BuildWeapon(Mathf.Clamp(m.Kind, 0, MeleeCount - 1), g.GloveL.Grip);
                    w.transform.localPosition = new Vector3(0, -0.01f, 0);
                    w.transform.localRotation = Quaternion.Euler(180, 0, 0);
                    w.transform.localScale = Vector3.one * Stage.GuestScale;
                    Destroy(w, 1.05f);
                }
            }
            StartCoroutine(SwingHit(m, mine ? 0.12f : 0.4f));
        }

        IEnumerator SwingHit(MeleeMsg m, float delay)
        {
            AudioManager.I.Play("sfx_throw", 0.5f, 0.75f);
            yield return new WaitForSeconds(delay);
            var st = app.Stage;
            if (m.Target < 0 || m.Target >= st.Guests.Count || m.Target == m.Seat) yield break;
            AudioManager.I.Play("sfx_slap", 0.9f, 0.95f + Random.value * 0.1f);
            if (m.Target == st.LocalSeat)
            {
                // 내가 맞으면 화면이 번쩍 + 흔들림 (눈앞에 입자를 만들지 않는다)
                splatUntil = Time.unscaledTime + 0.5f;
                splatKind = -1;
                app.Rig.Kick(1.2f);
            }
            else Fx.Splash(st.SeatHead(m.Target), new Color(0.95f, 0.92f, 0.82f), false);
            SocialHub.I.OnBonk(m.Seat, m.Target, m.Kind);
        }

        static float Radius(int kind)
        {
            switch (kind)
            {
                case 0: return 0.042f;
                case 1: return 0.038f;
                case 2: return 0.034f;
                case 3: return 0.03f;
                default: return 0.024f;
            }
        }

        /// <summary>물건 모양 (빵·사과·종이 뭉치·장미·코르크).</summary>
        public static GameObject BuildModel(int kind, Transform parent)
        {
            var b = new ModelBuilder();
            var k = b.Kit;
            var lib = ArtLibrary.I;
            switch (kind)
            {
                case 0:
                {
                    int crust = b.M("crust", lib.Tinted("Skin", new Color(0.78f, 0.5f, 0.22f))), top = b.M("top", lib.Tinted("Skin", new Color(0.62f, 0.36f, 0.14f)));
                    k.Ellipsoid(crust, Vector3.zero, new Vector3(0.055f, 0.034f, 0.036f), 18, 10);
                    for (int i = -1; i <= 1; i++)
                        k.Ellipsoid(top, new Vector3(i * 0.02f, 0.03f, 0), new Vector3(0.006f, 0.004f, 0.026f), 8, 4);
                    break;
                }
                case 1:
                {
                    int skin = b.M("apple", lib.Tinted("Lacquer", new Color(0.75f, 0.06f, 0.08f))), stem = b.M("Mahogany"), leaf = b.M("leaf", lib.Tinted("Cloth", new Color(0.2f, 0.5f, 0.18f)));
                    k.Lathe(skin, new[] { P(0, -0.034f), P(0.024f, -0.032f), P(0.038f, -0.012f), P(0.039f, 0.01f), P(0.03f, 0.03f), P(0.01f, 0.032f), P(0, 0.026f) }, 20);
                    k.Tube(stem, new List<Vector3> { new Vector3(0, 0.026f, 0), new Vector3(0.004f, 0.046f, 0) }, new[] { 0.0025f }, 5, true);
                    k.Push(new Vector3(0.012f, 0.04f, 0), new Vector3(0, 0, -40));
                    k.Ellipsoid(leaf, Vector3.zero, new Vector3(0.014f, 0.003f, 0.008f), 8, 4);
                    k.Pop();
                    break;
                }
                case 2:
                {
                    // 구겨진 종이: 울퉁불퉁한 공
                    int paper = b.M("paper", lib.Tinted("Cloth", new Color(0.93f, 0.9f, 0.82f)));
                    var rnd = new System.Random(5);
                    k.Sphere(paper, Vector3.zero, 0.03f, 12, 8);
                    for (int i = 0; i < 9; i++)
                    {
                        var d = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
                        k.Ellipsoid(paper, d * 0.02f, new Vector3(0.016f, 0.012f, 0.014f), 6, 4);
                    }
                    break;
                }
                case 3:
                {
                    // 토마토: 납작한 빨간 공 + 별 모양 꼭지 + 짧은 줄기
                    int red = b.M("tomato", lib.Tinted("Lacquer", new Color(0.86f, 0.12f, 0.08f))), green = b.M("calyx", lib.Tinted("Cloth", new Color(0.2f, 0.48f, 0.16f)));
                    k.Lathe(red, new[] { P(0, -0.03f), P(0.022f, -0.028f), P(0.036f, -0.012f), P(0.038f, 0.004f), P(0.031f, 0.022f), P(0.012f, 0.03f), P(0, 0.026f) }, 22);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * 360f;
                        k.Push(new Vector3(0, 0.028f, 0), new Vector3(0, a, 0));
                        k.Push(new Vector3(0.009f, 0.001f, 0), new Vector3(0, 0, -12));
                        k.Ellipsoid(green, Vector3.zero, new Vector3(0.011f, 0.0018f, 0.0035f), 8, 4);
                        k.Pop();
                        k.Pop();
                    }
                    k.Cylinder(green, 0.0022f, 0.026f, 0.01f, 6, true);
                    break;
                }
                default:
                {
                    int cork = b.M("cork", lib.Tinted("Skin", new Color(0.72f, 0.55f, 0.36f)));
                    k.Lathe(cork, new[] { P(0, -0.022f), P(0.017f, -0.022f), P(0.02f, 0.02f), P(0, 0.022f) }, 14);
                    break;
                }
            }
            return b.Build("Throwable" + kind, parent);
        }

        // ═════════════════ 집기·던지기 (나) ═════════════════

        void PickUp(Interactable it)
        {
            if (held != null || it == null || !it.gameObject.activeSelf) return;
            held = it;
            it.gameObject.SetActive(false);
            int kind = kindOf[it];
            heldModel = kind >= MeleeBase ? BuildWeapon(kind - MeleeBase, null) : BuildModel(kind, null);
            swingT = -1f;
            AudioManager.I.Play("ui_toggle", 0.6f, 1.2f, Bus.Ui);
        }

        void ThrowHeld()
        {
            if (held == null) return;
            var rig = app.Rig;
            int target = AimedSeat();
            SocialHub.I.SendThrow(kindOf[held], target, rig.Yaw, rig.Pitch, 1f);
            Destroy(heldModel);
            heldModel = null;
            var it = held;
            held = null;
            StartCoroutine(Respawn(it, 8f));
        }

        IEnumerator Respawn(Interactable it, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (it != null)
            {
                it.gameObject.SetActive(true);
                it.transform.localScale = Vector3.zero;
                float t = 0;
                while (t < 1f && it != null)
                {
                    t += Time.deltaTime * 3f;
                    it.transform.localScale = Vector3.one * Mathf.SmoothStep(0, 1, t);
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 조준점(화면 한가운데)이 가리키는 손님 좌석 (없으면 -1). 물리 충돌체 대신 카메라 광선과 각 손님의 머리·몸통 사이 거리로
        /// 고른다 — 탁자 위 물건에 가리거나, 머리를 겨눴는데 몸통 충돌체 위로 지나가 빗나가는 일이 없게.
        /// </summary>
        public int AimedSeat()
        {
            var cam = app.Rig != null ? app.Rig.Cam : null;
            if (cam == null) return -1;
            Vector3 o = cam.transform.position, d = cam.transform.forward;
            var st = app.Stage;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < st.Guests.Count; i++)
            {
                var g = st.Guests[i];
                if (i == st.LocalSeat || g == null || g.Head == null || !g.gameObject.activeInHierarchy) continue;
                float k = g.transform.lossyScale.y;
                Vector3 top = g.Head.position + Vector3.up * 0.06f * k;
                // 정수리(0)에서 가슴(6)까지 점 몇 개: 머리는 가늘게, 몸통은 굵게
                for (int s = 0; s <= 6; s++)
                {
                    Vector3 p = top + Vector3.down * (s * 0.085f * k);
                    float r = (s <= 2 ? 0.17f : 0.24f) * k;
                    Vector3 v = p - o;
                    float along = Vector3.Dot(v, d);
                    if (along < 0.25f) continue;
                    float off = (v - d * along).magnitude;
                    if (off > r) continue;
                    // 가운데에 가까울수록, 가까운 사람일수록 먼저
                    float score = off / r + along * 0.04f;
                    if (score < bestScore) { bestScore = score; best = i; }
                }
            }
            return best;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            bool typing = UI.UiRoot.I != null && UI.UiRoot.I.TextFocused;
            if (kb != null && !typing && kb.fKey.wasPressedThisFrame)
            {
                // F: 집기 / 던지기 (근접 무기는 내려놓기)
                if (held != null) { if (HoldingMelee) PutBack(); else ThrowHeld(); }
                else if (Interactor.Hovered != null && kindOf.ContainsKey(Interactor.Hovered)) PickUp(Interactor.Hovered);
            }
            else if (held != null && mouse != null && mouse.leftButton.wasPressedThisFrame && app.Rig.MouseLook)
            {
                if (HoldingMelee) Swing(); else ThrowHeld();
            }

            // 든 물건은 화면 오른쪽 아래에 (근접 무기는 세워 들고, 휘두르면 앞으로 내려친다)
            if (heldModel != null)
            {
                var cam = app.Rig.Cam.transform;
                float bob = Mathf.Sin(Time.time * 3f) * 0.006f;
                if (HoldingMelee)
                {
                    float chop = 0f;
                    if (swingT >= 0f)
                    {
                        swingT += Time.deltaTime / 0.42f;
                        chop = swingT < 0.35f ? swingT / 0.35f : Mathf.Max(0f, 1f - (swingT - 0.35f) / 0.65f);
                        chop = Mathf.SmoothStep(0, 1, chop);
                        if (swingT >= 1f) swingT = -1f;
                    }
                    heldModel.transform.position = cam.position + cam.forward * (0.34f + chop * 0.12f) + cam.right * (0.2f - chop * 0.08f) - cam.up * (0.26f - bob - chop * 0.04f);
                    heldModel.transform.rotation = cam.rotation * Quaternion.Euler(-12f + chop * 95f, -18f + chop * 10f, -14f);
                }
                else
                {
                    heldModel.transform.position = cam.position + cam.forward * 0.36f + cam.right * 0.15f - cam.up * (0.13f - bob);
                    heldModel.transform.rotation = cam.rotation * Quaternion.Euler(-20, 30, 0);
                }
            }
        }

        // ═════════════════ 날리기 (모두) ═════════════════

        public void Launch(ThrowMsg m)
        {
            var st = app.Stage;
            if (m.Seat < 0 || m.Seat >= st.Guests.Count) return;
            bool mine = m.Seat == st.LocalSeat;
            var thrower = st.Guests[m.Seat];
            Vector3 from;
            float delay = 0f;
            if (mine)
            {
                var cam = app.Rig.Cam.transform;
                from = cam.position + cam.forward * 0.4f + cam.right * 0.15f - cam.up * 0.12f;
            }
            else
            {
                thrower.Play(Gesture.Throw);
                delay = 0.28f;
                from = thrower.Head.position + thrower.transform.forward * 0.25f + thrower.transform.right * 0.18f + Vector3.up * 0.08f;
            }
            StartCoroutine(Fly(m, from, delay, mine));
        }

        IEnumerator Fly(ThrowMsg m, Vector3 from, float delay, bool mine)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            var st = app.Stage;
            Vector3 vel;
            if (m.Target >= 0 && m.Target < st.Guests.Count)
            {
                Vector3 to = m.Target == st.LocalSeat ? app.Rig.Cam.transform.position + Vector3.down * 0.05f : st.SeatHead(m.Target) + Vector3.down * 0.12f;
                if (!mine && m.Seat >= 0 && m.Seat < st.Guests.Count) from = st.Guests[m.Seat].Head.position + (to - st.Guests[m.Seat].Head.position).normalized * 0.3f;
                Vector3 d = to - from;
                float T = Mathf.Clamp(new Vector2(d.x, d.z).magnitude / (Speed * m.Power), 0.22f, 1.1f);
                vel = d / T + Vector3.up * (0.5f * 9.81f * T);
            }
            else
            {
                // 던진 사람 좌석 정면 기준 방향 (각 컴퓨터에서 그 사람 자리가 다르게 놓여도 같은 쪽으로)
                var seatRot = m.Seat == st.LocalSeat ? Quaternion.identity : Quaternion.LookRotation(Flat(-st.SeatWorld(m.Seat)), Vector3.up);
                var dir = seatRot * Quaternion.Euler(-m.Pitch, m.Yaw, 0) * Vector3.forward;
                vel = dir * Speed * m.Power + Vector3.up * 1.0f;
            }
            AudioManager.I.Play("sfx_throw", 0.6f, 0.9f + Random.value * 0.2f);
            var go = BuildModel(m.Kind, transform);
            go.transform.position = from;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = vel;
            var rnd = new System.Random(m.Seed);
            rb.angularVelocity = new Vector3((float)rnd.NextDouble() * 16 - 8, (float)rnd.NextDouble() * 16 - 8, (float)rnd.NextDouble() * 16 - 8);
            var col = go.AddComponent<SphereCollider>();
            col.radius = Radius(m.Kind);
            var p = go.AddComponent<Projectile>();
            p.Owner = this;
            p.Thrower = m.Seat;
            p.Kind = m.Kind;
            // 사과·토마토는 철퍽, 나머지는 퍽
            p.Soft = m.Kind == 1 || m.Kind == 3;
            Destroy(go, 7f);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized; }

        internal void OnProjectileHit(Projectile p, int victim, Vector3 at)
        {
            AudioManager.I.Play(p.Soft ? "sfx_splat" : "sfx_thud", 0.8f, 0.9f + Random.value * 0.2f);
            if (victim == app.Stage.LocalSeat)
            {
                // 내 얼굴에: 토마토는 씨가 보이는 주황 과즙, 사과는 과육 조각, 딱딱한 것은 짧게 어두워짐
                splatKind = p.Soft ? p.Kind : -1;
                splatUntil = Time.unscaledTime + (p.Soft ? SoftSplat : HardSplat);
                app.Rig.Kick(0.7f);
            }
            else
            {
                // 만화처럼 밝은 색 (어두운 빨강은 피처럼 보인다)
                var c = p.Kind == 3 ? new Color(1f, 0.4f, 0.14f) : (p.Kind == 1 ? new Color(0.98f, 0.92f, 0.7f) : new Color(0.95f, 0.9f, 0.8f));
                Fx.Splash(at, c, p.Soft);
            }
            SocialHub.I.OnHit(p.Thrower, victim);
        }

        /// <summary>내 눈앞까지 날아온 물건 (내 몸통 충돌체는 1인칭이라 꺼져 있다).</summary>
        internal bool NearMe(Vector3 pos)
        {
            return app.Rig.FirstPerson && Vector3.Distance(pos, app.Rig.Cam.transform.position) < 0.32f;
        }

        internal int SeatOf(Collider c)
        {
            var g = c.GetComponentInParent<Guest>();
            return g != null ? app.Stage.Guests.IndexOf(g) : -1;
        }

        internal int LocalSeat { get { return app.Stage.LocalSeat; } }
    }

    /// <summary>날아가는 물건: 사람에게 맞으면 한 번 알리고 튕겨 떨어진다.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        public Throwables Owner;
        public int Thrower;
        public int Kind;
        public bool Soft;
        bool hit;
        float born;

        void Start() { born = Time.time; }

        void OnTriggerEnter(Collider other)
        {
            if (hit || Owner == null) return;
            int seat = Owner.SeatOf(other);
            if (seat < 0 || seat == Thrower) return;
            Hit(seat);
        }

        void Hit(int seat)
        {
            hit = true;
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = -rb.linearVelocity * 0.25f + Vector3.up * 0.5f;
            Owner.OnProjectileHit(this, seat, transform.position);
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time - born > 0.05f && c.relativeVelocity.magnitude > 1.5f)
                AudioManager.I.Play(Soft ? "sfx_splat" : "sfx_thud", Mathf.Clamp01(c.relativeVelocity.magnitude / 8f) * 0.5f, 1.1f);
        }

        void Update()
        {
            if (!hit && Owner != null && Thrower != Owner.LocalSeat && Owner.NearMe(transform.position)) Hit(Owner.LocalSeat);
        }
    }
}
