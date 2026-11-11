using System.Globalization;
using Maia.Application.Common;
using Maia.Domain.Care;
using Maia.Domain.Common;

namespace Maia.Application.Plans;

/// <summary>The names Maia gives an entry it adds for her, in English and Greek, so they follow her language.</summary>
public static class EntryTitles
{
    /// <summary>
    /// A doctor visit: "Visit with Dr. Eleni Papadaki" when the doctor is known (her OB-GYN), else "Appointment".
    /// </summary>
    public static LocalizedText DoctorVisit(CareContact? doctor) => DoctorVisit(doctor?.DisplayName);

    /// <summary>As <see cref="DoctorVisit(CareContact?)"/>, from the doctor's name as her phone's contacts give it.</summary>
    public static LocalizedText DoctorVisit(string? doctorName)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var language in new[] { LocalizedText.English, LocalizedText.Greek })
        {
            names[language] = string.IsNullOrWhiteSpace(doctorName)
                ? AppStrings.DiscreetAppointment.In(language)
                : string.Format(CultureInfo.InvariantCulture, AppStrings.VisitWith.In(language), doctorName.Trim());
        }

        return new LocalizedText(names);
    }
}
