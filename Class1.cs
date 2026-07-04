using System;
using System.Diagnostics;
using System.IO.Compression;
using LegendScenarioAnalyzer;
using Spectre.Console;
using UmamusumeResponseAnalyzer.Plugin;

[assembly: SharedContextWith("LegendScenarioAnalyzer")]

namespace AIRedirector
{
    public class AIRedirector : IPlugin
    {
        public string Name => "AIRedirector";
        public string Author => "离披";
        public string[] Targets => [];
        public string DataDirectory => Path.Combine("PluginData", Name);

        public async Task UpdatePlugin(ProgressContext ctx)
        {
            var progress = ctx.AddTask($"[[{Name}]] 更新");

            using var client = new HttpClient();
            using var resp = await client.GetAsync($"https://api.github.com/repos/URA-Plugins/{Name}/releases/latest");
            var jo = Newtonsoft.Json.Linq.JObject.Parse(await resp.Content.ReadAsStringAsync());

            var isLatest = ("v" + ((IPlugin)this).Version.ToString()).Equals("v" + jo["tag_name"]?.ToString());
            if (isLatest)
            {
                progress.Increment(progress.MaxValue);
                progress.StopTask();
                return;
            }
            progress.Increment(25);

            var downloadUrl = jo["assets"]![0]!["browser_download_url"]!.ToString();
            using var msg = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            using var memoryStream = new MemoryStream();
            await using var stream = await msg.Content.ReadAsStreamAsync();
            var buffer = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                    break;
                memoryStream.Write(buffer, 0, read);
                if (msg.Content.Headers.ContentLength is { } contentLength and > 0)
                    progress.Increment((double)read / contentLength * 50);
            }
            memoryStream.Position = 0;
            using var archive = new ZipArchive(memoryStream);
            archive.ExtractToDirectory(Path.Combine("Plugins", Name), true);
            progress.Increment(25);

            progress.StopTask();
        }

        readonly Dictionary<ScenarioType, Process> Processes = [];
        ChildProcessManager? _childProcessManager;
        AIRedirectorConfig config = new();
        IDisposable? startedSubscription;
        readonly LegendAiOutputBuffer legendOutput = new();
        readonly UmaAiRawOutputWorkspace rawOutput = new();
        bool gameStarted;

        string ConfigPath => Path.Combine(DataDirectory, "settings.json");

        public void Initialize(IPluginContext context)
        {
            Directory.CreateDirectory(DataDirectory);
            rawOutput.Initialize(context.LiveDisplay);
            config = AIRedirectorConfig.Load(ConfigPath);
            startedSubscription = context.Events.OnStarted(_ =>
            {
                gameStarted = true;
                return ValueTask.CompletedTask;
            });

            var sendGameStatusDataDirectory = Path.Combine("PluginData", "SendGameStatusPlugin");
            if (Directory.Exists(sendGameStatusDataDirectory))
            {
                foreach (var i in Directory.EnumerateFiles(sendGameStatusDataDirectory, "*.json", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(i) == "thisTurn.json")
                    {
                        File.WriteAllText(i, "{}");
                    }
                }
            }

            ValidateConfiguredPath("UAF", config.UAF, config.UAF_Path);
            ValidateConfiguredPath("Cook", config.Cook, config.Cook_Path);
            ValidateConfiguredPath("Mecha", config.Mecha, config.Mecha_Path);
            ValidateConfiguredPath("Legend", config.Legend, config.Legend_Path);

            if (config.UAF)
            {
                var uaf = new Process
                {
                    StartInfo = UmaAiProcessStartInfo.Create(config.UAF_Path)
                };
                uaf.OutputDataReceived += (sender, e) =>
                {
                    rawOutput.AppendLine(e.Data);
                    if (gameStarted && !string.IsNullOrEmpty(e.Data))
                    {
                        if (e.Data.Contains("运气指标") || e.Data.Contains("相谈"))
                        {
                            Console.WriteLine(e.Data);
                        }
                    }
                };
                Processes.Add(ScenarioType.UAF, uaf);
                Trace.WriteLine($"UAF Path: {config.UAF_Path}");
            }
            if (config.Cook)
            {
                var cook = new Process
                {
                    StartInfo = UmaAiProcessStartInfo.Create(config.Cook_Path)
                };
                cook.OutputDataReceived += (sender, e) =>
                {
                    rawOutput.AppendLine(e.Data);
                    if (gameStarted && !string.IsNullOrEmpty(e.Data))
                    {
                        if (!string.IsNullOrEmpty(e.Data) &&
                        e.Data.Contains("手写逻辑")
                        || e.Data.Contains("蒙特卡洛")
                        || e.Data.Contains("运气指标")
                        || (e.Data.Contains("速:") && e.Data.Contains("| 休息:"))
                        || e.Data.Contains("先做料理"))
                        {
                            Console.WriteLine(e.Data);
                        }
                    }
                };
                Processes.Add(ScenarioType.Cook, cook);
                Trace.WriteLine($"Cook Path: {config.Cook_Path}");
            }
            if (config.Mecha)
            {
                var mecha = new Process
                {
                    StartInfo = UmaAiProcessStartInfo.Create(config.Mecha_Path)
                };
                mecha.OutputDataReceived += (sender, e) =>
                {
                    rawOutput.AppendLine(e.Data);
                    if (gameStarted && !string.IsNullOrEmpty(e.Data))
                    {
                        if (!string.IsNullOrEmpty(e.Data) &&
                        e.Data.Contains("手写逻辑")
                        || e.Data.Contains("蒙特卡洛")
                        || e.Data.Contains("运气指标")
                        || (e.Data.Contains("速:") && e.Data.Contains("| 休息:"))
                        || e.Data.Contains("开启齿轮")
                        || e.Data.Contains("级胸"))
                        {
                            Console.WriteLine(e.Data);
                        }
                    }
                };
                Processes.Add(ScenarioType.Mecha, mecha);
                Trace.WriteLine($"Mecha Path: {config.Mecha_Path}");
            }
            if (config.Legend)
            {
                var legend = new Process
                {
                    StartInfo = UmaAiProcessStartInfo.Create(config.Legend_Path)
                };
                legend.OutputDataReceived += (sender, e) =>
                {
                    rawOutput.AppendLine(e.Data);
                    if (gameStarted && !string.IsNullOrEmpty(e.Data))
                    {
                        if (legendOutput.ProcessLine(e.Data))
                            legendOutput.ApplyCurrentDisplay();
                    }
                };
                Processes.Add(ScenarioType.Legend, legend);
                Trace.WriteLine($"Legend Path: {config.Legend_Path}");
            }

            _childProcessManager = new ChildProcessManager();
            foreach (var (_, process) in Processes)
            {
                process.Start();
                _childProcessManager.AddProcess(process);
                process.BeginOutputReadLine();
            }
        }

        static void ValidateConfiguredPath(string name, bool enabled, string path)
        {
            if (!enabled)
                return;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"{name} AI 已启用，但配置的程序路径不存在: {path}", path);
        }

        public Task ConfigPromptAsync()
        {
            Directory.CreateDirectory(DataDirectory);
            config = AIRedirectorConfig.Load(ConfigPath);
            ConfigureScenario("UAF", value => config.UAF = value, () => config.UAF, value => config.UAF_Path = value, () => config.UAF_Path);
            ConfigureScenario("Cook", value => config.Cook = value, () => config.Cook, value => config.Cook_Path = value, () => config.Cook_Path);
            ConfigureScenario("Mecha", value => config.Mecha = value, () => config.Mecha, value => config.Mecha_Path = value, () => config.Mecha_Path);
            ConfigureScenario("Legend", value => config.Legend = value, () => config.Legend, value => config.Legend_Path = value, () => config.Legend_Path);
            config.Save(ConfigPath);
            return Task.CompletedTask;
        }

        static void ConfigureScenario(
            string name,
            Action<bool> setEnabled,
            Func<bool> getEnabled,
            Action<string> setPath,
            Func<string> getPath)
        {
            var enabled = AnsiConsole.Confirm($"启用 {name} AI 输出转发？", getEnabled());
            setEnabled(enabled);
            if (!enabled)
                return;

            setPath(AnsiConsole.Prompt(
                new TextPrompt<string>($"{name} AI 程序路径")
                    .DefaultValue(getPath())
                    .AllowEmpty()));
        }

        public void Dispose()
        {
            startedSubscription?.Dispose();
            startedSubscription = null;
            foreach (var (_, process) in Processes)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            }
            Processes.Clear();
            _childProcessManager?.Dispose();
            _childProcessManager = null;
        }
    }
}
