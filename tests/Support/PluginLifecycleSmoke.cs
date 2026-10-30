using UmamusumeResponseAnalyzer.Plugin;
using UmamusumeResponseAnalyzer.TerminalGui;

static class PluginLifecycleSmoke
{
    public static void Run(WorkspaceSmokeSession ui)
    {
        using var directory = new TempCurrentDirectory("AIRedirector-unused-lifecycle");
        using var context = new RuntimePluginContext(ui.Application);
        ui.Bootstrap.SwitchTo();
        IPlugin plugin = new AIRedirector.AIRedirector();
        try
        {
            plugin.Initialize(context);
            ui.Flush();
            if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
                throw new InvalidOperationException("Initialize changed the active workspace.");
        }
        finally
        {
            plugin.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        ui.Flush();
        if (!ReferenceEquals(Workspace.Current, ui.Bootstrap))
            throw new InvalidOperationException("Unused Dispose changed the active workspace.");
        Console.WriteLine("PASS AIRedirector unused workspace lifecycle");
    }
}
