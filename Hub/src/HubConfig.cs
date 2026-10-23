using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GameHub
{
    internal static class HubConfig
    {
        private static readonly object Gate = new object();

        public static Dictionary<string, object> Load()
        {
            lock (Gate)
            {
                try
                {
                    return File.Exists(HubPaths.ConfigFile)
                        ? Json.ParseObject(File.ReadAllText(HubPaths.ConfigFile, Encoding.UTF8)) ?? new Dictionary<string, object>()
                        : new Dictionary<string, object>();
                }
                catch
                {
                    return new Dictionary<string, object>();
                }
            }
        }

        public static List<string> GetList(Dictionary<string, object> config, string key)
        {
            return config.TryGetValue(key, out object value) && value is object[] items
                ? items.OfType<string>().ToList()
                : new List<string>();
        }

        public static void Update(System.Action<Dictionary<string, object>> change)
        {
            lock (Gate)
            {
                Dictionary<string, object> config = Load();
                change(config);
                string backup = HubPaths.ConfigFile + ".bak";
                if (File.Exists(HubPaths.ConfigFile)) File.Copy(HubPaths.ConfigFile, backup, true);
                File.WriteAllText(HubPaths.ConfigFile, Indent(Json.Serialize(config)), new UTF8Encoding(false));
            }
        }

        public static string Indent(string json)
        {
            var builder = new StringBuilder();
            int depth = 0;
            bool inString = false;
            for (int index = 0; index < json.Length; index++)
            {
                char current = json[index];
                if (current == '"' && (index == 0 || json[index - 1] != '\\')) inString = !inString;
                if (inString)
                {
                    builder.Append(current);
                    continue;
                }
                switch (current)
                {
                    case '{':
                    case '[':
                        builder.Append(current).Append('\n').Append(' ', ++depth * 2);
                        break;
                    case '}':
                    case ']':
                        builder.Append('\n').Append(' ', --depth * 2).Append(current);
                        break;
                    case ',':
                        builder.Append(current).Append('\n').Append(' ', depth * 2);
                        break;
                    case ':':
                        builder.Append(": ");
                        break;
                    default:
                        builder.Append(current);
                        break;
                }
            }
            return builder.ToString();
        }
    }
}
