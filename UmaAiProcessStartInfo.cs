using System.Diagnostics;
using System.Text;

namespace AIRedirector;

internal static class UmaAiProcessStartInfo
{
    public static ProcessStartInfo Create(string executablePath)
    {
        var fullPath = Path.GetFullPath(executablePath);
        var workingDirectory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException($"无法解析 UmaAI 程序所在目录: {executablePath}", nameof(executablePath));

        return new ProcessStartInfo
        {
            FileName = fullPath,
            WorkingDirectory = workingDirectory,
            Arguments = string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }
}
