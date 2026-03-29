using WaermepumpeSimulator.Services;

namespace WaermepumpeSimulator.Tests;

public class SimulationEngineTests
{
    // Validation

    [Fact]
    public void Run_TooFewWeatherPoints_ThrowsArgumentException()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather().Take(100).ToList();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SimulationEngine.Run(parameters, weather));
        Assert.Contains("unvollständig", ex.Message);
    }

    [Fact]
    public void Run_TooFewPMaxPoints_ThrowsArgumentException()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        parameters.RawPMax = "-7, 6.8"; // only 1 point
        var weather = TestHelpers.ConstantWeather();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SimulationEngine.Run(parameters, weather));
        Assert.Contains("PMax", ex.Message);
    }

    [Fact]
    public void Run_NegativePMaxValue_ThrowsArgumentException()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        parameters.RawPMax = "-7, -1.0\n7, 7.0";
        var weather = TestHelpers.ConstantWeather();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SimulationEngine.Run(parameters, weather));
        Assert.Contains("Leistungswerte", ex.Message);
    }

    [Fact]
    public void Run_TooFewCopPoints_ThrowsArgumentException()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        parameters.RawCopData = "35, -7, 2.80"; // only 1 point
        var weather = TestHelpers.ConstantWeather();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SimulationEngine.Run(parameters, weather));
        Assert.Contains("COP", ex.Message);
    }

    [Fact]
    public void Run_ZeroCopValue_ThrowsArgumentException()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        parameters.RawCopData = "35, -7, 0\n35, 7, 3.0";
        var weather = TestHelpers.ConstantWeather();

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => SimulationEngine.Run(parameters, weather));
        Assert.Contains("COP-Werte", ex.Message);
    }

    // Result Structure

    [Fact]
    public void Run_ValidInput_Returns8760HourlyArrays()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(8760, result.Temperature.Length);
        Assert.Equal(8760, result.Load.Length);
        Assert.Equal(8760, result.Cop.Length);
        Assert.Equal(8760, result.ThermalPower.Length);
        Assert.Equal(8760, result.ElectricalPower.Length);
        Assert.Equal(8760, result.HeizstabPower.Length);
        Assert.Equal(8760, result.Deficit.Length);
    }

    [Fact]
    public void Run_ValidInput_ReturnsLookupTableFrom_Minus25_To_40()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(131, result.LookupTemperatures.Length);
        Assert.Equal(-25.0, result.LookupTemperatures[0]);
        Assert.Equal(40.0, result.LookupTemperatures[^1]);
        Assert.Equal(0.5, result.LookupTemperatures[1] - result.LookupTemperatures[0], precision: 10);
    }

    // Energy Conservation

    [Fact]
    public void Run_Efficiency1_NoHotWater_TotalHeatMatchesJahresverbrauch()
    {
        // Arrange- Wirkungsgrad=1 and WarmwasserAnteil=0 means all energy is heating
        var parameters = TestHelpers.DefaultParams();
        parameters.Wirkungsgrad = 1.0;
        parameters.WarmwasserAnteil = 0;
        parameters.HeizstabMax = 100; // large enough to cover all deficit
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- total heat delivered should match input energy
        double totalDelivered = result.ThermalPower.Sum() + result.HeizstabPower.Sum();
        Assert.Equal(parameters.Jahresverbrauch, totalDelivered, precision: 0);
    }

    [Fact]
    public void Run_TotalElectricity_EqualsWpPlusHeizstab()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        double expectedElec = result.ElectricalPower.Sum() + result.HeizstabPower.Sum();
        Assert.Equal(expectedElec, result.TotalElectricity, precision: 6);
    }

    [Fact]
    public void Run_TotalHeat_EqualsThermalPlusHeizstab()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        double expectedHeat = result.ThermalPower.Sum() + result.HeizstabPower.Sum();
        Assert.Equal(expectedHeat, result.TotalHeat, precision: 6);
    }

    // JAZ Calculation

    [Fact]
    public void Run_Jaz_EqualsTotalHeatDividedByTotalElectricity()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        double expectedJaz = result.TotalHeat / result.TotalElectricity;
        Assert.Equal(expectedJaz, result.Jaz, precision: 6);
    }

    [Fact]
    public void Run_Jaz_IsInRealisticRange()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- JAZ for a typical system should be between 2 and 7
        Assert.InRange(result.Jaz, 2.0, 7.0);
    }

    // COP Constraints

    [Fact]
    public void Run_CopValues_AreAlwaysPositive()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- COP should be > 0 for every hour that has load
        for (int i = 0; i < 8760; i++)
        {
            if (result.Load[i] > 0)
                Assert.True(result.Cop[i] > 0, $"COP at hour {i} should be positive");
        }
    }

    // Power Balance

    [Fact]
    public void Run_EachHour_ThermalPlusHeizstabPlusDeficitEqualsLoad()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- energy balance must hold for every hour
        for (int i = 0; i < 8760; i++)
        {
            double delivered = result.ThermalPower[i] + result.HeizstabPower[i] + result.Deficit[i];
            Assert.Equal(result.Load[i], delivered, precision: 6);
        }
    }

    [Fact]
    public void Run_HeizstabNeverExceedsMax()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        parameters.HeizstabMax = 5.0;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.True(result.HeizstabPower.All(h => h <= parameters.HeizstabMax + 0.001));
    }

    [Fact]
    public void Run_HeizstabZero_AllGapBecomesDeficit()
    {
        // Arrange- small pump (3kW) + large building + cold weather → guaranteed deficit
        var parameters = TestHelpers.DefaultParams();
        parameters.HeizstabMax = 0;
        parameters.Jahresverbrauch = 30000;
        parameters.RawPMax = "-7, 3.0\n2, 3.5\n7, 4.0";
        var weather = TestHelpers.SinusoidalWeather(mean: -2, amplitude: 15);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- with no backup heater, all excess load is deficit
        Assert.True(result.HeizstabPower.All(h => h == 0));
        Assert.True(result.DeficitKwh > 0, "Small pump with no heizstab should have deficit");
    }

    // No Heating Above Heizgrenze

    [Fact]
    public void Run_AllHoursAboveHeizgrenze_NoHeatingLoad()
    {
        // Arrange- constant 25°C, way above Heizgrenze of 15°C
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 25.0);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- no heating should occur
        Assert.Equal(0, result.TotalHeat, precision: 6);
        Assert.Equal(0, result.TotalElectricity, precision: 6);
    }

    // Design Point

    [Fact]
    public void Run_DesignTemperature_MatchesNormAussentemperatur()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(parameters.NormAussentemperatur, result.DesignTemperature);
        Assert.Equal(parameters.Heizgrenze, result.HeatingLimitTemperature);
    }

    [Fact]
    public void Run_BivalenceTemperature_IsBelowHeizgrenze()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- bivalence point (if exists) must be below heating limit
        if (result.BivalenceTemperature.HasValue)
            Assert.True(result.BivalenceTemperature.Value < parameters.Heizgrenze);
    }

    // Costs

    [Fact]
    public void Run_CostHeatPump_EqualsTotalElectricityTimesStromPreis()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(result.TotalElectricity * parameters.PreisStrom, result.CostHeatPump, precision: 6);
    }

    [Fact]
    public void Run_CostOldHeating_EqualsJahresverbrauchTimesPreisAlt()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(parameters.Jahresverbrauch * parameters.PreisAlt, result.CostOldHeating, precision: 6);
    }

    [Fact]
    public void Run_Savings_EqualsCostOldMinusCostHeatPump()
    {
        // Arrange
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(result.CostOldHeating - result.CostHeatPump, result.Savings, precision: 6);
    }

    // Night Setback

    [Fact]
    public void Run_NightSetbackActive_ReducesNightLoad()
    {
        // Arrange
        var paramsWithout = TestHelpers.DefaultParams();
        paramsWithout.NachtabsenkungAktiv = false;

        var paramsWith = TestHelpers.DefaultParams();
        paramsWith.NachtabsenkungAktiv = true;
        paramsWith.NachtDeltaT = 5.0;

        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var resultWithout = SimulationEngine.Run(paramsWithout, weather);
        var resultWith = SimulationEngine.Run(paramsWith, weather);

        // Assert- night setback should reduce total heat demand
        Assert.True(resultWith.TotalHeat < resultWithout.TotalHeat,
            "Night setback should reduce total heat demand");
    }

    // Icing

    [Fact]
    public void Run_ColdHumidWeather_DetectsIcing()
    {
        // Arrange- 1°C, 95% humidity, large building to ensure load > PMin * 1.2 (icing gate)
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 40000;
        var weather = TestHelpers.ConstantWeather(temperature: 1.0, humidity: 95.0);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- should detect some icing hours
        Assert.True(result.IcingHours > 0, "Should detect icing in cold humid conditions with high load");
    }

    [Fact]
    public void Run_WarmWeather_NoIcing()
    {
        // Arrange- 10°C is above icing range
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 10.0, humidity: 95.0);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.Equal(0, result.IcingHours);
    }

    [Fact]
    public void Run_ExtendedIcingRange_DetectsIcingAt6Celsius()
    {
        // 6°C is in the new 2–7°C frost-critical range.
        // With Jahresverbrauch=80000 the building demand (≈8.7 kW) exceeds PMax (≈7 kW) → loadFactor=1.0
        // → evap = 6 - (0.5 + 7.0×1.0) = -1.5°C < -0.5°C threshold → icing.
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 80000;
        var weather = TestHelpers.ConstantWeather(temperature: 6.0, humidity: 95.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.True(result.IcingHours > 0, "Icing should be detected at 6°C with high load after range extension");
    }

    // FrostCriticalHours

    [Fact]
    public void Run_FrostCriticalConditions_CountsAllHours()
    {
        // 4°C and 90% RH is inside the frost-critical ambient zone (2–7°C, RH ≥ 85%)
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 4.0, humidity: 90.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.Equal(8760, result.FrostCriticalHours);
    }

    [Fact]
    public void Run_BelowFrostCriticalTemp_NoFrostCriticalHours()
    {
        // 1°C is below the 2°C lower bound
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 1.0, humidity: 90.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.Equal(0, result.FrostCriticalHours);
    }

    [Fact]
    public void Run_AboveFrostCriticalTemp_NoFrostCriticalHours()
    {
        // 10°C is above the 7°C upper bound
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 10.0, humidity: 90.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.Equal(0, result.FrostCriticalHours);
    }

    [Fact]
    public void Run_LowHumidity_NoFrostCriticalHours()
    {
        // 70% RH is below the 85% threshold even though temperature is in range
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 4.0, humidity: 70.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.Equal(0, result.FrostCriticalHours);
    }

    // DefrostCycles / DefrostQuote

    [Fact]
    public void Run_WithIcingHours_DefrostCyclesMatchFormula()
    {
        // DefrostCyclesEstimate = round(IcingHours / 1.5)
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 40000;
        var weather = TestHelpers.ConstantWeather(temperature: 3.0, humidity: 95.0);

        var result = SimulationEngine.Run(parameters, weather);

        if (result.IcingHours > 0)
        {
            int expected = (int)Math.Round(result.IcingHours / 1.5);
            Assert.Equal(expected, result.DefrostCyclesEstimate);
        }
    }

    [Fact]
    public void Run_WithIcingHours_DefrostQuoteIsAboutElevenPercent()
    {
        // DefrostQuote = (cycles × 10min) / IcingHours ≈ 11.1% regardless of weather
        // because it's determined by the fixed model constants alone.
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 40000;
        var weather = TestHelpers.ConstantWeather(temperature: 3.0, humidity: 95.0);

        var result = SimulationEngine.Run(parameters, weather);

        if (result.IcingHours > 0)
            Assert.InRange(result.DefrostQuote, 10.0, 13.0); // ~11.1%, small rounding delta
    }

    [Fact]
    public void Run_NoIcingHours_DefrostQuoteIsZero()
    {
        // No icing → defrost quota must be zero (guard against divide-by-zero)
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: 15.0, humidity: 80.0);

        var result = SimulationEngine.Run(parameters, weather);

        Assert.Equal(0, result.IcingHours);
        Assert.Equal(0.0, result.DefrostQuote);
    }

    // PMin / Cycling

    [Fact]
    public void Run_OversizedPump_HasHighCycling()
    {
        // Arrange- very large pump for a small building
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 5000; // small building
        parameters.RawPMax = "-7, 20.0\n2, 25.0\n7, 30.0"; // huge pump
        parameters.RawPMin = "-7, 8.0\n2, 10.0\n7, 12.0"; // high PMin
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- should have significant cycling
        Assert.True(result.CyclingPercent > 0, "Oversized pump should cause cycling");
    }

    // Hot Water Share

    [Fact]
    public void Run_WithHotWater_HasLoadEvenAboveHeizgrenze()
    {
        // Arrange- warm weather but with hot water demand
        var parameters = TestHelpers.DefaultParams();
        parameters.WarmwasserAnteil = 20;
        var weather = TestHelpers.ConstantWeather(temperature: 20.0);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- hot water creates load even when no space heating needed
        Assert.True(result.TotalHeat > 0, "Hot water should create load above Heizgrenze");
    }

    // Edge Cases

    [Fact]
    public void Run_HeizgrenzeEqualsNormAussentemperatur_DoesNotThrow()
    {
        // Arrange- triggers the division-by-zero guard in heating curve slope
        var parameters = TestHelpers.DefaultParams();
        parameters.Heizgrenze = -13;
        parameters.NormAussentemperatur = -13;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- should complete without NaN or infinity
        Assert.Equal(8760, result.Temperature.Length);
        Assert.True(double.IsFinite(result.Jaz));
        Assert.DoesNotContain(double.NaN, result.ThermalPower);
    }

    [Fact]
    public void Run_VorlaufMinEqualsVorlaufMax_FlatHeatingCurve()
    {
        // Arrange- flat heating curve, no temperature-dependent vorlauf change
        var parameters = TestHelpers.DefaultParams();
        parameters.VorlaufMin = 40;
        parameters.VorlaufMax = 40;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- should run without issues
        Assert.True(result.Jaz > 0);
        Assert.Equal(8760, result.ThermalPower.Length);
    }

    [Fact]
    public void Run_VorlaufMaxExactly35_UsesOnlyVL35Curves()
    {
        // Arrange- vorlauf at exactly the lower reference temperature
        var parameters = TestHelpers.DefaultParams();
        parameters.VorlaufMin = 30;
        parameters.VorlaufMax = 35;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.True(result.Jaz > 0);
        Assert.Equal(8760, result.ThermalPower.Length);
    }

    [Fact]
    public void Run_VorlaufMaxExactly55_UsesOnlyVL55Curves()
    {
        // Arrange- vorlauf at exactly the upper reference temperature
        var parameters = TestHelpers.DefaultParams();
        parameters.VorlaufMin = 40;
        parameters.VorlaufMax = 55;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert
        Assert.True(result.Jaz > 0);
        Assert.Equal(8760, result.ThermalPower.Length);
    }

    [Fact]
    public void Run_NightSetbackWrappingMidnight_ReducesLoad()
    {
        // Arrange- night from 22:00 to 06:00 crosses midnight
        var paramsWithout = TestHelpers.DefaultParams();
        paramsWithout.NachtabsenkungAktiv = false;

        var paramsWith = TestHelpers.DefaultParams();
        paramsWith.NachtabsenkungAktiv = true;
        paramsWith.NachtStart = 22;
        paramsWith.NachtEnde = 6;
        paramsWith.NachtDeltaT = 5.0;

        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var resultWithout = SimulationEngine.Run(paramsWithout, weather);
        var resultWith = SimulationEngine.Run(paramsWith, weather);

        // Assert- night setback wrapping midnight should still reduce demand
        Assert.True(resultWith.TotalHeat < resultWithout.TotalHeat,
            "Night setback wrapping midnight should reduce total heat demand");
    }

    [Fact]
    public void Run_NightSetbackSameStartAndEnd_NoReduction()
    {
        // Arrange- NachtStart == NachtEnde means 0-hour night window
        var paramsWithout = TestHelpers.DefaultParams();
        paramsWithout.NachtabsenkungAktiv = false;

        var paramsWith = TestHelpers.DefaultParams();
        paramsWith.NachtabsenkungAktiv = true;
        paramsWith.NachtStart = 22;
        paramsWith.NachtEnde = 22;
        paramsWith.NachtDeltaT = 5.0;

        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var resultWithout = SimulationEngine.Run(paramsWithout, weather);
        var resultWith = SimulationEngine.Run(paramsWith, weather);

        // Assert- same start/end means no night hours, so no reduction
        Assert.Equal(resultWithout.TotalHeat, resultWith.TotalHeat, precision: 6);
    }

    [Fact]
    public void Run_WarmwasserAnteil100_AllLoadIsHotWater()
    {
        // Arrange- 100% hot water, 0% space heating
        var parameters = TestHelpers.DefaultParams();
        parameters.WarmwasserAnteil = 100;
        var weather = TestHelpers.SinusoidalWeather();

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- load should be constant every hour (no temperature dependency)
        Assert.True(result.TotalHeat > 0);
        // Check that load is roughly uniform (hot water is constant)
        double expectedPerHour = result.TotalHeat / 8760;
        for (int i = 0; i < 8760; i++)
        {
            Assert.Equal(expectedPerHour, result.Load[i], precision: 3);
        }
    }

    [Fact]
    public void Run_ExtremelyColdWeather_CompletesWithoutErrors()
    {
        // Arrange- constant -25°C pushes everything to extremes
        var parameters = TestHelpers.DefaultParams();
        var weather = TestHelpers.ConstantWeather(temperature: -25.0);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- should produce valid results without NaN/infinity
        Assert.True(result.Jaz > 0 || result.TotalElectricity == 0);
        Assert.DoesNotContain(double.NaN, result.ThermalPower);
        Assert.DoesNotContain(double.NaN, result.Cop);
        Assert.True(result.DeficitKwh >= 0);
    }

    [Fact]
    public void Run_HeizstabExactlyCoversGap_NoDeficit()
    {
        // Arrange- pump can deliver 3kW, building needs more, but heizstab is very large
        var parameters = TestHelpers.DefaultParams();
        parameters.Jahresverbrauch = 30000;
        parameters.RawPMax = "-7, 3.0\n2, 3.5\n7, 4.0";
        parameters.HeizstabMax = 100; // huge heizstab covers any gap
        var weather = TestHelpers.SinusoidalWeather(mean: -2, amplitude: 15);

        // Act
        var result = SimulationEngine.Run(parameters, weather);

        // Assert- large enough heizstab should prevent any deficit
        Assert.Equal(0, result.DeficitKwh, precision: 6);
        Assert.Equal(0, result.DeficitHours);
    }
}
