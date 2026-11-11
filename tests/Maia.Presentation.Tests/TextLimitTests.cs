using Maia.Domain.Plans;
using Maia.Presentation.Common;
using Maia.Presentation.Localization;
using Maia.Presentation.Week;

namespace Maia.Presentation.Tests;

/// <summary>A field with a limit says, calmly, how much room is left once she nears it, instead of dropping letters without a word.</summary>
public sealed class TextLimitTests
{
    [Fact]
    public void Nothing_shows_while_there_is_room_then_the_letters_left_then_that_it_is_full()
    {
        Assert.Null(TextLimit.Hint(new string('a', 179), 200));
        Assert.Equal("20 characters left", TextLimit.Hint(new string('a', 180), 200));
        Assert.Equal("1 character left", TextLimit.Hint(new string('a', 199), 200));
        Assert.Equal("That's all that fits: 200 characters.", TextLimit.Hint(new string('a', 200), 200));
        Assert.Null(TextLimit.Hint(null, 200));
    }

    [Fact]
    public void A_task_has_room_for_as_much_as_the_plan_keeps()
    {
        Assert.Equal(PlanTask.MaxTitleLength, WeekViewModel.MaxTaskLength);
        Assert.Equal(PlanTask.MaxTitleLength, Add.AddEntryViewModel.MaxTaskLength);
    }

    [Fact]
    public void The_hint_reads_in_greek_too()
    {
        using var _ = new CultureScope("el-GR");

        Assert.Equal("Απομένουν 5 χαρακτήρες", TextLimit.Hint(new string('α', 195), 200));
        Assert.Equal("Ως εδώ χωράει: 200 χαρακτήρες.", Text.Format("Common_LimitReached", 200));
    }
}
