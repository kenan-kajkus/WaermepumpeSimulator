using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Tests;

public static class TestHelpers
{
    public static SimulationParameters DefaultParams() => new()
    {
        Jahresverbrauch = 17000,
        Wirkungsgrad = 0.85,
        WarmwasserAnteil = 0,
        Heizgrenze = 15,
        NormAussentemperatur = -13,
        RaumSollTemperatur = 22.0,
        PreisStrom = 0.30,
        PreisAlt = 0.12,
        VorlaufMax = 34,
        VorlaufMin = 30.5,
        WarmwasserTemp = 50,
        HeizstabMax = 9,
        NachtabsenkungAktiv = false,
        NachtStart = 22,
        NachtEnde = 6,
        NachtDeltaT = 5.0,
        RawPMax = "-7, 6.8\n2, 7.0\n7, 7.0",
        RawPMin = "",
        RawCopData = "35, -7, 2.80\n35, 2, 3.41\n35, 7, 4.55\n55, -7, 2.13\n55, 2, 2.41\n55, 7, 3.03"
    };

    /// <summary>
    /// Creates 8760 weather points at a constant temperature and humidity.
    /// </summary>
    public static List<WeatherDataPoint> ConstantWeather(double temperature = 5.0, double humidity = 70.0)
    {
        return Enumerable.Range(0, 8760)
            .Select(i => new WeatherDataPoint
            {
                Temperature = temperature,
                RelativeHumidity = humidity,
                Index = i,
                Year = 2024
            })
            .ToList();
    }

    /// <summary>
    /// Creates 8760 weather points with a sinusoidal temperature curve
    /// simulating a realistic year (-5°C winter, +25°C summer).
    /// </summary>
    public static List<WeatherDataPoint> SinusoidalWeather(
        double mean = 10.0, double amplitude = 15.0, double humidity = 75.0)
    {
        return Enumerable.Range(0, 8760)
            .Select(i => new WeatherDataPoint
            {
                Temperature = mean - amplitude * Math.Cos(2 * Math.PI * i / 8760),
                RelativeHumidity = humidity,
                Index = i,
                Year = 2024
            })
            .ToList();
    }
}
