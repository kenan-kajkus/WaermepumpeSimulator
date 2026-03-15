using WaermepumpeSimulator.Models;

namespace WaermepumpeSimulator.Tests;

public class SimulationParametersTests
{
    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = TestHelpers.DefaultParams();

        // Act
        var clone = original.Clone();
        clone.Jahresverbrauch = 99999;
        clone.RawPMax = "changed";

        // Assert- original should not be affected
        Assert.Equal(17000, original.Jahresverbrauch);
        Assert.NotEqual("changed", original.RawPMax);
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        // Arrange
        var original = new SimulationParameters
        {
            Jahresverbrauch = 25000,
            Wirkungsgrad = 0.95,
            WarmwasserAnteil = 15,
            Heizgrenze = 18,
            NormAussentemperatur = -16,
            RaumSollTemperatur = 21,
            PreisStrom = 0.35,
            PreisAlt = 0.10,
            VorlaufMax = 45,
            VorlaufMin = 28,
            WarmwasserTemp = 55,
            HeizstabMax = 6,
            NachtabsenkungAktiv = true,
            NachtStart = 23,
            NachtEnde = 5,
            NachtDeltaT = 3.0,
            RawPMax = "custom pmax",
            RawPMin = "custom pmin",
            RawCopData = "custom cop"
        };

        // Act
        var clone = original.Clone();

        // Assert
        Assert.Equal(original.Jahresverbrauch, clone.Jahresverbrauch);
        Assert.Equal(original.Wirkungsgrad, clone.Wirkungsgrad);
        Assert.Equal(original.WarmwasserAnteil, clone.WarmwasserAnteil);
        Assert.Equal(original.Heizgrenze, clone.Heizgrenze);
        Assert.Equal(original.NormAussentemperatur, clone.NormAussentemperatur);
        Assert.Equal(original.RaumSollTemperatur, clone.RaumSollTemperatur);
        Assert.Equal(original.PreisStrom, clone.PreisStrom);
        Assert.Equal(original.PreisAlt, clone.PreisAlt);
        Assert.Equal(original.VorlaufMax, clone.VorlaufMax);
        Assert.Equal(original.VorlaufMin, clone.VorlaufMin);
        Assert.Equal(original.WarmwasserTemp, clone.WarmwasserTemp);
        Assert.Equal(original.HeizstabMax, clone.HeizstabMax);
        Assert.Equal(original.NachtabsenkungAktiv, clone.NachtabsenkungAktiv);
        Assert.Equal(original.NachtStart, clone.NachtStart);
        Assert.Equal(original.NachtEnde, clone.NachtEnde);
        Assert.Equal(original.NachtDeltaT, clone.NachtDeltaT);
        Assert.Equal(original.RawPMax, clone.RawPMax);
        Assert.Equal(original.RawPMin, clone.RawPMin);
        Assert.Equal(original.RawCopData, clone.RawCopData);
    }
}
