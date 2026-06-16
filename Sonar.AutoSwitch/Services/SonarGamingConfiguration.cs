namespace Sonar.AutoSwitch.Services;

public record SonarGamingConfiguration(string? Id, string Name)
{
    // Return empty so AutoCompleteBox shows its watermark when no config is selected.
    public override string ToString() => Id is null ? "" : Name;
}