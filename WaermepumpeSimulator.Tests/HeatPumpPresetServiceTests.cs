using WaermepumpeSimulator.Helpers;
using WaermepumpeSimulator.Services;

namespace WaermepumpeSimulator.Tests;

public class HeatPumpPresetServiceTests
{
    [Fact]
    public void GetAllPresets_ReturnsNonEmptyList()
    {
        // Arrange
        var service = new HeatPumpPresetService();

        // Act
        var presets = service.GetAllPresets();

        // Assert
        Assert.NotEmpty(presets);
    }

    [Fact]
    public void GetAllPresets_AllPresetsHaveUniqueKeys()
    {
        // Arrange
        var service = new HeatPumpPresetService();

        // Act
        var presets = service.GetAllPresets();

        // Assert
        var keys = presets.Select(p => p.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void GetAllPresets_AllPresetsHaveValidPMax()
    {
        // Arrange
        var service = new HeatPumpPresetService();

        // Act
        var presets = service.GetAllPresets();

        // Assert
        foreach (var preset in presets)
        {
            var points = MathHelpers.ParseTextAreaPoints(preset.PMax);
            Assert.True(points.Count >= 2, $"Preset {preset.Key}: PMax needs at least 2 points");
            Assert.True(points.All(p => p[1] > 0), $"Preset {preset.Key}: PMax values must be > 0");
        }
    }

    [Fact]
    public void GetAllPresets_AllPresetsHaveValidCopData()
    {
        // Arrange
        var service = new HeatPumpPresetService();

        // Act
        var presets = service.GetAllPresets();

        // Assert
        foreach (var preset in presets)
        {
            var points = MathHelpers.ParseCopData(preset.CopData);
            Assert.True(points.Count >= 2, $"Preset {preset.Key}: COP needs at least 2 points");
            Assert.True(points.All(p => p[2] > 0), $"Preset {preset.Key}: COP values must be > 0");
        }
    }

    [Fact]
    public void GetAllPresets_AllPresetsCanRunSimulation()
    {
        // Arrange
        var service = new HeatPumpPresetService();
        var presets = service.GetAllPresets();
        var weather = TestHelpers.SinusoidalWeather();

        // Act & Assert- every preset should produce a valid simulation
        foreach (var preset in presets)
        {
            var parameters = TestHelpers.DefaultParams();
            parameters.RawPMax = preset.PMax;
            parameters.RawPMin = preset.PMin ?? "";
            parameters.RawCopData = preset.CopData;

            var result = SimulationEngine.Run(parameters, weather);

            Assert.True(result.Jaz > 0, $"Preset {preset.Key}: JAZ should be > 0");
            Assert.Equal(8760, result.Temperature.Length);
        }
    }
}
