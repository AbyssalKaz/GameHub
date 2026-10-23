using System;
using System.Collections.Generic;
using System.Linq;

namespace GameHub
{
    internal sealed class UserDataStore
    {
        private readonly object gate = new object();

        public Dictionary<string, Dictionary<string, object>> Games { get; set; } = new Dictionary<string, Dictionary<string, object>>();

        public static UserDataStore Load()
        {
            var store = Json.ReadFile<UserDataStore>(HubPaths.UserDataFile) ?? new UserDataStore();
            store.Games ??= new Dictionary<string, Dictionary<string, object>>();
            return store;
        }

        public Dictionary<string, object> Get(string id)
        {
            lock (gate)
            {
                return Games.TryGetValue(id ?? "", out var data) ? new Dictionary<string, object>(data) : new Dictionary<string, object>();
            }
        }

        public object Get(string id, string key)
        {
            lock (gate)
            {
                return Games.TryGetValue(id ?? "", out var data) && data.TryGetValue(key, out object value) ? value : null;
            }
        }

        public string GetString(string id, string key) => Get(id, key) as string;

        public bool GetBool(string id, string key) => Get(id, key) is bool value && value;

        public Dictionary<string, object> Merge(string id, Dictionary<string, object> patch)
        {
            if (string.IsNullOrEmpty(id) || patch == null) return Get(id);
            lock (gate)
            {
                if (!Games.TryGetValue(id, out var data))
                {
                    data = new Dictionary<string, object>();
                    Games[id] = data;
                }
                foreach (var entry in patch)
                {
                    if (entry.Value == null) data.Remove(entry.Key);
                    else data[entry.Key] = entry.Value;
                }
                if (data.Count == 0) Games.Remove(id);
                Save();
                return new Dictionary<string, object>(data);
            }
        }

        public void Set(string id, string key, object value) => Merge(id, new Dictionary<string, object> { [key] = value });

        public Dictionary<string, Dictionary<string, object>> Snapshot()
        {
            lock (gate)
            {
                return Games.ToDictionary(entry => entry.Key, entry => new Dictionary<string, object>(entry.Value));
            }
        }

        public void ReplaceAll(Dictionary<string, Dictionary<string, object>> games)
        {
            lock (gate)
            {
                Games = games ?? new Dictionary<string, Dictionary<string, object>>();
                Save();
            }
        }

        private void Save() => Json.WriteFile(HubPaths.UserDataFile, this);
    }

    internal sealed class SettingsStore
    {
        private readonly object gate = new object();

        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();

        public event Action<string, object> Changed;

        public static SettingsStore Load()
        {
            var store = Json.ReadFile<SettingsStore>(HubPaths.SettingsFile) ?? new SettingsStore();
            store.Values ??= new Dictionary<string, object>();
            return store;
        }

        public object Get(string key)
        {
            lock (gate)
            {
                return Values.TryGetValue(key, out object value) ? value : null;
            }
        }

        public bool GetBool(string key, bool fallback = false) => Get(key) is bool value ? value : fallback;

        public string GetString(string key, string fallback = null) => Get(key) as string ?? fallback;

        public int GetInt(string key, int fallback = 0)
        {
            object value = Get(key);
            try
            {
                return value == null ? fallback : Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        public void Set(string key, object value)
        {
            lock (gate)
            {
                if (value == null) Values.Remove(key);
                else Values[key] = value;
                Json.WriteFile(HubPaths.SettingsFile, this);
            }
            Changed?.Invoke(key, value);
        }

        public Dictionary<string, object> Snapshot()
        {
            lock (gate)
            {
                return new Dictionary<string, object>(Values);
            }
        }

        public void ReplaceAll(Dictionary<string, object> values)
        {
            lock (gate)
            {
                Values = values ?? new Dictionary<string, object>();
                Json.WriteFile(HubPaths.SettingsFile, this);
            }
        }
    }
}
