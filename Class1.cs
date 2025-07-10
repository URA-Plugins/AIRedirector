using Newtonsoft.Json.Linq;
using Spectre.Console;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using UmamusumeResponseAnalyzer;
using UmamusumeResponseAnalyzer.Plugin;

namespace AIRedirector
{
    public class AIRedirector : IPlugin
    {
        public Version Version => new(1, 0, 0);
        [PluginDescription("转发AI的输出到控制台")]
        public string Name => "AIRedirector";
        public string Author => "离披";
        public string[] Targets => [];

        public async Task UpdatePlugin(ProgressContext ctx)
        {
            var progress = ctx.AddTask($"[{Name}] 更新");

            using var client = new HttpClient();
            using var resp = await client.GetAsync($"https://api.github.com/repos/URA-Plugins/{Name}/releases/latest");
            var json = await resp.Content.ReadAsStringAsync();
            var jo = JObject.Parse(json);

            var isLatest = ("v" + Version.ToString()).Equals("v" + jo["tag_name"]?.ToString());
            if (isLatest)
            {
                progress.Increment(progress.MaxValue);
                progress.StopTask();
                return;
            }
            progress.Increment(25);

            var downloadUrl = jo["assets"][0]["browser_download_url"].ToString();
            if (Config.Updater.IsGithubBlocked && !Config.Updater.ForceUseGithubToUpdate)
            {
                downloadUrl = downloadUrl.Replace("https://", "https://gh.shuise.dev/");
            }
            using var msg = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            using var stream = await msg.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                    break;
                progress.Increment(read / msg.Content.Headers.ContentLength ?? 1 * 0.5);
            }
            using var archive = new ZipArchive(stream);
            archive.ExtractToDirectory(Path.Combine("Plugins", Name), true);
            progress.Increment(25);

            progress.StopTask();
        }
        [PluginSetting]
        public bool UAF { get; set; } = false;
        [PluginSetting]
        public string UAF_Path { get; set; } = string.Empty;
        [PluginSetting]
        public bool Legend { get; set; } = false;
        [PluginSetting]
        public string Legend_Path { get; set; } = string.Empty;

        readonly Dictionary<ScenarioType, Process> Processes = [];

        public void Initialize()
        {
            if (Directory.Exists("GameData"))
            {
                foreach (var i in Directory.EnumerateFiles("GameData")) File.Delete(i);
            }
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (UAF && File.Exists(UAF_Path))
            {
                var uaf = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = UAF_Path,
                        Arguments = string.Empty,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.GetEncoding("gbk")
                    }
                };
                uaf.OutputDataReceived += (sender, e) =>
                {
                    if (UmamusumeResponseAnalyzer.UmamusumeResponseAnalyzer.Started && !string.IsNullOrEmpty(e.Data))
                    {
                        if (e.Data.Contains("运气指标") || e.Data.Contains("相谈"))
                        {
                            Console.WriteLine(e.Data);
                        }
                    }
                };
                Processes.Add(ScenarioType.UAF, uaf);
                Trace.WriteLine($"UAF Path: {UAF_Path}");
            }
            if (Legend && File.Exists(Legend_Path))
            {
                var legend = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Legend_Path,
                        Arguments = string.Empty,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.GetEncoding("gbk")
                    }
                };
                legend.OutputDataReceived += (sender, e) =>
                {
                    if (UmamusumeResponseAnalyzer.UmamusumeResponseAnalyzer.Started && !string.IsNullOrEmpty(e.Data))
                    {
                        Console.WriteLine(e.Data);
                    }
                };
                Processes.Add(ScenarioType.Legend, legend);
                Trace.WriteLine($"Legend Path: {Legend_Path}");
            }

            foreach (var (_, process) in Processes)
            {
                process.Start();
                process.BeginOutputReadLine();
            }
        }

        public void Dispose()
        {
            foreach (var (_, process) in Processes)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            }
            Processes.Clear();
        }
    }
}
