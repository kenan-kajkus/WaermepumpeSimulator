using WaermepumpeSimulator.Models;
using WaermepumpeSimulator.Services;

namespace WaermepumpeSimulator.Tests;

public class EvaluationServiceTests
{
    // JAZ Rating

    [Theory]
    [InlineData(2.5, "red")]
    [InlineData(3.2, "yellow")]
    [InlineData(3.8, "green")]
    [InlineData(4.5, "blue")]
    public void Evaluate_JazThresholds_ReturnsCorrectColor(double jaz, string expectedColor)
    {
        // Arrange
        var result = CreateResult(jaz: jaz);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal(expectedColor, eval.JazColor);
    }

    [Fact]
    public void Evaluate_JazExactly3_ReturnsYellow()
    {
        // Arrange
        var result = CreateResult(jaz: 3.0);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert- 3.0 is not < 3.0, so it falls into the 3.0-3.5 bucket
        Assert.Equal("yellow", eval.JazColor);
    }

    [Fact]
    public void Evaluate_JazExactly4_ReturnsGreen()
    {
        // Arrange
        var result = CreateResult(jaz: 4.0);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert- 4.0 is <= 4.0, so green
        Assert.Equal("green", eval.JazColor);
    }

    // Cycling Rating

    [Theory]
    [InlineData(10, "green")]
    [InlineData(40, "orange")]
    [InlineData(60, "red")]
    public void Evaluate_CyclingThresholds_ReturnsCorrectColor(double cyclingPct, string expectedColor)
    {
        // Arrange
        var result = CreateResult(cyclingPercent: cyclingPct);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal(expectedColor, eval.TaktColor);
    }

    // Sizing Rating

    [Fact]
    public void Evaluate_HighBivalenceTemp_ReturnsRedUndersized()
    {
        // Arrange- bivalence at 0°C means pump is too small
        var result = CreateResult(bivalenceTemp: 0.0, loadAtDesign: 10, powerAtDesign: 6);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal("red", eval.StabColor);
        Assert.Contains("Unterdimensioniert", eval.StabText);
    }

    [Fact]
    public void Evaluate_HighHeizstabShare_ReturnsRed()
    {
        // Arrange- heizstab covers > 5% of total heat
        var result = CreateResult(heizstabShare: 8.0, bivalenceTemp: -10);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal("red", eval.StabColor);
        Assert.Contains("Heizstab", eval.StabText);
    }

    [Fact]
    public void Evaluate_ModerateBivalence_ReturnsOrange()
    {
        // Arrange- bivalence between -5 and -2°C
        var result = CreateResult(bivalenceTemp: -3.0, heizstabShare: 2.0);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal("orange", eval.StabColor);
    }

    [Fact]
    public void Evaluate_LowBivalence_ReturnsGreen()
    {
        // Arrange- bivalence below -5°C, low heizstab share
        var result = CreateResult(bivalenceTemp: -8.0, heizstabShare: 1.0);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal("green", eval.StabColor);
    }

    [Fact]
    public void Evaluate_NoBivalence_ReturnsGreen()
    {
        // Arrange- no bivalence point (pump always covers load)
        var result = CreateResult(bivalenceTemp: null, heizstabShare: 0);

        // Act
        var eval = EvaluationService.Evaluate(result);

        // Assert
        Assert.Equal("green", eval.StabColor);
        Assert.Contains("Keiner", eval.StabText);
    }

    // CalcMonthly

    [Fact]
    public void CalcMonthly_Returns12Months()
    {
        // Arrange
        var result = CreateFullResult();

        // Act
        var monthly = EvaluationService.CalcMonthly(result, 0.30);

        // Assert
        Assert.Equal(12, monthly.Count);
        Assert.Equal("Jan", monthly[0].Name);
        Assert.Equal("Dez", monthly[11].Name);
    }

    [Fact]
    public void CalcMonthly_TotalWaerme_MatchesHourlySum()
    {
        // Arrange
        var result = CreateFullResult();

        // Act
        var monthly = EvaluationService.CalcMonthly(result, 0.30);

        // Assert
        double monthlyTotal = monthly.Sum(m => m.Waerme);
        double hourlyTotal = result.ThermalPower.Sum() + result.HeizstabPower.Sum();
        Assert.Equal(hourlyTotal, monthlyTotal, precision: 6);
    }

    [Fact]
    public void CalcMonthly_MonthlyJaz_EqualWaermeDividedByStrom()
    {
        // Arrange
        var result = CreateFullResult();

        // Act
        var monthly = EvaluationService.CalcMonthly(result, 0.30);

        // Assert
        foreach (var m in monthly.Where(m => m.Strom > 0))
        {
            Assert.Equal(m.Waerme / m.Strom, m.Jaz, precision: 6);
        }
    }

    [Fact]
    public void CalcMonthly_Kosten_EqualsStromTimesPrice()
    {
        // Arrange
        var result = CreateFullResult();
        double stromPreis = 0.30;

        // Act
        var monthly = EvaluationService.CalcMonthly(result, stromPreis);

        // Assert
        foreach (var m in monthly)
        {
            Assert.Equal(m.Strom * stromPreis, m.Kosten, precision: 6);
        }
    }

    // Helpers

    private static SimulationResult CreateResult(
        double jaz = 3.5,
        double heizstabShare = 1.0,
        double cyclingPercent = 10,
        double? bivalenceTemp = -8,
        double loadAtDesign = 10,
        double powerAtDesign = 12)
    {
        return new SimulationResult
        {
            Jaz = jaz,
            HeizstabShare = heizstabShare,
            CyclingPercent = cyclingPercent,
            BivalenceTemperature = bivalenceTemp,
            LoadAtDesignTemp = loadAtDesign,
            HeatPumpPowerAtDesignTemp = powerAtDesign,
            Temperature = new double[8760],
            ThermalPower = new double[8760],
            ElectricalPower = new double[8760],
            HeizstabPower = new double[8760],
            Deficit = new double[8760],
            Icing = new int[8760],
        };
    }

    private static SimulationResult CreateFullResult()
    {
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();
        return SimulationEngine.Run(parameters, weather);
    }
}
