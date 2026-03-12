namespace WaermepumpeSimulator.Models;

public class ClimateProfile
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CsvFileName { get; set; } = "";
    public double Avg { get; set; }
    public double Amp { get; set; }
}
