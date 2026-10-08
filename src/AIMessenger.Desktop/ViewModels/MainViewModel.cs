using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIMessenger.Desktop.Models;
using AIMessenger.Desktop.Services.Agent;
using AIMessenger.Desktop.Services.Ai;
using AIMessenger.Desktop.Services.Storage;

namespace AIMessenger.Desktop.ViewModels;

public partial class MainViewModel(
    AgentRuntime runtime,
    WorkspaceStore store,
    OpenClawBridge openClaw) : ObservableObject
{
    public ObservableCollection<ChatMessage> Messages { get; } = [];
    public ObservableCollection<AgentDefinition> Agents { get; } =
        [.. AgentCatalog.Defaults];

    public ObservableCollection<ToolApproval> Approvals { get; } = [];

    [ObservableProperty]
    private AgentDefinition? selectedAgent = AgentCatalog.Defaults.First();

    [ObservableProperty]
    private string inputText = string.Empty;

    [ObservableProperty]
    private string statusText = "Initializing...";

    [ObservableProperty]
    private bool isBusy;

    public bool OpenClawAvailable => openClaw.IsAvailable();

    public async Task InitializeAsync()
    {
        await store.InitializeAsync();

        Messages.Clear();

        foreach (var message in await store.LoadRecentAsync())
            Messages.Add(message);

        await RefreshApprovalsAsync();

        StatusText =
            $"Ready • {SelectedAgent?.Name ?? "No agent"}" +
            (OpenClawAvailable ? " • OpenClaw online" : " • OpenClaw unavailable");

        OnPropertyChanged(nameof(OpenClawAvailable));
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsBusy ||
            SelectedAgent is null ||
            string.IsNullOrWhiteSpace(InputText))
        {
            return;
        }

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

            await RefreshApprovalsAsync();

            StatusText =
                $"Ready • {SelectedAgent.Name}";
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

    [RelayCommand]
    private async Task RefreshApprovalsAsync()
    {
        Approvals.Clear();

        foreach (var approval in
                 await store.LoadPendingApprovalsAsync())
        {
            Approvals.Add(approval);
        }

        StatusText =
            $"Ready • pending approvals: {Approvals.Count}";
    }

    [RelayCommand]
    private async Task ApproveAsync(ToolApproval? approval)
    {
        if (approval is null || IsBusy)
            return;

        IsBusy = true;
        StatusText = $"Executing approval #{approval.Id}";

        try
        {
            var result = await runtime.ApproveAsync(
                approval.Id);

            var message = new ChatMessage(
                0,
                "system",
                $"Approval #{approval.Id} executed:{Environment.NewLine}{result}",
                DateTimeOffset.Now);

            Messages.Add(message);
            await store.SaveMessageAsync(message);
        }
        catch (Exception ex)
        {
            var error = new ChatMessage(
                0,
                "system",
                $"Approval #{approval.Id} failed: {ex.Message}",
                DateTimeOffset.Now);

            Messages.Add(error);
            await store.SaveMessageAsync(error);
        }
        finally
        {
            IsBusy = false;
            await RefreshApprovalsAsync();
        }
    }

    [RelayCommand]
    private async Task DenyAsync(ToolApproval? approval)
    {
        if (approval is null || IsBusy)
            return;

        var denied = await runtime.DenyAsync(approval.Id);

        var message = new ChatMessage(
            0,
            "system",
            denied
                ? $"Approval #{approval.Id} denied."
                : $"Approval #{approval.Id} was already resolved.",
            DateTimeOffset.Now);

        Messages.Add(message);
        await store.SaveMessageAsync(message);

        await RefreshApprovalsAsync();
    }
}
