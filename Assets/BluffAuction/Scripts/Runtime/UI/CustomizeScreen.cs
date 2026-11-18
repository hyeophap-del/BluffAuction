using System;
using System.Collections.Generic;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.Loc;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 캐릭터 꾸미기: 왼쪽 부위 목록, 가운데 돌려 볼 수 있는 미리보기, 오른쪽 모양·색 고르기.
    /// 고친 생김새는 저장 파일에 남고, 온라인 대기실에서 다른 사람에게도 그대로 보인다.
    /// </summary>
    public sealed class CustomizeScreen
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        readonly Action back;
        readonly CharacterLook look;
        readonly CustomizePreview preview;
        readonly VisualElement options;
        readonly List<Button> catButtons = new List<Button>();
        int category;

        static readonly string[] Categories = { "body", "eyes", "hair", "mask", "hat", "outfit", "extras", "voice" };

        /// <summary>개발용 자동 촬영이 버튼 대신 부르는 곳.</summary>
        public static CustomizeScreen Current { get; private set; }
        public void DevSelect(int i) { SelectCategory(i); }
        public void DevRandom() { Randomize(); }
        public static int CategoryCount { get { return Categories.Length; } }

        public CustomizeScreen(GameApp app, Action back)
        {
            Current = this;
            this.app = app;
            this.back = back;
            look = SaveData.MyLook.Clone();
            Root = Ui.Col("dim-bg");
            Root.style.paddingLeft = 60;
            Root.style.paddingRight = 60;
            Root.style.paddingTop = 40;
            Root.style.paddingBottom = 30;

            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("back"), Cancel, "btn-ghost"));
            var title = Ui.Text(L.T("cz_title"), "h2");
            title.style.marginLeft = 20;
            head.Add(title, Ui.Div("spacer"));
            head.Add(Ui.Btn(L.T("cz_random"), Randomize, "btn-small", "btn-ghost"));
            var reset = Ui.Btn(L.T("cz_reset"), () => { Copy(CharacterLook.DefaultPlayer(), look); Changed(); BuildOptions(); }, "btn-small", "btn-ghost");
            reset.style.marginLeft = 8;
            head.Add(reset);
            Root.Add(head);

            var body = Ui.Row();
            body.style.flexGrow = 1;
            body.style.alignItems = Align.Stretch;
            body.style.marginTop = 16;
            Root.Add(body);

            // ── 왼쪽: 부위
            var cats = Ui.Col("panel");
            cats.style.width = 230;
            for (int i = 0; i < Categories.Length; i++)
            {
                int ci = i;
                var b = Ui.Btn(L.T("cz_cat_" + Categories[i]), () => SelectCategory(ci), "cz-cat");
                catButtons.Add(b);
                cats.Add(b);
            }
            cats.Add(Ui.Div("spacer"));
            cats.Add(Ui.Text(L.T("cz_presets"), "small"));
            var presets = Ui.Row();
            presets.style.flexWrap = Wrap.Wrap;
            foreach (var p in CharacterLook.Players)
            {
                var pl = p;
                var pb = new Button();
                pb.AddToClassList("cz-preset");
                pb.Add(Ui.Portrait(pl.Id, 44));
                Ui.Wire(pb, () => { Copy(pl, look); look.Id = "custom"; Changed(); BuildOptions(); }, "ui_toggle");
                presets.Add(pb);
            }
            cats.Add(presets);
            body.Add(cats);

            // ── 가운데: 미리보기
            var mid = Ui.Col();
            mid.style.width = 520;
            mid.style.marginLeft = 18;
            mid.style.marginRight = 18;
            mid.style.alignItems = Align.Center;
            preview = CustomizePreview.Create(520, 640);
            var view = Ui.Div("cz-view");
            view.style.width = 520;
            view.style.height = 640;
            view.style.backgroundImage = Background.FromRenderTexture(preview.RT);
            // 끌어서 돌리기
            bool dragging = false;
            float lastX = 0;
            view.RegisterCallback<PointerDownEvent>(e => { dragging = true; lastX = e.position.x; view.CapturePointer(e.pointerId); });
            view.RegisterCallback<PointerMoveEvent>(e => { if (dragging) { preview.Rotate(-(e.position.x - lastX) * 0.6f); lastX = e.position.x; } });
            view.RegisterCallback<PointerUpEvent>(e => { dragging = false; view.ReleasePointer(e.pointerId); });
            view.RegisterCallback<WheelEvent>(e => preview.SetFace(e.delta.y < 0));
            mid.Add(view);
            var ctl = Ui.Row();
            ctl.style.marginTop = 10;
            ctl.Add(Ui.Btn("◀", () => preview.Rotate(45), "btn-icon", "btn-secondary"));
            Button faceBtn = null;
            faceBtn = Ui.Btn(L.T("cz_face"), () => { preview.SetFace(!preview.Face); faceBtn.text = preview.Face ? L.T("cz_full") : L.T("cz_face"); }, "btn-small");
            faceBtn.style.marginLeft = 8;
            faceBtn.style.marginRight = 8;
            ctl.Add(faceBtn);
            ctl.Add(Ui.Btn("▶", () => preview.Rotate(-45), "btn-icon", "btn-secondary"));
            var emote = Ui.Btn(L.T("cz_try_emote"), () => preview.Emote(TryEmotes[UnityEngine.Random.Range(0, TryEmotes.Length)]), "btn-small", "btn-ghost");
            emote.style.marginLeft = 16;
            ctl.Add(emote);
            mid.Add(ctl);
            body.Add(mid);

            // ── 오른쪽: 고르기
            var right = Ui.Col("panel");
            right.style.flexGrow = 1;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            options = scroll.contentContainer;
            right.Add(scroll);
            var done = Ui.Btn(L.T("cz_done"), Save, "btn-big");
            done.style.marginTop = 10;
            right.Add(done);
            body.Add(right);

            Root.RegisterCallback<DetachFromPanelEvent>(e => { if (preview != null) UnityEngine.Object.Destroy(preview.gameObject); });
            preview.SetLook(look);
            SelectCategory(0);
        }

        static readonly Gesture[] TryEmotes = { Gesture.Wave, Gesture.ThumbsUp, Gesture.Laugh, Gesture.Cheer, Gesture.Shrug, Gesture.Peace };

        void SelectCategory(int i)
        {
            category = i;
            for (int k = 0; k < catButtons.Count; k++) catButtons[k].EnableInClassList("cz-cat-on", k == i);
            BuildOptions();
            // 얼굴 부위는 가까이
            string c = Categories[i];
            preview.SetFace(c == "eyes" || c == "hair" || c == "mask" || c == "hat");
        }

        void BuildOptions()
        {
            options.Clear();
            switch (Categories[category])
            {
                case "body":
                    Numbers("cz_width", new[] { 0.9f, 1.0f, 1.12f, 1.24f }, "cz_width_", () => look.Width, v => look.Width = v);
                    Numbers("cz_height", new[] { 0.94f, 1.0f, 1.05f }, "cz_height_", () => look.Height, v => look.Height = v);
                    Numbers("cz_head", new[] { 0.94f, 1.0f, 1.08f }, "cz_headsize_", () => look.Head, v => look.Head = v);
                    Colors("cz_skin", CharacterLook.SkinTones, () => look.Skin, c => look.Skin = c);
                    break;
                case "eyes":
                    Styles<EyeStyle>("cz_eyes", () => look.Eyes, v => look.Eyes = v);
                    Colors("cz_eye_color", CharacterLook.EyeColors, () => look.EyeColor, c => look.EyeColor = c);
                    break;
                case "hair":
                    Styles<HairStyle>("cz_hair", () => look.HairStyle, v => look.HairStyle = v);
                    Colors("cz_hair_color", CharacterLook.HairColors, () => look.Hair, c => look.Hair = c);
                    Styles<FacialHair>("cz_facial", () => look.Facial, v => look.Facial = v);
                    break;
                case "mask":
                    Styles<MaskStyle>("cz_mask", () => look.Mask, v => look.Mask = v);
                    Colors("cz_mask_color", Join(CharacterLook.ClothColors, CharacterLook.MetalColors), () => look.MaskColor, c => look.MaskColor = c);
                    Colors("cz_mask_trim", CharacterLook.MetalColors, () => look.MaskTrim, c => look.MaskTrim = c);
                    Toggle("cz_feather", () => look.MaskFeather, v => look.MaskFeather = v);
                    break;
                case "hat":
                    Styles<HatStyle>("cz_hat", () => look.Hat, v => look.Hat = v);
                    Colors("cz_hat_color", CharacterLook.ClothColors, () => look.HatColor, c => look.HatColor = c);
                    break;
                case "outfit":
                    Styles<Outfit>("cz_outfit", () => look.Outfit, v => look.Outfit = v);
                    Colors("cz_coat", CharacterLook.ClothColors, () => look.Coat, c => look.Coat = c);
                    Colors("cz_shirt", CharacterLook.ClothColors, () => look.Shirt, c => look.Shirt = c);
                    Colors("cz_trim", CharacterLook.MetalColors, () => look.Trim, c => look.Trim = c);
                    Colors("cz_pants", CharacterLook.ClothColors, () => look.Pants, c => look.Pants = c);
                    Colors("cz_gloves", CharacterLook.ClothColors, () => look.Glove, c => look.Glove = c);
                    break;
                case "extras":
                    Styles<Neckwear>("cz_neck", () => look.Neck, v => look.Neck = v);
                    Colors("cz_accent", Join(CharacterLook.ClothColors, CharacterLook.MetalColors), () => look.Accent, c => look.Accent = c);
                    Styles<Eyewear>("cz_eyewear", () => look.Eyewear, v => look.Eyewear = v);
                    Styles<Earring>("cz_earrings", () => look.Earrings, v => look.Earrings = v);
                    break;
                case "voice":
                    var row = Section("cz_voice");
                    for (int v = 0; v < 8; v++)
                    {
                        int vv = v;
                        var b = Ui.Btn(L.T("cz_voice_n", v + 1), () => { look.Voice = vv; AudioManager.I.Voice(vv); preview.Emote(Gesture.Talk); BuildOptions(); }, "btn-chip");
                        b.EnableInClassList("btn-chip-on", look.Voice == v);
                        row.Add(b);
                    }
                    break;
            }
        }

        static Color[] Join(Color[] a, Color[] b)
        {
            var r = new Color[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        VisualElement Section(string labelKey)
        {
            options.Add(Ui.Text(L.T(labelKey), "cz-section"));
            var row = Ui.Row("cz-row");
            row.style.flexWrap = Wrap.Wrap;
            options.Add(row);
            return row;
        }

        void Styles<T>(string labelKey, Func<T> get, Action<T> set) where T : struct
        {
            var row = Section(labelKey);
            foreach (T v in Enum.GetValues(typeof(T)))
            {
                var val = v;
                string key = "cz_" + typeof(T).Name.ToLowerInvariant() + "_" + v.ToString().ToLowerInvariant();
                var b = Ui.Btn(L.Has(key) ? L.T(key) : v.ToString(), () => { set(val); Changed(); BuildOptions(); }, "btn-chip");
                b.EnableInClassList("btn-chip-on", get().Equals(v));
                row.Add(b);
            }
        }

        void Numbers(string labelKey, float[] values, string namePrefix, Func<float> get, Action<float> set)
        {
            var row = Section(labelKey);
            for (int i = 0; i < values.Length; i++)
            {
                float v = values[i];
                var b = Ui.Btn(L.T(namePrefix + i), () => { set(v); Changed(); BuildOptions(); }, "btn-chip");
                b.EnableInClassList("btn-chip-on", Mathf.Abs(get() - v) < 0.01f);
                row.Add(b);
            }
        }

        void Colors(string labelKey, Color[] palette, Func<Color> get, Action<Color> set)
        {
            var row = Section(labelKey);
            foreach (var c in palette)
            {
                var col = c;
                var sw = new Button();
                sw.AddToClassList("swatch");
                sw.style.backgroundColor = col;
                sw.EnableInClassList("swatch-on", Close(get(), col));
                Ui.Wire(sw, () => { set(col); Changed(); BuildOptions(); }, "ui_toggle");
                row.Add(sw);
            }
        }

        void Toggle(string labelKey, Func<bool> get, Action<bool> set)
        {
            var row = Section(labelKey);
            foreach (bool v in new[] { false, true })
            {
                bool vv = v;
                var b = Ui.Btn(L.T(v ? "on" : "off"), () => { set(vv); Changed(); BuildOptions(); }, "btn-chip");
                b.EnableInClassList("btn-chip-on", get() == v);
                row.Add(b);
            }
        }

        static bool Close(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.02f;
        }

        void Changed()
        {
            preview.SetLook(look);
        }

        void Randomize()
        {
            var r = new System.Random();
            T Pick<T>() where T : struct { var vals = Enum.GetValues(typeof(T)); return (T)vals.GetValue(r.Next(vals.Length)); }
            Color From(Color[] p) { return p[r.Next(p.Length)]; }
            look.Outfit = Pick<Outfit>();
            look.HairStyle = Pick<HairStyle>();
            look.Hair = From(CharacterLook.HairColors);
            look.Facial = r.Next(3) == 0 ? Pick<FacialHair>() : FacialHair.None;
            look.Mask = Pick<MaskStyle>();
            look.MaskColor = From(Join(CharacterLook.ClothColors, CharacterLook.MetalColors));
            look.MaskTrim = From(CharacterLook.MetalColors);
            look.Hat = r.Next(3) == 0 ? HatStyle.None : Pick<HatStyle>();
            look.HatColor = From(CharacterLook.ClothColors);
            look.Eyes = Pick<EyeStyle>();
            look.EyeColor = From(CharacterLook.EyeColors);
            look.Coat = From(CharacterLook.ClothColors);
            look.Shirt = r.Next(2) == 0 ? new Color(0.97f, 0.95f, 0.9f) : From(CharacterLook.ClothColors);
            look.Trim = From(CharacterLook.MetalColors);
            look.Neck = Pick<Neckwear>();
            look.Accent = From(CharacterLook.ClothColors);
            look.Eyewear = r.Next(3) == 0 ? Pick<Eyewear>() : Eyewear.None;
            look.Earrings = r.Next(3) == 0 ? Pick<Earring>() : Earring.None;
            look.Skin = From(CharacterLook.SkinTones);
            look.Width = new[] { 0.9f, 1.0f, 1.12f, 1.24f }[r.Next(4)];
            look.MaskFeather = r.Next(4) == 0;
            look.Voice = r.Next(8);
            Changed();
            BuildOptions();
            preview.Emote(Gesture.Cheer);
        }

        static void Copy(CharacterLook from, CharacterLook to)
        {
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from), to);
        }

        void Save()
        {
            look.Id = "custom";
            SaveData.I.Last.Custom = CharacterLook.FromJson(look.ToJson()) ?? CharacterLook.DefaultPlayer();
            SaveData.Save();
            Characters.PortraitStudio.Request(SaveData.MyLook);
            UiRoot.I.Toast(L.T("cz_saved"), null, 2f);
            back();
        }

        void Cancel()
        {
            if (look.ToJson() == SaveData.MyLook.ToJson()) { back(); return; }
            UiRoot.I.Confirm(L.T("cz_discard"), null, L.T("cz_discard_yes"), () => back(), L.T("cancel"));
        }
    }
}
