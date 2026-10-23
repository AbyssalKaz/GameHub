using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal static class StoreDomains
    {
        private static readonly HashSet<string> Official = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "steampowered.com", "steamcommunity.com", "steamstatic.com", "steamcontent.com", "steamserver.net", "steamusercontent.com", "valvesoftware.com", "steam-chat.com",
            "steamcdn-a.akamaihd.net", "steamuserimages-a.akamaihd.net", "steamstore-a.akamaihd.net", "cdn.akamai.steamstatic.com",
            "epicgames.com", "epicgames.dev", "epicgames.net", "unrealengine.com", "epicgamescdn.com", "fortnite.com", "epicgames-download1.akamaized.net", "download.epicgames.com",
            "gog.com", "gog-statics.com", "gogalaxy.com", "gog-cdn.com",
            "xbox.com", "xboxlive.com", "microsoft.com", "live.com", "microsoftonline.com", "msauth.net", "msftauth.net", "s-microsoft.com", "assets-xbox.com",
            "ea.com", "origin.com", "eaassets-a.akamaihd.net", "easports.com",
            "ubisoft.com", "ubi.com", "ubisoftconnect.com", "ubisoft.org",
            "humblebundle.com", "humblebundle-a.akamaihd.net",
            "itch.io", "itch.zone", "itchio-mirror.com",
            "battle.net", "blizzard.com", "blzstatic.cn", "riotgames.com", "leagueoflegends.com", "rockstargames.com", "hoyoverse.com", "mihoyo.com", "hoyolab.com",
        };

        public static bool IsOfficial(string host) => !string.IsNullOrEmpty(host) && Matches(Official, host);

        public static bool Matches(HashSet<string> domains, string host)
        {
            if (string.IsNullOrEmpty(host) || domains.Count == 0) return false;
            string candidate = host.TrimEnd('.');
            while (true)
            {
                if (domains.Contains(candidate)) return true;
                int dot = candidate.IndexOf('.');
                if (dot < 0 || dot == candidate.Length - 1) return false;
                candidate = candidate.Substring(dot + 1);
            }
        }

        public static string HostOf(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri address) && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps) ? address.Host.ToLowerInvariant() : "";
        }
    }

    internal sealed class StoreAdBlock
    {
        private static readonly string UserListFile = Path.Combine(HubPaths.CacheDir, "store-filters.txt");
        private static readonly TimeSpan UserListLifetime = TimeSpan.FromDays(3);

        private static readonly string[] BuiltInHosts =
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com", "google-analytics.com", "googletagservices.com", "googletagmanager.com", "adservice.google.com",
            "pagead2.googlesyndication.com", "tpc.googlesyndication.com", "imasdk.googleapis.com", "app-measurement.com", "2mdn.net", "admob.com", "adsense.com",
            "analytics.google.com", "stats.g.doubleclick.net", "fundingchoicesmessages.google.com",
            "connect.facebook.net", "pixel.facebook.com", "an.facebook.com",
            "analytics.twitter.com", "ads-twitter.com", "ads-api.twitter.com", "static.ads-twitter.com",
            "ads.linkedin.com", "px.ads.linkedin.com", "snap.licdn.com",
            "ads.tiktok.com", "analytics.tiktok.com", "business-api.tiktok.com",
            "sc-static.net", "tr.snapchat.com", "ct.pinterest.com", "analytics.pinterest.com", "ads.pinterest.com",
            "ads.reddit.com", "events.reddit.com", "rereddit.com", "alb.reddit.com",
            "bat.bing.com", "ads.microsoft.com", "c.bing.com", "clarity.ms", "c.clarity.ms",
            "amazon-adsystem.com", "aax.amazon-adsystem.com", "assoc-amazon.com", "adsystem.amazon.com",
            "adnxs.com", "adnxs-simple.com", "adsrvr.org", "rubiconproject.com", "pubmatic.com", "openx.net", "openx.com", "casalemedia.com", "indexexchange.com",
            "criteo.com", "criteo.net", "taboola.com", "taboolasyndication.com", "outbrain.com", "outbrainimg.com", "revcontent.com", "mgid.com", "zergnet.com",
            "smartadserver.com", "sascdn.com", "adform.net", "adformdsp.net", "yieldmo.com", "sharethrough.com", "triplelift.com", "3lift.com", "teads.tv", "teads.com",
            "spotxchange.com", "spotx.tv", "springserve.com", "smaato.net", "inmobi.com", "applovin.com", "applvn.com", "unityads.unity3d.com", "ironsrc.mobi", "supersonicads.com",
            "chartboost.com", "vungle.com", "adcolony.com", "mopub.com", "tapjoy.com", "fyber.com", "startapp.com", "liftoff.io",
            "media.net", "contextual.media.net", "adroll.com", "nextroll.com", "quantserve.com", "quantcount.com", "quantcast.com", "scorecardresearch.com", "comscore.com",
            "bluekai.com", "krxd.net", "exelator.com", "eyeota.net", "crwdcntrl.net", "lotame.com", "demdex.net", "everesttech.net", "omtrdc.net", "2o7.net",
            "adsafeprotected.com", "moatads.com", "moatpixel.com", "doubleverify.com", "iasds01.com", "serving-sys.com", "flashtalking.com", "innovid.com", "sizmek.com",
            "mathtag.com", "mediamath.com", "turn.com", "rlcdn.com", "liveramp.com", "tapad.com", "agkn.com", "bidswitch.net", "bidr.io", "contextweb.com",
            "gumgum.com", "33across.com", "sovrn.com", "lijit.com", "districtm.io", "emxdgt.com", "conversantmedia.com", "dotomi.com", "dtscout.com", "yieldlab.net",
            "adition.com", "improvedigital.com", "360yield.com", "rhythmone.com", "1rx.io", "undertone.com", "kargo.com", "medianet.com", "sonobi.com", "synacor.com",
            "zemanta.com", "nativo.com", "powerinbox.com", "adblade.com", "adengage.com", "infolinks.com", "kontera.com", "vibrantmedia.com", "chitika.com", "bidvertiser.com",
            "propellerads.com", "propellerclick.com", "propellerpops.com", "popads.net", "popcash.net", "popmyads.com", "popunder.net", "poptm.com", "pophub.net",
            "adsterra.com", "adsterratools.com", "a-ads.com", "hilltopads.net", "hilltopads.com", "clickadu.com", "clickaine.com", "exoclick.com", "exosrv.com", "exdynsrv.com",
            "juicyads.com", "trafficjunky.net", "trafficjunky.com", "trafficstars.com", "tsyndicate.com", "plugrush.com", "adcash.com", "adxpansion.com", "ero-advertising.com",
            "onclickads.net", "onclkds.com", "onclickmax.com", "onclickperformance.com", "clickaduclick.com", "admaven.com", "ad-maven.com", "admvn.com", "richpush.co",
            "pushwhy.com", "pushnami.com", "pushengage.com", "push.express", "sendpul.se", "subscribers.com", "izooto.com", "pushcrew.com",
            "zeropark.com", "mobidea.com", "mobiyield.com", "evadav.com", "galaksion.com", "adskeeper.com", "adskeeper.co.uk", "admitad.com", "clickbooth.com",
            "revenuehits.com", "intellitxt.com", "yllix.com", "ylx-1.com", "ylx-2.com", "linkbucks.com", "adf.ly", "shorte.st", "sh.st", "ouo.io", "bc.vc",
            "coinhive.com", "coin-hive.com", "jsecoin.com", "crypto-loot.com", "minero.cc", "webminepool.com",
            "hotjar.com", "hotjar.io", "mouseflow.com", "crazyegg.com", "luckyorange.com", "luckyorange.net", "fullstory.com", "smartlook.com", "inspectlet.com", "logrocket.io",
            "mixpanel.com", "amplitude.com", "heap.io", "heapanalytics.com", "kissmetrics.com", "segment.io", "cdn.segment.com", "api.segment.io",
            "nr-data.net", "bugsnag.com", "sentry-cdn.com", "chartbeat.com", "chartbeat.net", "parsely.com", "parse.ly", "statcounter.com", "histats.com",
            "clicky.com", "getclicky.com", "woopra.com",
            "branch.io", "adjust.com", "appsflyer.com", "kochava.com", "singular.net", "tenjin.com",
            "mc.yandex.ru", "an.yandex.ru", "adfox.ru", "adriver.ru", "top-fwz1.mail.ru", "counter.yadro.ru", "tns-counter.ru",
            "pubads.g.doubleclick.net", "securepubads.g.doubleclick.net", "ad.doubleclick.net", "cm.g.doubleclick.net", "googleads.g.doubleclick.net",
            "zedo.com", "adtech.de", "adtechus.com", "advertising.com", "atwola.com", "adsonar.com", "tacoda.net", "yieldmanager.com", "yieldmanager.net", "rightmedia.com",
            "adbrite.com", "adbureau.net", "admeld.com", "adzerk.net", "adzerk.com", "carbonads.com", "carbonads.net", "buysellads.com", "buysellads.net", "servedby-buysellads.com",
            "ezoic.net", "ezodn.com", "ezojs.com", "mediavine.com", "adthrive.com", "raptive.com", "freestar.io", "freestar.com", "pbstck.com", "pubnation.com",
            "cpx.to", "cpxinteractive.com", "adspirit.de", "adhese.com", "adhigh.net", "adpushup.com", "vdo.ai", "primis.tech", "connatix.com", "aniview.com", "anyclip.com",
            "jwpltx.com", "ads.jwplayer.com", "imrworldwide.com", "effectivemeasure.net", "gemius.pl", "hit.gemius.pl", "cxense.com", "permutive.com",
            "permutive.app", "id5-sync.com", "uidapi.com", "adsymptotic.com", "pippio.com", "addthis.com", "addthisedge.com", "sharethis.com",
            "zqtk.net", "evidon.com", "fwmrm.net", "freewheel.tv", "tremorhub.com", "telaria.com", "unrulymedia.com",
            "brightmountainmedia.com", "adkernel.com", "adtelligent.com", "e-planning.net", "smilewanted.com", "seedtag.com", "ogury.io", "presage.io", 
            "trafficfactory.biz", "trafficforce.com", "trafficshop.com", "adultforce.com", "realsrv.com", "magsrv.com", "pemsrv.com", "a-delivery.net", "mndsrv.com",
            "aclickads.com", "acint.net", "adrecover.com", "adlightning.com", "confiant-integrations.net", "geoedge.be",
            "dianomi.com", "lockerdome.com", "adsco.re", "adscore.com", "pubfuture.com", "adpone.com", "adpushup.net", "admixer.net", "adriverru.com",
            "bounceexchange.com", "bouncex.net", "wunderkind.co", "justpremium.com", "yieldlove.com", "yieldlove-ad-serving.net", "stroeerdigitalgroup.de",
            "adsrvr.com", "trackcmp.net", "tradedoubler.com", "awin1.com", "zenaps.com", "dwin1.com", "linksynergy.com", "anrdoezrs.net", "jdoqocy.com", "tkqlhce.com",
            "kqzyfj.com", "dpbolvw.net", "emjcd.com", "ftjcfx.com", "lduhtrp.net", "qksrv.net", "shareasale-analytics.com", "pepperjamnetwork.com", "impactradius-event.com",
            "pxf.io", "sjv.io", "ojrq.net", "evyy.net", "gotrackier.com", "voluum.com", "volumtrk.com", "redtrack.io", "bemobtrk.com", "binom.org",
        };

        private readonly SettingsStore settings;
        private readonly HashSet<string> builtIn = new HashSet<string>(BuiltInHosts, StringComparer.OrdinalIgnoreCase);
        private HashSet<string> userHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> allowedSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public StoreAdBlock(SettingsStore settings)
        {
            this.settings = settings;
            ReadAllowedSites();
            LoadCachedUserList();
        }

        public bool Enabled => settings.GetBool("store.blockAds", true);

        public int BuiltInCount => builtIn.Count;

        public int UserCount => userHosts.Count;

        public string UserListStatus { get; private set; } = "";

        public bool IsSiteAllowed(string host) => !string.IsNullOrEmpty(host) && StoreDomains.Matches(allowedSites, host);

        public bool ShouldBlock(string requestHost, string pageHost)
        {
            if (string.IsNullOrEmpty(requestHost) || StoreDomains.IsOfficial(requestHost)) return false;
            return StoreDomains.Matches(builtIn, requestHost) || StoreDomains.Matches(userHosts, requestHost);
        }

        public bool ToggleSite(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string site = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
            List<string> sites = ReadAllowedList();
            bool nowAllowed;
            if (IsSiteAllowed(host))
            {
                sites.RemoveAll(entry => host.Equals(entry, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase));
                nowAllowed = false;
            }
            else
            {
                sites.Add(site.ToLowerInvariant());
                nowAllowed = true;
            }
            settings.Set("store.adAllowSites", sites.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            ReadAllowedSites();
            return nowAllowed;
        }

        public void ReadAllowedSites()
        {
            allowedSites = new HashSet<string>(ReadAllowedList(), StringComparer.OrdinalIgnoreCase);
        }

        private List<string> ReadAllowedList()
        {
            return (settings.Get("store.adAllowSites") as object[] ?? Array.Empty<object>()).OfType<string>().Where(entry => entry.Length > 0).ToList();
        }

        private void LoadCachedUserList()
        {
            try
            {
                if (File.Exists(UserListFile))
                {
                    userHosts = ParseList(File.ReadAllText(UserListFile, Encoding.UTF8));
                    UserListStatus = $"{userHosts.Count:N0} hosts from your filter list";
                }
            }
            catch
            {
                userHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public async Task RefreshUserListAsync(bool force)
        {
            string url = (settings.GetString("store.filterListUrl") ?? "").Trim();
            if (url.Length == 0)
            {
                userHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                UserListStatus = "";
                return;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri address) || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
            {
                UserListStatus = "The filter list address isn't a web address.";
                return;
            }

            string marker = UserListFile + ".source";
            bool sameSource = File.Exists(marker) && File.ReadAllText(marker).Trim() == url;
            bool fresh = sameSource && File.Exists(UserListFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(UserListFile) < UserListLifetime;
            if (fresh && !force)
            {
                if (userHosts.Count == 0) LoadCachedUserList();
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
                string text = await client.GetStringAsync(address).ConfigureAwait(false);
                HashSet<string> parsed = ParseList(text);
                File.WriteAllText(UserListFile, text, Encoding.UTF8);
                File.WriteAllText(marker, url);
                userHosts = parsed;
                UserListStatus = $"{parsed.Count:N0} hosts from your filter list";
            }
            catch (Exception error)
            {
                if (sameSource && userHosts.Count == 0) LoadCachedUserList();
                UserListStatus = "Couldn't download the filter list: " + error.Message;
            }
        }

        public static HashSet<string> ParseList(string text)
        {
            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StringReader(text ?? "");
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == '!' || line[0] == '[' || line.StartsWith("@@")) continue;

                string host = null;
                if (line.StartsWith("||"))
                {
                    if (line.EndsWith("^") && line.IndexOfAny(new[] { '/', '*', '$', '|' }, 2) < 0) host = line.Substring(2, line.Length - 3);
                }
                else
                {
                    int comment = line.IndexOf('#');
                    if (comment > 0) line = line.Substring(0, comment).Trim();
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2 && (parts[0] == "0.0.0.0" || parts[0] == "127.0.0.1" || parts[0] == "::" || parts[0] == "::1")) host = parts[1];
                    else if (parts.Length == 1 && parts[0].Contains(".") && parts[0].IndexOfAny(new[] { '/', '*', '$', '|', '^', ':' }) < 0) host = parts[0];
                }

                if (host == null) continue;
                host = host.Trim().TrimEnd('.').ToLowerInvariant();
                if (host.Length < 4 || !host.Contains(".") || host == "localhost" || host == "local" || host == "0.0.0.0" || host.Any(character => !(char.IsLetterOrDigit(character) || character == '.' || character == '-' || character == '_'))) continue;
                hosts.Add(host);
            }
            return hosts;
        }
    }
}
