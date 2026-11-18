using System;
using BluffAuction.Audio;
using BluffAuction.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BluffAuction.Table
{
    /// <summary>시선(또는 마우스)으로 가리켜 누를 수 있는 탁자 위 물건.</summary>
    public sealed class Interactable : MonoBehaviour
    {
        public Func<string> Tooltip;
        public Action OnClick;
        public Func<bool> Usable;
        public float HoverLift = 0.012f;
        Vector3 basePos;
        Vector3 baseScale;
        float hover;
        bool init;

        public bool IsUsable { get { return Usable == null || Usable(); } }

        void LateUpdate()
        {
            if (!init)
            {
                basePos = transform.localPosition;
                baseScale = transform.localScale;
                init = true;
            }
            float target = Interactor.Hovered == this ? 1f : 0f;
            hover = Mathf.MoveTowards(hover, target, Time.unscaledDeltaTime * 6f);
            if (HoverLift > 0)
            {
                transform.localPosition = basePos + Vector3.up * HoverLift * hover;
                transform.localScale = baseScale * (1f + 0.06f * hover);
            }
        }

        public void ResetBase()
        {
            init = false;
        }
    }

    /// <summary>
    /// 화면 가운데(마우스로 보기 중) 또는 마우스 위치의 물건을 가리키고, 왼쪽 클릭·패드 A 로 누른다.
    /// 커서가 UI 위에 있으면 물건을 누르지 않는다.
    /// </summary>
    public sealed class Interactor : MonoBehaviour
    {
        public static Interactable Hovered { get; private set; }
        /// <summary>참이면 클릭을 물건에 주지 않는다 (던질 물건을 들고 있을 때).</summary>
        public static System.Func<bool> SuppressClick;
        public Camera Cam;
        public bool Enabled = true;
        public Func<bool> LookMode;
        Interactable last;

        void Update()
        {
            Hovered = null;
            if (!Enabled || Cam == null) return;
            var mouse = Mouse.current;
            bool look = LookMode != null && LookMode();
            Vector2 screen = look || mouse == null ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) : mouse.position.ReadValue();
            if (!look && OverUi(screen)) return;
            var ray = Cam.ScreenPointToRay(screen);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 5f, ~0, QueryTriggerInteraction.Collide))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it != null) Hovered = it;
            }
            if (Hovered != last)
            {
                if (Hovered != null && AudioManager.I != null) AudioManager.I.Play("ui_hover", 0.5f, 1.1f, Bus.Ui);
                last = Hovered;
            }
            bool click = (mouse != null && mouse.leftButton.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.rightShoulder.wasPressedThisFrame);
            if (click && SuppressClick != null && SuppressClick()) click = false;
            if (click && Hovered != null)
            {
                if (Hovered.IsUsable)
                {
                    if (Hovered.OnClick != null) Hovered.OnClick();
                }
                else if (AudioManager.I != null) AudioManager.I.Play("ui_error", 0.6f, 1f, Bus.Ui);
            }
        }

        static bool OverUi(Vector2 screen)
        {
            var root = UiRoot.I != null ? UiRoot.I.Root : null;
            if (root == null || root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            var picked = root.panel.Pick(p);
            return picked != null && picked.pickingMode == PickingMode.Position && picked != root;
        }
    }
}
