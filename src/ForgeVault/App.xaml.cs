using ForgeVault.Mcp;
using System.Windows;

namespace ForgeVault;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length >= 2 && e.Args[0].Equals("--mcp", StringComparison.OrdinalIgnoreCase))
        {
            await McpHost.RunAsync(e.Args[1]);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
