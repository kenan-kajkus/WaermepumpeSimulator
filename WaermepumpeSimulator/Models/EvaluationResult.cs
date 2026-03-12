namespace WaermepumpeSimulator.Models;

public class EvaluationResult
{
    // JAZ
    public string JazColor { get; set; } = "blue";
    public string JazText { get; set; } = "";

    // Taktung
    public string TaktColor { get; set; } = "green";
    public string TaktText { get; set; } = "";

    // Auslegung & Heizstab
    public string StabColor { get; set; } = "green";
    public string StabText { get; set; } = "";
}

public class MonthlyAggregate
{
    public string Name { get; set; } = "";
    public double Waerme { get; set; }
    public double Strom { get; set; }
    public double Jaz { get; set; }
    public double Kosten { get; set; }
    public int IcingCount { get; set; }
}
