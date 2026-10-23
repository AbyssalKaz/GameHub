using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace GameHub
{
    internal static class Json
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private static JavaScriptSerializer CreateSerializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
        }

        public static string Serialize(object value) => CreateSerializer().Serialize(value);

        public static T Deserialize<T>(string json) => CreateSerializer().Deserialize<T>(json);

        public static object Parse(string json) => CreateSerializer().DeserializeObject(json);

        public static Dictionary<string, object> ParseObject(string json) => Parse(json) as Dictionary<string, object>;

        public static T ReadFile<T>(string path) where T : class
        {
            try
            {
                return File.Exists(path) ? Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)) : null;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteFile(string path, object value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, Serialize(value), Utf8NoBom);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        public static string GetString(this Dictionary<string, object> source, string key)
        {
            return source != null && source.TryGetValue(key, out object value) ? value as string : null;
        }
    }
}
