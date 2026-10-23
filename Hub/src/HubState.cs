using System.Collections.Generic;

namespace GameHub
{
    internal sealed class HubState
    {
        public Dictionary<string, string> Launches { get; set; } = new Dictionary<string, string>();
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }
        public bool WindowFullscreen { get; set; }
        public string FullscreenDevice { get; set; } = "";
        public string ThemeBackground { get; set; } = "";
        public bool ThemeLight { get; set; }
        public string SteamGridDbKey { get; set; } = "";

        public static HubState Load()
        {
            var state = Json.ReadFile<HubState>(HubPaths.StateFile) ?? new HubState();
            state.Launches ??= new Dictionary<string, string>();
            state.SteamGridDbKey ??= "";
            state.FullscreenDevice ??= "";
            state.ThemeBackground ??= "";
            return state;
        }

        public void Save()
        {
            Json.WriteFile(HubPaths.StateFile, this);
        }
    }
}
