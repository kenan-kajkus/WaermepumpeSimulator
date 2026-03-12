namespace WaermepumpeSimulator.Models;

public class SimulationResult
{
    // Hourly arrays (8760 entries)
    public double[] Temperature { get; set; } = [];
    public double[] RelativeHumidity { get; set; } = [];
    public double[] DewPoint { get; set; } = [];
    public double[] Load { get; set; } = [];
    public double[] Cop { get; set; } = [];
    public double[] ThermalPower { get; set; } = [];
    public double[] ElectricalPower { get; set; } = [];
    public double[] HeizstabPower { get; set; } = [];
    public double[] Deficit { get; set; } = [];
    public int[] Icing { get; set; } = [];
    public int[] Cycling { get; set; } = [];
    public double[] PMaxAvail { get; set; } = [];
    public double[] PMinAvail { get; set; } = [];

    // WP characteristic data for plots
    public double[] LutTemps { get; set; } = [];
    public double[] WpP35 { get; set; } = [];
    public double[] WpP55 { get; set; } = [];
    public double[] WpPMin { get; set; } = [];
    public double[] WpPMaxCustom { get; set; } = [];
    public double[] WpCop35 { get; set; } = [];
    public double[] WpCop55 { get; set; } = [];
    public double[] WpEta35 { get; set; } = [];
    public double[] WpEta55 { get; set; } = [];

    // Aggregated results
    public double Jaz { get; set; }
    public double TotalStrom { get; set; }
    public double TotalWaerme { get; set; }
    public double HeizstabAnteil { get; set; }
    public int IcingHours { get; set; }
    public double CyclingPercent { get; set; }
    public int DeficitHours { get; set; }
    public double DeficitKwh { get; set; }

    // Auslegung
    public double LoadAtNat { get; set; }
    public double WpAtNat { get; set; }
    public double? BivalenzTemp { get; set; }
    public double? BivalenzPower { get; set; }
    public double PlotNat { get; set; }
    public double PlotHeizgrenze { get; set; }
    public double PlotLoadHg { get; set; }

    // Kosten
    public double KostenWp { get; set; }
    public double KostenAlt { get; set; }
    public double Ersparnis { get; set; }

    // Raw COP data points for check plots
    public List<double[]> RawCopPoints { get; set; } = [];
}
