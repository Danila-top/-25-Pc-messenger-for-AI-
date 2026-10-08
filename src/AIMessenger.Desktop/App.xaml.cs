using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AIMessenger.Desktop.Services.Ai;
using AIMessenger.Desktop.Services.Agent;
using AIMessenger.Desktop.Services.Automation;
using AIMessenger.Desktop.Services.Storage;
using AIMessenger.Desktop.Services.Tools;
using AIMessenger.Desktop.ViewModels;

namespace AIMessenger.Desktop;

public partial class App : Application
{
    public static IHost Host { get; } =
        Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(new System.Net.Http.HttpClient());

                services.AddSingleton<OpenAiCompatibleProvider>();
                services.AddSingleton<GeminiInteractionsProvider>();
                services.AddSingleton<AnthropicMessagesProvider>();
                services.AddSingleton<OpenClawBridge>();
                services.AddSingleton<IAiProvider, AiProviderRouter>();

                services.AddSingleton<SecretStore>();
                services.AddSingleton<FlaUiAutomationService>();
                services.AddSingleton<ToolRegistry>();
                services.AddSingleton<WorkspaceStore>();
                services.AddSingleton<AgentRuntime>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

    private async void Application_Startup(
        object sender,
        StartupEventArgs e)
    {
        await Host.StartAsync();

        var window = Host.Services.GetRequiredService<MainWindow>();

        await window.InitializeAsync();

        window.Show();
    }

    private async void Application_Exit(
        object sender,
        ExitEventArgs e)
    {
        if (Host is IAsyncDisposable asyncHost)
            await asyncHost.DisposeAsync();
        else
            Host.Dispose();
    }
}
