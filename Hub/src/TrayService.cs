using System;
using System.Drawing;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly ToolStripSeparator moduleItemsEnd = new ToolStripSeparator();
        private Action balloonClick;

        public TrayService(MainForm form)
        {
            menu.Items.Add("Open Game Hub", null, (sender, args) => form.ShowAndActivate());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(moduleItemsEnd);
            menu.Items.Add("Quit", null, (sender, args) => form.QuitApplication());

            icon = new NotifyIcon
            {
                Icon = form.Icon ?? SystemIcons.Application,
                Text = MainForm.WindowTitle,
                ContextMenuStrip = menu,
                Visible = true,
            };
            icon.MouseClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) form.ShowAndActivate();
            };
            icon.BalloonTipClicked += (sender, args) =>
            {
                Action action = balloonClick;
                balloonClick = null;
                (action ?? form.ShowAndActivate)();
            };
            menu.Opening += (sender, args) => Opening?.Invoke(menu);
        }

        public event Action<ContextMenuStrip> Opening;

        public ContextMenuStrip Menu => menu;

        public int ModuleInsertIndex => menu.Items.IndexOf(moduleItemsEnd);

        public ToolStripMenuItem AddMenuItem(string text, Action onClick)
        {
            var item = new ToolStripMenuItem(text, null, (sender, args) => onClick());
            menu.Items.Insert(ModuleInsertIndex, item);
            return item;
        }

        public void Notify(string title, string text, Action onClick = null)
        {
            balloonClick = onClick;
            icon.BalloonTipTitle = title ?? MainForm.WindowTitle;
            icon.BalloonTipText = string.IsNullOrEmpty(text) ? " " : text;
            icon.ShowBalloonTip(8000);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
        }
    }
}
