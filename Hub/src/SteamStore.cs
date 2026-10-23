using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameHub
{
    internal static class SteamStore
    {
        public const string AssetBase = "https://shared.akamai.steamstatic.com/store_item_assets/";

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetUserDefaultGeoName(StringBuilder geoName, int geoNameCount);

        public static string Country
        {
            get
            {
                try
                {
                    var name = new StringBuilder(16);
                    if (GetUserDefaultGeoName(name, name.Capacity) > 0 && Regex.IsMatch(name.ToString(), "^[A-Z]{2}$")) return name.ToString();
                }
                catch
                {
                }
                return "US";
            }
        }

        public static async Task<Dictionary<string, Dictionary<string, object>>> GetItemsAsync(HttpClient http, IEnumerable<string> appIds, bool purchase = false, bool release = false, bool reviews = false)
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            foreach (var chunk in appIds.Distinct().Select((id, position) => new { id, position }).GroupBy(entry => entry.position / 30))
            {
                string ids = string.Join(",", chunk.Select(entry => "{\"appid\":" + entry.id + "}"));
                string request = "{\"include_assets\":true" +
                                 (purchase ? ",\"include_all_purchase_options\":true" : "") +
                                 (release ? ",\"include_release\":true" : "") +
                                 (reviews ? ",\"include_reviews\":true" : "") + "}";
                string input = "{\"ids\":[" + ids + "],\"context\":{\"language\":\"english\",\"country_code\":\"" + Country + "\"},\"data_request\":" + request + "}";

                string json = await http.GetStringAsync("https://api.steampowered.com/IStoreBrowseService/GetItems/v1?input_json=" + Uri.EscapeDataString(input));
                var response = Json.ParseObject(json)?["response"] as Dictionary<string, object>;
                if (response == null || !(response.TryGetValue("store_items", out object items) && items is object[] list)) continue;

                foreach (Dictionary<string, object> item in list.OfType<Dictionary<string, object>>())
                {
                    if (item.ContainsKey("id")) result[Convert.ToString(item["id"])] = item;
                }
            }
            return result;
        }

        public static string AssetUrl(Dictionary<string, object> item, params string[] keys)
        {
            if (item == null || !(item.TryGetValue("assets", out object value) && value is Dictionary<string, object> assets)) return null;
            string format = assets.GetString("asset_url_format");
            if (format == null) return null;

            foreach (string key in keys)
            {
                string file = assets.GetString(key);
                if (!string.IsNullOrEmpty(file)) return AssetBase + format.Replace("${FILENAME}", file);
            }
            return null;
        }

        public static async Task<List<(string Id, string Name)>> SearchAsync(HttpClient http, string term)
        {
            var results = new List<(string, string)>();
            string json = await http.GetStringAsync("https://store.steampowered.com/api/storesearch/?l=english&cc=" + Country + "&term=" + Uri.EscapeDataString(term));
            if (Json.ParseObject(json)?["items"] is object[] items)
            {
                foreach (Dictionary<string, object> item in items.OfType<Dictionary<string, object>>())
                {
                    results.Add((Convert.ToString(item["id"]), item.GetString("name")));
                }
            }
            return results;
        }

        public static async Task<List<string>> SearchResultAppIdsAsync(HttpClient http, string query)
        {
            string json = await http.GetStringAsync("https://store.steampowered.com/search/results/?infinite=1&l=english&cc=" + Country + "&" + query);
            string html = Json.ParseObject(json)?.GetString("results_html") ?? "";
            return Regex.Matches(html, "data-ds-appid=\"(\\d+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .ToList();
        }

        public static object Path(object node, params object[] steps)
        {
            foreach (object step in steps)
            {
                if (node == null) return null;
                if (step is int index)
                {
                    node = node is object[] array && index < array.Length ? array[index] : null;
                }
                else
                {
                    node = node is Dictionary<string, object> map && map.TryGetValue((string)step, out object child) ? child : null;
                }
            }
            return node;
        }

        public static string UnixToIso(object seconds)
        {
            if (seconds == null || !long.TryParse(Convert.ToString(seconds), out long value) || value <= 0) return null;
            return DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime.ToString("o");
        }
    }
}
