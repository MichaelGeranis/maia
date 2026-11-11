using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maia.Infrastructure.Persistence;

namespace Maia.Infrastructure.Tests;

/// <summary>
/// The migration steps on stored plans that are odd but possible: fields an old app left out or wrote twice, a next
/// visit that matches no booking or no doctor, and values of a type no Maia wrote. The whole plans are in
/// <see cref="EncryptedPlanRepositoryTests"/>; these are the JSON alone.
/// </summary>
public sealed class PlanDocumentMigratorTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("\"plan\"")]
    public void Json_that_is_not_an_object_is_not_a_plan(string json) =>
        Assert.Throws<InvalidDataException>(() => PlanDocumentMigrator.Read(Encoding.UTF8.GetBytes(json)));

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"schemaVersion":9""")]
    public void Json_that_is_empty_or_ends_early_is_not_json(string json) =>
        Assert.ThrowsAny<JsonException>(() => PlanDocumentMigrator.Read(Encoding.UTF8.GetBytes(json)));

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_version_before_the_first_has_no_way_up(int version)
    {
        var ex = Assert.Throws<InvalidDataException>(() => Migrate($$"""{"schemaVersion":{{version}}}"""));

        Assert.Contains("No migration", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Questions_already_in_a_first_schema_plan_are_kept()
    {
        var plan = Migrate("""{"schemaVersion":1,"questions":[{"text":"Is swimming fine?"}]}""");

        Assert.Equal("Is swimming fine?", (string?)Assert.Single(plan["questions"]!.AsArray())!["text"]);
    }

    [Fact]
    public void A_plan_from_before_entries_had_kinds_without_checks_ends_up_with_none_and_no_results()
    {
        var plan = Migrate("""{"schemaVersion":4}""");

        Assert.Empty(plan["checks"]!.AsArray());
        Assert.Empty(plan["results"]!.AsArray());
        Assert.Null(plan["untaggedResults"]);
        Assert.Equal(PlanDocument.CurrentSchemaVersion, (int)plan["schemaVersion"]!);
    }

    [Fact]
    public void Results_a_schema_4_plan_already_kept_apart_from_its_checks_stay_with_it()
    {
        var plan = Migrate("""{"schemaVersion":4,"untaggedResults":[{"id":"r1","kind":"Document"}]}""");

        Assert.Equal("r1", (string?)Assert.Single(plan["results"]!.AsArray())!["id"]);
    }

    [Fact]
    public void A_schema_5_plan_without_checks_still_gets_its_results_list()
    {
        var plan = Migrate("""{"schemaVersion":5,"untaggedResults":[{"id":"r1","kind":"Photo"}]}""");

        Assert.Equal("r1", (string?)Assert.Single(plan["results"]!.AsArray())!["id"]);
        Assert.Null(plan["checks"]);
    }

    [Fact]
    public void A_schema_7_plan_moves_up_as_it_is_with_its_tasks_for_no_trimester_or_week()
    {
        var plan = Migrate("""{"schemaVersion":7,"tasks":[{"id":"7f9c0bd2-4d4a-4f4e-9d3c-6c1b8a0d2e11","title":"Pack the bag","isDone":false}]}""");

        var task = Assert.Single(plan["tasks"]!.AsArray())!.AsObject();
        Assert.Equal(PlanDocument.CurrentSchemaVersion, (int)plan["schemaVersion"]!);
        Assert.Equal(["id", "title", "isDone"], task.Select(p => p.Key));
    }

    [Fact]
    public void A_schema_8_plan_moves_to_9_as_it_is_with_no_blood_pressure_readings()
    {
        var plan = Migrate("""{"schemaVersion":8,"questions":[{"id":"3d0c4f0e-1b2a-4c5d-8e9f-0a1b2c3d4e5f","text":"Is swimming fine?"}]}""");

        Assert.Equal(9, (int)plan["schemaVersion"]!);
        Assert.Empty(plan["bloodPressure"]!.AsArray());
        Assert.Equal("Is swimming fine?", (string?)Assert.Single(plan["questions"]!.AsArray())!["text"]);
    }

    [Fact]
    public void A_schema_6_plan_without_results_is_left_without_them()
    {
        var plan = Migrate("""{"schemaVersion":6,"checks":[]}""");

        Assert.Null(plan["results"]);
        Assert.Equal(PlanDocument.CurrentSchemaVersion, (int)plan["schemaVersion"]!);
    }

    [Fact]
    public void A_next_visit_without_a_date_is_not_a_plan()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Migrate("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{"id":"c","origin":"Template","state":"Booked","appointment":{"date":"2026-10-06"}}],
             "nextVisit":{"time":"11:00:00"}}
            """));

        Assert.Contains("no date", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_next_visit_in_a_plan_without_a_due_date_is_not_a_plan()
    {
        var ex = Assert.Throws<InvalidDataException>(() => Migrate("""{"schemaVersion":4,"nextVisit":{"date":"2026-10-20"}}"""));

        Assert.Contains("no due date", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_next_visit_with_no_doctor_and_no_contacts_is_an_appointment()
    {
        var plan = Migrate("""{"schemaVersion":4,"dueDate":"2027-01-17","nextVisit":{"date":"2026-10-20"}}""");

        var visit = Assert.Single(plan["checks"]!.AsArray())!;
        Assert.Equal(("Appointment", "Ραντεβού"), ((string?)visit["title"]!["en"], (string?)visit["title"]!["el"]));
        Assert.Equal("DoctorVisit", (string?)visit["kind"]);
    }

    [Theory]
    [InlineData("1b2c3d4e-0000-4000-8000-000000000001")] // nobody's id
    [InlineData("not an id")]
    [InlineData(null)] // with no doctor at all
    public void A_next_visit_with_a_doctor_she_no_longer_has_is_named_after_her_ob_gyn(string? contactId)
    {
        var document = JsonNode.Parse("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "contacts":[{"id":"not a guid","role":"Midwife","displayName":"Anna"},
                         {"id":"9e8d7c6b-5a49-4382-9170-6f5e4d3c2b1a","role":"ObGyn","displayName":"Dr. Eleni Papadaki"}],
             "nextVisit":{"date":"2026-10-20"}}
            """)!.AsObject();
        if (contactId is not null)
        {
            document["nextVisit"]!["contactId"] = contactId;
        }

        var plan = PlanDocumentMigrator.Migrate(document);

        var visit = Assert.Single(plan["checks"]!.AsArray())!;
        Assert.Equal("Visit with Dr. Eleni Papadaki", (string?)visit["title"]!["en"]);
    }

    [Fact]
    public void A_next_visit_gets_an_id_no_entry_of_hers_has_taken()
    {
        var plan = Migrate("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{"id":"custom-next-visit","origin":"Template","state":"Pending"},
                       {"id":"custom-next-visit-2","origin":"Template","state":"Pending"}],
             "nextVisit":{"date":"2026-10-20"}}
            """);

        Assert.Equal("custom-next-visit-3", (string?)plan["checks"]![2]!["id"]);
    }

    [Theory]
    [InlineData("""{"state":"Booked","appointment":{"date":"2026-10-20","time":"11:00:00"}}""", false)]
    [InlineData("""{"state":"Done","appointment":{"date":"2026-10-20","time":"11:00:00"}}""", false)]
    [InlineData("""{"state":"Booked","appointment":{"date":"2026-10-20","time":"11:30:00"}}""", true)]
    [InlineData("""{"state":"Booked","appointment":{"date":"2026-10-21","time":"11:00:00"}}""", true)]
    [InlineData("""{"state":"Booked","appointment":{"time":"11:00:00"}}""", true)]
    [InlineData("""{"state":"Booked"}""", true)]
    [InlineData("""{"state":"Pending","appointment":{"date":"2026-10-20","time":"11:00:00"}}""", true)]
    [InlineData("""{"appointment":{"date":"2026-10-20","time":"11:00:00"}}""", true)]
    [InlineData("""{"state":"Open","appointment":{"date":"2026-10-20","time":"11:00:00"}}""", true)]
    [InlineData("""{"state":"Missed","appointment":{"date":"2026-10-20","time":"11:00:00"}}""", true)]
    public void A_next_visit_is_a_new_entry_unless_a_booking_stands_at_its_day_and_time(string check, bool added)
    {
        var booking = JsonNode.Parse(check)!.AsObject();
        booking["id"] = "c";
        booking["origin"] = "Template";
        var plan = Migrate($$$"""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{{{booking.ToJsonString()}}}],
             "nextVisit":{"date":"2026-10-20","time":"11:00:00"}}
            """);

        Assert.Equal(added ? 2 : 1, plan["checks"]!.AsArray().Count);
    }

    [Fact]
    public void A_next_visit_without_a_time_matches_a_booking_on_its_day_without_one()
    {
        var plan = Migrate("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{"id":"c","origin":"Template","state":"Booked","appointment":{"date":"2026-10-20"}}],
             "nextVisit":{"date":"2026-10-20"}}
            """);

        Assert.Single(plan["checks"]!.AsArray());
    }

    [Fact]
    public void A_next_visit_without_a_time_is_not_a_booking_at_midnight()
    {
        var plan = Migrate("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{"id":"c","origin":"Template","state":"Booked","appointment":{"date":"2026-10-20","time":"00:00:00"}}],
             "nextVisit":{"date":"2026-10-20"}}
            """);

        Assert.Equal(2, plan["checks"]!.AsArray().Count);
    }

    [Fact]
    public void A_check_she_added_that_is_booked_without_a_day_keeps_its_weeks()
    {
        var plan = Migrate("""
            {"schemaVersion":4,"dueDate":"2027-01-17",
             "checks":[{"id":"custom-1","origin":"AddedByParent","title":{"en":"Ferritin"},"fromWeek":24,"toWeek":26,"state":"Booked"}]}
            """);

        var check = Assert.Single(plan["checks"]!.AsArray())!;
        Assert.Equal((24, 26), ((int)check["fromWeek"]!, (int)check["toWeek"]!));
        Assert.Equal("Exam", (string?)check["kind"]);
    }

    [Theory]
    [InlineData("\"Iron recheck\"")] // not a title in languages
    [InlineData("""{"en":"Iron recheck","el":"Επανέλεγχος σιδήρου","de":"Eisen"}""")] // more than one name already
    [InlineData("""{"el":"Επανέλεγχος σιδήρου"}""")] // its one name is not under English
    [InlineData("""{"en":42}""")] // not a name
    public void A_title_that_is_not_one_suggestion_name_is_left_as_it_is(string title)
    {
        var plan = Migrate($$"""
            {"schemaVersion":4,"checks":[{"id":"custom-1","origin":"AddedByParent","title":{{title}},"state":"Pending"}]}
            """);

        Assert.Equal(JsonNode.Parse(title)!.ToJsonString(), plan["checks"]![0]!["title"]!.ToJsonString());
    }

    [Fact]
    public void A_check_whose_origin_is_not_a_name_is_not_one_she_added()
    {
        var plan = Migrate("""{"schemaVersion":4,"checks":[{"id":"c","origin":5,"title":{"en":"Iron recheck"},"state":"Pending"}]}""");

        var check = plan["checks"]![0]!;
        Assert.Null(check["kind"]);
        Assert.Equal("""{"en":"Iron recheck"}""", check["title"]!.ToJsonString());
    }

    [Fact]
    public void The_exceptions_for_a_plan_from_a_newer_maia_say_so_with_or_without_its_schema()
    {
        var inner = new InvalidOperationException("inner");

        Assert.Null(new NewerPlanSchemaException().SchemaVersion);
        Assert.Contains("newer version of Maia", new NewerPlanSchemaException().Message, StringComparison.Ordinal);
        Assert.Contains("schema 9", new NewerPlanSchemaException(9).Message, StringComparison.Ordinal);
        Assert.Equal(9, new NewerPlanSchemaException(9).SchemaVersion);
        Assert.Equal("Saved by Maia 3.", new NewerPlanSchemaException("Saved by Maia 3.").Message);
        Assert.Same(inner, new NewerPlanSchemaException("Saved by Maia 3.", inner).InnerException);
    }

    private static JsonObject Migrate(string json) => PlanDocumentMigrator.Migrate(JsonNode.Parse(json)!.AsObject());
}
