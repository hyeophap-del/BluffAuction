using System;
using System.Collections.Generic;
using BluffAuction.Art;
using BluffAuction.Audio;
using BluffAuction.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// UI 문서 하나와 층: 화면(메뉴·게임) → 월드 표시(이름표·말풍선) → 덮개(일시정지·확인창) → 알림.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public static UiRoot I { get; private set; }
        public UIDocument Doc;
        public VisualElement Root, ScreenLayer, WorldLayer, OverlayLayer, ToastLayer;
        VisualElement current;
        readonly List<VisualElement> modals = new List<VisualElement>();
        public bool ModalOpen { get { return modals.Count > 0; } }

        /// <summary>글 입력 칸에 커서가 있는지 (그때는 단축키를 쓰지 않는다).</summary>
        public bool TextFocused
        {
            get
            {
                var f = Root != null && Root.panel != null ? Root.panel.focusController.focusedElement : null;
                return f is TextField || (f is VisualElement ve && ve.GetFirstAncestorOfType<TextField>() != null);
            }
        }

        public static UiRoot Create(Transform parent)
        {
            var go = new GameObject("UI");
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<UiRoot>();
            I = ui;
            ui.Doc = go.AddComponent<UIDocument>();
            ui.Doc.panelSettings = ArtLibrary.I.Panel;
            AddSystemFallbacks(ArtLibrary.I.Panel);
            ui.Build();
            return ui;
        }

        /// <summary>
        /// Steam 이름·채팅에는 게임 글꼴(Noto)에 없는 기호·이모지·다른 문자가 섞여 온다 → 운영체제 글꼴을 마지막 대체 글꼴로 붙인다
        /// (없는 글꼴은 건너뛴다). 탁자 위 패널도 같은 글자 설정을 쓰므로 함께 적용된다.
        /// </summary>
        static void AddSystemFallbacks(PanelSettings ps)
        {
            var ts = ps != null ? ps.textSettings : null;
            if (ts == null || ts.fallbackFontAssets == null) return;
            foreach (var family in new[] { "Segoe UI", "Segoe UI Symbol", "Segoe UI Emoji" })
            {
                if (ts.fallbackFontAssets.Exists(f => f != null && f.name == "OS " + family)) continue;
                try
                {
                    var fa = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(family, "Regular");
                    if (fa == null) continue;
                    fa.name = "OS " + family;
                    ts.fallbackFontAssets.Add(fa);
                }
                catch (Exception e) { Debug.LogWarning("[UI] system font " + family + ": " + e.Message); }
            }
        }

        void Build()
        {
            Root = Doc.rootVisualElement;
            Root.Clear();
            if (ArtLibrary.I.Style != null) Root.styleSheets.Add(ArtLibrary.I.Style);
            Root.AddToClassList("root");
            if (ArtLibrary.I.UiFont != null) Root.style.unityFontDefinition = FontDefinition.FromSDFFont(ArtLibrary.I.UiFont);
            Root.pickingMode = PickingMode.Ignore;
            ScreenLayer = Layer("screens");
            WorldLayer = Layer("world");
            OverlayLayer = Layer("overlay");
            ToastLayer = Layer("toasts");
            // 알림은 오른쪽 가운데 (위는 물건 카드, 아래는 입찰 막대, 가운데는 회전판이 쓴다)
            ToastLayer.style.alignItems = Align.FlexEnd;
            ToastLayer.style.justifyContent = Justify.FlexStart;
            ToastLayer.style.paddingTop = 390;
            ToastLayer.style.paddingRight = 24;
            ApplyScale();
        }

        VisualElement Layer(string name)
        {
            var l = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            l.AddToClassList("screen");
            Root.Add(l);
            return l;
        }

        public void ApplyScale()
        {
            if (Doc.panelSettings != null) Doc.panelSettings.scale = Mathf.Clamp(SaveData.I.Settings.UiScale, 0.75f, 1.4f);
        }

        public void SetScreen(VisualElement screen)
        {
            if (current != null) current.RemoveFromHierarchy();
            current = screen;
            if (screen != null)
            {
                screen.AddToClassList("screen");
                ScreenLayer.Add(screen);
                Ui.FadeIn(screen, 0.35f, 0f);
            }
        }

        public VisualElement Current { get { return current; } }

        /// <summary>위에 덮는 창. 닫을 때 Close(e).</summary>
        public VisualElement Open(VisualElement content, bool dim = true)
        {
            var wrap = Ui.Div("screen", "center");
            wrap.style.alignItems = Align.Center;
            wrap.style.justifyContent = Justify.Center;
            if (dim) wrap.AddToClassList("dim-bg");
            wrap.Add(content);
            OverlayLayer.Add(wrap);
            modals.Add(wrap);
            Ui.FadeIn(content, 0.22f, 20f);
            if (AudioManager.I != null) AudioManager.I.Play("ui_open", 0.7f, 1f, Bus.Ui);
            return wrap;
        }

        public void Close(VisualElement wrapOrContent)
        {
            if (wrapOrContent == null) return;
            var w = modals.Contains(wrapOrContent) ? wrapOrContent : wrapOrContent.parent;
            if (w == null) return;
            modals.Remove(w);
            w.RemoveFromHierarchy();
            if (AudioManager.I != null) AudioManager.I.Play("ui_close", 0.6f, 1f, Bus.Ui);
        }

        public void CloseTop()
        {
            if (modals.Count == 0) return;
            Close(modals[modals.Count - 1]);
        }

        /// <summary>확인 창.</summary>
        public void Confirm(string title, string body, string okText, Action ok, string cancelText = null, Action cancel = null)
        {
            var panel = Ui.Col("panel");
            panel.style.width = 640;
            panel.style.alignItems = Align.Center;
            panel.Add(Ui.Text(title, "h2", "center-text"));
            if (!string.IsNullOrEmpty(body))
            {
                var b = Ui.Text(body, "sub", "wrap", "center-text");
                b.style.marginTop = 10;
                b.style.marginBottom = 16;
                panel.Add(b);
            }
            var row = Ui.Row();
            VisualElement wrap = null;
            if (cancelText != null) row.Add(Ui.Btn(cancelText, () => { Close(wrap); if (cancel != null) cancel(); }, "btn-secondary"));
            var okb = Ui.Btn(okText, () => { Close(wrap); if (ok != null) ok(); });
            row.Add(okb);
            panel.Add(row);
            wrap = Open(panel);
            okb.Focus();
        }

        /// <summary>잠깐 뜨는 알림 (위 가운데).</summary>
        public void Toast(string text, string kind = null, float seconds = 3f)
        {
            var t = Ui.Text(text, "toast");
            if (kind != null) t.AddToClassList(kind);
            ToastLayer.Add(t);
            Ui.FadeIn(t, 0.25f, -14f);
            t.schedule.Execute(() => { t.style.opacity = 0; }).StartingIn((long)(seconds * 1000));
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn((long)(seconds * 1000) + 400);
            while (ToastLayer.childCount > 4) ToastLayer.RemoveAt(0);
        }

        /// <summary>월드 좌표 → 패널 좌표 (이름표·말풍선 위치).</summary>
        public bool WorldToPanel(Camera cam, Vector3 world, out Vector2 p)
        {
            p = Vector2.zero;
            if (cam == null || Root.panel == null) return false;
            var vp = cam.WorldToViewportPoint(world);
            if (vp.z <= 0) return false;
            p = RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, world, cam);
            return vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
        }
    }
}
