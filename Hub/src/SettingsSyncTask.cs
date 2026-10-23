using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace GameHub
{
    internal static class SettingsSyncTask
    {
        public const string TaskName = "Game Shortcut Sync";
        public const string LogonOnly = "logon";
        public static readonly string[] Intervals = { "PT15M", "PT30M", "PT1H", "PT3H", "PT6H", LogonOnly };

        private static readonly XNamespace Task = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        public static string ReadInterval()
        {
            XDocument document = Export();
            return document == null ? null : ReadInterval(document);
        }

        public static string ReadInterval(XDocument document)
        {
            XElement trigger = document.Descendants(Task + "TimeTrigger").FirstOrDefault();
            if (trigger == null) return LogonOnly;
            string enabled = (string)trigger.Element(Task + "Enabled");
            if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase)) return LogonOnly;
            string interval = (string)trigger.Element(Task + "Repetition")?.Element(Task + "Interval");
            return string.IsNullOrEmpty(interval) ? LogonOnly : interval;
        }

        public static XDocument WithInterval(XDocument source, string interval)
        {
            if (!Intervals.Contains(interval)) throw new ArgumentException("Unknown interval " + interval);
            var document = new XDocument(source);
            XElement triggers = document.Descendants(Task + "Triggers").FirstOrDefault();
            if (triggers == null)
            {
                XElement root = document.Root ?? throw new InvalidOperationException("The task XML has no root element.");
                triggers = new XElement(Task + "Triggers");
                root.AddFirst(triggers);
            }

            XElement trigger = triggers.Elements(Task + "TimeTrigger").FirstOrDefault();
            if (interval == LogonOnly)
            {
                if (trigger != null) SetEnabled(trigger, false);
                return document;
            }

            if (trigger == null)
            {
                trigger = new XElement(Task + "TimeTrigger",
                    new XElement(Task + "Repetition",
                        new XElement(Task + "Interval", interval),
                        new XElement(Task + "StopAtDurationEnd", "false")),
                    new XElement(Task + "StartBoundary", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XElement(Task + "Enabled", "true"));
                triggers.Add(trigger);
                return document;
            }

            XElement repetition = trigger.Element(Task + "Repetition");
            if (repetition == null)
            {
                repetition = new XElement(Task + "Repetition", new XElement(Task + "StopAtDurationEnd", "false"));
                trigger.AddFirst(repetition);
            }

            XElement intervalElement = repetition.Element(Task + "Interval");
            if (intervalElement == null) repetition.AddFirst(new XElement(Task + "Interval", interval));
            else intervalElement.Value = interval;

            XElement duration = repetition.Element(Task + "Duration");
            if (duration != null && Parse(duration.Value) < Parse(interval)) duration.Remove();
            SetEnabled(trigger, true);
            return document;
        }

        public static async Task<string> ApplyAsync(string interval)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                XDocument current = Export() ?? throw new InvalidOperationException("The scheduled task \"" + TaskName + "\" was not found. Run the sync script with -Install first.");
                XDocument updated = WithInterval(current, interval);
                string file = Path.Combine(Path.GetTempPath(), "GameHubSyncTask.xml");
                try
                {
                    Save(updated, file);
                    var result = Run("/Create", "/TN", TaskName, "/XML", file, "/F");
                    if (result.ExitCode != 0) throw new InvalidOperationException((result.Error + " " + result.Output).Trim());
                }
                finally
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }
                return ReadInterval();
            });
        }

        public static void Save(XDocument document, string path)
        {
            var settings = new XmlWriterSettings { Encoding = new UnicodeEncoding(false, true), Indent = true };
            using var writer = XmlWriter.Create(path, settings);
            document.Declaration = new XDeclaration("1.0", "UTF-16", null);
            document.Save(writer);
        }

        private static XDocument Export()
        {
            var result = Run("/Query", "/TN", TaskName, "/XML");
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output)) return null;
            try
            {
                return XDocument.Parse(result.Output.Trim());
            }
            catch
            {
                return null;
            }
        }

        private static void SetEnabled(XElement trigger, bool enabled)
        {
            XElement element = trigger.Element(Task + "Enabled");
            if (element != null)
            {
                element.Value = enabled ? "true" : "false";
                return;
            }
            XElement anchor = trigger.Element(Task + "EndBoundary") ?? trigger.Element(Task + "StartBoundary");
            var created = new XElement(Task + "Enabled", enabled ? "true" : "false");
            if (anchor != null) anchor.AddAfterSelf(created);
            else trigger.Add(created);
        }

        private static TimeSpan Parse(string duration)
        {
            try
            {
                return XmlConvert.ToTimeSpan(duration);
            }
            catch
            {
                return TimeSpan.MaxValue;
            }
        }

        private static (int ExitCode, string Output, string Error) Run(params string[] arguments)
        {
            var start = new ProcessStartInfo("schtasks.exe", string.Join(" ", arguments.Select(Quote)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using Process process = Process.Start(start);
            System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            return (process.HasExited ? process.ExitCode : -1, output.Result, error);
        }

        private static string Quote(string argument) => argument.StartsWith("/") ? argument : "\"" + argument.Replace("\"", "\\\"") + "\"";
    }
}
