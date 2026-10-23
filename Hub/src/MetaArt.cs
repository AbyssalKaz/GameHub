using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class MetaArtFiller
    {
        private static readonly string[] AllKinds = { "cover", "hero", "logo", "icon" };

        private readonly HubContext hub;
        private readonly Func<GameEntry, string> appIdFor;
        private readonly CoverFinder finder = new CoverFinder();
        private CancellationTokenSource running;
        private Task runningTask = Task.CompletedTask;
        private bool runningQuiet;

        public MetaArtFiller(HubContext hub, Func<GameEntry, string> appIdFor)
        {
            this.hub = hub;
            this.appIdFor = appIdFor;
        }

        public bool Running => running != null;

        public static Action<GameEntry> PostArt(HubContext hub, ArtService art) => game => hub.Post(new
        {
            type = "art",
            id = game.Id,
            cover = game.CoverUrl,
            hero = game.HeroUrl,
            logo = game.LogoUrl,
            header = game.HeaderUrl,
            icon = game.IconUrl,
            custom = art.OverriddenKinds(game),
        });

        public void Cancel() => running?.Cancel();

        private static bool Missing(GameEntry game, string kind) => kind switch
        {
            "cover" => game.CoverUrl == null,
            "hero" => game.HeroUrl == null,
            "logo" => game.LogoUrl == null,
            "icon" => game.IconUrl == null,
            _ => false,
        };

        private static int SourceRank(string source) => source switch
        {
            "Steam" => 0,
            "SteamGridDB" => 1,
            "Epic Games" => 2,
            _ => 9,
        };

        public async Task RunAsync(List<string> kinds, IEnumerable<GameEntry> only = null, bool quiet = false)
        {
            ArtService art = ArtService.Current;
            if (art == null) return;
            if (running != null)
            {
                if (quiet || !runningQuiet) return;
                running.Cancel();
                try
                {
                    await runningTask;
                }
                catch (Exception)
                {
                }
                if (running != null) return;
            }

            var finished = new TaskCompletionSource<bool>();
            runningTask = finished.Task;
            runningQuiet = quiet;
            try
            {
                await RunCoreAsync(art, kinds, only, quiet);
            }
            finally
            {
                finished.TrySetResult(true);
            }
        }

        private async Task RunCoreAsync(ArtService art, List<string> kinds, IEnumerable<GameEntry> only, bool quiet)
        {
            kinds = kinds.Where(AllKinds.Contains).Distinct().ToList();
            if (kinds.Count == 0) kinds.Add("cover");

            var source = new CancellationTokenSource();
            running = source;
            CancellationToken token = source.Token;
            Action<GameEntry> post = PostArt(hub, art);

            List<GameEntry> games = (only ?? hub.Library.Games).ToList()
                .Where(game => game.Status != "leftover" && kinds.Any(kind => Missing(game, kind)))
                .OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missed = new List<string>();
            int applied = 0;
            int done = 0;
            string current = null;
            void Progress(bool active, bool cancelled = false)
            {
                if (quiet) return;
                hub.Post(new
                {
                type = "metaFill",
                running = active,
                done,
                total = games.Count,
                current,
                applied,
                missed,
                cancelled,
                kinds,
                });
            }

            try
            {
                foreach (GameEntry game in games)
                {
                    token.ThrowIfCancellationRequested();
                    current = game.Name;
                    Progress(true);

                    bool changed = false;
                    try
                    {
                        changed = await FillGameAsync(art, game, kinds, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                    }

                    if (changed)
                    {
                        applied++;
                        art.ApplyLocal(game);
                        post(game);
                    }
                    else
                    {
                        missed.Add(game.Name);
                    }
                    done++;
                    await Task.Delay(TimeSpan.FromSeconds(quiet ? 2.5 : 1.2), token);
                }
                current = null;
                Progress(false);
            }
            catch (OperationCanceledException)
            {
                current = null;
                Progress(false, true);
            }
            finally
            {
                running = null;
                source.Dispose();
            }
        }

        private async Task<bool> FillGameAsync(ArtService art, GameEntry game, List<string> kinds, CancellationToken token)
        {
            ArtSearchResult result = await finder.FindAsync(game, MetaText.SearchName(game.Name), hub.State.SteamGridDbKey);
            token.ThrowIfCancellationRequested();

            string nameKey = ArtService.NameKey(game.Name);
            string appId = appIdFor(game);
            bool Confident(ArtCandidate candidate) =>
                (appId != null && candidate.AppId == appId) || (nameKey.Length > 0 && ArtService.NameKey(candidate.Title) == nameKey);

            bool changed = false;
            foreach (string kind in kinds.Where(kind => Missing(game, kind)))
            {
                var options = result.Candidates
                    .Where(candidate => candidate.Kind == kind && SourceRank(candidate.Source) < 9 && Confident(candidate))
                    .OrderBy(candidate => SourceRank(candidate.Source))
                    .ThenBy(candidate => appId != null && candidate.AppId == appId ? 0 : 1)
                    .Take(3);

                foreach (ArtCandidate candidate in options)
                {
                    token.ThrowIfCancellationRequested();
                    string error;
                    try
                    {
                        error = await art.SetOverrideFromUrlAsync(game, kind, candidate.Url);
                    }
                    catch (Exception exception) when (!(exception is OperationCanceledException))
                    {
                        error = exception.Message;
                    }
                    if (error != null) continue;
                    changed = true;
                    break;
                }
            }
            return changed;
        }

        public async Task HandleIconAsync(Dictionary<string, object> message)
        {
            ArtService art = ArtService.Current;
            GameEntry game = hub.Find(message.GetString("id"));
            if (art == null || game == null) return;
            const string kind = "icon";

            string error = null;
            switch (message.GetString("action"))
            {
                case "setArt":
                    try
                    {
                        error = await art.SetOverrideFromUrlAsync(game, kind, message.GetString("url"));
                    }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                    }
                    break;
                case "setArtFile":
                {
                    using var dialog = new System.Windows.Forms.OpenFileDialog
                    {
                        Title = $"Choose icon art for {game.Name}",
                        Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp",
                    };
                    if (dialog.ShowDialog(hub.Form) != System.Windows.Forms.DialogResult.OK)
                    {
                        hub.Post(new { type = "artSet", id = game.Id, kind, cancelled = true });
                        return;
                    }
                    try
                    {
                        error = art.SetOverrideFromFile(game, kind, dialog.FileName);
                    }
                    catch (Exception exception)
                    {
                        error = exception.Message;
                    }
                    break;
                }
                case "resetArt":
                    art.ResetOverride(game, kind);
                    break;
                default:
                    return;
            }

            if (error == null)
            {
                art.ApplyLocal(game);
                PostArt(hub, art)(game);
            }
            hub.Post(new { type = "artSet", id = game.Id, kind, ok = error == null, error });
        }
    }
}
