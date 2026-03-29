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
    public double[] MaxPowerAvailable { get; set; } = [];
    public double[] MinPowerAvailable { get; set; } = [];
    public double[] EvaporatorTemp { get; set; } = [];

    // WP characteristic curves (indexed by LookupTemperatures)
    public double[] LookupTemperatures { get; set; } = [];
    public double[] PowerMaxAtVL35 { get; set; } = [];
    public double[] PowerMaxAtVL55 { get; set; } = [];
    public double[] PowerMinCurve { get; set; } = [];
    public double[] PowerMaxAdjusted { get; set; } = [];
    public double[] CopAtVL35 { get; set; } = [];
    public double[] CopAtVL55 { get; set; } = [];
    public double[] EtaAtVL35 { get; set; } = [];
    public double[] EtaAtVL55 { get; set; } = [];
    public double[] CopPMinAtVL35 { get; set; } = [];
    public double[] CopPMinAtVL55 { get; set; } = [];
    public double[] EtaPMinAtVL35 { get; set; } = [];
    public double[] EtaPMinAtVL55 { get; set; } = [];
    public bool HasPMinCop { get; set; }

    // Aggregated results
    public double Jaz { get; set; }
    public double TotalElectricity { get; set; }
    public double TotalHeat { get; set; }
    public double HeizstabShare { get; set; }
    public int IcingHours { get; set; }
    public int FrostCriticalHours { get; set; }
    public int DefrostCyclesEstimate { get; set; }
    public double DefrostHoursEstimate { get; set; }
    public double DefrostQuote { get; set; }
    public double CyclingPercent { get; set; }
    public int DeficitHours { get; set; }
    public double DeficitKwh { get; set; }

    // Design point (Auslegung)
    public double LoadAtDesignTemp { get; set; }
    public double HeatPumpPowerAtDesignTemp { get; set; }
    public double? BivalenceTemperature { get; set; }
    public double? BivalencePower { get; set; }
    public double DesignTemperature { get; set; }
    public double HeatingLimitTemperature { get; set; }
    public double WarmwaterBaseLoad { get; set; }

    // Costs
    public double CostHeatPump { get; set; }
    public double CostOldHeating { get; set; }
    public double Savings { get; set; }

    // Raw COP data points for check plots
    public List<double[]> RawCopPoints { get; set; } = [];
}
