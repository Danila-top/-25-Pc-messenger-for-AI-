using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIMessenger.Desktop.Models;
using AIMessenger.Desktop.Services.Agent;
using AIMessenger.Desktop.Services.Storage;

namespace AIMessenger.Desktop.ViewModels;

public partial class MainViewModel(
    AgentRuntime runtime,
    WorkspaceStore store) : ObservableObject
{
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<AgentDefinition> Agents { get; } = [.. AgentCatalog.Defaults];

    [ObservableProperty]
    private AgentDefinition? selectedAgent = AgentCatalog.Defaults.First();

    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private string statusText = "Initializing...";

    [ObservableProperty]
    private bool isBusy;

    public async Task InitializeAsync()
    {
        await store.InitializeAsync();
        Messages.Clear();

        foreach (var message in await store.LoadRecentAsync())
            Messages.Add(message);

        StatusText = $"Ready • {SelectedAgent?.Name ?? "No agent"}";
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsBusy || SelectedAgent is null || string.IsNullOrWhiteSpace(InputText))
            return;

        var text = InputText.Trim();
        InputText = string.Empty;
        IsBusy = true;
        StatusText = $"Working • {SelectedAgent.Name}";

        var user = new ChatMessage(
            0,
            "user",
            text,
            DateTimeOffset.Now);

        Messages.Add(user);
        await store.SaveMessageAsync(user);

        try
        {
            var history = Messages.ToArray();
            var answer = await runtime.RunAsync(
                SelectedAgent,
                history,
                text);

            var assistant = new ChatMessage(
                0,
                "assistant",
                answer,
                DateTimeOffset.Now,
                SelectedAgent.Id);

            Messages.Add(assistant);
            await store.SaveMessageAsync(assistant);
            StatusText = $"Ready • {SelectedAgent.Name}";
        }
        catch (Exception ex)
        {
            var error = new ChatMessage(
                0,
                "system",
                ex.Message,
                DateTimeOffset.Now);

            Messages.Add(error);
            StatusText = "Error";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
