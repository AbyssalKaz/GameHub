using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace GameHub
{
    internal sealed class MainForm : Form
    {
        public const string WindowTitle = "Game Hub";
        public static readonly Color Background = Color.FromArgb(9, 11, 15);
        private static readonly Color Border = Color.FromArgb(32, 36, 46);

        private readonly WebView2 webView = new WebView2();
        private readonly HubState state = HubState.Load();
        private readonly HubController controller;

        private bool fullscreen;
        private Rectangle windowedBounds;
        private bool windowedMaximized;

        private bool quitting;
        private Color themeBackground = Background;
        private bool themeLight;

        public event Action WindowModeChanged;

        public event Action ShuttingDown;

        public bool HideOnClose { get; set; }

        public MainForm()
        {
            Text = WindowTitle;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            themeBackground = ThemeNative.ParseColor(state.ThemeBackground) ?? Background;
            themeLight = state.ThemeLight;
            BackColor = themeBackground;
            StartPosition = FormStartPosition.Manual;
            ApplySavedBounds(state);

            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = themeBackground;
            Controls.Add(webView);

            controller = new HubController(this, webView, state);
            Load += async (sender, args) => await controller.InitializeAsync();
            FormClosing += (sender, args) =>
            {
                if (HideOnClose && !quitting && args.CloseReason == CloseReason.UserClosing)
                {
                    args.Cancel = true;
                    SaveBounds();
                    Hide();
                    return;
                }
                SaveBounds();
                ShuttingDown?.Invoke();
            };
            Resize += (sender, args) => WindowModeChanged?.Invoke();
            Shown += (sender, args) => RestoreFullscreen();
        }

        public bool IsFullscreen => fullscreen;

        public bool IsMaximized => IsHandleCreated && NativeMethods.IsZoomed(Handle);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.UseDarkTitleBar(Handle, themeBackground);
            NativeMethods.AddFrameStyles(Handle);
            NativeMethods.UseBorderlessFrame(Handle, themeLight ? ThemeNative.LightBorder : Border);
            if (themeLight) ThemeNative.ApplyFrame(Handle, themeBackground, ThemeNative.LightBorder, false);
        }

        public bool IsLightTheme => themeLight;

        public void ApplyTheme(Color background, bool light)
        {
            themeBackground = background;
            themeLight = light;
            state.ThemeBackground = ColorTranslator.ToHtml(background);
            state.ThemeLight = light;
            state.Save();
            BackColor = background;
            webView.DefaultBackgroundColor = background;
            if (IsHandleCreated) ThemeNative.ApplyFrame(Handle, background, light ? ThemeNative.LightBorder : Border, !light);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style &= ~NativeMethods.FrameStyles;
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.WmStyleChanging && message.WParam.ToInt64() == NativeMethods.StyleIndexValue)
            {
                var styles = (NativeMethods.StyleStruct)Marshal.PtrToStructure(message.LParam, typeof(NativeMethods.StyleStruct));
                styles.New = fullscreen ? styles.New & ~NativeMethods.FrameStyles : styles.New | NativeMethods.FrameStyles;
                Marshal.StructureToPtr(styles, message.LParam, false);
            }

            if (message.Msg == NativeMethods.WmNcCalcSize && message.WParam != IntPtr.Zero)
            {
                if (!fullscreen && NativeMethods.IsZoomed(Handle))
                {
                    var parameters = (NativeMethods.NcCalcSizeParams)Marshal.PtrToStructure(message.LParam, typeof(NativeMethods.NcCalcSizeParams));
                    parameters.Proposed = NativeMethods.GetWorkArea(Handle);
                    Marshal.StructureToPtr(parameters, message.LParam, false);
                }
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }

        public void BeginDrag()
        {
            if (fullscreen) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WmNcLeftButtonDown, new IntPtr(NativeMethods.HitCaption), NativeMethods.PackPoint(Cursor.Position));
        }

        public void BeginResize(int hitTest)
        {
            if (fullscreen || IsMaximized || hitTest < 10 || hitTest > 17) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WmNcLeftButtonDown, new IntPtr(hitTest), NativeMethods.PackPoint(Cursor.Position));
        }

        public void ToggleMaximize()
        {
            if (fullscreen)
            {
                ToggleFullscreen();
                return;
            }
            WindowState = IsMaximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        public void Minimize()
        {
            WindowState = FormWindowState.Minimized;
        }

        public void ToggleFullscreen() => ToggleFullscreen(null);

        private void ToggleFullscreen(Screen target)
        {
            if (!fullscreen)
            {
                Screen screen = target ?? Screen.FromHandle(Handle);
                windowedMaximized = IsMaximized;
                windowedBounds = windowedMaximized ? NativeMethods.GetNormalBounds(Handle) : Bounds;
                fullscreen = true;
                state.FullscreenDevice = screen.DeviceName;
                if (windowedMaximized) WindowState = FormWindowState.Normal;
                NativeMethods.SetFramed(Handle, false, screen.Bounds);
            }
            else
            {
                fullscreen = false;
                NativeMethods.SetFramed(Handle, true, windowedBounds);
                if (windowedMaximized) WindowState = FormWindowState.Maximized;
            }
            WindowModeChanged?.Invoke();
        }

        private void RestoreFullscreen()
        {
            if (!state.WindowFullscreen || fullscreen) return;
            Screen remembered = Screen.AllScreens.FirstOrDefault(screen => string.Equals(screen.DeviceName, state.FullscreenDevice, StringComparison.OrdinalIgnoreCase));
            ToggleFullscreen(remembered);
        }

        private void ApplySavedBounds(HubState saved)
        {
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            MinimumSize = new Size(Math.Min(900, workArea.Width), Math.Min(600, workArea.Height));

            var bounds = new Rectangle(saved.WindowX, saved.WindowY, saved.WindowWidth, saved.WindowHeight);
            bool visible = bounds.Width >= MinimumSize.Width && bounds.Height >= MinimumSize.Height &&
                           Array.Exists(Screen.AllScreens, screen => screen.WorkingArea.IntersectsWith(bounds));

            if (!visible)
            {
                int width = Math.Max(MinimumSize.Width, (int)(workArea.Width * 0.84));
                int height = Math.Max(MinimumSize.Height, (int)(workArea.Height * 0.88));
                bounds = new Rectangle(workArea.X + (workArea.Width - width) / 2, workArea.Y + (workArea.Height - height) / 2, width, height);
            }

            Bounds = bounds;
            if (saved.WindowMaximized) WindowState = FormWindowState.Maximized;
        }

        private void SaveBounds()
        {
            Rectangle bounds;
            bool maximized;
            if (fullscreen)
            {
                bounds = windowedBounds;
                maximized = windowedMaximized;
            }
            else
            {
                bounds = WindowState == FormWindowState.Normal ? Bounds : NativeMethods.GetNormalBounds(Handle);
                maximized = WindowState == FormWindowState.Maximized;
            }

            state.WindowX = bounds.X;
            state.WindowY = bounds.Y;
            state.WindowWidth = bounds.Width;
            state.WindowHeight = bounds.Height;
            state.WindowMaximized = maximized;
            state.WindowFullscreen = fullscreen;
            state.Save();
        }

        public void ShowAndActivate()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            NativeMethods.ActivateExistingWindow(WindowTitle);
            Activate();
        }

        public void QuitApplication()
        {
            quitting = true;
            Close();
        }

        public void RunOnUi(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }
    }
}
