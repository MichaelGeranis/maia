using Maia.Domain.Care;
using Maia.Domain.Common;

namespace Maia.Application.Plans;

/// <summary>Input for creating a plan from the setup flow.</summary>
/// <param name="DueDate">The due date, already converted from the last period if needed.</param>
/// <param name="CountryCode">Selects the plan template, for example <c>GR</c>.</param>
/// <param name="Language">UI language; content falls back to English.</param>
/// <param name="IncludeOverrides">The switches the parent changed in the suggested plan.</param>
public sealed record CreatePlanRequest(
    DateOnly DueDate,
    string CountryCode,
    string Language,
    IReadOnlyDictionary<CheckId, bool>? IncludeOverrides = null);

/// <summary>A file the parent picked or photographed.</summary>
public sealed record FileToAttach(string FileName, string MediaType, ReadOnlyMemory<byte> Content);

/// <summary>A contact chosen in the phone's contact picker.</summary>
public sealed record ContactToLink(CareRole Role, string DisplayName, string PhoneNumber, string? Label, string? DeviceContactId);
