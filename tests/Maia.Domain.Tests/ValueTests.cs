using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;
using Maia.Domain.Templates;
using Maia.Testing;

namespace Maia.Domain.Tests;

/// <summary>The small values the plan is built from: ids, text in two languages, ages, week windows, bookings and rule failures.</summary>
public sealed class ValueTests
{
    [Fact]
    public void Ids_read_as_their_bare_32_digit_form()
    {
        var guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

        Assert.Equal("0f8fad5bd9cb469fa16570867728950e", new PlanId(guid).ToString());
        Assert.Equal("0f8fad5bd9cb469fa16570867728950e", new AttachmentId(guid).ToString());
        Assert.NotEqual(PlanId.New(), PlanId.New());
    }

    [Fact]
    public void Text_drops_blank_languages_and_blank_wording()
    {
        var text = new LocalizedText(new Dictionary<string, string>
        {
            [" EN "] = " Glucose test ",
            ["el"] = "   ",
            ["  "] = "Καμπύλη σακχάρου",
        });

        Assert.Equal(new Dictionary<string, string> { ["en"] = "Glucose test" }, text.Values);
        Assert.False(text.Has(LocalizedText.Greek));
        Assert.Equal("Glucose test", text.In(LocalizedText.Greek));
    }

    [Fact]
    public void Text_reads_as_its_English_and_equal_texts_hash_alike()
    {
        var text = TestData.Text("Glucose test", "Καμπύλη σακχάρου");
        var same = TestData.Text("Glucose test", "Καμπύλη σακχάρου");

        Assert.Equal("Glucose test", text.ToString());
        Assert.Equal(same, text);
        Assert.Equal(same.GetHashCode(), text.GetHashCode());
        Assert.NotEqual(TestData.Text("Glucose test", "Σάκχαρο"), text);
        Assert.False(text.Equals((object?)null));
    }

    [Fact]
    public void A_pregnancy_age_compares_by_its_days_and_counts_part_weeks()
    {
        var earlier = GestationalAge.FromWeeks(24, 3);
        var later = GestationalAge.FromWeeks(24, 4);

        Assert.True(earlier < later);
        Assert.False(later < earlier);
        Assert.False(earlier < GestationalAge.FromWeeks(24, 3));
        Assert.Equal(24 + (3 / 7.0), new GestationalAge((24 * 7) + 3).ExactWeeks, precision: 10);
        Assert.Equal(1.0, GestationalAge.FromWeeks(1).ExactWeeks);
    }

    [Fact]
    public void A_week_window_reads_as_its_weeks()
    {
        Assert.Equal("24", WeekWindow.OfWeek(24).ToString());
        Assert.Equal("24-28", new WeekWindow(24, 28).ToString());
    }

    [Fact]
    public void A_booking_place_has_a_length_limit_and_a_blank_place_is_none()
    {
        var day = new DateOnly(2026, 10, 12);

        var ex = Assert.Throws<DomainException>(() => new Appointment(day, location: new string('x', Appointment.MaxLocationLength + 1)));

        Assert.Equal("appointment.location.too_long", ex.Code);
        Assert.Equal(new string('x', Appointment.MaxLocationLength), new Appointment(day, location: new string('x', Appointment.MaxLocationLength)).Location);
        Assert.Null(new Appointment(day, location: "   ").Location);
        Assert.Equal("Clinic", new Appointment(day, location: " Clinic ").Location);
    }

    [Theory]
    [InlineData(121, 20, null, false)]
    [InlineData(20, 121, null, false)]
    [InlineData(20, 20, 121, false)]
    [InlineData(20, 20, 120, true)]
    [InlineData(120, 120, null, true)]
    public void A_contact_s_name_number_and_label_each_have_a_length_limit(int name, int number, int? label, bool fits)
    {
        CareContact Make() => new(CareContactId.New(), CareRole.ObGyn, new string('n', name), new string('1', number), label is { } l ? new string('l', l) : null);

        if (fits)
        {
            Assert.Equal(new string('n', name), Make().DisplayName);
        }
        else
        {
            Assert.Equal("contact.field.too_long", Assert.Throws<DomainException>(Make).Code);
        }
    }

    [Fact]
    public void A_rule_failure_without_a_code_says_unknown()
    {
        var cause = new InvalidOperationException("cause");

        Assert.Equal("domain.unknown", new DomainException().Code);
        Assert.Equal(("domain.unknown", "rule"), (new DomainException("rule").Code, new DomainException("rule").Message));
        var wrapped = new DomainException("rule", cause);
        Assert.Equal(("domain.unknown", "rule", cause), (wrapped.Code, wrapped.Message, wrapped.InnerException));
        Assert.Equal(("check.is_milestone", "milestone"), (new DomainException("check.is_milestone", "milestone").Code, new DomainException("check.is_milestone", "milestone").Message));
        Assert.Throws<ArgumentException>(() => new DomainException(" ", "message"));
    }
}
