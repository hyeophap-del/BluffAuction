using System;
using BluffAuction.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>UI Toolkit 요소를 코드로 짧게 만드는 도구와 효과(눌림·흔들림·튀어나옴).</summary>
    public static class Ui
    {
        public static VisualElement Div(params string[] classes)
        {
            var e = new VisualElement();
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            return e;
        }

        public static VisualElement Row(params string[] classes)
        {
            var e = Div(classes);
            e.AddToClassList("row");
            return e;
        }

        public static VisualElement Col(params string[] classes)
        {
            var e = Div(classes);
            e.AddToClassList("col");
            return e;
        }

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text);
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) l.AddToClassList(c);
            return l;
        }

        public static VisualElement Add(this VisualElement parent, params VisualElement[] children)
        {
            foreach (var c in children) if (c != null) parent.Add(c);
            return parent;
        }

        public static T Cls<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            return e;
        }

        public static void Show(this VisualElement e, bool on)
        {
            e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>버튼: 누르면 소리와 눌림 효과. sound 가 null 이면 기본 '톡'.</summary>
        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button();
            b.text = text;
            b.AddToClassList("btn");
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) b.AddToClassList(c);
            Wire(b, onClick, "ui_click");
            return b;
        }

        /// <summary>글자 옆에 단축키 표시가 붙은 버튼. 글자는 b.Q&lt;Label&gt;("text") 로 바꾼다.</summary>
        public static Button KeyBtn(string text, string key, Action onClick, params string[] classes)
        {
            var b = Btn("", onClick, classes);
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            var t = new Label(text) { name = "text" };
            t.style.unityTextAlign = TextAnchor.MiddleCenter;
            t.pickingMode = PickingMode.Ignore;
            b.Add(t);
            if (!string.IsNullOrEmpty(key))
            {
                var k = Text(key, "key");
                k.pickingMode = PickingMode.Ignore;
                b.Add(k);
            }
            return b;
        }

        public static void SetText(Button b, string text)
        {
            var l = b.Q<Label>("text");
            if (l != null) l.text = text;
            else b.text = text;
        }

        public static void Wire(Button b, Action onClick, string sound)
        {
            b.clicked += () =>
            {
                if (AudioManager.I != null) AudioManager.I.Play(sound, 0.8f, 1f, Bus.Ui);
                if (onClick != null) onClick();
            };
            b.RegisterCallback<PointerEnterEvent>(e =>
            {
                if (b.enabledSelf && AudioManager.I != null) AudioManager.I.Play("ui_hover", 0.5f, 1f, Bus.Ui);
            });
        }

        /// <summary>안 되는 조작: 붉게 번쩍이며 좌우로 흔들린다.</summary>
        public static void Deny(VisualElement e)
        {
            if (e == null) return;
            if (AudioManager.I != null) AudioManager.I.Play("ui_error", 0.8f, 1f, Bus.Ui);
            e.AddToClassList("flash-red");
            float t0 = Time.unscaledTime;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                float t = Time.unscaledTime - t0;
                if (t > 0.4f)
                {
                    e.style.translate = new Translate(0, 0);
                    e.RemoveFromClassList("flash-red");
                    item.Pause();
                    return;
                }
                float x = Mathf.Sin(t * 60f) * 9f * (1f - t / 0.4f);
                e.style.translate = new Translate(x, 0);
            }).Every(16);
        }

        /// <summary>잠깐 커졌다 돌아온다 (값이 바뀔 때).</summary>
        public static void Pop(VisualElement e, float amount = 0.12f)
        {
            if (e == null) return;
            float t0 = Time.unscaledTime;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                float t = (Time.unscaledTime - t0) / 0.28f;
                if (t >= 1f)
                {
                    e.style.scale = new Scale(Vector3.one);
                    item.Pause();
                    return;
                }
                float s = 1f + Mathf.Sin(t * Mathf.PI) * amount;
                e.style.scale = new Scale(new Vector3(s, s, 1));
            }).Every(16);
        }

        /// <summary>서서히 나타나기 (투명 → 불투명, 아래에서 위로).</summary>
        public static void FadeIn(VisualElement e, float seconds = 0.3f, float rise = 16f)
        {
            if (e == null) return;
            float t0 = Time.unscaledTime;
            e.style.opacity = 0;
            IVisualElementScheduledItem item = null;
            item = e.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
                float k = 1f - (1f - t) * (1f - t);
                e.style.opacity = k;
                e.style.translate = new Translate(0, (1f - k) * rise);
                if (t >= 1f) item.Pause();
            }).Every(16);
        }

        /// <summary>숫자가 올라가며 바뀐다.</summary>
        public static void CountTo(Label l, int from, int to, float seconds, Func<int, string> format, bool tick = true)
        {
            float t0 = Time.unscaledTime;
            IVisualElementScheduledItem item = null;
            int last = from;
            item = l.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.unscaledTime - t0) / seconds);
                float k = 1f - Mathf.Pow(1f - t, 3f);
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, k) / 100f) * 100;
                if (t >= 1f) v = to;
                l.text = format(v);
                if (tick && v != last && AudioManager.I != null) AudioManager.I.Play("sfx_count", 0.35f, 1f + k * 0.4f, Bus.Ui);
                last = v;
                if (t >= 1f) item.Pause();
            }).Every(33);
        }

        public static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString(h, out c);
            return c;
        }

        /// <summary>좌석 색 동그라미.</summary>
        public static VisualElement Dot(Color c, float size = 14f)
        {
            var d = Div("dot");
            d.style.backgroundColor = c;
            d.style.width = size;
            d.style.height = size;
            d.style.borderTopLeftRadius = d.style.borderTopRightRadius = d.style.borderBottomLeftRadius = d.style.borderBottomRightRadius = size * 0.5f;
            return d;
        }

        /// <summary>캐릭터 얼굴 사진 (PortraitStudio 가 찍은 것). 아직 없으면 찍힐 때까지 기다렸다 채운다.</summary>
        public static VisualElement Portrait(string lookId, float size)
        {
            var e = Div("portrait");
            e.style.width = size;
            e.style.height = size;
            e.style.flexShrink = 0;
            SetPortrait(e, lookId);
            return e;
        }

        /// <summary>꾸민 생김새의 얼굴 사진 (없으면 찍어 달라고 하고 기다린다).</summary>
        public static VisualElement Portrait(Characters.CharacterLook look, float size)
        {
            return Portrait(Characters.PortraitStudio.Request(look), size);
        }

        public static void SetPortrait(VisualElement e, Characters.CharacterLook look)
        {
            SetPortrait(e, Characters.PortraitStudio.Request(look));
        }

        public static void SetPortrait(VisualElement e, string key)
        {
            if (e == null) return;
            e.userData = key;
            Apply(e, key, 0);
        }

        static void Apply(VisualElement e, string key, int tries)
        {
            // 그새 다른 얼굴로 바뀌었으면 그만
            if (!(e.userData is string) || (string)e.userData != key) return;
            var tex = Characters.PortraitStudio.Get(key);
            if (tex != null)
            {
                e.style.backgroundImage = Background.FromTexture2D(tex);
                return;
            }
            if (tries < 150) e.schedule.Execute(() => Apply(e, key, tries + 1)).StartingIn(200);
        }

        /// <summary>왼쪽 ◀ 값 ▶ 오른쪽 고르기.</summary>
        public static VisualElement Picker(Func<string> getText, Action prev, Action next, string valueClass, out Label value)
        {
            var row = Row("stepper");
            var l = Btn("◀", prev, "btn-icon", "btn-secondary");
            var r = Btn("▶", next, "btn-icon", "btn-secondary");
            value = Text(getText(), valueClass);
            value.style.flexGrow = 1;
            row.Add(l, value, r);
            return row;
        }
    }
}
