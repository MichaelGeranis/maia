using Maia.Application.Abstractions;

namespace Maia.Infrastructure;

/// <summary>The real clock, in the phone's current time zone.</summary>
public sealed class SystemClock : IClock
{
    private readonly TimeProvider _time;

    public SystemClock(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    public DateTimeOffset UtcNow => _time.GetUtcNow();

    public TimeZoneInfo TimeZone => _time.LocalTimeZone;
}
