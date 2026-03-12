using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public class WeatherDataService
{
    private readonly HttpClient _http;

    public WeatherDataService(HttpClient http)
    {
        _http = http;
    }

    public static List<ClimateProfile> GetClimateProfiles() =>
    [
        new() { Key = "hamburg", Name = "Hamburg", DisplayName = "Hamburg (20095)", CsvFileName = "Hamburg.csv", Avg = 9.8, Amp = 16.0 },
        new() { Key = "berlin", Name = "Berlin", DisplayName = "Berlin (10115)", CsvFileName = "Berlin.csv", Avg = 10.3, Amp = 19.5 },
        new() { Key = "koeln", Name = "Köln", DisplayName = "Köln (50667)", CsvFileName = "Koeln.csv", Avg = 11.2, Amp = 15.5 },
        new() { Key = "frankfurt", Name = "Frankfurt", DisplayName = "Frankfurt (60311)", CsvFileName = "Frankfurt.csv", Avg = 10.8, Amp = 18.0 },
        new() { Key = "muenchen", Name = "München", DisplayName = "München (80331)", CsvFileName = "Muenchen.csv", Avg = 9.5, Amp = 21.0 },
        new() { Key = "hof", Name = "Hof", DisplayName = "Hof (95028)", CsvFileName = "Hof.csv", Avg = 7.2, Amp = 20.0 },
        new() { Key = "garmisch", Name = "Garmisch", DisplayName = "Garmisch (82467)", CsvFileName = "GarmischPartenkirchen.csv", Avg = 3.8, Amp = 14.0 },
    ];

    public async Task<(List<WeatherDataPoint> data, bool isSynthetic, HashSet<int> years)> LoadPresetWeatherAsync(string cityKey)
    {
        var profile = GetClimateProfiles().Find(p => p.Key == cityKey) ?? GetClimateProfiles()[3]; // default frankfurt
        try
        {
            var csvText = await _http.GetStringAsync($"data/weather/{profile.CsvFileName}");
            var result = ParseCsv(csvText);
            if (result.data.Count >= 100)
                return (result.data, false, result.years);
        }
        catch { /* fall through to synthetic */ }

        return (GenerateSyntheticWeather(profile), true, new HashSet<int> { 2023 });
    }

    public (List<WeatherDataPoint> data, HashSet<int> years) ParseCsv(string csvRaw)
    {
        var lines = csvRaw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int headerIdx = -1;

        for (int i = 0; i < Math.Min(lines.Length, 100); i++)
        {
            var l = lines[i].ToLower();
            if ((l.Contains("time") || l.Contains("date")) && (l.Contains("t2m") || l.Contains("temp") || l.Contains("temperature")))
            {
                headerIdx = i;
                break;
            }
        }

        if (headerIdx == -1)
            return ([], new HashSet<int>());

        var headers = lines[headerIdx].Split(',');
        int tempCol = -1, rhCol = -1, timeCol = -1;

        for (int c = 0; c < headers.Length; c++)
        {
            var h = headers[c].Trim().ToLower();
            if (tempCol == -1 && (h.Contains("t2m") || h.Contains("temp"))) tempCol = c;
            if (rhCol == -1 && (h.Contains("rh") || h.Contains("hum"))) rhCol = c;
            if (timeCol == -1 && (h.Contains("time") || h.Contains("date"))) timeCol = c;
        }

        if (tempCol == -1)
            return ([], new HashSet<int>());

        var data = new List<WeatherDataPoint>();
        var years = new HashSet<int>();
        int idx = 0;

        for (int i = headerIdx + 1; i < lines.Length; i++)
        {
            var parts = lines[i].Split(',');
            if (parts.Length <= tempCol) continue;

            if (!double.TryParse(parts[tempCol].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double t))
                continue;

            double rh = 80;
            if (rhCol >= 0 && rhCol < parts.Length)
                double.TryParse(parts[rhCol].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out rh);

            int year = 2023;
            if (timeCol >= 0 && timeCol < parts.Length)
            {
                var timeStr = parts[timeCol].Trim();
                if (timeStr.Length >= 4 && int.TryParse(timeStr[..4], out int y))
                    year = y;
            }

            data.Add(new WeatherDataPoint { Temperature = t, RelativeHumidity = double.IsNaN(rh) ? 80 : rh, Index = idx++, Year = year });
            years.Add(year);
        }

        return (data, years);
    }

    public static List<WeatherDataPoint> GenerateSyntheticWeather(ClimateProfile profile)
    {
        var rng = new Random(42);
        var data = new List<WeatherDataPoint>(8760);
        for (int i = 0; i < 8760; i++)
        {
            int hour = i % 24;
            double annualRad = ((i + 300.0) / 8760.0) * 2 * Math.PI;
            double annualTemp = profile.Avg - profile.Amp * Math.Cos(annualRad);
            double dailyRad = ((hour - 15.0) / 24.0) * 2 * Math.PI;
            double dailyTemp = (3.5 + 2.5 * Math.Sin(annualRad)) * Math.Cos(dailyRad);
            double t = annualTemp + dailyTemp + (rng.NextDouble() - 0.5) * 4.0;
            double rhVal = 85 - 15 * Math.Sin(annualRad) - 15 * Math.Cos(dailyRad) + (rng.NextDouble() - 0.5) * 10;
            data.Add(new WeatherDataPoint
            {
                Temperature = t,
                RelativeHumidity = Math.Clamp(rhVal, 20, 100),
                Index = i,
                Year = 2023
            });
        }
        return data;
    }

    public static List<WeatherDataPoint> FilterByYear(List<WeatherDataPoint> allData, string selectedYear)
    {
        List<WeatherDataPoint> result;

        if (selectedYear == "all")
        {
            var acc = new (double t, double rh, int count)[8760];
            int currentYear = -1, hourIndex = 0;
            foreach (var d in allData)
            {
                if (d.Year != currentYear) { currentYear = d.Year; hourIndex = 0; }
                if (hourIndex < 8760)
                {
                    acc[hourIndex].t += d.Temperature;
                    acc[hourIndex].rh += d.RelativeHumidity;
                    acc[hourIndex].count++;
                }
                hourIndex++;
            }
            result = acc.Select((e, i) => new WeatherDataPoint
            {
                Temperature = e.count > 0 ? e.t / e.count : 0,
                RelativeHumidity = e.count > 0 ? e.rh / e.count : 80,
                Index = i,
                Year = 0
            }).ToList();
        }
        else
        {
            int y = int.Parse(selectedYear);
            result = allData.Where(d => d.Year == y).ToList();
        }

        // Pad or trim to 8760
        if (result.Count > 0)
        {
            int original = result.Count;
            while (result.Count < 8760)
            {
                var copy = result[result.Count % original];
                result.Add(new WeatherDataPoint
                {
                    Temperature = copy.Temperature,
                    RelativeHumidity = copy.RelativeHumidity,
                    Index = result.Count,
                    Year = copy.Year
                });
            }
            if (result.Count > 8760) result = result.Take(8760).ToList();
        }

        return result;
    }
}
