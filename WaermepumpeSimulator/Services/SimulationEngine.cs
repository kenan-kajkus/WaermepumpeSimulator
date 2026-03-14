using WaermepumpeSimulator.Helpers;
using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public class SimulationEngine
{
    private const int HoursPerYear = 8760;
    private const double VorlaufLow = 35.0;
    private const double VorlaufHigh = 55.0;
    private const double P55ScalingFactor = 0.92;

    public SimulationResult Run(SimulationParameters parameters, List<WeatherDataPoint> weather)
    {
        ValidateInputs(parameters, weather);

        var lookupTemps = BuildLookupTable();
        var kennfeld = ParseKennfeld(parameters, lookupTemps);
        var loadProfile = CalcLoadProfile(parameters, weather);
        var result = InitResult(lookupTemps, kennfeld);

        RunHourlySimulation(parameters, weather, lookupTemps, kennfeld, loadProfile, result);
        CalcAggregates(result);
        CalcDesignPoint(parameters, lookupTemps, kennfeld, loadProfile, result);
        CalcCosts(parameters, result);

        return result;
    }

    private static void ValidateInputs(SimulationParameters parameters, List<WeatherDataPoint> weather)
    {
        if (weather.Count < HoursPerYear)
            throw new ArgumentException($"Wetterdaten unvollständig: {weather.Count} von {HoursPerYear} Stunden vorhanden.");

        var pMax = MathHelpers.ParseTextAreaPoints(parameters.RawPMax);
        if (pMax.Count < 2)
            throw new ArgumentException("Kennfeld PMax: Mindestens 2 Datenpunkte erforderlich.");
        if (pMax.Any(p => p[1] <= 0))
            throw new ArgumentException("Kennfeld PMax: Alle Leistungswerte müssen > 0 sein.");

        var cop = MathHelpers.ParseCopData(parameters.RawCopData);
        if (cop.Count < 2)
            throw new ArgumentException("Kennfeld COP: Mindestens 2 Datenpunkte erforderlich.");
        if (cop.Any(p => p[2] <= 0))
            throw new ArgumentException("Kennfeld COP: Alle COP-Werte müssen > 0 sein.");
    }

    // --- Lookup Table ---

    private static double[] BuildLookupTable()
    {
        var temperatures = new List<double>();
        for (double t = -25; t <= 40; t += 0.5) temperatures.Add(t);
        return temperatures.ToArray();
    }

    // --- Kennfeld (characteristic map) ---

    private record KennfeldCurves(
        double[] PMax35, double[] PMax55,
        double[] PMin35, double[] PMin55,
        double[] PMaxCustom,
        double[] Cop35, double[] Cop55,
        double[] Eta35, double[] Eta55,
        List<double[]> RawCopPoints);

    private static KennfeldCurves ParseKennfeld(SimulationParameters parameters, double[] lookupTemps)
    {
        var rawPowerMax = MathHelpers.ParseTextAreaPoints(parameters.RawPMax);
        var rawPowerMin = MathHelpers.ParseTextAreaPoints(parameters.RawPMin);
        var rawCopData = MathHelpers.ParseCopData(parameters.RawCopData);

        double[] powerMaxTemps = rawPowerMax.Select(point => point[0]).ToArray();
        double[] powerMaxValues = rawPowerMax.Select(point => point[1]).ToArray();

        var pMax35 = lookupTemps.Select(temp => MathHelpers.Interp(temp, powerMaxTemps, powerMaxValues)).ToArray();
        var pMax55 = pMax35.Select(value => value * P55ScalingFactor).ToArray();

        var pMin35 = rawPowerMin.Count > 0
            ? lookupTemps.Select(temp => MathHelpers.Interp(temp,
                rawPowerMin.Select(point => point[0]).ToArray(),
                rawPowerMin.Select(point => point[1]).ToArray())).ToArray()
            : pMax35.Select(value => value * 0.25).ToArray();
        var pMin55 = pMin35.Select(value => value * P55ScalingFactor).ToArray();

        var etaPoints35 = ExtractEtaPoints(rawCopData, VorlaufLow);
        var etaPoints55 = ExtractEtaPoints(rawCopData, VorlaufHigh);

        var (cop35, eta35) = CalcCopCurve(lookupTemps, VorlaufLow, etaPoints35);
        var (cop55, eta55) = CalcCopCurve(lookupTemps, VorlaufHigh, etaPoints55);

        var pMaxCustom = CalcVorlaufAdjustedCurve(lookupTemps, pMax35, pMax55, parameters);

        return new KennfeldCurves(pMax35, pMax55, pMin35, pMin55, pMaxCustom,
            cop35, cop55, eta35, eta55, rawCopData);
    }

    private static List<double[]> ExtractEtaPoints(List<double[]> copData, double targetVorlauf)
    {
        return copData
            .Where(point => Math.Abs(point[0] - targetVorlauf) < 5)
            .Select(point => new[] { point[1], point[2] / MathHelpers.GetCarnotCop(point[1], point[0]) })
            .OrderBy(point => point[0])
            .ToList();
    }

    private static double[] CalcVorlaufAdjustedCurve(double[] lookupTemps, double[] vals35, double[] vals55, SimulationParameters parameters)
    {
        var result = new double[lookupTemps.Length];
        for (int i = 0; i < lookupTemps.Length; i++)
        {
            double vorlauf = CalcVorlauf(lookupTemps[i], parameters);
            double vorlaufBlend = VorlaufFactor(vorlauf);
            result[i] = Lerp(vals35[i], vals55[i], vorlaufBlend);
        }
        return result;
    }

    // --- Load Profile ---

    private record LoadProfile(double LoadPerKelvin, double WarmwaterPerHour);

    private static LoadProfile CalcLoadProfile(SimulationParameters parameters, List<WeatherDataPoint> weather)
    {
        double sumDeltaT = 0;
        foreach (var weatherPoint in weather)
            if (weatherPoint.Temperature < parameters.Heizgrenze)
                sumDeltaT += parameters.Heizgrenze - weatherPoint.Temperature;

        double heatingShare = 1.0 - parameters.WarmwasserAnteil / 100.0;
        double totalUsableHeat = parameters.Jahresverbrauch * parameters.Wirkungsgrad;

        double loadPerKelvin = sumDeltaT > 0 ? (totalUsableHeat * heatingShare) / sumDeltaT : 0;
        double warmwaterPerHour = (totalUsableHeat * (parameters.WarmwasserAnteil / 100.0)) / HoursPerYear;

        return new LoadProfile(loadPerKelvin, warmwaterPerHour);
    }

    // --- Hourly Simulation ---

    private static void RunHourlySimulation(
        SimulationParameters parameters, List<WeatherDataPoint> weather,
        double[] lookupTemps, KennfeldCurves kennfeld, LoadProfile load, SimulationResult result)
    {
        int icingHoursTotal = 0, cyclingHoursTotal = 0, heatingHoursTotal = 0;
        double smoothedOutsideTemp = 5.0;

        for (int hour = 0; hour < HoursPerYear; hour++)
        {
            var weatherPoint = weather[hour];
            double outsideTemp = weatherPoint.Temperature;
            double humidity = weatherPoint.RelativeHumidity;
            double dewPoint = MathHelpers.CalculateDewPoint(outsideTemp, humidity);
            int hourOfDay = hour % 24;
            smoothedOutsideTemp = smoothedOutsideTemp * 0.96 + outsideTemp * 0.04;

            // Heating load & vorlauf for this hour
            var (heatingLoad, vorlauf) = CalcHourlyLoad(parameters, outsideTemp, smoothedOutsideTemp, hourOfDay, load);
            double totalLoad = heatingLoad + load.WarmwaterPerHour;

            // Available power at current conditions
            double vorlaufBlend = VorlaufFactor(vorlauf);
            double maxPowerAvail = LerpInterp(outsideTemp, lookupTemps, kennfeld.PMax35, kennfeld.PMax55, vorlaufBlend);
            double minPowerAvail = LerpInterp(outsideTemp, lookupTemps, kennfeld.PMin35, kennfeld.PMin55, vorlaufBlend);

            // Icing detection
            var (isIcing, icingPenalty) = DetectIcing(outsideTemp, humidity, dewPoint, totalLoad, minPowerAvail, maxPowerAvail);
            if (isIcing) icingHoursTotal++;

            // Cycling detection
            bool isHeating = heatingLoad > 0.1;
            bool isCycling = isHeating && totalLoad < minPowerAvail;
            if (isHeating) heatingHoursTotal++;
            if (isCycling) cyclingHoursTotal++;

            // COP at current conditions
            double cop = LerpInterp(outsideTemp, lookupTemps, kennfeld.Cop35, kennfeld.Cop55, vorlaufBlend) * icingPenalty;

            // Power balance
            var (thermal, electrical, heizstab, deficit) = CalcPowerBalance(totalLoad, maxPowerAvail, cop, parameters.HeizstabMax);

            // Store hourly results
            result.Temperature[hour] = outsideTemp;
            result.RelativeHumidity[hour] = humidity;
            result.DewPoint[hour] = dewPoint;
            result.Load[hour] = totalLoad;
            result.Cop[hour] = cop;
            result.ThermalPower[hour] = thermal;
            result.ElectricalPower[hour] = electrical;
            result.HeizstabPower[hour] = heizstab;
            result.Deficit[hour] = deficit;
            result.Icing[hour] = isIcing ? 1 : 0;
            result.Cycling[hour] = isCycling ? 1 : 0;
            result.MaxPowerAvailable[hour] = maxPowerAvail;
            result.MinPowerAvailable[hour] = minPowerAvail;
        }

        result.IcingHours = icingHoursTotal;
        result.CyclingPercent = heatingHoursTotal > 0 ? (cyclingHoursTotal / (double)heatingHoursTotal) * 100 : 0;
    }

    private static (double heatingLoad, double vorlauf) CalcHourlyLoad(
        SimulationParameters parameters, double outsideTemp, double smoothedTemp, int hourOfDay, LoadProfile load)
    {
        if (smoothedTemp >= parameters.Heizgrenze)
            return (0, parameters.WarmwasserTemp);

        double heatingLoad = Math.Max(0, (parameters.Heizgrenze - outsideTemp) * load.LoadPerKelvin);
        double heatingCurveSlope = (parameters.VorlaufMax - parameters.VorlaufMin) / (parameters.Heizgrenze - parameters.NormAussentemperatur);
        double vorlauf = parameters.VorlaufMin + heatingCurveSlope * (parameters.Heizgrenze - outsideTemp);

        if (parameters.NachtabsenkungAktiv && IsNightHour(hourOfDay, parameters.NachtStart, parameters.NachtEnde))
        {
            double roomReduction = Math.Max(0.0, (parameters.RaumSollTemperatur - parameters.NachtDeltaT) - outsideTemp)
                                   / Math.Max(0.1, parameters.RaumSollTemperatur - outsideTemp);
            heatingLoad *= roomReduction;
            vorlauf -= heatingCurveSlope * parameters.NachtDeltaT * 1.5;
        }

        vorlauf = Math.Clamp(vorlauf, parameters.VorlaufMin, parameters.VorlaufMax);
        return (heatingLoad, vorlauf);
    }

    private static (bool isIcing, double penalty) DetectIcing(
        double outsideTemp, double humidity, double dewPoint,
        double totalLoad, double minPowerAvail, double maxPowerAvail)
    {
        if (totalLoad <= minPowerAvail * 1.2)
            return (false, 1.0);

        double loadFactor = maxPowerAvail > 0 ? Math.Min(1.0, totalLoad / maxPowerAvail) : 1.0;
        double evaporatorTemp = outsideTemp - (0.5 + 3.0 * loadFactor);

        bool isIcing = evaporatorTemp < -0.5
                       && evaporatorTemp < dewPoint
                       && humidity > 88
                       && outsideTemp is >= -4 and <= 3;

        double penalty = isIcing ? 1.0 - 0.15 * loadFactor : 1.0;
        return (isIcing, penalty);
    }

    private static (double thermal, double electrical, double heizstab, double deficit) CalcPowerBalance(
        double totalLoad, double maxPowerAvail, double cop, double heizstabMax)
    {
        if (totalLoad <= 0.001)
            return (0, 0, 0, 0);

        if (maxPowerAvail >= totalLoad)
            return (totalLoad, totalLoad / cop, 0, 0);

        double thermal = maxPowerAvail;
        double electrical = maxPowerAvail / cop;
        double gap = totalLoad - maxPowerAvail;
        double heizstab = Math.Min(gap, heizstabMax);
        double deficit = gap - heizstab;
        return (thermal, electrical, heizstab, deficit);
    }

    // --- Aggregation ---

    private static void CalcAggregates(SimulationResult result)
    {
        double totalThermal = result.ThermalPower.Sum() + result.HeizstabPower.Sum();
        double totalElectrical = result.ElectricalPower.Sum() + result.HeizstabPower.Sum();
        double totalHeizstab = result.HeizstabPower.Sum();

        result.Jaz = totalElectrical > 0 ? totalThermal / totalElectrical : 0;
        result.TotalElectricity = totalElectrical;
        result.TotalHeat = totalThermal;
        result.HeizstabShare = totalThermal > 0 ? (totalHeizstab / totalThermal) * 100 : 0;
        result.DeficitHours = result.Deficit.Count(d => d > 0.1);
        result.DeficitKwh = result.Deficit.Sum();
    }

    // --- Design Point (Auslegung) ---

    private static void CalcDesignPoint(
        SimulationParameters parameters, double[] lookupTemps, KennfeldCurves kennfeld, LoadProfile load, SimulationResult result)
    {
        double loadAtDesign = (parameters.Heizgrenze - parameters.NormAussentemperatur) * load.LoadPerKelvin + load.WarmwaterPerHour;
        double vorlaufBlendAtDesign = VorlaufFactor(parameters.VorlaufMax);
        double heatPumpPowerAtDesign = LerpInterp(parameters.NormAussentemperatur, lookupTemps, kennfeld.PMax35, kennfeld.PMax55, vorlaufBlendAtDesign);

        result.LoadAtDesignTemp = loadAtDesign;
        result.HeatPumpPowerAtDesignTemp = heatPumpPowerAtDesign;
        result.DesignTemperature = parameters.NormAussentemperatur;
        result.HeatingLimitTemperature = parameters.Heizgrenze;
        result.WarmwaterBaseLoad = load.WarmwaterPerHour;

        // Bivalence point: where load exceeds WP capacity
        for (double temp = parameters.Heizgrenze; temp >= -25; temp -= 0.1)
        {
            double currentLoad = (parameters.Heizgrenze - temp) * load.LoadPerKelvin + load.WarmwaterPerHour;
            int idx = Array.FindIndex(lookupTemps, val => val >= temp);
            if (idx == -1) idx = 0;
            if (currentLoad > kennfeld.PMaxCustom[idx])
            {
                result.BivalenceTemperature = Math.Round(temp, 1);
                result.BivalencePower = currentLoad;
                break;
            }
        }
    }

    // --- Costs ---

    private static void CalcCosts(SimulationParameters parameters, SimulationResult result)
    {
        result.CostHeatPump = result.TotalElectricity * parameters.PreisStrom;
        result.CostOldHeating = parameters.Jahresverbrauch * parameters.PreisAlt;
        result.Savings = result.CostOldHeating - result.CostHeatPump;
    }

    // --- COP Curve Calculation ---

    private static (double[] cop, double[] eta) CalcCopCurve(double[] lookupTemps, double flowTemp, List<double[]> etaPoints)
    {
        double maxCop = MathHelpers.GetMaxCop(flowTemp);
        var cop = new double[lookupTemps.Length];
        var eta = new double[lookupTemps.Length];

        for (int i = 0; i < lookupTemps.Length; i++)
        {
            double sourceTemp = lookupTemps[i];
            double sourceTempClamped = Math.Min(sourceTemp, 15.0);
            double rawEta = MathHelpers.GetFlatEta(sourceTempClamped, etaPoints);
            cop[i] = Math.Clamp(rawEta * MathHelpers.GetCarnotCop(sourceTempClamped, flowTemp), 1.0, maxCop);
            eta[i] = cop[i] / MathHelpers.GetCarnotCop(sourceTemp, flowTemp);
        }

        return (cop, eta);
    }

    // --- Helpers ---

    private static SimulationResult InitResult(double[] lookupTemps, KennfeldCurves kennfeld)
    {
        return new SimulationResult
        {
            Temperature = new double[HoursPerYear],
            RelativeHumidity = new double[HoursPerYear],
            DewPoint = new double[HoursPerYear],
            Load = new double[HoursPerYear],
            Cop = new double[HoursPerYear],
            ThermalPower = new double[HoursPerYear],
            ElectricalPower = new double[HoursPerYear],
            HeizstabPower = new double[HoursPerYear],
            Deficit = new double[HoursPerYear],
            Icing = new int[HoursPerYear],
            Cycling = new int[HoursPerYear],
            MaxPowerAvailable = new double[HoursPerYear],
            MinPowerAvailable = new double[HoursPerYear],
            LookupTemperatures = lookupTemps,
            PowerMaxAtVL35 = kennfeld.PMax35,
            PowerMaxAtVL55 = kennfeld.PMax55,
            PowerMinCurve = kennfeld.PMin35,
            PowerMaxAdjusted = kennfeld.PMaxCustom,
            CopAtVL35 = kennfeld.Cop35,
            CopAtVL55 = kennfeld.Cop55,
            EtaAtVL35 = kennfeld.Eta35,
            EtaAtVL55 = kennfeld.Eta55,
            RawCopPoints = kennfeld.RawCopPoints,
        };
    }

    private static double CalcVorlauf(double outsideTemp, SimulationParameters parameters)
    {
        if (outsideTemp >= parameters.Heizgrenze)
            return parameters.VorlaufMin;
        double slope = (parameters.VorlaufMax - parameters.VorlaufMin) / (parameters.Heizgrenze - parameters.NormAussentemperatur);
        return Math.Clamp(parameters.VorlaufMin + slope * (parameters.Heizgrenze - outsideTemp), parameters.VorlaufMin, parameters.VorlaufMax);
    }

    private static double VorlaufFactor(double vorlauf)
        => vorlauf <= VorlaufLow ? 0 : vorlauf >= VorlaufHigh ? 1 : (vorlauf - VorlaufLow) / (VorlaufHigh - VorlaufLow);

    private static double Lerp(double valueA, double valueB, double blend) => valueA + blend * (valueB - valueA);

    private static double LerpInterp(double outsideTemp, double[] lookupTemps, double[] vals35, double[] vals55, double vorlaufBlend)
        => Lerp(MathHelpers.Interp(outsideTemp, lookupTemps, vals35), MathHelpers.Interp(outsideTemp, lookupTemps, vals55), vorlaufBlend);

    private static bool IsNightHour(int hourOfDay, int nightStart, int nightEnd)
        => nightStart > nightEnd
            ? (hourOfDay >= nightStart || hourOfDay < nightEnd)
            : (hourOfDay >= nightStart && hourOfDay < nightEnd);
}
