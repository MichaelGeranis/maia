using System.Globalization;
using Maia.Domain.Common;

namespace Maia.Application.Common;

/// <summary>
/// What the problem log on this phone may hold, one line per failure: when it happened (UTC), Maia's version, the
/// operation code, the exception types with their stable codes, and where it was thrown, as code names only. Never an
/// exception's message, and never a stack trace's arguments, files or lines: those can name a check or a result's
/// file, and health data never reaches a log or a report. The log is shared only when she chooses to, and "Delete
/// everything" removes it.
/// </summary>
public static class ProblemReport
{
    /// <summary>How many lines the log keeps: the newest; older ones roll off.</summary>
    public const int MaxEntries = 200;

    private const int MaxCodeLength = 64;
    private const int MaxDepth = 3;

    // COR_E_IO: what an IOException carries when nothing more precise is known. Any other value is worth keeping.
    private const int GenericIOResult = unchecked((int)0x80131620);

    /// <summary>
    /// "2026-10-01T09:12:03Z 1.0(12) plan.book IOException(0x0000001C)@RandomAccess.WriteAtOffset/EncryptedPlanRepository.SaveAsync".
    /// </summary>
    public static string Entry(DateTimeOffset at, string? version, string? operation, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return string.Create(CultureInfo.InvariantCulture, $"{at.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'} {Code(version)} {Code(operation)} {Describe(exception)}");
    }

    /// <summary>The exception's <see cref="Types"/>, then "@" and <see cref="Where"/> it was thrown, when that is known.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var where = Where(exception);
        return where.Length == 0 ? Types(exception) : $"{Types(exception)}@{where}";
    }

    /// <summary>The newest <see cref="MaxEntries"/> lines, oldest first.</summary>
    public static IReadOnlyList<string> Keep(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var kept = lines.Where(l => l.Length > 0).ToList();
        return kept.Count <= MaxEntries ? kept : kept.GetRange(kept.Count - MaxEntries, MaxEntries);
    }

    /// <summary>
    /// A code as it may appear in the log: letters, digits and <c>. _ - + ( )</c> pass, anything else becomes "_",
    /// and it is cut to 64 characters, so no free text can ride along.
    /// </summary>
    public static string Code(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "-";
        }

        var kept = text.Length > MaxCodeLength ? text[..MaxCodeLength] : text;
        return string.Create(kept.Length, kept, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '+' or '(' or ')' ? c : '_';
            }
        });
    }

    /// <summary>
    /// The exception's type and up to two inner ones, each with its stable code when it has one, what was disposed for
    /// a disposed object, and the result code for an I/O error that has a precise one:
    /// "PlanUnreadableException[key.unavailable]&lt;DataKeyUnavailableException", "ObjectDisposedException(SemaphoreSlim)".
    /// </summary>
    public static string Types(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var parts = new List<string>(MaxDepth);
        for (var current = exception; current is not null && parts.Count < MaxDepth; current = current.InnerException)
        {
            var code = current switch
            {
                DomainException domain => domain.Code,
                MaiaApplicationException application => application.Code,
                _ => null,
            };
            var detail = current switch
            {
                ObjectDisposedException disposed => DisposedName(disposed.ObjectName),
                IOException when current.HResult != GenericIOResult => string.Create(CultureInfo.InvariantCulture, $"0x{current.HResult:X8}"),
                _ => null,
            };
            var part = Code(current.GetType().Name);
            part = code is null ? part : $"{part}[{Code(code)}]";
            parts.Add(detail is null ? part : $"{part}({detail})");
        }

        return string.Join('<', parts);
    }

    /// <summary>
    /// Where the failure was thrown, as code names only: the frame that threw, then the first of Maia's own frames when
    /// that is another one, "SemaphoreSlim.WaitAsync/DataKeyProvider.GetKeyAsync". Read from the innermost exception
    /// first, the one that started it. Only "Type.Method" passes, never a frame's arguments, file or line, and each
    /// name is cleaned like any <see cref="Code"/>. Empty when the exception was never thrown.
    /// </summary>
    public static string Where(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var chain = new List<Exception>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            chain.Add(current);
        }

        chain.Reverse();
        Frame? top = null;
        Frame? own = null;
        foreach (var frame in chain.SelectMany(Frames))
        {
            top ??= frame;
            if (frame.IsMaia)
            {
                own = frame;
                break;
            }
        }

        if (top is null)
        {
            return string.Empty;
        }

        return own is null || ReferenceEquals(own, top) ? top.Name : $"{top.Name}/{own.Name}";
    }

    /// <summary>
    /// The frames of one exception's own stack trace. The trace is read as text, so nothing here needs reflection
    /// metadata that a trimmed build may have removed. Lines that aren't frames ("--- End of stack trace", a Java
    /// exception's first line) are skipped.
    /// </summary>
    private static IEnumerable<Frame> Frames(Exception exception)
    {
        string? trace;
        try
        {
            trace = exception.StackTrace;
        }
#pragma warning disable CA1031 // A trace that can't be read (a platform exception's own override) only loses the "where".
        catch (Exception)
        {
            yield break;
        }
#pragma warning restore CA1031

        if (string.IsNullOrEmpty(trace))
        {
            yield break;
        }

        foreach (var line in trace.Split('\n'))
        {
            if (Frame.Parse(line) is { } frame)
            {
                yield return frame;
            }
        }
    }

    /// <summary>
    /// A disposed object's type name, its last part only ("SemaphoreSlim"), when it is a plain identifier. The name is
    /// set by the code that throws, so anything that doesn't look like a type name is left out.
    /// </summary>
    private static string? DisposedName(string? objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        // A type's full name, up to its generic arguments: "System.Threading.SemaphoreSlim", "Maia.X.Outer+Inner".
        var cut = objectName.IndexOfAny(['[', '`']);
        var full = cut < 0 ? objectName : objectName[..cut];
        if (!full.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '+'))
        {
            return null;
        }

        var name = full[(full.LastIndexOfAny(['.', '+']) + 1)..];
        return name.Length > 0 && char.IsAsciiLetter(name[0]) ? Code(name) : null;
    }

    /// <summary>One stack frame as "Type.Method", and whether it is in Maia's own code.</summary>
    private sealed class Frame
    {
        private Frame(string name, bool isMaia)
        {
            Name = name;
            IsMaia = isMaia;
        }

        public string Name { get; }

        public bool IsMaia { get; }

        /// <summary>
        /// Reads a frame line in .NET's form ("at Maia.X.Type.Method(Args) in path:line 3"), Mono's ("at Maia.X.Type.Method
        /// (Args) [0x0001] in &lt;id&gt;:0") or Java's ("at java.io.File.open(File.java:12)"): everything from the first
        /// bracket or space after the name is dropped. Compiler-made names are read back to their method
        /// ("&lt;LoadAsync&gt;d__5.MoveNext" and "&lt;&gt;c.&lt;LoadAsync&gt;b__0" are LoadAsync). A name of fewer than three parts
        /// (namespace, type, method) isn't taken for a frame.
        /// </summary>
        public static Frame? Parse(string line)
        {
            var text = line.Trim();
            if (!text.StartsWith("at ", StringComparison.Ordinal))
            {
                return null;
            }

            text = text[3..].TrimStart();
            var cut = text.IndexOfAny(['(', '[', ' ']);
            var full = cut < 0 ? text : text[..cut];
            var parts = full.Split(['.', '+', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return null;
            }

            var end = parts.Length - 1;
            var method = parts[end];
            if (method == "MoveNext" && parts[end - 1].StartsWith('<'))
            {
                method = parts[--end];
            }

            // Past the compiler's own types between the method and the type it belongs to (<>c, <>c__DisplayClass5_0).
            var type = end - 1;
            while (type > 0 && parts[type].StartsWith('<'))
            {
                type--;
            }

            return new Frame($"{Code(parts[type])}.{Code(Unmangle(method))}", full.StartsWith("Maia.", StringComparison.Ordinal));
        }

        /// <summary>"&lt;LoadAsync&gt;b__0" is LoadAsync; any other name stays as it is.</summary>
        private static string Unmangle(string name)
        {
            var close = name.IndexOf('>', StringComparison.Ordinal);
            return name.StartsWith('<') && close > 1 ? name[1..close] : name;
        }
    }
}
