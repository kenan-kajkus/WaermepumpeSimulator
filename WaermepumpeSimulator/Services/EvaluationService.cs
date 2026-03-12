using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Services;

public static class EvaluationService
{
    public static EvaluationResult Evaluate(SimulationResult res)
    {
        var eval = new EvaluationResult();
        double jaz = res.Jaz;
        double stabPct = res.HeizstabShare;
        double cyclingPct = res.CyclingPercent;

        // JAZ
        if (jaz < 3.0)
        {
            eval.JazColor = "red";
            eval.JazText = "Nicht gut. Zu hoher Stromverbrauch (Vorlauftemperatur prüfen!).";
        }
        else if (jaz < 3.5)
        {
            eval.JazColor = "yellow";
            eval.JazText = "In Ordnung, aber es gibt noch Optimierungspotenzial.";
        }
        else if (jaz <= 4.0)
        {
            eval.JazColor = "green";
            eval.JazText = "Gut. Die Anlage arbeitet effizient und sparsam.";
        }
        else
        {
            eval.JazColor = "blue";
            eval.JazText = "Hervorragend! Sehr hohe Effizienz.";
        }

        // Taktung
        if (cyclingPct < 30)
        {
            eval.TaktColor = "green";
            eval.TaktText = "Alles Ok. Die WP moduliert gut und taktet selten.";
        }
        else if (cyclingPct <= 50)
        {
            eval.TaktColor = "orange";
            eval.TaktText = "Erhöht. Läuft oft unter Min-Leistung & wird häufig takten (Problem für Lebensdauer).";
        }
        else
        {
            eval.TaktColor = "red";
            eval.TaktText = "Überdimensioniert! Minimalleistung passt nicht gut zur Heizlast.";
        }

        // Auslegung & Heizstab
        double natCoverage = res.LoadAtDesignTemp > 0 ? (res.HeatPumpPowerAtDesignTemp / res.LoadAtDesignTemp) * 100 : 100;

        if (res.BivalenceTemperature.HasValue && res.BivalenceTemperature > -2.0)
        {
            eval.StabColor = "red";
            eval.StabText = $"<b>Unterdimensioniert!</b> Bivalenzpunkt ({res.BivalenceTemperature:F1}°C) ist zu hoch. Deckt an NAT nur {natCoverage:F0}% der Last.";
        }
        else if (stabPct > 5.0)
        {
            eval.StabColor = "red";
            eval.StabText = $"Achtung: Heizstab frisst extrem viel Strom ({stabPct:F1}% Anteil an der Gesamtenergie p.a.).";
        }
        else if (res.BivalenceTemperature.HasValue && res.BivalenceTemperature > -5.0)
        {
            eval.StabColor = "orange";
            eval.StabText = $"Knapp bemessen. Heizstab greift ab {res.BivalenceTemperature:F1}°C ein. Jahresanteil: {stabPct:F1}%.";
        }
        else
        {
            string bivText = res.BivalenceTemperature.HasValue ? $"{res.BivalenceTemperature:F1}°C" : "Keiner";
            eval.StabColor = "green";
            eval.StabText = $"Sehr gute Auslegung! Bivalenzpunkt bei {bivText}. Heizstab-Anteil minimal ({stabPct:F1}%).";
        }

        return eval;
    }

    public static List<MonthlyAggregate> CalcMonthly(SimulationResult res, double stromPreis)
    {
        string[] names = ["Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"];
        var data = new MonthlyAggregate[12];
        for (int m = 0; m < 12; m++)
            data[m] = new MonthlyAggregate { Name = names[m] };

        int[] monthStartHour = [0, 744, 1416, 2160, 2880, 3624, 4344, 5088, 5832, 6552, 7296, 8016];
        for (int i = 0; i < 8760; i++)
        {
            int mIdx = 11;
            for (int m = 11; m >= 0; m--)
            {
                if (i >= monthStartHour[m]) { mIdx = m; break; }
            }
            data[mIdx].Waerme += res.ThermalPower[i] + res.HeizstabPower[i];
            data[mIdx].Strom += res.ElectricalPower[i] + res.HeizstabPower[i];
            data[mIdx].IcingCount += res.Icing[i];
        }

        foreach (var m in data)
        {
            m.Jaz = m.Strom > 0 ? m.Waerme / m.Strom : 0;
            m.Kosten = m.Strom * stromPreis;
        }

        return data.ToList();
    }
}
