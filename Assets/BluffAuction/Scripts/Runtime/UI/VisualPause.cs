using System;
using BluffAuction.Game;
using BluffAuction.Loc;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>일시정지 창 (Esc): 계속 · 게임 방법 · 설정 · 지금 끝내기 · 메인 메뉴.</summary>
    public sealed class VisualPause
    {
        readonly VisualElement wrap;
        readonly Action onClose;
        bool closed;

        public VisualPause(GameApp app, Action onClose)
        {
            this.onClose = onClose;
            var panel = Ui.Col("panel");
            panel.style.width = 520;
            panel.style.alignItems = Align.Stretch;
            panel.Add(Ui.Text(L.T("pause"), "h2", "center-text"));
            panel.Add(Ui.Div("divider"));
            panel.Add(Ui.Btn(L.T("resume"), Close, "btn-big"));
            panel.Add(Ui.Btn(L.T("menu_howto"), () => Sub(new HowToScreen(Back).Root), "btn-secondary"));
            panel.Add(Ui.Btn(L.T("menu_settings"), () => Sub(new SettingsScreen(app, Back).Root), "btn-secondary"));
            var end = Ui.Btn(L.T("end_now"), () => UiRoot.I.Confirm(L.T("end_now"), L.T("end_now_confirm"), L.T("end_now"), () =>
            {
                Close();
                app.Session.EndVote(true);
            }, L.T("cancel")), "btn-ghost");
            var ph = app.Session.View != null ? app.Session.View.Phase : Core.Phase.Setup;
            end.SetEnabled(ph == Core.Phase.Presenting || ph == Core.Phase.Bidding || ph == Core.Phase.PriceDrop || ph == Core.Phase.Reauction || ph == Core.Phase.AuctionResult);
            panel.Add(end);
            panel.Add(Ui.Btn(L.T("to_menu"), () => UiRoot.I.Confirm(L.T("to_menu"), L.T("to_menu_confirm"), L.T("to_menu"), () =>
            {
                Close();
                app.QuitToMenu();
            }, L.T("cancel")), "btn-red"));
            wrap = UiRoot.I.Open(panel);
        }

        VisualElement sub;

        void Sub(VisualElement screen)
        {
            sub = screen;
            sub.AddToClassList("screen");
            wrap.Add(sub);
        }

        void Back()
        {
            if (sub != null) sub.RemoveFromHierarchy();
            sub = null;
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            UiRoot.I.Close(wrap);
            if (onClose != null) onClose();
        }
    }
}
