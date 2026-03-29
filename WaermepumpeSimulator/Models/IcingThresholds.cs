namespace WaermepumpeSimulator.Models;

/// <summary>
/// Shared constants for the frost-critical ambient zone.
/// Used by both SimulationEngine (counting) and chart components (coloring).
/// Keeping them in one place prevents chart/sim mismatch when thresholds are revised.
/// </summary>
public static class IcingThresholds
{
    /// <summary>Lower bound of the climatically frost-critical outside temperature range (°C).</summary>
    public const double FrostCriticalTempMin = 2.0;

    /// <summary>Upper bound of the climatically frost-critical outside temperature range (°C).</summary>
    public const double FrostCriticalTempMax = 7.0;

    /// <summary>Minimum relative humidity for the frost-critical ambient zone (%).</summary>
    public const double FrostCriticalHumidity = 85.0;
}
