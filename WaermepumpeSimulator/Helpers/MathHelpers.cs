namespace WaermepumpeSimulator.Helpers;

public static class MathHelpers
{
    public static double Interp(double x, double[] xp, double[] yp)
    {
        if (xp.Length == 0) return 0;
        if (x <= xp[0]) return yp[0];
        if (x >= xp[^1]) return yp[^1];
        int i = 0;
        while (i < xp.Length - 2 && x > xp[i + 1]) i++;
        return yp[i] + (x - xp[i]) * (yp[i + 1] - yp[i]) / (xp[i + 1] - xp[i]);
    }

    public static double CalculateDewPoint(double t, double rh)
    {
        const double a = 17.62, b = 243.12;
        double rhSafe = Math.Max(rh, 0.1);
        double alpha = Math.Log(rhSafe / 100.0) + (a * t) / (b + t);
        return (b * alpha) / (a - alpha);
    }

    public static double Gelu(double x)
    {
        double g = 0.5 * x * (1.0 + Math.Tanh(0.79788 * (x + 0.044715 * Math.Pow(x, 3))));
        return g > 0 ? Math.Log(1.0 + g) : g;
    }

    public static double GetCarnotCop(double ta, double tv)
    {
        double dt = Math.Max((tv + 273.15) - (ta + 273.15), 5.0);
        return (tv + 273.15) / dt;
    }

    public static double GetMaxCop(double tv)
    {
        return Math.Max(2.0, 8.0 - (tv - 35.0) * 0.15);
    }

    public static double GetFlatEta(double tq, List<double[]> pts)
    {
        if (pts.Count == 0) return 0.4;
        if (tq <= pts[0][0]) return pts[0][1];
        if (tq >= pts[^1][0]) return pts[^1][1];
        for (int i = 0; i < pts.Count - 1; i++)
        {
            if (tq >= pts[i][0] && tq <= pts[i + 1][0])
            {
                double f = (tq - pts[i][0]) / (pts[i + 1][0] - pts[i][0]);
                return pts[i][1] + f * (pts[i + 1][1] - pts[i][1]);
            }
        }
        return 0.4;
    }

    /// <summary>
    /// Parse "x, y" lines from textarea text into sorted points.
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
    /// Parse "vl, at, cop" lines from textarea text.
    /// </summary>
    public static List<double[]> ParseCopData(string text)
    {
        var points = new List<double[]>();
        if (string.IsNullOrWhiteSpace(text)) return points;
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(',');
            if (parts.Length >= 3 &&
                double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tv) &&
                double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tq) &&
                double.TryParse(parts[2].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cop))
            {
                points.Add([tv, tq, cop]);
            }
        }
        return points;
    }
}
