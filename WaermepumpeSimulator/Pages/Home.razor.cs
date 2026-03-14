using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using WaermepumpeSimulator.Models;
using WaermepumpeSimulator.Services;

namespace WaermepumpeSimulator.Pages;

public partial class Home
{
    [Inject] private WeatherDataService WeatherSvc { get; set; } = default!;
    [Inject] private HeatPumpPresetService PresetSvc { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private HttpClient Http { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private SimulationParameters Params { get; set; } = new();
    private SimulationResult? _result;
    private EvaluationResult? _eval;
    private List<MonthlyAggregate>? _monthly;
    private readonly Dictionary<string, (SimulationResult Result, EvaluationResult Eval, List<MonthlyAggregate> Monthly)> _resultCache = new();

    private List<ClimateProfile> _profiles = [];
    private List<HeatPumpPreset> _presets = [];
    private List<HeatPumpPreset> _customPresets = [];
    private List<ComparisonEntry> _comparisonEntries = [];
    private List<WeatherDataPoint> _allWeatherData = [];
    private List<WeatherDataPoint> _weatherData = [];
    private HashSet<int> _availableYears = [];

    private string _selectedCity = "";
    private string _selectedYear = "2023";
    private string _selectedPreset = "custom";
    private string _activeTab = "dashboard";
    private readonly HashSet<string> _visitedTabs = ["dashboard"];

    private bool _initializing = true;
    private string _statusText = "Lade...";
    private string _statusSub = "-";
    private string _statusClass = "text-gray-500";

    private string? _geoLocationName;
    private double? _geoLatitude;
    private double? _geoLongitude;
    private bool _geoLoading;
    private bool _simulating;
    private bool _simulationDirty;
    private bool _shareCopied;
    private bool _sidebarOpen;
    private Timer? _debounceTimer;
    private Timer? _renderTimer;
    private CancellationTokenSource? _precalcCts;
    private readonly Stack<SimulationParameters> _undoStack = new();
    private readonly Stack<SimulationParameters> _redoStack = new();
    private SimulationParameters _lastCommittedState = new SimulationParameters().Clone();
    private SimulationParameters? _burstSnapshot;
    private const int MaxUndoSteps = 50;
    private bool _hasValidationErrors;
    private bool _vJahresverbrauch, _vWirkungsgrad, _vWwAnteil, _vHeizgrenze, _vNat;
    private bool _wRaumSoll, _vPreisStrom, _vPreisAlt, _vVlMax, _wVlMax, _vVlMin;
    private bool _wWwTemp, _vHeizstab, _vPMax, _vCop;
    private bool _wPMaxNegative, _wCopRange, _wPMinExceedsPMax;
    private bool _renderEnabled = true;
    private DateTime _startDate = new(DateTime.Now.Year - 1, 1, 1);
    private DateTime _endDate = new(DateTime.Now.Year - 1, 12, 31);

    private readonly Dictionary<string, string> _tabs = new()
    {
        ["dashboard"] = "1. Effizienz & Dauerlinie",
        ["details"] = "2. Verlauf & Taktung",
        ["icing"] = "3. Icing Map",
        ["monthly"] = "4. Monatsbilanz",
        ["check"] = "5. Kennlinien Check",
    };

    private ValidationState GetValidationState() => new()
    {
        VJahresverbrauch = _vJahresverbrauch, VWirkungsgrad = _vWirkungsgrad,
        VWwAnteil = _vWwAnteil, VHeizgrenze = _vHeizgrenze, VNat = _vNat,
        WRaumSoll = _wRaumSoll, VPreisStrom = _vPreisStrom, VPreisAlt = _vPreisAlt,
        VVlMax = _vVlMax, WVlMax = _wVlMax, VVlMin = _vVlMin,
        WWwTemp = _wWwTemp, VHeizstab = _vHeizstab, VPMax = _vPMax, VCop = _vCop,
        WPMaxNegative = _wPMaxNegative, WCopRange = _wCopRange, WPMinExceedsPMax = _wPMinExceedsPMax
    };

    protected override bool ShouldRender() => _renderEnabled;

    protected override void OnInitialized()
    {
        _presets = PresetSvc.GetAllPresets();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _profiles = await WeatherSvc.LoadClimateProfilesAsync();
            await LoadCustomPresets();
            await RestoreState();
            ApplySharedConfig();
            _lastCommittedState = Params.Clone();
            UpdateValidationCache();
            if (!string.IsNullOrEmpty(_selectedCity))
            {
                await LoadWeather();
                await RunSimulation();
            }
            _initializing = false;
            StateHasChanged();
        }
        else
        {
            await SaveState();
        }
    }

    private async Task LoadWeather()
    {
        _statusText = "Lade...";
        _statusClass = "text-gray-500";

        List<WeatherDataPoint> data;
        HashSet<int> years;
        string displayName;

        if (_selectedCity == "__geo__" && _geoLatitude.HasValue && _geoLongitude.HasValue)
        {
            (data, years) = await WeatherSvc.FetchOpenMeteoAsync(_geoLatitude.Value, _geoLongitude.Value, _startDate, _endDate);
            displayName = _geoLocationName ?? "Mein Standort";
        }
        else
        {
            var (d, _, y) = await WeatherSvc.LoadPresetWeatherAsync(_selectedCity, _startDate, _endDate);
            data = d;
            years = y;
            var profile = _profiles.Find(p => p.Key == _selectedCity);
            displayName = profile?.Name ?? _selectedCity;
        }

        ApplyWeatherData(data, years, displayName);
    }

    private void ApplyWeatherData(List<WeatherDataPoint> data, HashSet<int> years, string displayName)
    {
        _allWeatherData = data;
        _availableYears = years;

        if (years.Count > 1)
        {
            _selectedYear = years.Max().ToString();
            _weatherData = WeatherDataService.FilterByYear(_allWeatherData, _selectedYear);
            _statusSub = $"Jahr {_selectedYear} ({_weatherData.Count}h)";
        }
        else
        {
            _selectedYear = years.First().ToString();
            _weatherData = data.Take(8760).ToList();
            while (_weatherData.Count < 8760)
                _weatherData.Add(_weatherData[_weatherData.Count % Math.Max(1, _weatherData.Count)]);
            _statusSub = $"Open-Meteo {_startDate:yyyy-MM-dd} – {_endDate:yyyy-MM-dd} ({data.Count}h)";
        }

        _statusText = displayName;
        _statusClass = "text-blue-600";
    }

    private async Task HandleCitySelected(string city)
    {
        _selectedCity = city;
        if (string.IsNullOrEmpty(_selectedCity)) return;
        await LoadWeather();
        await RunSimulation();
    }

    private async Task UseMyLocation()
    {
        _geoLoading = true;
        StateHasChanged();

        try
        {
            var coords = await JS.InvokeAsync<double[]>("blazorGetLocation");
            double lat = coords[0], lon = coords[1];

            string cityName = $"{lat:F2}°N, {lon:F2}°E";
            try
            {
                var reverseUrl = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&format=json&zoom=10&accept-language=de";
                using var request = new HttpRequestMessage(HttpMethod.Get, reverseUrl);
                request.Headers.Add("User-Agent", "WPSimulator/1.0");
                var response = await Http.SendAsync(request);
                var geoJson = await response.Content.ReadAsStringAsync();
                var doc = System.Text.Json.JsonDocument.Parse(geoJson);
                if (doc.RootElement.TryGetProperty("address", out var addr))
                {
                    cityName = addr.TryGetProperty("city", out var city) ? city.GetString()!
                        : addr.TryGetProperty("town", out var town) ? town.GetString()!
                        : addr.TryGetProperty("village", out var village) ? village.GetString()!
                        : cityName;
                }
            }
            catch { /* keep coordinate name */ }

            var (data, years) = await WeatherSvc.FetchOpenMeteoAsync(lat, lon, _startDate, _endDate);

            _geoLocationName = cityName;
            _geoLatitude = lat;
            _geoLongitude = lon;
            _selectedCity = "__geo__";

            ApplyWeatherData(data, years, cityName);
            _statusClass = "text-green-600";

            await RunSimulation();
        }
        catch (Exception ex)
        {
            _statusText = "Standort-Fehler";
            _statusSub = ex.Message.Length > 30 ? ex.Message[..30] + "..." : ex.Message;
            _statusClass = "text-red-600";
        }
        finally
        {
            _geoLoading = false;
        }
    }

    private async Task HandleYearSelected(string year)
    {
        _selectedYear = year;
        await OnYearChanged();
    }

    private async Task OnYearChanged()
    {
        _weatherData = WeatherDataService.FilterByYear(_allWeatherData, _selectedYear);
        _statusSub = _selectedYear == "all"
            ? $"Ø aus {_allWeatherData.Count / 8760} Jahren"
            : $"Jahr {_selectedYear} ({_weatherData.Count}h)";

        if (_resultCache.TryGetValue(_selectedYear, out var cached))
        {
            _result = cached.Result;
            _eval = cached.Eval;
            _monthly = cached.Monthly;
        }
        else
        {
            await RunSimulation();
        }
    }

    private async Task OnCsvUpload(InputFileChangeEventArgs e)
    {
        using var reader = new StreamReader(e.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024));
        var csvText = await reader.ReadToEndAsync();
        var (data, years) = WeatherSvc.ParseCsv(csvText);
        if (data.Count >= 100)
        {
            ApplyWeatherData(data, years, "Upload");
            _statusSub = $"{data.Count} Datenpunkte";
            await RunSimulation();
        }
    }

    private void HandlePresetSelected(string preset)
    {
        _selectedPreset = preset;
        OnPresetChanged();
    }

    private void OnPresetChanged()
    {
        if (_selectedPreset == "custom") return;
        var preset = _presets.Find(p => p.Key == _selectedPreset)
                     ?? _customPresets.Find(p => p.Key == _selectedPreset);
        if (preset == null) return;
        Params.RawPMax = preset.PMax;
        Params.RawPMin = preset.PMin ?? "";
        Params.RawCopData = preset.CopData;
        OnParamChanged();
    }

    private void HandleResetPreset() => _selectedPreset = "custom";

    private void ResetToDefaults()
    {
        CommitUndoEntry(_lastCommittedState);
        Params = new SimulationParameters();
        _lastCommittedState = Params.Clone();
        _selectedPreset = "custom";
        ScheduleDebounce();
    }

    private void CommitUndoEntry(SimulationParameters snapshot)
    {
        if (_undoStack.Count >= MaxUndoSteps)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = Math.Min(items.Length - 1, MaxUndoSteps - 2); i >= 0; i--)
                _undoStack.Push(items[i]);
        }
        _undoStack.Push(snapshot);
        _redoStack.Clear();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        _burstSnapshot = null;
        _redoStack.Push(Params.Clone());
        Params = _undoStack.Pop();
        _lastCommittedState = Params.Clone();
        _selectedPreset = "custom";
        ScheduleDebounce();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;
        _burstSnapshot = null;
        _undoStack.Push(Params.Clone());
        Params = _redoStack.Pop();
        _lastCommittedState = Params.Clone();
        _selectedPreset = "custom";
        ScheduleDebounce();
    }

    private void OnParamChanged()
    {
        _burstSnapshot ??= _lastCommittedState;
        _renderEnabled = false;
        ScheduleRenderUpdate();
        ScheduleDebounce();
    }

    private void ScheduleRenderUpdate()
    {
        _renderTimer?.Dispose();
        _renderTimer = new Timer(_ =>
        {
            InvokeAsync(() =>
            {
                _renderEnabled = true;
                UpdateValidationCache();
                StateHasChanged();
            });
        }, null, 150, Timeout.Infinite);
    }

    private void ScheduleDebounce()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ =>
        {
            InvokeAsync(async () =>
            {
                _renderEnabled = true;
                _renderTimer?.Dispose();
                UpdateValidationCache();
                if (_burstSnapshot != null)
                {
                    CommitUndoEntry(_burstSnapshot);
                    _burstSnapshot = null;
                    _lastCommittedState = Params.Clone();
                }
                if (_weatherData.Count > 0) await RunSimulation();
                else StateHasChanged();
            });
        }, null, 600, Timeout.Infinite);
    }

    private const string CustomPresetsKey = "wp-custom-presets";

    private async Task SaveCustomPreset()
    {
        var name = await JS.InvokeAsync<string?>("prompt", "Name für die Wärmepumpe:");
        if (string.IsNullOrWhiteSpace(name)) return;

        var key = "user_" + Guid.NewGuid().ToString("N")[..8];
        var preset = new HeatPumpPreset
        {
            Key = key,
            Name = name.Trim(),
            Group = "Eigene Modelle",
            PMax = Params.RawPMax,
            PMin = Params.RawPMin,
            CopData = Params.RawCopData
        };
        _customPresets.Add(preset);
        _selectedPreset = key;
        await PersistCustomPresets();
    }

    private async Task RenameCustomPreset()
    {
        var preset = _customPresets.Find(p => p.Key == _selectedPreset);
        if (preset == null) return;
        var name = await JS.InvokeAsync<string?>("prompt", "Neuer Name:", preset.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        preset.Name = name.Trim();
        await PersistCustomPresets();
    }

    private async Task DeleteCustomPreset()
    {
        _customPresets.RemoveAll(p => p.Key == _selectedPreset);
        _selectedPreset = "custom";
        await PersistCustomPresets();
    }

    private async Task PersistCustomPresets()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_customPresets);
        await JS.InvokeVoidAsync("blazorStorage.set", CustomPresetsKey, json);
    }

    private async Task LoadCustomPresets()
    {
        try
        {
            var json = await JS.InvokeAsync<string?>("blazorStorage.get", CustomPresetsKey);
            if (!string.IsNullOrEmpty(json))
                _customPresets = System.Text.Json.JsonSerializer.Deserialize<List<HeatPumpPreset>>(json) ?? [];
        }
        catch { _customPresets = []; }
    }

    private async Task SaveKennfeld()
    {
        var data = new KennfeldData
        {
            PMax = Params.RawPMax,
            PMin = Params.RawPMin,
            CopData = Params.RawCopData
        };
        var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var base64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("eval", $"{{const a=document.createElement('a');a.href='data:application/json;base64,{base64}';a.download='kennfeld.json';a.click();}}");
    }

    private async Task LoadKennfeld(InputFileChangeEventArgs e)
    {
        using var reader = new StreamReader(e.File.OpenReadStream(maxAllowedSize: 1024 * 1024));
        var json = await reader.ReadToEndAsync();
        var data = System.Text.Json.JsonSerializer.Deserialize<KennfeldData>(json);
        if (data != null)
        {
            Params.RawPMax = data.PMax;
            Params.RawPMin = data.PMin;
            Params.RawCopData = data.CopData;
            _selectedPreset = "custom";
        }
    }

    private async Task DownloadCsv(string filename, string csv)
    {
        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv)).ToArray();
        var base64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("eval", $"{{const a=document.createElement('a');a.href='data:text/csv;base64,{base64}';a.download='{filename}';a.click();}}");
    }

    private async Task ExportComparisonCsv()
    {
        if (_comparisonEntries.Count == 0) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Modell;Standort;JAZ;Strom (kWh);Wärme (kWh);Kosten WP (€);Ersparnis (€);Heizstab %;Taktung %;Abtau (h);Defizit (h);Last an NAT (kW);WP-Leistung an NAT (kW)");
        foreach (var e in _comparisonEntries)
        {
            sb.AppendLine($"{e.Name};{e.City};{e.Jaz:F2};{e.TotalElectricity:F0};{e.TotalHeat:F0};{e.CostHeatPump:F0};{e.Savings:F0};{e.HeizstabShare:F1};{e.CyclingPercent:F1};{e.IcingHours};{e.DeficitHours};{e.LoadAtDesignTemp:F1};{e.HeatPumpPowerAtDesignTemp:F1}");
        }
        await DownloadCsv("wp-vergleich.csv", sb.ToString());
    }

    private async Task ExportSummaryCsv()
    {
        if (_result == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Kennzahl;Wert;Einheit");
        sb.AppendLine($"JAZ;{_result.Jaz:F2};");
        sb.AppendLine($"Gesamtstrom;{_result.TotalElectricity:F0};kWh");
        sb.AppendLine($"Gesamtwärme;{_result.TotalHeat:F0};kWh");
        sb.AppendLine($"Heizstab-Anteil;{_result.HeizstabShare:F1};%");
        sb.AppendLine($"Taktung;{_result.CyclingPercent:F1};%");
        sb.AppendLine($"Vereisungsstunden;{_result.IcingHours};h");
        sb.AppendLine($"Defizit-Stunden;{_result.DeficitHours};h");
        sb.AppendLine($"Defizit-Energie;{_result.DeficitKwh:F1};kWh");
        sb.AppendLine($"Kosten WP;{_result.CostHeatPump:F0};€");
        sb.AppendLine($"Kosten Alt;{_result.CostOldHeating:F0};€");
        sb.AppendLine($"Ersparnis;{_result.Savings:F0};€");
        sb.AppendLine($"Last an NAT;{_result.LoadAtDesignTemp:F1};kW");
        sb.AppendLine($"WP-Leistung an NAT;{_result.HeatPumpPowerAtDesignTemp:F1};kW");
        sb.AppendLine($"Bivalenztemperatur;{(_result.BivalenceTemperature.HasValue ? _result.BivalenceTemperature.Value.ToString("F1") : "-")};°C");
        sb.AppendLine();
        sb.AppendLine("Parameter;Wert");
        sb.AppendLine($"Jahresverbrauch;{Params.Jahresverbrauch} kWh");
        sb.AppendLine($"Wirkungsgrad;{Params.Wirkungsgrad}");
        sb.AppendLine($"WW-Anteil;{Params.WarmwasserAnteil}%");
        sb.AppendLine($"Heizgrenze;{Params.Heizgrenze}°C");
        sb.AppendLine($"NAT;{Params.NormAussentemperatur}°C");
        sb.AppendLine($"VL Max;{Params.VorlaufMax}°C");
        sb.AppendLine($"VL Min;{Params.VorlaufMin}°C");
        sb.AppendLine($"Strompreis;{Params.PreisStrom} €/kWh");
        if (_monthly != null)
        {
            sb.AppendLine();
            sb.AppendLine("Monat;Wärme (kWh);Strom (kWh);JAZ;Kosten (€);Vereisungsstunden");
            foreach (var m in _monthly)
                sb.AppendLine($"{m.Name};{m.Waerme:F0};{m.Strom:F0};{m.Jaz:F2};{m.Kosten:F0};{m.IcingCount}");
        }
        await DownloadCsv("wp-zusammenfassung.csv", sb.ToString());
    }

    private async Task ExportHourlyCsv()
    {
        if (_result == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Stunde;Temperatur (°C);Feuchte (%);Taupunkt (°C);Heizlast (kW);COP;Thermisch (kW);Elektrisch (kW);Heizstab (kW);Defizit (kW);Vereisung;Taktung;PMax (kW);PMin (kW)");
        for (int i = 0; i < _result.Temperature.Length; i++)
        {
            sb.AppendLine($"{i + 1};{_result.Temperature[i]:F1};{_result.RelativeHumidity[i]:F0};{_result.DewPoint[i]:F1};{_result.Load[i]:F2};{_result.Cop[i]:F2};{_result.ThermalPower[i]:F2};{_result.ElectricalPower[i]:F2};{_result.HeizstabPower[i]:F2};{_result.Deficit[i]:F2};{_result.Icing[i]};{_result.Cycling[i]};{_result.MaxPowerAvailable[i]:F2};{_result.MinPowerAvailable[i]:F2}");
        }
        await DownloadCsv("wp-stundenwerte.csv", sb.ToString());
    }

    private async Task PrintPage()
    {
        await JS.InvokeVoidAsync("window.print");
    }

    private bool HasValidationErrors() => _hasValidationErrors;

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;

    private void UpdateValidationCache()
    {
        _vJahresverbrauch = Params.Jahresverbrauch <= 0;
        _vWirkungsgrad = Params.Wirkungsgrad is <= 0 or > 1.0;
        _vWwAnteil = Params.WarmwasserAnteil is < 0 or > 100;
        _vHeizgrenze = Params.Heizgrenze <= Params.NormAussentemperatur;
        _vNat = Params.NormAussentemperatur >= Params.Heizgrenze;
        _wRaumSoll = Params.RaumSollTemperatur is < 15 or > 30;
        _vPreisStrom = Params.PreisStrom <= 0;
        _vPreisAlt = Params.PreisAlt <= 0;
        _vVlMax = Params.VorlaufMax <= Params.VorlaufMin;
        _wVlMax = !_vVlMax && Params.VorlaufMax > 60;
        _vVlMin = Params.VorlaufMin >= Params.VorlaufMax;
        _wWwTemp = Params.WarmwasserTemp is < 30 or > 70;
        _vHeizstab = Params.HeizstabMax < 0;

        var pMaxPoints = Helpers.MathHelpers.ParseTextAreaPoints(Params.RawPMax);
        var pMinPoints = Helpers.MathHelpers.ParseTextAreaPoints(Params.RawPMin);
        var copPoints = Helpers.MathHelpers.ParseCopData(Params.RawCopData);

        _vPMax = pMaxPoints.Count < 2;
        _vCop = copPoints.Count < 2;

        // Warnings for parseable but questionable Kennfeld values
        _wPMaxNegative = !_vPMax && pMaxPoints.Any(p => p[1] <= 0);
        _wCopRange = !_vCop && copPoints.Any(p => p[2] <= 0 || p[2] > 15);
        _wPMinExceedsPMax = false;
        if (!_vPMax && pMinPoints.Count >= 2)
        {
            var maxTemps = pMaxPoints.Select(p => p[0]).ToArray();
            var maxVals = pMaxPoints.Select(p => p[1]).ToArray();
            _wPMinExceedsPMax = pMinPoints.Any(p => p[1] > Helpers.MathHelpers.Interp(p[0], maxTemps, maxVals));
        }

        _hasValidationErrors = _vJahresverbrauch || _vWirkungsgrad || _vWwAnteil || _vHeizgrenze ||
            _vVlMax || _vPreisStrom || _vPreisAlt || _vHeizstab || _vPMax || _vCop;
    }

    private async Task RunSimulation()
    {
        UpdateValidationCache();
        if (_weatherData.Count == 0 || _hasValidationErrors) return;

        if (_simulating)
        {
            _simulationDirty = true;
            return;
        }

        _precalcCts?.Cancel();
        _simulating = true;
        _simulationDirty = false;
        try
        {
            _resultCache.Clear();
            StateHasChanged();
            await Task.Delay(1);
            _result = SimulationEngine.Run(Params, _weatherData);
            _eval = EvaluationService.Evaluate(_result);
            _monthly = EvaluationService.CalcMonthly(_result, Params.PreisStrom);
            _resultCache[_selectedYear] = (_result, _eval, _monthly);
            _simulating = false;

            if (_simulationDirty)
            {
                _simulationDirty = false;
                await RunSimulation();
                return;
            }

            StateHasChanged();
            await SaveState();
            await PrecalculateOtherYears();
        }
        catch (Exception ex)
        {
            _simulating = false;
            _simulationDirty = false;
            _statusText = "Fehler";
            _statusSub = ex.Message.Length > 40 ? ex.Message[..40] + "…" : ex.Message;
            _statusClass = "text-red-600";
            StateHasChanged();
        }
    }

    private async Task PrecalculateOtherYears()
    {
        _precalcCts = new CancellationTokenSource();
        var ct = _precalcCts.Token;

        var keys = new List<string>();
        foreach (var y in _availableYears)
            keys.Add(y.ToString());
        if (_availableYears.Count > 1)
            keys.Add("all");

        foreach (var key in keys)
        {
            if (ct.IsCancellationRequested) return;
            if (_resultCache.ContainsKey(key)) continue;
            var weatherData = WeatherDataService.FilterByYear(_allWeatherData, key);
            if (weatherData.Count == 0) continue;
            // Task.Delay(1) uses setTimeout — yields to browser's macrotask queue
            // so it can paint and process input between each year's simulation
            await Task.Delay(1);
            if (ct.IsCancellationRequested) return;
            var result = SimulationEngine.Run(Params, weatherData);
            var eval = EvaluationService.Evaluate(result);
            var monthly = EvaluationService.CalcMonthly(result, Params.PreisStrom);
            if (ct.IsCancellationRequested) return;
            _resultCache[key] = (result, eval, monthly);
        }
    }

    private async Task AddToComparison()
    {
        if (_result == null) return;

        var presetName = _selectedPreset != "custom"
            ? _presets.Find(p => p.Key == _selectedPreset)?.Name ?? "Benutzerdefiniert"
            : "Benutzerdefiniert";

        var cityName = _selectedCity == "__geo__"
            ? _geoLocationName ?? "Mein Standort"
            : _profiles.Find(p => p.Key == _selectedCity)?.Name ?? _selectedCity;

        _comparisonEntries.Add(new ComparisonEntry
        {
            Name = presetName,
            City = cityName,
            Jaz = _result.Jaz,
            TotalElectricity = _result.TotalElectricity,
            TotalHeat = _result.TotalHeat,
            HeizstabShare = _result.HeizstabShare,
            CostHeatPump = _result.CostHeatPump,
            Savings = _result.Savings,
            CyclingPercent = _result.CyclingPercent,
            IcingHours = _result.IcingHours,
            DeficitHours = _result.DeficitHours,
            LoadAtDesignTemp = _result.LoadAtDesignTemp,
            HeatPumpPowerAtDesignTemp = _result.HeatPumpPowerAtDesignTemp
        });
        await SaveState();
    }

    private async Task RemoveComparison(int index)
    {
        if (index >= 0 && index < _comparisonEntries.Count)
        {
            _comparisonEntries.RemoveAt(index);
            await SaveState();
        }
    }

    private async Task ClearComparison()
    {
        _comparisonEntries.Clear();
        await SaveState();
    }

    private const string StateKey = "wp-sim-state";

    private async Task SaveState()
    {
        try
        {
            var state = new AppState
            {
                Params = Params,
                SelectedCity = _selectedCity,
                SelectedYear = _selectedYear,
                SelectedPreset = _selectedPreset,
                StartDate = _startDate,
                EndDate = _endDate,
                GeoLocationName = _geoLocationName,
                GeoLatitude = _geoLatitude,
                GeoLongitude = _geoLongitude,
                ComparisonEntries = _comparisonEntries
            };
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            await JS.InvokeVoidAsync("blazorStorage.set", StateKey, json);
        }
        catch { /* localStorage may be unavailable */ }
    }

    private async Task RestoreState()
    {
        try
        {
            var json = await JS.InvokeAsync<string?>("blazorStorage.get", StateKey);
            if (string.IsNullOrEmpty(json)) return;

            var state = System.Text.Json.JsonSerializer.Deserialize<AppState>(json);
            if (state == null) return;

            Params = state.Params;
            _selectedCity = state.SelectedCity;
            _selectedYear = state.SelectedYear;
            _selectedPreset = state.SelectedPreset;
            _startDate = state.StartDate;
            _endDate = state.EndDate;
            _geoLocationName = state.GeoLocationName;
            _geoLatitude = state.GeoLatitude;
            _geoLongitude = state.GeoLongitude;
            _comparisonEntries = state.ComparisonEntries ?? [];
        }
        catch { /* ignore corrupt/missing state */ }
    }

    private async Task ShareConfig()
    {
        var p = Params;
        var d = new SimulationParameters();
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var q = new List<string>();

        void Add(string key, double val, double def) { if (Math.Abs(val - def) > 0.0001) q.Add($"{key}={val.ToString(ic)}"); }
        void AddStr(string key, string val, string def) { if (val != def) q.Add($"{key}={Uri.EscapeDataString(val)}"); }
        void AddBool(string key, bool val, bool def) { if (val != def) q.Add($"{key}={(val ? "1" : "0")}"); }
        void AddInt(string key, int val, int def) { if (val != def) q.Add($"{key}={val}"); }

        Add("jv", p.Jahresverbrauch, d.Jahresverbrauch);
        Add("wg", p.Wirkungsgrad, d.Wirkungsgrad);
        Add("wwa", p.WarmwasserAnteil, d.WarmwasserAnteil);
        Add("hg", p.Heizgrenze, d.Heizgrenze);
        Add("nat", p.NormAussentemperatur, d.NormAussentemperatur);
        Add("rst", p.RaumSollTemperatur, d.RaumSollTemperatur);
        Add("ps", p.PreisStrom, d.PreisStrom);
        Add("pa", p.PreisAlt, d.PreisAlt);
        Add("vlmax", p.VorlaufMax, d.VorlaufMax);
        Add("vlmin", p.VorlaufMin, d.VorlaufMin);
        Add("wwt", p.WarmwasserTemp, d.WarmwasserTemp);
        Add("hsm", p.HeizstabMax, d.HeizstabMax);
        AddBool("naa", p.NachtabsenkungAktiv, d.NachtabsenkungAktiv);
        AddInt("ns", p.NachtStart, d.NachtStart);
        AddInt("ne", p.NachtEnde, d.NachtEnde);
        Add("ndt", p.NachtDeltaT, d.NachtDeltaT);
        AddStr("pmax", p.RawPMax, d.RawPMax);
        AddStr("pmin", p.RawPMin, d.RawPMin);
        AddStr("cop", p.RawCopData, d.RawCopData);
        AddStr("city", _selectedCity, "");
        AddStr("preset", _selectedPreset, "custom");

        if (_geoLocationName != null) q.Add($"gn={Uri.EscapeDataString(_geoLocationName)}");
        if (_geoLatitude.HasValue) q.Add($"glat={_geoLatitude.Value.ToString(ic)}");
        if (_geoLongitude.HasValue) q.Add($"glon={_geoLongitude.Value.ToString(ic)}");

        var url = Nav.BaseUri + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", url);
        _shareCopied = true;
        StateHasChanged();
        await Task.Delay(2000);
        _shareCopied = false;
        StateHasChanged();
    }

    private void ApplySharedConfig()
    {
        try
        {
            var uri = new Uri(Nav.Uri);
            var query = uri.Query;
            if (string.IsNullOrEmpty(query)) return;

            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var pars = System.Web.HttpUtility.ParseQueryString(query);
            if (pars.Count == 0) return;

            double D(string key, double def) => pars[key] is string v && double.TryParse(v, ic, out var r) ? r : def;
            int I(string key, int def) => pars[key] is string v && int.TryParse(v, out var r) ? r : def;
            string S(string key, string def) => pars[key] ?? def;
            bool B(string key, bool def) => pars[key] is string v ? v == "1" : def;

            Params.Jahresverbrauch = D("jv", Params.Jahresverbrauch);
            Params.Wirkungsgrad = D("wg", Params.Wirkungsgrad);
            Params.WarmwasserAnteil = D("wwa", Params.WarmwasserAnteil);
            Params.Heizgrenze = D("hg", Params.Heizgrenze);
            Params.NormAussentemperatur = D("nat", Params.NormAussentemperatur);
            Params.RaumSollTemperatur = D("rst", Params.RaumSollTemperatur);
            Params.PreisStrom = D("ps", Params.PreisStrom);
            Params.PreisAlt = D("pa", Params.PreisAlt);
            Params.VorlaufMax = D("vlmax", Params.VorlaufMax);
            Params.VorlaufMin = D("vlmin", Params.VorlaufMin);
            Params.WarmwasserTemp = D("wwt", Params.WarmwasserTemp);
            Params.HeizstabMax = D("hsm", Params.HeizstabMax);
            Params.NachtabsenkungAktiv = B("naa", Params.NachtabsenkungAktiv);
            Params.NachtStart = I("ns", Params.NachtStart);
            Params.NachtEnde = I("ne", Params.NachtEnde);
            Params.NachtDeltaT = D("ndt", Params.NachtDeltaT);
            Params.RawPMax = S("pmax", Params.RawPMax);
            Params.RawPMin = S("pmin", Params.RawPMin);
            Params.RawCopData = S("cop", Params.RawCopData);

            var city = S("city", "");
            if (!string.IsNullOrEmpty(city)) _selectedCity = city;
            var preset = S("preset", "");
            if (!string.IsNullOrEmpty(preset)) _selectedPreset = preset;

            _geoLocationName = pars["gn"];
            if (pars["glat"] is string lat && double.TryParse(lat, ic, out var latV)) _geoLatitude = latV;
            if (pars["glon"] is string lon && double.TryParse(lon, ic, out var lonV)) _geoLongitude = lonV;
        }
        catch { /* ignore invalid share data */ }
    }

    private static string GetBgClass(string c) => c switch { "red" => "bg-red-50", "orange" => "bg-orange-50", "yellow" => "bg-yellow-50", "green" => "bg-green-50", _ => "bg-blue-50" };
    private static string GetBorderClass(string c) => c switch { "red" => "border-red-200", "orange" => "border-orange-200", "yellow" => "border-yellow-200", "green" => "border-green-200", _ => "border-blue-200" };
    private static string GetTextClass(string c) => c switch { "red" => "text-red-600", "orange" => "text-orange-600", "yellow" => "text-yellow-600", "green" => "text-green-600", _ => "text-blue-600" };
}
