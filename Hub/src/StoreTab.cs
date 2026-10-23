using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GameHub
{
    internal sealed class StoreWebView : WebView2
    {
        public Func<Keys, bool> Shortcut { get; set; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Shortcut != null && Shortcut(e.KeyData))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }

    internal sealed class StoreTab
    {
        public StoreTab(int id, string profile, StoreWebView view)
        {
            Id = id;
            Profile = profile ?? "";
            View = view;
        }

        public int Id { get; }
        public string Profile { get; }
        public StoreWebView View { get; }
        public CoreWebView2 Core => View.IsDisposed ? null : View.CoreWebView2;
        public Task Ready { get; set; }
        public bool Closed { get; set; }
        public bool Loading { get; set; }
        public int Blocked { get; set; }
        public int PopupsBlocked { get; set; }
        public string PendingUri { get; set; }
        public string PageHost { get; set; } = "";
        public DateTime LastActive { get; set; } = DateTime.UtcNow;
        public bool ApplyingZoom { get; set; }
        public bool FullscreenElement { get; set; }

        public string Url
        {
            get
            {
                string source = Core?.Source;
                return string.IsNullOrEmpty(source) || source == "about:blank" ? PendingUri ?? "" : source;
            }
        }

        public bool IsBlank
        {
            get
            {
                if (Core == null) return string.IsNullOrEmpty(PendingUri);
                string source = Core.Source;
                return string.IsNullOrEmpty(source) || source == "about:blank";
            }
        }

        public string Title
        {
            get
            {
                string title = Core?.DocumentTitle;
                if (!string.IsNullOrWhiteSpace(title) && title != "about:blank") return title;
                string host = StoreDomains.HostOf(Url);
                return host.Length > 0 ? host : "New tab";
            }
        }
    }
}
