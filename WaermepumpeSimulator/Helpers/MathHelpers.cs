namespace WaermepumpeSimulator.Helpers;

public static class MathHelpers
{
    public static double Interp(double x, double[] knownX, double[] knownY)
    {
        if (knownX.Length == 0) return 0;
        if (x <= knownX[0]) return knownY[0];
        if (x >= knownX[^1]) return knownY[^1];
        int i = 0;
        while (i < knownX.Length - 2 && x > knownX[i + 1]) i++;
        return knownY[i] + (x - knownX[i]) * (knownY[i + 1] - knownY[i]) / (knownX[i + 1] - knownX[i]);
    }

    public static double CalculateDewPoint(double temperature, double relativeHumidity)
    {
        const double magnusA = 17.62;
        const double magnusB = 243.12;
        double rhSafe = Math.Max(relativeHumidity, 0.1);
        double alpha = Math.Log(rhSafe / 100.0) + (magnusA * temperature) / (magnusB + temperature);
        return (magnusB * alpha) / (magnusA - alpha);
    }

    public static double GetCarnotCop(double sourceTemp, double flowTemp)
    {
        double deltaT = Math.Max(flowTemp - sourceTemp, 5.0);
        return (flowTemp + 273.15) / deltaT;
    }

    public static double GetMaxCop(double flowTemp)
    {
        return Math.Max(2.0, 8.0 - (flowTemp - 35.0) * 0.15);
    }

    public static double GetFlatEta(double sourceTemp, List<double[]> etaPoints)
    {
        if (etaPoints.Count == 0) return 0.4;
        if (sourceTemp <= etaPoints[0][0]) return etaPoints[0][1];
        if (sourceTemp >= etaPoints[^1][0]) return etaPoints[^1][1];
        for (int i = 0; i < etaPoints.Count - 1; i++)
        {
            if (sourceTemp >= etaPoints[i][0] && sourceTemp <= etaPoints[i + 1][0])
            {
                double fraction = (sourceTemp - etaPoints[i][0]) / (etaPoints[i + 1][0] - etaPoints[i][0]);
                return etaPoints[i][1] + fraction * (etaPoints[i + 1][1] - etaPoints[i][1]);
            }
        }
        return 0.4;
    }

    /// <summary>
    /// Parse "temperature, power" lines from textarea text into sorted points.
    /// </summary>
    public static List<double[]> ParseTextAreaPoints(string text)
    {
        var points = new List<double[]>();
        if (string.IsNullOrWhiteSpace(text)) return points;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(',');
            if (parts.Length >= 2 &&
                double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
            {
                points.Add([x, y]);
            }
        }
        points.Sort((a, b) => a[0].CompareTo(b[0]));
        return points;
    }

    /// <summary>
    /// Parse "flowTemp, outsideTemp, cop" lines from textarea text.
    /// </summary>
    public static List<double[]> ParseCopData(string text)
    {
        var points = new List<double[]>();
        if (string.IsNullOrWhiteSpace(text)) return points;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(',');
            if (parts.Length >= 3 &&
                double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double flowTemp) &&
                double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double outsideTemp) &&
                double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cop))
            {
                points.Add([flowTemp, outsideTemp, cop]);
            }
        }
        return points;
    }
}
