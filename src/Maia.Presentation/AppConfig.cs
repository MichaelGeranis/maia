namespace Maia.Presentation;

/// <summary>Product settings that are not user preferences.</summary>
public static class AppConfig
{
    /// <summary>The first market. Selects the plan template until more countries are supported.</summary>
    public const string DefaultCountry = "GR";

    /// <summary>The emergency number shown on Care team (112 across Greece and the EU). Maia is not for emergencies.</summary>
    public const string EmergencyNumber = "112";
}
