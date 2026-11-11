using Maia.Application.Plans;

namespace Maia.Presentation.Navigation;

/// <summary>
/// The group Your tasks should open at, chosen on Your week ("12 from earlier", "Later · 177"…). Register as a
/// singleton; Your tasks takes the request once.
/// </summary>
public sealed class TasksFocus
{
    private TaskGroup? _pending;

    public void Request(TaskGroup group) => _pending = group;

    public TaskGroup? Take()
    {
        var group = _pending;
        _pending = null;
        return group;
    }
}
