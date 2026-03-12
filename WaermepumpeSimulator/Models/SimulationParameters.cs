namespace WaermepumpeSimulator.Models;

public class SimulationParameters
{
    // Gebäude & Heizlast
    public double Jahresverbrauch { get; set; } = 17000;
    public double Wirkungsgrad { get; set; } = 0.85;
    public double WarmwasserAnteil { get; set; } = 0;
    public double Heizgrenze { get; set; } = 15;
    public double NormAussentemperatur { get; set; } = -13;
    public double RaumSollTemperatur { get; set; } = 22.0;

    // Preise
    public double PreisStrom { get; set; } = 0.30;
    public double PreisAlt { get; set; } = 0.12;

    // Hydraulik
    public double VorlaufMax { get; set; } = 34;
    public double VorlaufMin { get; set; } = 30.5;
    public double WarmwasserTemp { get; set; } = 50;

    // Heizstab
    public double HeizstabMax { get; set; } = 9;

    // Nachtabsenkung
    public bool NachtabsenkungAktiv { get; set; } = false;
    public int NachtStart { get; set; } = 22;
    public int NachtEnde { get; set; } = 6;
    public double NachtDeltaT { get; set; } = 5.0;

    // WP Kennfeld (raw text from textareas)
    public string RawPMax { get; set; } = "-7, 6.8\n2, 7.0\n7, 7.0";
    public string RawPMin { get; set; } = "";
    public string RawCopData { get; set; } = "35, -7, 2.80\n35, 2, 3.41\n35, 7, 4.55\n55, -7, 2.13\n55, 2, 2.41\n55, 7, 3.03";
}
