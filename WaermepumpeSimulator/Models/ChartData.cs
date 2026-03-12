namespace WaermepumpeSimulator.Models;

public class XYPoint
{
    public decimal X { get; set; }
    public decimal Y { get; set; }

    public XYPoint() { }
    public XYPoint(double x, double y) { X = (decimal)x; Y = (decimal)y; }
}

public class ScatterPoint
{
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string Color { get; set; } = "";
    public string Group { get; set; } = "";

    public ScatterPoint() { }
    public ScatterPoint(double x, double y, string group)
    {
        X = (decimal)x; Y = (decimal)y; Group = group;
    }
}

public class MonthlyChartPoint
{
    public string Month { get; set; } = "";
    public decimal Waerme { get; set; }
    public decimal Strom { get; set; }
}

public class DurationPoint
{
    public int Hour { get; set; }
    public decimal Wp { get; set; }
    public decimal Stab { get; set; }
    public decimal Defizit { get; set; }
}
