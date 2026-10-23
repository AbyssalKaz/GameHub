using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace GameHub
{
    internal abstract class HubModule
    {
        protected HubContext Hub { get; private set; }

        internal void Attach(HubContext hub) => Hub = hub;

        public virtual int Order => 0;

        public virtual void Start()
        {
        }

        public virtual bool Handle(string type, Dictionary<string, object> message) => false;

        public virtual void Decorate(LibraryDocument library)
        {
        }

        public virtual void OnLibraryLoaded(LibraryDocument library)
        {
        }

        public virtual void AddLibraryData(Dictionary<string, object> extras)
        {
        }

        public virtual void BeforeLaunch(GameEntry game, LaunchRequest request)
        {
        }

        public virtual void AfterLaunch(GameEntry game, LaunchRequest request, Process process)
        {
        }

        public virtual void Stop()
        {
        }
    }

    internal sealed class LaunchRequest
    {
        public ProcessStartInfo StartInfo { get; set; }
        public string Origin { get; set; } = "hub";
        public bool Cancel { get; set; }
        public string CancelMessage { get; set; }
    }

    internal sealed class HubContext
    {
        private readonly HubController controller;

        public HubContext(HubController controller, MainForm form, HubState state)
        {
            this.controller = controller;
            Form = form;
            State = state;
        }

        public MainForm Form { get; }
        public HubState State { get; }
        public UserDataStore UserData { get; } = UserDataStore.Load();
        public SettingsStore Settings { get; } = SettingsStore.Load();
        public TrayService Tray { get; internal set; }
        public CoreWebView2 Web { get; internal set; }
        public CoreWebView2Environment Environment { get; internal set; }
        internal List<HubModule> Modules { get; } = new List<HubModule>();

        public LibraryDocument Library => controller.CurrentLibrary;

        public GameEntry Find(string id) => Library.Games.FirstOrDefault(game => game.Id == id);

        public T Module<T>() where T : HubModule => Modules.OfType<T>().FirstOrDefault();

        public void Post(object payload) => controller.PostMessage(payload);

        public void Toast(string message, string kind = "info") => Post(new { type = "toast", kind, message });

        public void Notify(string title, string text, Action onClick = null) => Tray?.Notify(title, text, onClick);

        public void RunOnUi(Action action) => Form.RunOnUi(action);

        public void Launch(string id, string origin = "hub") => controller.LaunchGame(id, origin);

        public Task RefreshLibraryAsync() => controller.RefreshLibraryAsync();

        public Task ReloadLibraryAsync() => controller.ReloadLibraryAsync();

        public void SendLibrary() => controller.ResendLibrary();

        public void ShowWindow() => Form.RunOnUi(Form.ShowAndActivate);
    }
}
