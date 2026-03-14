namespace WaermepumpeSimulator.Models;

public class ValidationState
{
    public bool VJahresverbrauch { get; set; }
    public bool VWirkungsgrad { get; set; }
    public bool VWwAnteil { get; set; }
    public bool VHeizgrenze { get; set; }
    public bool VNat { get; set; }
    public bool WRaumSoll { get; set; }
    public bool VPreisStrom { get; set; }
    public bool VPreisAlt { get; set; }
    public bool VVlMax { get; set; }
    public bool WVlMax { get; set; }
    public bool VVlMin { get; set; }
    public bool WWwTemp { get; set; }
    public bool VHeizstab { get; set; }
    public bool VPMax { get; set; }
    public bool VCop { get; set; }

    // Warnings (orange) — simulation runs but results may be questionable
    public bool WPMaxNegative { get; set; }
    public bool WCopRange { get; set; }
    public bool WPMinExceedsPMax { get; set; }
}
