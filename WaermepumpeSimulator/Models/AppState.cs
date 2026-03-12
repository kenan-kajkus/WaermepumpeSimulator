namespace WaermepumpeSimulator.Models;

public class AppState
{
    public SimulationParameters Params { get; set; } = new();
    public string SelectedCity { get; set; } = "frankfurt";
    public string SelectedYear { get; set; } = "2023";
    public string SelectedPreset { get; set; } = "custom";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? GeoLocationName { get; set; }
    public double? GeoLatitude { get; set; }
    public double? GeoLongitude { get; set; }
    public List<ComparisonEntry> ComparisonEntries { get; set; } = [];
}
