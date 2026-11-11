using Maia.Application.Abstractions;

namespace Maia.Presentation.Common;

/// <summary>
/// Runs one plan change from a screen: failures are reported by operation code (never health data) and turned
/// into a calm, plain message for the screen to show. A failure with a known code says what happened ("This check is
/// already marked done."); anything else gets the general "Something went wrong" (see <see cref="ErrorMessages"/>).
/// </summary>
public static class PlanChange
{
    /// <returns>Null on success, otherwise the message to show.</returns>
    public static async Task<string?> RunAsync(Func<Task> change, string operation, IErrorReporter errors)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(errors);
        try
        {
            await change();
            return null;
        }
#pragma warning disable CA1031 // Any failure is shown to the parent; details go to the error reporter.
        catch (Exception ex)
        {
            errors.Report(ex, operation);
            return ErrorMessages.For(ex);
        }
#pragma warning restore CA1031
    }
}
