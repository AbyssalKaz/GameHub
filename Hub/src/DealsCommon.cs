using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameHub
{
    internal static class DealsMoney
    {
        private static readonly Dictionary<string, string> Currencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AU"] = "AUD", ["NZ"] = "NZD", ["US"] = "USD", ["CA"] = "CAD", ["GB"] = "GBP", ["JP"] = "JPY", ["KR"] = "KRW",
            ["CN"] = "CNY", ["BR"] = "BRL", ["MX"] = "MXN", ["IN"] = "INR", ["NO"] = "NOK", ["SE"] = "SEK", ["DK"] = "DKK",
            ["PL"] = "PLN", ["CH"] = "CHF", ["SG"] = "SGD", ["HK"] = "HKD", ["TW"] = "TWD", ["ZA"] = "ZAR", ["TR"] = "USD",
            ["DE"] = "EUR", ["FR"] = "EUR", ["IT"] = "EUR", ["ES"] = "EUR", ["NL"] = "EUR", ["BE"] = "EUR", ["AT"] = "EUR",
            ["IE"] = "EUR", ["FI"] = "EUR", ["PT"] = "EUR", ["GR"] = "EUR", ["SK"] = "EUR", ["SI"] = "EUR", ["EE"] = "EUR",
            ["LV"] = "EUR", ["LT"] = "EUR", ["LU"] = "EUR", ["CY"] = "EUR", ["MT"] = "EUR", ["HR"] = "EUR",
        };

        public static string CurrencyFor(string country) => Currencies.TryGetValue(country ?? "", out string currency) ? currency : "USD";

        public static long? Cents(object value)
        {
            if (value == null) return null;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long cents) ? cents : (long?)null;
        }

        public static long? CentsFromAmount(object value)
        {
            if (value == null) return null;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount) ? (long)Math.Round(amount * 100m) : (long?)null;
        }
    }

    internal static class DealsHttp
    {
        public static readonly HttpClient Client = Create();

        private static HttpClient Create()
        {
            var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            {
                Timeout = TimeSpan.FromSeconds(25),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
            return client;
        }

        public static async Task<string> GetWithCurlFallbackAsync(string url)
        {
            using (var response = await Client.GetAsync(url))
            {
                if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode != 403) throw new InvalidOperationException("HTTP " + (int)response.StatusCode);
            }

            string curl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            if (!File.Exists(curl) || url.IndexOfAny(new[] { '"', ' ', '\r', '\n' }) >= 0) throw new InvalidOperationException("blocked by the store's bot protection");

            var start = new ProcessStartInfo(curl, "-s -f -m 20 --compressed -A GameHub/1.0 \"" + url + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var process = Process.Start(start);
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit(25000));
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                }
                throw new InvalidOperationException("timed out");
            }
            await errors;
            if (process.ExitCode != 0) throw new InvalidOperationException("blocked by the store's bot protection");
            return await output;
        }
    }

    internal static class DealsText
    {
        public static string Key(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            string text = title.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant();
            text = Regex.Replace(text, @"\p{Mn}", "");
            text = Regex.Replace(text, @"\b(standard|digital|deluxe)?\s*edition\b|™|®|©", "");
            return Regex.Replace(text, @"[^\p{L}\p{N}]", "");
        }
    }

    internal static class DealsLinks
    {
        private static readonly string[] Hosts =
        {
            "store.steampowered.com", "steamcommunity.com", "help.steampowered.com",
            "store.epicgames.com", "www.epicgames.com",
            "www.gog.com", "gog.com",
            "itch.io",
            "store.ubisoft.com", "www.ubisoft.com", "free.ubisoft.com", "freeweekend.ubisoft.com",
            "isthereanydeal.com", "itad.link",
        };

        private static readonly string[] ClientPrefixes = { "steam://store/", "steam://openurl/https://store.steampowered.com/", "com.epicgames.launcher://store/" };

        public static bool IsAllowed(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            if (ClientPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return true;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)) return false;
            string host = uri.Host.ToLowerInvariant();
            return Hosts.Contains(host) || host.EndsWith(".itch.io", StringComparison.Ordinal);
        }
    }
}
