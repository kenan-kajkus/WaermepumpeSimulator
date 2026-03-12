namespace WaermepumpeSimulator.Models;

public class HeatPumpPreset
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Group { get; set; } = "";
    public string PMax { get; set; } = "";
    public string? PMin { get; set; }
    public string CopData { get; set; } = "";
}
