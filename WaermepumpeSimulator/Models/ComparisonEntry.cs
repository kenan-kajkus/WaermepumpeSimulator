namespace WaermepumpeSimulator.Models;

public class ComparisonEntry
{
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public double Jaz { get; set; }
    public double TotalElectricity { get; set; }
    public double TotalHeat { get; set; }
    public double HeizstabShare { get; set; }
    public double CostHeatPump { get; set; }
    public double Savings { get; set; }
    public double CyclingPercent { get; set; }
    public int IcingHours { get; set; }
    public int DeficitHours { get; set; }
    public double LoadAtDesignTemp { get; set; }
    public double HeatPumpPowerAtDesignTemp { get; set; }
}
