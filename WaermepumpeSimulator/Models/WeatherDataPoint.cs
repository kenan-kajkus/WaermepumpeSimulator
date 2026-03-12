namespace WaermepumpeSimulator.Models;

public class WeatherDataPoint
{
    public double Temperature { get; set; }
    public double RelativeHumidity { get; set; }
    public int Index { get; set; }
    public int Year { get; set; }
}
