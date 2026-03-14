using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public class WeatherDataService
{
    private readonly HttpClient _http;
    private List<ClimateProfile>? _profiles;

    public WeatherDataService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ClimateProfile>> LoadClimateProfilesAsync()
    {
        if (_profiles != null) return _profiles;
        try
        {
            _profiles = await _http.GetFromJsonAsync<List<ClimateProfile>>("data/cities.json") ?? [];
        }
        catch
        {
            _profiles = [];
        }
        return _profiles;
    }

    public List<ClimateProfile> GetClimateProfiles() => _profiles ?? [];

    public async Task<(List<WeatherDataPoint> data, bool isSynthetic, HashSet<int> years)> LoadPresetWeatherAsync(
        string cityKey, DateTime startDate, DateTime endDate)
    {
        var profiles = await LoadClimateProfilesAsync();
        var profile = profiles.Find(p => p.Key == cityKey) ?? profiles[0];
        var (data, years) = await FetchOpenMeteoAsync(profile.Latitude, profile.Longitude, startDate, endDate);
        if (data.Count > 0)
            return (data, false, years);

        // Should not happen, but return empty as last resort
        return ([], true, new HashSet<int>());
    }

    public async Task<(List<WeatherDataPoint> data, HashSet<int> years)> FetchOpenMeteoAsync(
        double lat, double lon, DateTime startDate, DateTime endDate)
    {
        var latStr = lat.ToString(CultureInfo.InvariantCulture);
        var lonStr = lon.ToString(CultureInfo.InvariantCulture);
        var startStr = startDate.ToString("yyyy-MM-dd");
        var endStr = endDate.ToString("yyyy-MM-dd");

        var url = $"https://archive-api.open-meteo.com/v1/archive?latitude={latStr}&longitude={lonStr}&start_date={startStr}&end_date={endStr}&hourly=temperature_2m,relative_humidity_2m";
        var json = await _http.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);
        var hourly = doc.RootElement.GetProperty("hourly");
        var temps = hourly.GetProperty("temperature_2m");
        var rhs = hourly.GetProperty("relative_humidity_2m");
        var times = hourly.GetProperty("time");

        var data = new List<WeatherDataPoint>();
        var years = new HashSet<int>();

        for (int i = 0; i < temps.GetArrayLength(); i++)
        {
            int yr = startDate.Year;
            if (i < times.GetArrayLength())
            {
                var ts = times[i].GetString();
                if (ts is { Length: >= 4 } && int.TryParse(ts[..4], out int parsedYr))
                    yr = parsedYr;
            }
            years.Add(yr);
            data.Add(new WeatherDataPoint
            {
                Temperature = temps[i].ValueKind == JsonValueKind.Null ? 5.0 : temps[i].GetDouble(),
                RelativeHumidity = i < rhs.GetArrayLength() && rhs[i].ValueKind != JsonValueKind.Null ? rhs[i].GetDouble() : 80.0,
                Index = i,
                Year = yr
            });
        }

        return (data, years);
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

            if (!double.TryParse(parts[tempCol].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double t))
                continue;

            double rh = 80;
            if (rhCol >= 0 && rhCol < parts.Length)
                double.TryParse(parts[rhCol].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out rh);

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
