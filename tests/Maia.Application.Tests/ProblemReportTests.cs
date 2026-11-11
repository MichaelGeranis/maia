using System.Runtime.CompilerServices;
using Maia.Application.Common;
using Maia.Domain.Common;

namespace Maia.Application.Tests;

/// <summary>The problem log on her phone holds codes and types only: never anything from her plan.</summary>
public sealed class ProblemReportTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 1, 9, 12, 3, TimeSpan.FromHours(3));

    [Fact]
    public void A_problem_line_holds_when_the_version_the_code_and_the_type_never_the_message()
    {
        var failure = new IOException("Couldn't save Glucose test at Papadaki Clinic, scan.pdf");

        var line = ProblemReport.Entry(At, "1.0(12)", "plan.book", failure);

        Assert.Equal("2026-10-01T06:12:03Z 1.0(12) plan.book IOException", line);
    }

    [Fact]
    public void A_coded_failure_carries_its_code_and_what_caused_it()
    {
        var failure = PlanUnreadableException.KeyUnavailable(new InvalidOperationException("keystore said: Glucose"));

        var line = ProblemReport.Entry(At, "1.0(12)", "startup.load", failure);

        Assert.EndsWith("startup.load PlanUnreadableException[key.unavailable]<InvalidOperationException", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Glucose", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_domain_rule_names_its_code_not_the_check()
    {
        var failure = new DomainException("check.already_done", "Check 'nipt' is already done.");

        Assert.Equal("DomainException[check.already_done]", ProblemReport.Types(failure));
    }

    [Fact]
    public void An_operation_stays_one_short_word_so_no_sentence_can_ride_along()
    {
        var code = ProblemReport.Code("plan.book\n\"a note\" " + new string('x', 100));

        Assert.Equal(64, code.Length);
        Assert.StartsWith("plan.book__a_note__xxx", code, StringComparison.Ordinal);
        Assert.Equal("-", ProblemReport.Code(null));
    }

    [Fact]
    public void A_disposed_object_is_named_by_its_type()
    {
        var gate = new SemaphoreSlim(1, 1);
        gate.Dispose();

        var failure = Assert.Throws<ObjectDisposedException>(() => gate.Wait());

        Assert.Equal("ObjectDisposedException(SemaphoreSlim)", ProblemReport.Types(failure));
    }

    [Fact]
    public void A_disposed_object_named_in_words_leaves_the_words_out()
    {
        var failure = new ObjectDisposedException("Glucose test, scan.pdf");

        Assert.Equal("ObjectDisposedException", ProblemReport.Types(failure));
    }

    [Fact]
    public void An_io_error_keeps_its_result_code_when_it_has_a_precise_one()
    {
        Assert.Equal("IOException(0x0000001C)", ProblemReport.Types(new IOException("No space for scan.pdf", 28)));
        Assert.Equal("IOException", ProblemReport.Types(new IOException("scan.pdf")));
    }

    [Fact]
    public void A_thrown_failure_says_where_it_was_thrown_and_where_in_Maia_never_the_message()
    {
        var failure = Assert.Throws<ObjectDisposedException>(() => Throwing.Wait("Glucose test at Papadaki Clinic"));

        var line = ProblemReport.Entry(At, "1.0(12)", "results.load", failure);

        // The frame that threw is the framework's own (its name may change between versions), then the place in Maia.
        Assert.Matches(@"results\.load ObjectDisposedException\(SemaphoreSlim\)@SemaphoreSlim\.\w+/Throwing\.Wait$", line);
        Assert.DoesNotContain("Glucose", line, StringComparison.Ordinal);
        Assert.DoesNotContain("ProblemReportTests.cs", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wrapped_failure_is_placed_where_its_cause_was_thrown()
    {
        var failure = Assert.Throws<PlanUnreadableException>(Throwing.Wrapped);

        Assert.Equal("Throwing.Cause", ProblemReport.Where(failure));
    }

    [Theory]
    [InlineData("   at Maia.Infrastructure.Security.DataKeyProvider.GetKeyAsync(CancellationToken cancellationToken) in C:\\src\\DataKeyProvider.cs:line 72", "DataKeyProvider.GetKeyAsync")]
    [InlineData("  at Maia.Infrastructure.Security.DataKeyProvider+<GetKeyAsync>d__5.MoveNext () [0x000a4] in <8f2c>:0", "DataKeyProvider.GetKeyAsync")]
    [InlineData("   at Maia.Presentation.Results.ResultsViewModel.<>c__DisplayClass5_0.<LoadAsync>b__0()", "ResultsViewModel.LoadAsync")]
    [InlineData("\tat java.io.FileInputStream.open0(Native Method)", "FileInputStream.open0")]
    public void A_frame_is_read_from_every_runtime_s_form_as_type_and_method(string line, string expected)
    {
        var failure = new TracedException(line);

        Assert.Equal(expected, ProblemReport.Where(failure));
    }

    [Theory]
    [InlineData("--- End of stack trace from previous location ---")]
    [InlineData("java.io.FileNotFoundException: /data/user/0/scan.pdf: open failed")]
    [InlineData("   at Glucose test(")]
    public void A_line_that_is_not_a_frame_is_left_out(string line)
    {
        Assert.Equal(string.Empty, ProblemReport.Where(new TracedException(line)));
    }

    [Fact]
    public void A_failure_never_thrown_says_nowhere()
    {
        Assert.Equal("IOException", ProblemReport.Describe(new IOException("scan.pdf")));
    }

    [Fact]
    public void The_log_keeps_the_newest_lines()
    {
        var lines = Enumerable.Range(1, ProblemReport.MaxEntries + 50).Select(i => $"line {i}").ToList();

        var kept = ProblemReport.Keep(lines);

        Assert.Equal(ProblemReport.MaxEntries, kept.Count);
        Assert.Equal("line 51", kept[0]);
        Assert.Equal($"line {ProblemReport.MaxEntries + 50}", kept[^1]);
    }

    [Fact]
    public void A_short_log_keeps_every_line_and_drops_blank_ones()
    {
        Assert.Equal(new[] { "line 1", "line 2" }, ProblemReport.Keep(new[] { "line 1", "", "line 2" }));
    }

    [Fact]
    public void A_trace_that_cannot_be_read_only_loses_the_where()
    {
        var failure = new UnreadableTraceException();

        Assert.Equal(string.Empty, ProblemReport.Where(failure));
        Assert.Equal("UnreadableTraceException", ProblemReport.Describe(failure));
    }

    [Theory]
    [InlineData("System.Collections.Generic.List`1[[System.Int32]]", "ObjectDisposedException(List)")]
    [InlineData("Maia.Outer+Inner[]", "ObjectDisposedException(Inner)")]
    [InlineData("", "ObjectDisposedException")]
    [InlineData("Maia.Plans.", "ObjectDisposedException")]
    [InlineData("Maia.Plans.2Fast", "ObjectDisposedException")]
    public void A_disposed_object_is_named_only_by_a_type_name(string objectName, string expected)
    {
        Assert.Equal(expected, ProblemReport.Types(new ObjectDisposedException(objectName)));
    }

    [Theory]
    [InlineData("   at Maia.Application.Plans.PlanService.Rearm", "PlanService.Rearm")]
    [InlineData("   at <Module>.<StartupCode>.Main()", "_Module_.Main")]
    [InlineData("   at Maia.Application.Plans.<>m()", "Plans.__m")]
    [InlineData("   at Maia.Application.Plans.TaskList.MoveNext()", "TaskList.MoveNext")]
    public void A_frame_without_arguments_or_with_only_compiler_names_still_reads_as_type_and_method(string line, string expected)
    {
        Assert.Equal(expected, ProblemReport.Where(new TracedException(line)));
    }

    /// <summary>Failures thrown from a known place, so where they were thrown can be checked.</summary>
    private static class Throwing
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Wait(string checkTitle)
        {
            var gate = new SemaphoreSlim(1, 1);
            gate.Dispose();
            gate.Wait(TimeSpan.FromSeconds(checkTitle.Length));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Wrapped()
        {
            try
            {
                Cause();
            }
            catch (InvalidOperationException ex)
            {
                throw PlanUnreadableException.KeyUnavailable(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Cause() => throw new InvalidOperationException("keystore said: Glucose");
    }

    /// <summary>An exception with a given stack trace, as another runtime would write it.</summary>
    private sealed class TracedException(string trace) : Exception
    {
        public override string StackTrace => trace;
    }

    /// <summary>A platform exception whose own stack trace throws when read.</summary>
    private sealed class UnreadableTraceException : Exception
    {
        public override string StackTrace => throw new InvalidOperationException("No trace for Glucose test.");
    }
}
