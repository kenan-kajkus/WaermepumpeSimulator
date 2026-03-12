namespace WaermepumpeSimulator.Models;

public class ClimateProfile
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
