using WaermepumpeSimulator.Helpers;
using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public class SimulationEngine
{
    public SimulationResult Run(SimulationParameters p, List<WeatherDataPoint> weather)
    {
        // Build LUT range
        var lutRange = new List<double>();
        for (double t = -25; t <= 40; t += 0.5) lutRange.Add(t);
        var lutArr = lutRange.ToArray();

        // Parse kennfeld
        var rawPMax = MathHelpers.ParseTextAreaPoints(p.RawPMax);
        var rawPMin = MathHelpers.ParseTextAreaPoints(p.RawPMin);
        var rawCopData = MathHelpers.ParseCopData(p.RawCopData);

        double[] pMaxX = rawPMax.Select(pt => pt[0]).ToArray();
        double[] pMaxY = rawPMax.Select(pt => pt[1]).ToArray();
        double[] pMinX = rawPMin.Count > 0 ? rawPMin.Select(pt => pt[0]).ToArray() : [];
        double[] pMinY = rawPMin.Count > 0 ? rawPMin.Select(pt => pt[1]).ToArray() : [];

        var p35Vals = lutArr.Select(t => MathHelpers.Interp(t, pMaxX, pMaxY)).ToArray();
        var pMinVals = rawPMin.Count > 0
            ? lutArr.Select(t => MathHelpers.Interp(t, pMinX, pMinY)).ToArray()
            : p35Vals.Select(v => v * 0.25).ToArray();

        // Eta points
        var etaPoints35 = rawCopData
            .Where(pt => Math.Abs(pt[0] - 35) < 5)
            .Select(pt => new[] { pt[1], pt[2] / MathHelpers.GetCarnotCop(pt[1], pt[0]) })
            .OrderBy(pt => pt[0])
            .ToList();

        var etaPoints55 = rawCopData
            .Where(pt => Math.Abs(pt[0] - 55) < 5)
            .Select(pt => new[] { pt[1], pt[2] / MathHelpers.GetCarnotCop(pt[1], pt[0]) })
            .OrderBy(pt => pt[0])
            .ToList();

        // Calculate COP curves
        var (cop35, eta35) = CalcCurve(lutArr, 35, etaPoints35);
        var (cop55, eta55) = CalcCurve(lutArr, 55, etaPoints55);

        var p55Vals = p35Vals.Select(v => v * 0.92).ToArray();
        var pMin55Vals = pMinVals.Select(v => v * 0.92).ToArray();

        // Custom p_max considering vorlauf
        var pMaxCustom = new double[lutArr.Length];
        for (int i = 0; i < lutArr.Length; i++)
        {
            double t = lutArr[i];
            double vl = t < p.Heizgrenze
                ? Math.Clamp(p.VorlaufMin + ((p.VorlaufMax - p.VorlaufMin) / (p.Heizgrenze - p.NormAussentemperatur)) * (p.Heizgrenze - t), p.VorlaufMin, p.VorlaufMax)
                : p.VorlaufMin;
            double fVl = vl <= 35 ? 0 : vl >= 55 ? 1 : (vl - 35) / 20.0;
            pMaxCustom[i] = p35Vals[i] + fVl * (p55Vals[i] - p35Vals[i]);
        }

        // Calculate load_per_k
        double sumDeltaT = 0;
        foreach (var w in weather)
            if (w.Temperature < p.Heizgrenze) sumDeltaT += p.Heizgrenze - w.Temperature;

        double loadPerK = sumDeltaT > 0 ? (p.Jahresverbrauch * p.Wirkungsgrad * (1 - p.WarmwasserAnteil / 100.0)) / sumDeltaT : 0;
        double wwLastH = (p.Jahresverbrauch * p.Wirkungsgrad * p.WarmwasserAnteil / 100.0) / 8760.0;

        // Hourly simulation
        int n = 8760;
        var res = new SimulationResult
        {
            Temperature = new double[n],
            RelativeHumidity = new double[n],
            DewPoint = new double[n],
            Load = new double[n],
            Cop = new double[n],
            ThermalPower = new double[n],
            ElectricalPower = new double[n],
            HeizstabPower = new double[n],
            Deficit = new double[n],
            Icing = new int[n],
            Cycling = new int[n],
            PMaxAvail = new double[n],
            PMinAvail = new double[n],
            LutTemps = lutArr,
            WpP35 = p35Vals,
            WpP55 = p55Vals,
            WpPMin = pMinVals,
            WpPMaxCustom = pMaxCustom,
            WpCop35 = cop35,
            WpCop55 = cop55,
            WpEta35 = eta35,
            WpEta55 = eta55,
            RawCopPoints = rawCopData,
        };

        int icingHours = 0, cyclingHoursCount = 0, totalHeatingHours = 0;
        double tAvg = 5.0;

        for (int i = 0; i < n; i++)
        {
            var w = weather[i];
            double tA = w.Temperature, rh = w.RelativeHumidity;
            double tp = MathHelpers.CalculateDewPoint(tA, rh);
            int hour = i % 24;
            tAvg = tAvg * 0.96 + tA * 0.04;

            bool isNight = false;
            if (p.NachtabsenkungAktiv)
                isNight = p.NachtStart > p.NachtEnde
                    ? (hour >= p.NachtStart || hour < p.NachtEnde)
                    : (hour >= p.NachtStart && hour < p.NachtEnde);

            double lH = 0, vl = p.WarmwasserTemp;
            if (tAvg < p.Heizgrenze)
            {
                lH = Math.Max(0, (p.Heizgrenze - tA) * loadPerK);
                double steigung = (p.VorlaufMax - p.VorlaufMin) / (p.Heizgrenze - p.NormAussentemperatur);
                vl = p.VorlaufMin + steigung * (p.Heizgrenze - tA);
                if (isNight)
                {
                    lH *= Math.Max(0.0, (p.RaumSollTemperatur - p.NachtDeltaT) - tA) / Math.Max(0.1, p.RaumSollTemperatur - tA);
                    vl -= steigung * p.NachtDeltaT * 1.5;
                }
                vl = Math.Clamp(vl, p.VorlaufMin, p.VorlaufMax);
            }

            double lGes = lH + wwLastH;
            double fVl = vl <= 35 ? 0 : vl >= 55 ? 1 : (vl - 35) / 20.0;
            double pMaxAvail = MathHelpers.Interp(tA, lutArr, p35Vals) + fVl * (MathHelpers.Interp(tA, lutArr, p55Vals) - MathHelpers.Interp(tA, lutArr, p35Vals));
            double pMinAvail = MathHelpers.Interp(tA, lutArr, pMinVals) + fVl * (MathHelpers.Interp(tA, lutArr, pMin55Vals) - MathHelpers.Interp(tA, lutArr, pMinVals));

            int isIcing = 0;
            double penalty = 1.0;
            if (lGes > pMinAvail * 1.2)
            {
                double loadFactor = pMaxAvail > 0 ? Math.Min(1.0, lGes / pMaxAvail) : 1.0;
                double tEvap = tA - (0.5 + 3.0 * loadFactor);
                if (tEvap < -0.5 && tEvap < tp && rh > 88 && tA >= -4 && tA <= 3)
                {
                    isIcing = 1;
                    icingHours++;
                    penalty = 1.0 - 0.15 * loadFactor;
                }
            }

            if (lH > 0.1)
            {
                totalHeatingHours++;
                if (lGes < pMinAvail) cyclingHoursCount++;
            }

            double copReal = (MathHelpers.Interp(tA, lutArr, cop35) + fVl * (MathHelpers.Interp(tA, lutArr, cop55) - MathHelpers.Interp(tA, lutArr, cop35))) * penalty;

            double pTh = 0, pEl = 0, stab = 0, deficit = 0;
            if (lGes > 0.001)
            {
                if (pMaxAvail >= lGes) { pTh = lGes; pEl = lGes / copReal; }
                else
                {
                    pTh = pMaxAvail; pEl = pMaxAvail / copReal;
                    stab = Math.Min(lGes - pMaxAvail, p.HeizstabMax);
                    deficit = (lGes - pMaxAvail) - stab;
                }
            }

            res.Temperature[i] = tA;
            res.RelativeHumidity[i] = rh;
            res.DewPoint[i] = tp;
            res.Load[i] = lGes;
            res.Cop[i] = copReal;
            res.ThermalPower[i] = pTh;
            res.ElectricalPower[i] = pEl;
            res.HeizstabPower[i] = stab;
            res.Deficit[i] = deficit;
            res.Icing[i] = isIcing;
            res.Cycling[i] = lH > 0.1 && lGes < pMinAvail ? 1 : 0;
            res.PMaxAvail[i] = pMaxAvail;
            res.PMinAvail[i] = pMinAvail;
        }

        // Aggregation
        double sumTh = res.ThermalPower.Sum() + res.HeizstabPower.Sum();
        double sumEl = res.ElectricalPower.Sum() + res.HeizstabPower.Sum();
        double sumStab = res.HeizstabPower.Sum();
        int hoursDeficit = res.Deficit.Count(d => d > 0.1);

        res.Jaz = sumEl > 0 ? sumTh / sumEl : 0;
        res.TotalStrom = sumEl;
        res.TotalWaerme = sumTh;
        res.HeizstabAnteil = sumTh > 0 ? (sumStab / sumTh) * 100 : 0;
        res.IcingHours = icingHours;
        res.CyclingPercent = totalHeatingHours > 0 ? (cyclingHoursCount / (double)totalHeatingHours) * 100 : 0;
        res.DeficitHours = hoursDeficit;
        res.DeficitKwh = res.Deficit.Sum();

        // Auslegung
        double loadAtNat = (p.Heizgrenze - p.NormAussentemperatur) * loadPerK + wwLastH;
        double vlNat = p.VorlaufMax;
        double fVlNat = vlNat <= 35 ? 0 : vlNat >= 55 ? 1 : (vlNat - 35) / 20.0;
        double wpAtNat = MathHelpers.Interp(p.NormAussentemperatur, lutArr, p35Vals) + fVlNat * (MathHelpers.Interp(p.NormAussentemperatur, lutArr, p55Vals) - MathHelpers.Interp(p.NormAussentemperatur, lutArr, p35Vals));

        res.LoadAtNat = loadAtNat;
        res.WpAtNat = wpAtNat;
        res.PlotNat = p.NormAussentemperatur;
        res.PlotHeizgrenze = p.Heizgrenze;
        res.PlotLoadHg = wwLastH;

        // Bivalenzpunkt
        for (double t = p.Heizgrenze; t >= -25; t -= 0.1)
        {
            double currentLoad = (p.Heizgrenze - t) * loadPerK + wwLastH;
            int idx = Array.FindIndex(lutArr, val => val >= t);
            if (idx == -1) idx = 0;
            double currentPMax = pMaxCustom[idx];
            if (currentLoad > currentPMax)
            {
                res.BivalenzTemp = Math.Round(t, 1);
                res.BivalenzPower = currentLoad;
                break;
            }
        }

        // Kosten
        res.KostenWp = sumEl * p.PreisStrom;
        res.KostenAlt = p.Jahresverbrauch * p.PreisAlt;
        res.Ersparnis = res.KostenAlt - res.KostenWp;

        return res;
    }

    private static (double[] cop, double[] eta) CalcCurve(double[] lutRange, double tv, List<double[]> etaPts)
    {
        double maxCop = MathHelpers.GetMaxCop(tv);
        var arrCop = new double[lutRange.Length];
        var arrEta = new double[lutRange.Length];

        for (int i = 0; i < lutRange.Length; i++)
        {
            double tq = lutRange[i];
            double tqCalc = Math.Min(tq, 15.0);
            double rawEta = MathHelpers.GetFlatEta(tqCalc, etaPts);
            double cop = Math.Clamp(rawEta * MathHelpers.GetCarnotCop(tqCalc, tv), 1.0, maxCop);
            arrCop[i] = cop;
            arrEta[i] = cop / MathHelpers.GetCarnotCop(tq, tv);
        }

        return (arrCop, arrEta);
    }
}
