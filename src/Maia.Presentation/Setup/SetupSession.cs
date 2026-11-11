using System.Text.Json;
using System.Text.Json.Serialization;
using Maia.Application.Abstractions;
using Maia.Domain.Common;
using Maia.Presentation.Care;

namespace Maia.Presentation.Setup;

/// <summary>The setup step she was on: the due date, the suggested plan, or her doctor and reminders.</summary>
public enum SetupStep
{
    DueDate = 1,
    Plan = 2,
    Care = 3,
}

/// <summary>
/// Choices made across the setup screens, held until the plan is created. Register as a singleton. Each choice is also
/// kept on the phone, encrypted, until the plan is made, so if Android closes Maia in the background (while she picks
/// a contact, say) she comes back to the step she was on with everything she chose.
/// </summary>
public sealed class SetupSession
{
    private const string DraftName = "setup";

    private readonly IDraftStore? _drafts;
    // Set together with _drafts: only a session that keeps drafts can fail to, so only it reports.
    private readonly IErrorReporter? _errors;
    private Task _saving = Task.CompletedTask;

    /// <summary>A session kept in memory only.</summary>
    public SetupSession()
    {
    }

    public SetupSession(IDraftStore drafts, IErrorReporter errors)
    {
        _drafts = drafts;
        _errors = errors;
    }

    public DateOnly? DueDate { get; set; }

    public Dictionary<CheckId, bool> IncludeOverrides { get; } = [];

    /// <summary>Her OB-GYN contacts, picked in step 3.</summary>
    public List<PickedContact> ObGyns { get; } = [];

    /// <summary>Whether she chose "Remind me" in step 3 (and allowed notifications).</summary>
    public bool RemindMe { get; set; }

    public void Reset()
    {
        DueDate = null;
        IncludeOverrides.Clear();
        ObGyns.Clear();
        RemindMe = false;
    }

    /// <summary>
    /// Keeps every choice so far on the phone, noting that she has reached <paramref name="step"/>. Saves run one
    /// after another, the latest last. Never fails: a draft is a convenience, so a failure is only reported.
    /// </summary>
    public Task SaveAsync(SetupStep step)
    {
        if (_drafts is null)
        {
            return Task.CompletedTask;
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(ToDocument(step), SetupDraftJsonContext.Default.SetupDraftDocument);
        return _saving = WriteAfterAsync(_saving, content);
    }

    /// <summary>Brings back the choices kept by <see cref="SaveAsync"/>, if a setup was under way when Maia was closed.</summary>
    /// <returns>The step she had reached, or null when there is nothing to bring back.</returns>
    public async Task<SetupStep?> RestoreAsync()
    {
        if (_drafts is null)
        {
            return null;
        }

        try
        {
            if (await _drafts.ReadAsync(DraftName, CancellationToken.None) is not { } content
                || JsonSerializer.Deserialize(content, SetupDraftJsonContext.Default.SetupDraftDocument) is not { Version: SetupDraftDocument.CurrentVersion } document
                || !Enum.IsDefined(document.Step))
            {
                return null;
            }

            Reset();
            DueDate = document.DueDate;
            foreach (var (id, include) in document.IncludeOverrides)
            {
                IncludeOverrides[new CheckId(id)] = include;
            }

            ObGyns.AddRange(document.ObGyns
                .Where(c => !string.IsNullOrWhiteSpace(c.DisplayName) && !string.IsNullOrWhiteSpace(c.PhoneNumber))
                .Select(c => new PickedContact(c.DisplayName, c.PhoneNumber, c.Label, c.DeviceContactId)));
            RemindMe = document.RemindMe;
            return DueDate is null ? SetupStep.DueDate : document.Step;
        }
#pragma warning disable CA1031 // A draft that can't be brought back means setup starts afresh; the reporter gets the type.
        catch (Exception ex)
        {
            _errors!.Report(ex, "setup.draft_read");
            Reset();
            return null;
        }
#pragma warning restore CA1031
    }

    /// <summary>Forgets every choice, here and on the phone: the plan is made, or she started again.</summary>
    public async Task ClearAsync()
    {
        Reset();
        if (_drafts is null)
        {
            return;
        }

        var saving = _saving;
        _saving = Task.CompletedTask;
        await saving;
        try
        {
            await _drafts.DeleteAsync(DraftName, CancellationToken.None);
        }
#pragma warning disable CA1031 // Reported; start-up deletes it again once her plan exists.
        catch (Exception ex)
        {
            _errors!.Report(ex, "setup.draft_delete");
        }
#pragma warning restore CA1031
    }

    private async Task WriteAfterAsync(Task previous, byte[] content)
    {
        await previous;
        try
        {
            await _drafts!.WriteAsync(DraftName, content, CancellationToken.None);
        }
#pragma warning disable CA1031 // A draft is a convenience: setup goes on without it.
        catch (Exception ex)
        {
            _errors!.Report(ex, "setup.draft_save");
        }
#pragma warning restore CA1031
    }

    private SetupDraftDocument ToDocument(SetupStep step) => new()
    {
        Step = step,
        DueDate = DueDate,
        IncludeOverrides = IncludeOverrides.ToDictionary(p => p.Key.Value, p => p.Value, StringComparer.Ordinal),
        ObGyns = [.. ObGyns.Select(c => new SetupContactDocument
        {
            DisplayName = c.DisplayName,
            PhoneNumber = c.PhoneNumber,
            Label = c.Label,
            DeviceContactId = c.DeviceContactId,
        })],
        RemindMe = RemindMe,
    };
}

/// <summary>What <see cref="SetupSession"/> keeps on the phone, inside the encrypted draft.</summary>
internal sealed record SetupDraftDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; init; } = CurrentVersion;

    public SetupStep Step { get; init; }

    public DateOnly? DueDate { get; init; }

    public Dictionary<string, bool> IncludeOverrides { get; init; } = [];

    public List<SetupContactDocument> ObGyns { get; init; } = [];

    /// <summary>"Remind me" in step 3. A draft from before it (with the calendar choice it replaced) reads as off.</summary>
    public bool RemindMe { get; init; }
}

internal sealed record SetupContactDocument
{
    public string DisplayName { get; init; } = string.Empty;

    public string PhoneNumber { get; init; } = string.Empty;

    public string? Label { get; init; }

    public string? DeviceContactId { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SetupDraftDocument))]
internal sealed partial class SetupDraftJsonContext : JsonSerializerContext;
