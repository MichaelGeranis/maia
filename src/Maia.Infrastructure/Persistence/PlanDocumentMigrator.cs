using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Maia.Application.Plans;
using Maia.Domain.Plans;
using Maia.Domain.Pregnancy;

namespace Maia.Infrastructure.Persistence;

/// <summary>
/// Upgrades stored plans to <see cref="PlanDocument.CurrentSchemaVersion"/>, one step at a time,
/// on the raw JSON so old shapes never need their own C# types.
/// To change the format: bump the version, add a case that transforms vN into vN+1, add a test
/// with a real vN sample.
/// </summary>
internal static class PlanDocumentMigrator
{
    /// <summary>The id her next visit's entry gets (schema 5): a check id of hers (<c>custom-</c>) no generated one can take.</summary>
    internal const string VisitEntryId = "custom-next-visit";

    /// <summary>
    /// Maia's usual suggestions, by the names Maia gave them (English, Greek). They are a record of what it saved, so
    /// they never change with the app's text.
    /// </summary>
    private static readonly (string English, string Greek)[] Suggestions =
    [
        ("Extra ultrasound", "Επιπλέον υπερηχογράφημα"),
        ("Iron recheck", "Επανέλεγχος σιδήρου"),
        ("Urine culture", "Καλλιέργεια ούρων"),
        ("Doppler scan", "Υπερηχογράφημα Doppler"),
        ("Thyroid test", "Έλεγχος θυρεοειδούς"),
        ("Blood pressure checks", "Έλεγχοι πίεσης"),
    ];

    /// <summary>
    /// Reads a stored plan's JSON. A plan in the current schema (every plan after its first save) is read straight from
    /// its bytes; only an older one goes through the JSON tree and the migration steps.
    /// </summary>
    /// <exception cref="NewerPlanSchemaException">A newer version of Maia saved it.</exception>
    /// <exception cref="InvalidDataException">It isn't a plan.</exception>
    /// <exception cref="JsonException">It isn't JSON, or doesn't have a plan's shape.</exception>
    public static PlanDocument Read(ReadOnlySpan<byte> json)
    {
        // PeekSchemaVersion has made sure it is a JSON object, and an object never reads as null.
        if (PeekSchemaVersion(json) == PlanDocument.CurrentSchemaVersion)
        {
            return JsonSerializer.Deserialize(json, PersistenceJsonContext.Default.PlanDocument)!;
        }

        return Migrate(JsonNode.Parse(json)!.AsObject()).Deserialize(PersistenceJsonContext.Default.PlanDocument)!;
    }

    public static JsonObject Migrate(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var version = document["schemaVersion"]?.GetValue<int>() ?? 1;

        if (version > PlanDocument.CurrentSchemaVersion)
        {
            throw new NewerPlanSchemaException(version);
        }

        while (version < PlanDocument.CurrentSchemaVersion)
        {
            version = version switch
            {
                1 => MigrateV1ToV2(document),
                2 => MigrateV2ToV3(document),
                3 => MigrateV3ToV4(document),
                4 => MigrateV4ToV5(document),
                5 => MigrateV5ToV6(document),
                6 => MigrateV6ToV7(document),
                7 => MigrateV7ToV8(),
                8 => MigrateV8ToV9(document),
                _ => throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"No migration from schema {version}.")),
            };
            document["schemaVersion"] = version;
        }

        return document;
    }

    /// <summary>
    /// The top-level <c>schemaVersion</c>, wherever it stands among the other fields; null when it is missing or not a
    /// whole number (the migration path then decides, as it always has).
    /// </summary>
    private static int? PeekSchemaVersion(ReadOnlySpan<byte> json)
    {
        // The whole plan is one final block: Read throws on input that is empty or ends early, so it never returns false
        // before the object's end.
        var reader = new Utf8JsonReader(json);
        reader.Read();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidDataException("Stored plan is not a JSON object.");
        }

        for (reader.Read(); reader.TokenType == JsonTokenType.PropertyName; reader.Read())
        {
            var isVersion = reader.ValueTextEquals("schemaVersion"u8);
            reader.Read();
            if (isVersion)
            {
                return reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var version) ? version : null;
            }

            reader.Skip();
        }

        return null;
    }

    /// <summary>Schema 2 adds questions for the doctor; plans from before start with none.</summary>
    private static int MigrateV1ToV2(JsonObject document)
    {
        document["questions"] ??= new JsonArray();
        return 2;
    }

    /// <summary>Schema 3 adds an optional next visit; plans from before have none, so the shape needs no change.</summary>
    private static int MigrateV2ToV3(JsonObject document)
    {
        document.Remove("nextVisit");
        return 3;
    }

    /// <summary>
    /// Schema 4 adds the plan's home time zone. The stored plan does not say which zone its times were in, so none is
    /// written here: Maia sets it to the phone's zone the first time it opens the plan (PlanService), and saves it.
    /// </summary>
    private static int MigrateV3ToV4(JsonObject document)
    {
        document.Remove("timeZone");
        return 4;
    }

    /// <summary>
    /// Schema 5 gives the entries she adds a kind, lets a result belong to no entry, and makes her next visit an entry:
    /// <list type="bullet">
    /// <item>Every check she added was one her doctor asked for after a visit: an exam or test.</item>
    /// <item>One she booked or did stands at its day (see <see cref="StandAtItsDay"/>), as entries do from schema 5.</item>
    /// <item>Her next visit (<c>nextVisit</c>) becomes a booked doctor visit on her journey (see <see cref="VisitEntry"/>),
    /// unless a booking at that same day and time already stands for it, as Maia then counted it.</item>
    /// <item>No result is untagged yet.</item>
    /// <item>Maia's usual suggestions that Maia saved under one name only, before it kept both (the name in the language
    /// shown, under English), get their English and Greek names, so they follow a language switch as later ones do.</item>
    /// </list>
    /// </summary>
    private static int MigrateV4ToV5(JsonObject document)
    {
        if (document["checks"] is not JsonArray checks)
        {
            document["checks"] = checks = [];
        }

        foreach (var check in checks.OfType<JsonObject>().Where(c => Text(c["origin"]) == nameof(CheckOrigin.AddedByParent)))
        {
            check["kind"] ??= nameof(EntryKind.Exam);
            NameSuggestionInBothLanguages(check);
            StandAtItsDay(document, check);
        }

        if (document["nextVisit"] is JsonObject visit && !checks.OfType<JsonObject>().Any(c => IsBookedAt(c, visit)))
        {
            checks.Add(VisitEntry(document, checks, visit));
        }

        document.Remove("nextVisit");
        document["untaggedResults"] ??= new JsonArray();
        return 5;
    }

    /// <summary>
    /// Schema 6 keeps results apart from the entries on her journey: every result filed under an entry, then every one
    /// that belonged to none, goes into the plan's own <c>results</c>, and the entries no longer hold any.
    /// </summary>
    private static int MigrateV5ToV6(JsonObject document)
    {
        var results = new JsonArray();
        var checks = document["checks"] as JsonArray ?? [];
        foreach (var check in checks.OfType<JsonObject>())
        {
            MoveInto(results, check["results"]);
            check.Remove("results");
        }

        MoveInto(results, document["untaggedResults"]);
        document.Remove("untaggedResults");
        document["results"] = results;
        return 6;
    }

    /// <summary>
    /// Schema 7 has no lab links (the owner: they are not a feature of Maia any more): a result kept as a link goes, with
    /// its address. Files and photos stay as they were.
    /// </summary>
    private static int MigrateV6ToV7(JsonObject document)
    {
        if (document["results"] is JsonArray results)
        {
            foreach (var link in results.OfType<JsonObject>().Where(r => Text(r["kind"]) == "Link").ToList())
            {
                results.Remove(link);
            }

            foreach (var result in results.OfType<JsonObject>())
            {
                result.Remove("link");
            }
        }

        return 7;
    }

    /// <summary>
    /// Schema 8 adds what an older Maia would drop without a word: a task without a date may be for a trimester or a week
    /// (<c>trimester</c>, <c>week</c>), and an entry's trimester may be <c>AfterBirth</c>. Nothing stored before
    /// changes; the new version only makes an older Maia say it is needed rather than lose them.
    /// </summary>
    private static int MigrateV7ToV8() => 8;

    /// <summary>
    /// Schema 9 adds her blood pressure readings (<c>bloodPressure</c>), which an older Maia would drop without a word.
    /// Plans from before start with none; nothing else changes.
    /// </summary>
    private static int MigrateV8ToV9(JsonObject document)
    {
        document["bloodPressure"] ??= new JsonArray();
        return 9;
    }

    private static void MoveInto(JsonArray results, JsonNode? from)
    {
        if (from is JsonArray list)
        {
            foreach (var result in list.ToList())
            {
                list.Remove(result);
                results.Add(result);
            }
        }
    }

    /// <summary>
    /// Her next visit as a booked doctor visit, at its day and time, place and doctor. It is named after the doctor it
    /// was with, or else her first OB-GYN ("Visit with Dr. Eleni Papadaki"), or "Appointment" with no doctor linked, in
    /// English and Greek. Its id is the same every time the plan is read, so its reminders and anything on screen
    /// keep pointing at it until the plan is next saved in this schema.
    /// </summary>
    private static JsonObject VisitEntry(JsonObject document, JsonArray checks, JsonObject visit)
    {
        var date = DateOnly.Parse(Text(visit["date"]) ?? throw new InvalidDataException("The next visit has no date."), CultureInfo.InvariantCulture);
        var window = PregnancyPlan.WindowOn(Dating(document), date);
        var contacts = (document["contacts"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        var doctor = contacts.FirstOrDefault(c => Text(visit["contactId"]) is { } with && SameId(Text(c["id"]), with))
            ?? contacts.FirstOrDefault(c => Text(c["role"]) == "ObGyn");
        var title = new JsonObject();
        foreach (var (language, name) in EntryTitles.DoctorVisit(Text(doctor?["displayName"])).Values)
        {
            title[language] = name;
        }

        var id = VisitEntryId;
        for (var n = 2; checks.OfType<JsonObject>().Any(c => Text(c["id"]) == id); n++)
        {
            id = string.Create(CultureInfo.InvariantCulture, $"{VisitEntryId}-{n}");
        }

        return new JsonObject
        {
            ["id"] = id,
            ["origin"] = nameof(CheckOrigin.AddedByParent),
            ["title"] = title,
            ["category"] = "IfAdvised",
            ["fromWeek"] = window.FromWeek,
            ["toWeek"] = window.ToWeek,
            ["state"] = nameof(CheckState.Booked),
            ["appointment"] = visit.DeepClone(),
            ["results"] = new JsonArray(),
            ["kind"] = nameof(EntryKind.DoctorVisit),
        };
    }

    /// <summary>
    /// A check she added that is booked stands in the week of its booking, and one done in the week of the day it was
    /// done (<see cref="PregnancyPlan.WindowOn"/>). Before schema 5 it kept the weeks around the day she added it, however
    /// far from them she booked it. One still waiting for a date keeps those weeks.
    /// </summary>
    private static void StandAtItsDay(JsonObject document, JsonObject check)
    {
        var day = Text(check["state"]) switch
        {
            nameof(CheckState.Booked) => Text((check["appointment"] as JsonObject)?["date"]),
            nameof(CheckState.Done) => Text(check["completedOn"]),
            _ => null,
        };
        if (day is null)
        {
            return;
        }

        var window = PregnancyPlan.WindowOn(Dating(document), DateOnly.Parse(day, CultureInfo.InvariantCulture));
        check["fromWeek"] = window.FromWeek;
        check["toWeek"] = window.ToWeek;
    }

    private static PregnancyDating Dating(JsonObject document) =>
        PregnancyDating.FromDueDate(DateOnly.Parse(
            Text(document["dueDate"]) ?? throw new InvalidDataException("The plan has no due date."),
            CultureInfo.InvariantCulture));

    /// <summary>One of Maia's usual suggestions saved under one name only gets both.</summary>
    private static void NameSuggestionInBothLanguages(JsonObject check)
    {
        if (check["title"] is not JsonObject { Count: 1 } title || Text(title["en"]) is not { } name)
        {
            return;
        }

        foreach (var (english, greek) in Suggestions)
        {
            if (name == english || name == greek)
            {
                check["title"] = new JsonObject { ["en"] = english, ["el"] = greek };
                return;
            }
        }
    }

    /// <summary>Whether a check holds a booking (still booked, or kept once done) at the visit's day and time.</summary>
    private static bool IsBookedAt(JsonObject check, JsonObject visit) =>
        Text(check["state"]) is nameof(CheckState.Booked) or nameof(CheckState.Done)
        && check["appointment"] is JsonObject booked
        && Text(booked["date"]) is { } date && Text(visit["date"]) is { } visitDate
        && DateOnly.Parse(date, CultureInfo.InvariantCulture) == DateOnly.Parse(visitDate, CultureInfo.InvariantCulture)
        && Time(booked["time"]) == Time(visit["time"]);

    private static TimeOnly? Time(JsonNode? node) => Text(node) is { } time ? TimeOnly.Parse(time, CultureInfo.InvariantCulture) : null;

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool SameId(string? a, string b) => Guid.TryParse(a, out var x) && Guid.TryParse(b, out var y) && x == y;
}

/// <summary>
/// The plan was saved by a newer version of Maia. It is not damage (so not an <see cref="InvalidDataException"/>):
/// nothing may fall back to an older copy or write over it, because updating Maia opens it as she left it.
/// </summary>
internal sealed class NewerPlanSchemaException : Exception
{
    public NewerPlanSchemaException()
        : base("Plan was saved by a newer version of Maia. Update the app.")
    {
    }

    public NewerPlanSchemaException(int schemaVersion)
        : base(string.Create(CultureInfo.InvariantCulture, $"Plan was saved by a newer version of Maia (schema {schemaVersion}). Update the app."))
    {
        SchemaVersion = schemaVersion;
    }

    public NewerPlanSchemaException(string message)
        : base(message)
    {
    }

    public NewerPlanSchemaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public int? SchemaVersion { get; }
}
