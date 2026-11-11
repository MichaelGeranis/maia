using System.Text.Json.Nodes;
using Maia.Domain.Care;
using Maia.Domain.Common;
using Maia.Infrastructure.Persistence;
using Maia.Testing;

namespace Maia.Infrastructure.Tests;

/// <summary>A contact's role is stored by its name, so her other doctors were added to Care team without a new schema.</summary>
public sealed class CareRoleStorageTests : TempFolderTest
{
    [Fact]
    public async Task Every_role_is_stored_by_its_name_and_read_back()
    {
        var plan = TestData.Plan();
        var roles = Enum.GetValues<CareRole>();
        foreach (var (role, i) in roles.Select((role, i) => (role, i)))
        {
            plan.LinkContact(new CareContact(CareContactId.New(), role, $"Contact {i}", $"+30 690 000 00{i:00}"));
        }

        var repository = new EncryptedPlanRepository(Folder, Cipher);
        await repository.SaveAsync(plan, CancellationToken.None);
        var stored = await Cipher.DecryptAsync(await File.ReadAllBytesAsync(Path.Combine(Folder, EncryptedPlanRepository.FileName)), "plan", CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        var json = JsonNode.Parse(stored)!;
        Assert.Equal(roles.Select(r => r.ToString()), json["contacts"]!.AsArray().Select(c => c!["role"]!.GetValue<string>()));
        Assert.Equal(plan.CareTeam.Contacts, loaded!.CareTeam.Contacts);
    }

    [Fact]
    public void The_roles_stored_before_her_other_doctors_keep_their_names()
    {
        // Plans already on phones store these names; renaming one would make such a plan unreadable.
        Assert.Equal(
            new[] { "ObGyn", "Midwife", "Partner", "Hospital", "Lab", "Pediatrician", "Family" },
            Enum.GetValues<CareRole>().Take(7).Select(r => r.ToString()));
        Assert.Equal(
            new[] { "Dentist", "Internist", "Endocrinologist", "Haematologist", "Cardiologist", "Dermatologist", "Gastroenterologist" },
            Enum.GetValues<CareRole>().Skip(7).Select(r => r.ToString()));
    }
}
