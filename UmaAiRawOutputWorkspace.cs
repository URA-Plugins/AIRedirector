using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Rendering;
using UmamusumeResponseAnalyzer;
using UmamusumeResponseAnalyzer.LiveDisplay;

namespace AIRedirector;

internal sealed class UmaAiRawOutputWorkspace
{
    const string WorkspaceTitle = "AIRedirector";
    const string PanelKey = "umaai-raw-output";
    const string PanelTitle = "UmaAI.exe 原始输出";
    internal const int MaxLines = 300;
    const int FallbackVisibleLineCount = 12;
    static readonly Regex TerminalControlSequencePattern = new(
        @"\x1B\][^\a]*(?:\a|\x1B\\)|\x1B\[[0-?]*[ -/]*[@-~]|\x1B[@-Z\\-_]",
        RegexOptions.Compiled);

    readonly object gate = new();
    readonly List<string> lines = [];
    ILiveDisplayOutput? liveDisplay;
    LiveDisplayWorkspace? workspace;
    int scrollOffsetFromBottom;
    int lastVisibleLineCount = FallbackVisibleLineCount;

    public void Initialize(ILiveDisplayOutput output)
    {
        liveDisplay = output;
        workspace = output.CreateWorkspace(WorkspaceTitle);
        RegisterScrollHotkeys();
    }

    public void AppendLine(string? line)
    {
        if (line is null || liveDisplay is not { } output || workspace is not { } target)
            return;

        lock (gate)
        {
            if (scrollOffsetFromBottom > 0)
                scrollOffsetFromBottom++;

            lines.Add(line);
            if (lines.Count > MaxLines)
                lines.RemoveRange(0, lines.Count - MaxLines);
            ClampScrollOffset();

            output.SetPanel(target, PanelKey, PanelTitle, Render(), fullBleed: true, switchToWorkspace: false);
        }
    }

    IRenderable Render()
        => new RawOutputRenderable(
            lines.ToArray(),
            scrollOffsetFromBottom,
            visibleLineCount => lastVisibleLineCount = visibleLineCount);

    void RegisterScrollHotkeys()
    {
        KeyboardManager.Register(ConsoleKey.UpArrow, "AIRedirector 原始输出上滚一行", () => ScrollBy(1));
        KeyboardManager.Register(ConsoleKey.DownArrow, "AIRedirector 原始输出下滚一行", () => ScrollBy(-1));
        KeyboardManager.Register(ConsoleKey.PageUp, "AIRedirector 原始输出上翻一页", () => ScrollBy(lastVisibleLineCount));
        KeyboardManager.Register(ConsoleKey.PageDown, "AIRedirector 原始输出下翻一页", () => ScrollBy(-lastVisibleLineCount));
        KeyboardManager.Register(ConsoleKey.Home, "AIRedirector 原始输出跳到最早", ScrollToTop);
        KeyboardManager.Register(ConsoleKey.End, "AIRedirector 原始输出跳到最新", ScrollToBottom);
    }

    Task ScrollBy(int delta)
    {
        lock (gate)
        {
            if (!IsActiveWorkspace() || liveDisplay is not { } output || workspace is not { } target)
                return Task.CompletedTask;

            scrollOffsetFromBottom += delta;
            ClampScrollOffset();
            output.SetPanel(target, PanelKey, PanelTitle, Render(), fullBleed: true, switchToWorkspace: false);
            return Task.CompletedTask;
        }
    }

    Task ScrollToTop()
    {
        lock (gate)
        {
            if (!IsActiveWorkspace() || liveDisplay is not { } output || workspace is not { } target)
                return Task.CompletedTask;

            scrollOffsetFromBottom = MaxScrollOffset();
            output.SetPanel(target, PanelKey, PanelTitle, Render(), fullBleed: true, switchToWorkspace: false);
            return Task.CompletedTask;
        }
    }

    Task ScrollToBottom()
    {
        lock (gate)
        {
            if (!IsActiveWorkspace() || liveDisplay is not { } output || workspace is not { } target)
                return Task.CompletedTask;

            scrollOffsetFromBottom = 0;
            output.SetPanel(target, PanelKey, PanelTitle, Render(), fullBleed: true, switchToWorkspace: false);
            return Task.CompletedTask;
        }
    }

    bool IsActiveWorkspace()
        => liveDisplay?.CurrentWorkspace == workspace;

    void ClampScrollOffset()
        => scrollOffsetFromBottom = Math.Clamp(scrollOffsetFromBottom, 0, MaxScrollOffset());

    int MaxScrollOffset()
        => Math.Max(0, lines.Count - Math.Max(1, lastVisibleLineCount));

    static string SanitizeForLiveDisplay(string line)
    {
        var withoutTerminalSequences = TerminalControlSequencePattern.Replace(line, string.Empty);
        var text = new StringBuilder(withoutTerminalSequences.Length);
        foreach (var ch in withoutTerminalSequences)
        {
            if (ch == '\t')
            {
                text.Append("    ");
                continue;
            }

            if (!char.IsControl(ch))
                text.Append(ch);
        }

        return text.ToString();
    }

    static string TrimToCellWidth(string value, int maxWidth)
    {
        if (maxWidth <= 0 || value.Length == 0)
            return string.Empty;

        if (value.GetCellWidth() <= maxWidth)
            return value;

        var suffix = maxWidth >= 3 ? "..." : string.Empty;
        var contentWidth = maxWidth - suffix.GetCellWidth();
        var text = new StringBuilder(value.Length);
        var width = 0;
        foreach (var element in EnumerateTextElements(value))
        {
            var elementWidth = element.GetCellWidth();
            if (width + elementWidth > contentWidth)
                break;

            text.Append(element);
            width += elementWidth;
        }

        text.Append(suffix);
        return text.ToString();
    }

    static IEnumerable<string> EnumerateTextElements(string value)
    {
        var indexes = StringInfo.ParseCombiningCharacters(value);
        for (var i = 0; i < indexes.Length; i++)
        {
            var start = indexes[i];
            var end = i + 1 < indexes.Length ? indexes[i + 1] : value.Length;
            yield return value[start..end];
        }
    }

    sealed class RawOutputRenderable(
        string[] sourceLines,
        int scrollOffsetFromBottom,
        Action<int> visibleLineCountChanged) : IRenderable
    {
        public Measurement Measure(RenderOptions options, int maxWidth)
            => ((IRenderable)new Text(string.Empty)).Measure(options, maxWidth);

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            if (maxWidth <= 0)
                yield break;

            var lineLimit = ResolveLineLimit(options);
            visibleLineCountChanged(lineLimit);
            var end = Math.Max(0, sourceLines.Length - Math.Max(0, scrollOffsetFromBottom));
            var start = Math.Max(0, end - lineLimit);
            var visibleLines = sourceLines
                .Skip(start)
                .Take(end - start)
                .Select(SanitizeForLiveDisplay)
                .Select(line => TrimToCellWidth(line, maxWidth))
                .ToArray();

            for (var i = 0; i < visibleLines.Length; i++)
            {
                if (i > 0)
                    yield return Segment.LineBreak;

                yield return new Segment(visibleLines[i]);
            }
        }

        static int ResolveLineLimit(RenderOptions options)
        {
            var height = options.Height is > 0
                ? options.Height.Value
                : options.ConsoleSize.Height;
            if (height <= 0)
                return FallbackVisibleLineCount;

            return Math.Max(1, height);
        }
    }

}
