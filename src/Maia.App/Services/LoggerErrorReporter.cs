using System.Runtime.CompilerServices;
using Maia.Application.Abstractions;
using Maia.Application.Common;
using Microsoft.Extensions.Logging;

namespace Maia.App.Services;

/// <summary>
/// Where failures go: the problem log on this phone (<see cref="DiagnosticsFile"/>), in every build, and the debug log
/// while developing. Only the operation code, the exception types with their stable codes and where it was thrown, as
/// code names, are kept (see <see cref="ProblemReport"/>): exception messages and a stack trace's arguments and files
/// can name checks, and health data must never reach logs or reports. The same failure reported twice on its way out
/// (by a screen, then by the global handler) is kept once.
/// </summary>
public sealed partial class LoggerErrorReporter : IErrorReporter
{
    private static readonly object Seen = new();

    private readonly ILogger<LoggerErrorReporter> _logger;
    private readonly DiagnosticsFile _file;
    private readonly IClock _clock;
    private readonly ConditionalWeakTable<Exception, object> _reported = [];

    public LoggerErrorReporter(ILogger<LoggerErrorReporter> logger, DiagnosticsFile file, IClock clock)
    {
        _logger = logger;
        _file = file;
        _clock = clock;
    }

    public void Report(Exception exception, string operation)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!_reported.TryAdd(exception, Seen))
        {
            return;
        }

        LogFailure(_logger, ProblemReport.Code(operation), ProblemReport.Describe(exception));
        var entry = ProblemReport.Entry(_clock.UtcNow, DiagnosticsFile.AppVersion, operation, exception);

        // A crash is written at once (Maia may end straight after); anything else in the background, off the UI thread.
        if (operation.StartsWith(CrashReporting.OperationPrefix, StringComparison.Ordinal))
        {
            _file.AppendNow(entry);
        }
        else
        {
            _file.Append(entry);
        }
    }

    /// <summary>"Delete everything" removes the problem log too.</summary>
    public void Forget() => _file.Clear();

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Background operation {Operation} failed with {ExceptionType}")]
    private static partial void LogFailure(ILogger logger, string operation, string exceptionType);
}
