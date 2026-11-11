using Maia.Application.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Settings;

namespace Maia.App.Services;

/// <summary>
/// The problem log on this phone, in Maia's private data folder: one line per failure (see <see cref="ProblemReport"/>:
/// when, Maia's version, the operation code and the exception types, never anything from her plan), the newest 200
/// kept. It is written in Release builds too, so a problem on her phone leaves a trace. It leaves the phone only when
/// she taps "Share diagnostics" in Settings and chooses where it goes in the share sheet, and "Delete everything"
/// removes it.
/// <para>A line is written in the background: reports come from screens on the UI thread, and the log is read and
/// written whole. A crash is written at once, with any lines still waiting, since Maia may end straight after.</para>
/// </summary>
public sealed class DiagnosticsFile : IDiagnostics
{
    private const string FileName = "problems.log";
    private const string ShareName = "maia-diagnostics.txt";

    private readonly string _path;

    // Guards the file. Lines waiting to be written have their own lock, so adding one never waits for the disk.
    private readonly Lock _gate = new();
    private readonly Lock _waitingGate = new();
    private readonly List<string> _waiting = [];
    private bool _writing;

    /// <param name="directory">Maia's private data folder for the log.</param>
    public DiagnosticsFile(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _path = Path.Combine(directory, FileName);
    }

    /// <summary>"1.0(12)": the version each line is stamped with.</summary>
    public static string AppVersion => $"{AppInfo.Current.VersionString}({AppInfo.Current.BuildString})";

    /// <summary>"Maia 1.0 (12) · Android 14": the first line of what is shared.</summary>
    private static string Header =>
        $"Maia {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString}) · {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}";

    private static string ShareFolder => Path.Combine(FileSystem.CacheDirectory, "diagnostics");

    /// <summary>
    /// Adds a line in the background, dropping the oldest past 200. Never throws: this is where failures go, so one here
    /// has nowhere else to go.
    /// </summary>
    public void Append(string entry)
    {
        lock (_waitingGate)
        {
            _waiting.Add(entry);
            if (_writing)
            {
                return;
            }

            _writing = true;
        }

        _ = Task.Run(WriteWaiting);
    }

    /// <summary>Adds a line now, with any still waiting: for a crash, after which Maia may end. Never throws.</summary>
    public void AppendNow(string entry)
    {
        lock (_waitingGate)
        {
            _waiting.Add(entry);
        }

        lock (_gate)
        {
            Write(TakeWaiting());
        }
    }

    /// <summary>Removes the log, and any line still waiting, as part of "Delete everything". Never throws.</summary>
    public void Clear()
    {
        _ = TakeWaiting();
        lock (_gate)
        {
            try
            {
                File.Delete(_path);
                File.Delete(_path + ".tmp");
                if (Directory.Exists(ShareFolder))
                {
                    Directory.Delete(ShareFolder, recursive: true);
                }
            }
#pragma warning disable CA1031 // Best effort; the log holds codes only.
            catch (Exception)
            {
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>The log, with any line still waiting written first.</summary>
    public Task<DiagnosticsReport> ReadAsync() =>
        Task.Run(() =>
        {
            lock (_gate)
            {
                Write(TakeWaiting());
                return new DiagnosticsReport(Header, ReadLines());
            }
        });

    /// <summary>Writes the report into a private cache file and opens the share sheet for it.</summary>
    public async Task ShareAsync(DiagnosticsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var folder = ShareFolder;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, ShareName);
        await File.WriteAllLinesAsync(path, [report.Header, .. report.Entries]);
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = Text.Get("Diagnostics_ShareTitle"),
            File = new ShareFile(path, "text/plain"),
        }));
    }

    /// <summary>Writes the lines waiting until none are left.</summary>
    private void WriteWaiting()
    {
        while (true)
        {
            string[] lines;
            lock (_waitingGate)
            {
                if (_waiting.Count == 0)
                {
                    _writing = false;
                    return;
                }

                lines = [.. _waiting];
                _waiting.Clear();
            }

            lock (_gate)
            {
                Write(lines);
            }
        }
    }

    private string[] TakeWaiting()
    {
        lock (_waitingGate)
        {
            string[] lines = [.. _waiting];
            _waiting.Clear();
            return lines;
        }
    }

    /// <summary>Adds the lines to the log, dropping the oldest past 200. Call under the file lock. Never throws.</summary>
    private void Write(string[] lines)
    {
        if (lines.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllLines(temp, ProblemReport.Keep([.. ReadLines(), .. lines]));
            File.Move(temp, _path, overwrite: true);
        }
#pragma warning disable CA1031 // A problem log that can't be written loses a line; it must never cause a failure of its own.
        catch (Exception)
        {
        }
#pragma warning restore CA1031
    }

    private string[] ReadLines() => File.Exists(_path) ? File.ReadAllLines(_path) : [];
}
