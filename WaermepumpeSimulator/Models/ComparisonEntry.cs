namespace WaermepumpeSimulator.Models;

public class ComparisonEntry
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public double Jaz { get; set; }
    public double TotalStrom { get; set; }
    public double TotalWaerme { get; set; }
    public double HeizstabAnteil { get; set; }
    public double KostenWp { get; set; }
    public double Ersparnis { get; set; }
    public double CyclingPercent { get; set; }
    public int IcingHours { get; set; }
    public int DeficitHours { get; set; }
    public double LoadAtNat { get; set; }
    public double WpAtNat { get; set; }
}
