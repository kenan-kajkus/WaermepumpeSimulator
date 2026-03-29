using WaermepumpeSimulator.Helpers;

namespace WaermepumpeSimulator.Tests;

public class MathHelpersTests
{
    // Interp

    [Fact]
    public void Interp_EmptyArrays_ReturnsZero()
    {
        // Arrange
        double[] xs = [];
        double[] ys = [];

        // Act
        var result = MathHelpers.Interp(5.0, xs, ys);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Interp_BelowRange_ReturnsFirstValue()
    {
        // Arrange
        double[] xs = [0, 10];
        double[] ys = [100, 200];

        // Act
        var result = MathHelpers.Interp(-5, xs, ys);

        // Assert
        Assert.Equal(100, result);
    }

    [Fact]
    public void Interp_AboveRange_ReturnsLastValue()
    {
        // Arrange
        double[] xs = [0, 10];
        double[] ys = [100, 200];

        // Act
        var result = MathHelpers.Interp(15, xs, ys);

        // Assert
        Assert.Equal(200, result);
    }

    [Fact]
    public void Interp_Midpoint_ReturnsLinearInterpolation()
    {
        // Arrange
        double[] xs = [0, 10];
        double[] ys = [100, 200];

        // Act
        var result = MathHelpers.Interp(5, xs, ys);

        // Assert
        Assert.Equal(150, result, precision: 6);
    }

    [Theory]
    [InlineData(2.5, 125)]
    [InlineData(7.5, 175)]
    [InlineData(0, 100)]
    [InlineData(10, 200)]
    public void Interp_VariousPositions_InterpolatesCorrectly(double x, double expected)
    {
        // Arrange
        double[] xs = [0, 10];
        double[] ys = [100, 200];

        // Act
        var result = MathHelpers.Interp(x, xs, ys);

        // Assert
        Assert.Equal(expected, result, precision: 6);
    }

    [Fact]
    public void Interp_ThreePoints_InterpolatesBetweenCorrectPair()
    {
        // Arrange
        double[] xs = [-7, 2, 7];
        double[] ys = [6.8, 7.0, 7.0];

        // Act
        var result = MathHelpers.Interp(-2.5, xs, ys);

        // Assert- between -7 and 2: 6.8 + ((-2.5 - -7) / (2 - -7)) * (7.0 - 6.8) = 6.9
        Assert.Equal(6.9, result, precision: 6);
    }

    // CalculateDewPoint

    [Fact]
    public void CalculateDewPoint_AtFullHumidity_ReturnsSameAsTemp()
    {
        // Arrange & Act
        var dewPoint = MathHelpers.CalculateDewPoint(20.0, 100.0);

        // Assert- at 100% RH, dew point ≈ air temperature
        Assert.Equal(20.0, dewPoint, precision: 1);
    }

    [Fact]
    public void CalculateDewPoint_AtLowHumidity_ReturnsBelowTemp()
    {
        // Arrange & Act
        var dewPoint = MathHelpers.CalculateDewPoint(20.0, 40.0);

        // Assert
        Assert.True(dewPoint < 20.0);
        Assert.True(dewPoint > -20.0);
    }

    [Fact]
    public void CalculateDewPoint_ZeroHumidity_DoesNotThrow()
    {
        // Arrange & Act- uses Math.Max(rh, 0.1) so should not throw
        var dewPoint = MathHelpers.CalculateDewPoint(10.0, 0.0);

        // Assert- returns a finite number
        Assert.False(double.IsNaN(dewPoint));
        Assert.False(double.IsInfinity(dewPoint));
    }

    // CalculateAbsoluteHumidity / SaturationPressure

    [Fact]
    public void CalculateAbsoluteHumidity_At100Percent_IsPositive()
    {
        // At 20°C, 100% RH the absolute humidity is ~14.7 g/kg
        var x = MathHelpers.CalculateAbsoluteHumidity(20.0, 100.0);
        Assert.True(x > 0, "Absolute humidity must be positive");
        Assert.InRange(x, 14.0, 16.0);
    }

    [Fact]
    public void CalculateAbsoluteHumidity_HigherTempMoreHumidity()
    {
        // Same RH but higher temperature → more absolute humidity (warmer air holds more moisture)
        var xLow = MathHelpers.CalculateAbsoluteHumidity(5.0, 85.0);
        var xHigh = MathHelpers.CalculateAbsoluteHumidity(15.0, 85.0);
        Assert.True(xHigh > xLow, "Warmer air at same RH should have higher absolute humidity");
    }

    [Fact]
    public void CalculateAbsoluteHumidity_ZeroHumidity_ReturnsNearZero()
    {
        var x = MathHelpers.CalculateAbsoluteHumidity(20.0, 0.0);
        // RH is clamped to 0.1% internally, giving ~0.014 g/kg at 20°C — close to zero but > 0
        Assert.InRange(x, 0.0, 0.02);
    }

    [Fact]
    public void SaturationPressure_At0Celsius_IsAbout611Pa()
    {
        // Standard value: p_s(0°C) ≈ 610.5–611 Pa
        var ps = MathHelpers.SaturationPressure(0.0);
        Assert.InRange(ps, 608.0, 614.0);
    }

    [Fact]
    public void SaturationPressure_IncreasesWithTemperature()
    {
        Assert.True(MathHelpers.SaturationPressure(10.0) > MathHelpers.SaturationPressure(0.0));
        Assert.True(MathHelpers.SaturationPressure(20.0) > MathHelpers.SaturationPressure(10.0));
    }

    // GetCarnotCop

    [Fact]
    public void GetCarnotCop_TypicalConditions_ReturnsExpectedRange()
    {
        // Arrange & Act
        var cop = MathHelpers.GetCarnotCop(7, 35);

        // Assert- Carnot COP = (35 + 273.15) / max(35 - 7, 5) = 308.15 / 28 ≈ 11.0
        Assert.Equal(308.15 / 28.0, cop, precision: 4);
    }

    [Fact]
    public void GetCarnotCop_SmallDeltaT_ClampedToMinimum5K()
    {
        // Arrange- flowTemp only 2K above sourceTemp
        var cop = MathHelpers.GetCarnotCop(33, 35);

        // Assert- deltaT clamped to 5K: (35 + 273.15) / 5 = 61.63
        Assert.Equal((35 + 273.15) / 5.0, cop, precision: 4);
    }

    // GetMaxCop

    [Theory]
    [InlineData(35, 8.0)]
    [InlineData(55, 5.0)]
    [InlineData(75, 2.0)] // would be 2.0 due to Math.Max
    public void GetMaxCop_ReturnsExpectedValues(double flowTemp, double expected)
    {
        // Act
        var result = MathHelpers.GetMaxCop(flowTemp);

        // Assert
        Assert.Equal(expected, result, precision: 4);
    }

    // GetFlatEta

    [Fact]
    public void GetFlatEta_EmptyPoints_ReturnsDefault()
    {
        // Arrange & Act
        var eta = MathHelpers.GetFlatEta(5.0, []);

        // Assert
        Assert.Equal(0.4, eta);
    }

    [Fact]
    public void GetFlatEta_BelowRange_ReturnsFirst()
    {
        // Arrange
        var points = new List<double[]> { new[] {0, 0.3}, new[] {10, 0.5} };

        // Act
        var eta = MathHelpers.GetFlatEta(-5, points);

        // Assert
        Assert.Equal(0.3, eta);
    }

    [Fact]
    public void GetFlatEta_Midpoint_Interpolates()
    {
        // Arrange
        var points = new List<double[]> { new[] {0, 0.3}, new[] {10, 0.5} };

        // Act
        var eta = MathHelpers.GetFlatEta(5, points);

        // Assert
        Assert.Equal(0.4, eta, precision: 6);
    }

    // ParseTextAreaPoints

    [Fact]
    public void ParseTextAreaPoints_ValidInput_ParsesAndSorts()
    {
        // Arrange
        var input = "7, 7.0\n-7, 6.8\n2, 7.0";

        // Act
        var result = MathHelpers.ParseTextAreaPoints(input);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(-7, result[0][0]); // sorted by first column
        Assert.Equal(6.8, result[0][1]);
        Assert.Equal(2, result[1][0]);
        Assert.Equal(7, result[2][0]);
    }

    [Fact]
    public void ParseTextAreaPoints_EmptyInput_ReturnsEmptyList()
    {
        // Act
        var result = MathHelpers.ParseTextAreaPoints("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseTextAreaPoints_InvalidLines_SkipsGracefully()
    {
        // Arrange
        var input = "abc, def\n-7, 6.8\ngarbage";

        // Act
        var result = MathHelpers.ParseTextAreaPoints(input);

        // Assert
        Assert.Single(result);
        Assert.Equal(-7, result[0][0]);
    }

    // ParseCopData

    [Fact]
    public void ParseCopData_ValidInput_ParsesThreeColumns()
    {
        // Arrange
        var input = "35, -7, 2.80\n55, 2, 2.41";

        // Act
        var result = MathHelpers.ParseCopData(input);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(35, result[0][0]);
        Assert.Equal(-7, result[0][1]);
        Assert.Equal(2.80, result[0][2], precision: 2);
    }

    [Fact]
    public void ParseCopData_EmptyInput_ReturnsEmptyList()
    {
        // Act
        var result = MathHelpers.ParseCopData("");

        // Assert
        Assert.Empty(result);
    }

    // Edge Cases

    [Fact]
    public void Interp_DuplicateXValues_DoesNotThrowOrReturnNaN()
    {
        // Arrange- duplicate x=5 in the data
        double[] xs = [0, 5, 5, 10];
        double[] ys = [100, 150, 160, 200];

        // Act
        var result = MathHelpers.Interp(5, xs, ys);

        // Assert- should return a finite value (first match segment)
        Assert.True(double.IsFinite(result));
    }

    [Fact]
    public void Interp_SinglePoint_ReturnsValueForAnyX()
    {
        // Arrange
        double[] xs = [5];
        double[] ys = [42];

        // Act & Assert- below, at, and above the single point
        Assert.Equal(42, MathHelpers.Interp(0, xs, ys));
        Assert.Equal(42, MathHelpers.Interp(5, xs, ys));
        Assert.Equal(42, MathHelpers.Interp(100, xs, ys));
    }

    [Fact]
    public void GetCarnotCop_EqualSourceAndFlowTemp_ClampsDeltaTTo5K()
    {
        // Arrange- source == flow means deltaT=0, should be clamped to 5K
        var cop = MathHelpers.GetCarnotCop(35, 35);

        // Assert- (35 + 273.15) / 5 = 61.63
        Assert.Equal((35 + 273.15) / 5.0, cop, precision: 4);
    }

    [Fact]
    public void GetCarnotCop_SourceAboveFlow_ClampsDeltaTTo5K()
    {
        // Arrange- source > flow gives negative deltaT, clamped to 5K
        var cop = MathHelpers.GetCarnotCop(40, 35);

        // Assert
        Assert.Equal((35 + 273.15) / 5.0, cop, precision: 4);
    }

    [Fact]
    public void ParseTextAreaPoints_GermanLocaleDecimals_UsesInvariantCulture()
    {
        // Arrange- comma as decimal separator would fail with wrong locale
        // This input uses dots as decimal separator (correct for invariant culture)
        var input = "-7, 6.8\n2, 7.0";

        // Act
        var result = MathHelpers.ParseTextAreaPoints(input);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(6.8, result[0][1], precision: 6);
    }

    [Fact]
    public void ParseTextAreaPoints_WhitespaceAroundValues_ParsesCorrectly()
    {
        // Arrange- extra whitespace
        var input = "  -7 ,  6.8  \n  2  ,  7.0  ";

        // Act
        var result = MathHelpers.ParseTextAreaPoints(input);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(-7, result[0][0]);
        Assert.Equal(6.8, result[0][1]);
    }

    [Fact]
    public void GetFlatEta_SinglePoint_ReturnsThatValue()
    {
        // Arrange- only one point
        var points = new List<double[]> { new[] { 5.0, 0.42 } };

        // Act & Assert- below, at, and above the single point
        Assert.Equal(0.42, MathHelpers.GetFlatEta(0, points));
        Assert.Equal(0.42, MathHelpers.GetFlatEta(5, points));
        Assert.Equal(0.42, MathHelpers.GetFlatEta(20, points));
    }

    [Fact]
    public void GetMaxCop_VeryHighFlowTemp_ReturnsMinimum2()
    {
        // Arrange- flowTemp = 100 → 8.0 - (100-35)*0.15 = 8.0 - 9.75 = -1.75, clamped to 2.0
        var result = MathHelpers.GetMaxCop(100);

        // Assert
        Assert.Equal(2.0, result);
    }

    [Fact]
    public void ParseCopData_ExtraColumns_IgnoresExtras()
    {
        // Arrange- line has 4 columns instead of 3
        var input = "35, -7, 2.80, extra";

        // Act
        var result = MathHelpers.ParseCopData(input);

        // Assert
        Assert.Single(result);
        Assert.Equal(35, result[0][0]);
        Assert.Equal(-7, result[0][1]);
        Assert.Equal(2.80, result[0][2], precision: 2);
    }
}
