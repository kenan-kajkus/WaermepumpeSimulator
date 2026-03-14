using WaermepumpeSimulator.Helpers;
using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public static class SimulationEngine
{
    private const int HoursPerYear = 8760;

    // --- Kennfeld interpolation ---
    private const double VorlaufLow = 35.0;                // Lower reference flow temperature for COP/power blending (°C)
    private const double VorlaufHigh = 55.0;               // Upper reference flow temperature for COP/power blending (°C)
    private const double P55ScalingFactor = 0.92;           // Power derating at VL55 vs VL35 (typical ~8% loss)
    private const double DefaultPMinFraction = 0.25;        // PMin as fraction of PMax when no PMin data provided
    private const double SourceTempClamp = 15.0;            // Clamp source temp for COP curve to avoid extrapolation (°C)
    private const double EtaMatchRadius = 5.0;              // Max VL deviation to match COP data points to reference VL (K)

    // --- Temperature smoothing ---
    private const double SmoothingRetain = 0.96;            // Weight of previous smoothed temp (low-pass filter)
    private const double SmoothingNew = 0.04;               // Weight of current hour temp (= 1 - SmoothingRetain)
    private const double InitialSmoothedTemp = 5.0;         // Starting value for smoothed outside temperature (°C)

    // --- Icing model thresholds ---
    private const double IcingEvapTempThreshold = -0.5;     // Evaporator temp must be below this for icing (°C)
    private const double IcingHumidityThreshold = 88.0;     // Min relative humidity for icing risk (%)
    private const double IcingOutsideTempMin = -4.0;        // Outside temp lower bound for icing range (°C)
    private const double IcingOutsideTempMax = 3.0;         // Outside temp upper bound for icing range (°C)
    private const double IcingEvapBaseOffset = 0.5;         // Base evaporator-to-ambient offset (K)
    private const double IcingEvapLoadFactor = 3.0;         // Additional offset per unit load factor (K)
    private const double IcingCopPenalty = 0.15;            // Max COP reduction at full load during icing (15%)
    private const double IcingLoadThreshold = 1.2;          // Icing only checked when load > PMin × this factor

    // --- Night setback ---
    private const double NightSetbackVorlaufFactor = 1.5;   // Vorlauf reduction multiplier during night setback

    // --- Thresholds ---
    private const double MinHeatingLoad = 0.1;              // Minimum load to count as "heating active" (kW)
    private const double MinPowerBalance = 0.001;           // Below this load, power balance is zero (kW)

    public static SimulationResult Run(SimulationParameters parameters, List<WeatherDataPoint> weather)
    {
        var parsed = ParseAndValidateInputs(parameters, weather);

        var lookupTemps = BuildLookupTable();
        var kennfeld = BuildKennfeld(parameters, lookupTemps, parsed);
        var loadProfile = CalcLoadProfile(parameters, weather);
        var result = InitResult(lookupTemps, kennfeld);

        RunHourlySimulation(parameters, weather, lookupTemps, kennfeld, loadProfile, result);
        CalcAggregates(result);
        CalcDesignPoint(parameters, lookupTemps, kennfeld, loadProfile, result);
        CalcCosts(parameters, result);

        return result;
    }

    private record ParsedInput(List<double[]> RawPowerMax, List<double[]> RawPowerMin, List<double[]> RawCopData);

    private static ParsedInput ParseAndValidateInputs(SimulationParameters parameters, List<WeatherDataPoint> weather)
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

        var pMin = MathHelpers.ParseTextAreaPoints(parameters.RawPMin);

        return new ParsedInput(pMax, pMin, cop);
    }

    // --- Lookup Table ---

    private static double[] BuildLookupTable()
    {
        var temperatures = new double[131]; // -25 to 40 in 0.5° steps
        for (int i = 0; i < temperatures.Length; i++)
            temperatures[i] = -25 + i * 0.5;
        return temperatures;
    }

    // --- Kennfeld (characteristic map) ---

    private record KennfeldCurves(
        double[] PMax35, double[] PMax55,
        double[] PMin35, double[] PMin55,
        double[] PMaxCustom,
        double[] Cop35, double[] Cop55,
        double[] Eta35, double[] Eta55,
        List<double[]> RawCopPoints);

    private static KennfeldCurves BuildKennfeld(SimulationParameters parameters, double[] lookupTemps, ParsedInput parsed)
    {
        var rawPowerMax = parsed.RawPowerMax;
        var rawPowerMin = parsed.RawPowerMin;
        var rawCopData = parsed.RawCopData;

        double[] powerMaxTemps = rawPowerMax.Select(point => point[0]).ToArray();
        double[] powerMaxValues = rawPowerMax.Select(point => point[1]).ToArray();

        int n = lookupTemps.Length;
        var pMax35 = new double[n];
        var pMax55 = new double[n];
        for (int i = 0; i < n; i++)
        {
            pMax35[i] = MathHelpers.Interp(lookupTemps[i], powerMaxTemps, powerMaxValues);
            pMax55[i] = pMax35[i] * P55ScalingFactor;
        }

        var pMin35 = new double[n];
        if (rawPowerMin.Count > 0)
        {
            double[] pMinTemps = rawPowerMin.Select(point => point[0]).ToArray();
            double[] pMinValues = rawPowerMin.Select(point => point[1]).ToArray();
            for (int i = 0; i < n; i++)
                pMin35[i] = MathHelpers.Interp(lookupTemps[i], pMinTemps, pMinValues);
        }
        else
        {
            for (int i = 0; i < n; i++)
                pMin35[i] = pMax35[i] * DefaultPMinFraction;
        }

        var pMin55 = new double[n];
        for (int i = 0; i < n; i++)
            pMin55[i] = pMin35[i] * P55ScalingFactor;

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
            .Where(point => Math.Abs(point[0] - targetVorlauf) < EtaMatchRadius)
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
        double smoothedOutsideTemp = InitialSmoothedTemp;
        double heatingCurveSlope = Math.Abs(parameters.Heizgrenze - parameters.NormAussentemperatur) > 0.01
            ? (parameters.VorlaufMax - parameters.VorlaufMin) / (parameters.Heizgrenze - parameters.NormAussentemperatur)
            : 0;

        for (int hour = 0; hour < HoursPerYear; hour++)
        {
            var weatherPoint = weather[hour];
            double outsideTemp = weatherPoint.Temperature;
            double humidity = weatherPoint.RelativeHumidity;
            double dewPoint = MathHelpers.CalculateDewPoint(outsideTemp, humidity);
            int hourOfDay = hour % 24;
            smoothedOutsideTemp = smoothedOutsideTemp * SmoothingRetain + outsideTemp * SmoothingNew;

            // Heating load & vorlauf for this hour
            var (heatingLoad, vorlauf) = CalcHourlyLoad(parameters, outsideTemp, smoothedOutsideTemp, hourOfDay, load, heatingCurveSlope);
            double totalLoad = heatingLoad + load.WarmwaterPerHour;

            // Available power at current conditions
            double vorlaufBlend = VorlaufFactor(vorlauf);
            double maxPowerAvail = LerpInterp(outsideTemp, lookupTemps, kennfeld.PMax35, kennfeld.PMax55, vorlaufBlend);
            double minPowerAvail = LerpInterp(outsideTemp, lookupTemps, kennfeld.PMin35, kennfeld.PMin55, vorlaufBlend);

            // Icing detection
            var (isIcing, icingPenalty) = DetectIcing(outsideTemp, humidity, dewPoint, totalLoad, minPowerAvail, maxPowerAvail);
            if (isIcing) icingHoursTotal++;

            // Cycling detection
            bool isHeating = heatingLoad > MinHeatingLoad;
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
        SimulationParameters parameters, double outsideTemp, double smoothedTemp, int hourOfDay, LoadProfile load, double heatingCurveSlope)
    {
        if (smoothedTemp >= parameters.Heizgrenze)
            return (0, parameters.WarmwasserTemp);

        double heatingLoad = Math.Max(0, (parameters.Heizgrenze - outsideTemp) * load.LoadPerKelvin);
        double vorlauf = parameters.VorlaufMin + heatingCurveSlope * (parameters.Heizgrenze - outsideTemp);

        if (parameters.NachtabsenkungAktiv && IsNightHour(hourOfDay, parameters.NachtStart, parameters.NachtEnde))
        {
            double roomReduction = Math.Clamp(
                Math.Max(0.0, (parameters.RaumSollTemperatur - parameters.NachtDeltaT) - outsideTemp)
                / Math.Max(0.1, parameters.RaumSollTemperatur - outsideTemp), 0.0, 1.0);
            heatingLoad *= roomReduction;
            vorlauf -= heatingCurveSlope * parameters.NachtDeltaT * NightSetbackVorlaufFactor;
        }

        vorlauf = Math.Clamp(vorlauf, parameters.VorlaufMin, parameters.VorlaufMax);
        return (heatingLoad, vorlauf);
    }

    private static (bool isIcing, double penalty) DetectIcing(
        double outsideTemp, double humidity, double dewPoint,
        double totalLoad, double minPowerAvail, double maxPowerAvail)
    {
        if (totalLoad <= minPowerAvail * IcingLoadThreshold)
            return (false, 1.0);

        double loadFactor = maxPowerAvail > 0 ? Math.Min(1.0, totalLoad / maxPowerAvail) : 1.0;
        double evaporatorTemp = outsideTemp - (IcingEvapBaseOffset + IcingEvapLoadFactor * loadFactor);

        bool isIcing = evaporatorTemp < IcingEvapTempThreshold
                       && evaporatorTemp < dewPoint
                       && humidity > IcingHumidityThreshold
                       && outsideTemp >= IcingOutsideTempMin && outsideTemp <= IcingOutsideTempMax;

        double penalty = isIcing ? 1.0 - IcingCopPenalty * loadFactor : 1.0;
        return (isIcing, penalty);
    }

    private static (double thermal, double electrical, double heizstab, double deficit) CalcPowerBalance(
        double totalLoad, double maxPowerAvail, double cop, double heizstabMax)
    {
        if (totalLoad <= MinPowerBalance)
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
        double totalHeizstab = result.HeizstabPower.Sum();
        double totalThermal = result.ThermalPower.Sum() + totalHeizstab;
        double totalElectrical = result.ElectricalPower.Sum() + totalHeizstab;

        result.Jaz = totalElectrical > 0 ? totalThermal / totalElectrical : 0;
        result.TotalElectricity = totalElectrical;
        result.TotalHeat = totalThermal;
        result.HeizstabShare = totalThermal > 0 ? (totalHeizstab / totalThermal) * 100 : 0;
        result.DeficitHours = result.Deficit.Count(d => d > MinHeatingLoad);
        result.DeficitKwh = result.Deficit.Sum();
    }

    // --- Design Point (Auslegung) ---

    private static void CalcDesignPoint(
        SimulationParameters parameters, double[] lookupTemps, KennfeldCurves kennfeld, LoadProfile load, SimulationResult result)
    {
        double loadAtDesign = (parameters.Heizgrenze - parameters.NormAussentemperatur) * load.LoadPerKelvin + load.WarmwaterPerHour;
        double heatPumpPowerAtDesign = InterpUniform(parameters.NormAussentemperatur, lookupTemps, kennfeld.PMaxCustom);

        result.LoadAtDesignTemp = loadAtDesign;
        result.HeatPumpPowerAtDesignTemp = heatPumpPowerAtDesign;
        result.DesignTemperature = parameters.NormAussentemperatur;
        result.HeatingLimitTemperature = parameters.Heizgrenze;
        result.WarmwaterBaseLoad = load.WarmwaterPerHour;

        // Bivalence point: where load exceeds WP capacity
        // Integer steps (×10) to avoid floating point accumulation
        int startTenths = (int)(parameters.Heizgrenze * 10);
        for (int t10 = startTenths; t10 >= -250; t10--)
        {
            double temp = t10 / 10.0;
            double currentLoad = (parameters.Heizgrenze - temp) * load.LoadPerKelvin + load.WarmwaterPerHour;
            double wpPower = InterpUniform(temp, lookupTemps, kennfeld.PMaxCustom);
            if (currentLoad > wpPower)
            {
                result.BivalenceTemperature = temp;
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
            double sourceTempClamped = Math.Min(sourceTemp, SourceTempClamp);

            // COP = eta_carnot × COP_carnot, using clamped source temp to stay within data range
            double rawEta = MathHelpers.GetFlatEta(sourceTempClamped, etaPoints);
            double carnotCopClamped = MathHelpers.GetCarnotCop(sourceTempClamped, flowTemp);
            cop[i] = Math.Clamp(rawEta * carnotCopClamped, 1.0, maxCop);

            // Eta = actual COP / theoretical Carnot COP at the real (unclamped) source temp.
            // Below the clamp threshold both temps are identical, so we reuse the value above.
            double carnotCopActual = sourceTemp <= SourceTempClamp
                ? carnotCopClamped
                : MathHelpers.GetCarnotCop(sourceTemp, flowTemp);
            eta[i] = cop[i] / carnotCopActual;
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
        double slope = Math.Abs(parameters.Heizgrenze - parameters.NormAussentemperatur) > 0.01
            ? (parameters.VorlaufMax - parameters.VorlaufMin) / (parameters.Heizgrenze - parameters.NormAussentemperatur)
            : 0;
        return Math.Clamp(parameters.VorlaufMin + slope * (parameters.Heizgrenze - outsideTemp), parameters.VorlaufMin, parameters.VorlaufMax);
    }

    private static double VorlaufFactor(double vorlauf)
        => vorlauf <= VorlaufLow ? 0 : vorlauf >= VorlaufHigh ? 1 : (vorlauf - VorlaufLow) / (VorlaufHigh - VorlaufLow);

    private static double Lerp(double valueA, double valueB, double blend) => valueA + blend * (valueB - valueA);

    private static double LerpInterp(double outsideTemp, double[] lookupTemps, double[] vals35, double[] vals55, double vorlaufBlend)
        => Lerp(InterpUniform(outsideTemp, lookupTemps, vals35), InterpUniform(outsideTemp, lookupTemps, vals55), vorlaufBlend);

    /// <summary>
    /// Fast interpolation for uniformly-spaced lookup tables (0.5° steps from -25 to 40).
    /// Replaces linear scan with direct index calculation.
    /// </summary>
    private static double InterpUniform(double x, double[] knownX, double[] knownY)
    {
        if (x <= knownX[0]) return knownY[0];
        if (x >= knownX[^1]) return knownY[^1];
        double pos = (x - knownX[0]) / 0.5;
        int i = (int)pos;
        double frac = pos - i;
        return knownY[i] + frac * (knownY[i + 1] - knownY[i]);
    }

    private static bool IsNightHour(int hourOfDay, int nightStart, int nightEnd)
        => nightStart > nightEnd
            ? (hourOfDay >= nightStart || hourOfDay < nightEnd)
            : (hourOfDay >= nightStart && hourOfDay < nightEnd);
}
