using Maia.Domain.Common;

namespace Maia.Presentation.Navigation;

/// <summary>
/// The check Journey should open on (scrolled to, left closed) next time it loads, for example the entry Add has just saved.
/// Register as a singleton; the plan screen takes the request once.
/// </summary>
public sealed class PlanFocus
{
    private CheckId? _pending;

    /// <summary>Whether a check is waiting to be opened: Journey then opens on it rather than on today.</summary>
    public bool IsPending => _pending is not null;

    public void Request(CheckId check) => _pending = check;

    public CheckId? Take()
    {
        var check = _pending;
        _pending = null;
        return check;
    }
}
