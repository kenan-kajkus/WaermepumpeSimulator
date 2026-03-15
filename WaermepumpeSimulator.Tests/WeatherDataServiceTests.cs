using WaermepumpeSimulator.Models;
using WaermepumpeSimulator.Services;

namespace WaermepumpeSimulator.Tests;

public class WeatherDataServiceTests
{
    // ParseCsv

    [Fact]
    public void ParseCsv_ValidCsv_ParsesCorrectly()
    {
        // Arrange
        var service = CreateService();
        var csv = "time,temperature_2m,relative_humidity_2m\n2024-01-01T00:00,5.2,80\n2024-01-01T01:00,4.8,82";

        // Act
        var (data, years) = service.ParseCsv(csv);

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Equal(5.2, data[0].Temperature, precision: 1);
        Assert.Equal(80, data[0].RelativeHumidity);
        Assert.Equal(4.8, data[1].Temperature, precision: 1);
        Assert.Contains(2024, years);
    }

    [Fact]
    public void ParseCsv_NoHeader_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();
        var csv = "1,2,3\n4,5,6";

        // Act
        var (data, years) = service.ParseCsv(csv);

        // Assert
        Assert.Empty(data);
    }

    [Fact]
    public void ParseCsv_MissingHumidity_DefaultsTo80()
    {
        // Arrange
        var service = CreateService();
        var csv = "time,t2m\n2024-01-01T00:00,5.2\n2024-01-01T01:00,4.8";

        // Act
        var (data, _) = service.ParseCsv(csv);

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Equal(80, data[0].RelativeHumidity);
    }

    [Fact]
    public void ParseCsv_ExtractsYearFromTimestamp()
    {
        // Arrange
        var service = CreateService();
        var csv = "date,temp\n2023-06-15T12:00,22.5\n2024-01-01T00:00,1.0";

        // Act
        var (data, years) = service.ParseCsv(csv);

        // Assert
        Assert.Equal(2023, data[0].Year);
        Assert.Equal(2024, data[1].Year);
        Assert.Contains(2023, years);
        Assert.Contains(2024, years);
    }

    [Fact]
    public void ParseCsv_EmptyString_ReturnsEmpty()
    {
        // Arrange
        var service = CreateService();

        // Act
        var (data, years) = service.ParseCsv("");

        // Assert
        Assert.Empty(data);
        Assert.Empty(years);
    }

    // FilterByYear

    [Fact]
    public void FilterByYear_SingleYear_Returns8760Points()
    {
        // Arrange
        var allData = Enumerable.Range(0, 8760 * 2)
            .Select(i => new WeatherDataPoint
            {
                Temperature = i < 8760 ? 5.0 : 15.0,
                RelativeHumidity = 70,
                Index = i,
                Year = i < 8760 ? 2023 : 2024
            })
            .ToList();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "2023");

        // Assert
        Assert.Equal(8760, result.Count);
        Assert.All(result, p => Assert.Equal(5.0, p.Temperature));
    }

    [Fact]
    public void FilterByYear_AllYears_AveragesAndReturns8760()
    {
        // Arrange two years: year 1 all 0°C, year 2 all 10°C
        var allData = Enumerable.Range(0, 8760 * 2)
            .Select(i => new WeatherDataPoint
            {
                Temperature = i < 8760 ? 0.0 : 10.0,
                RelativeHumidity = 70,
                Index = i,
                Year = i < 8760 ? 2023 : 2024
            })
            .ToList();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "all");

        // Assert
        Assert.Equal(8760, result.Count);
        Assert.Equal(5.0, result[0].Temperature, precision: 1); // average of 0 and 10
    }

    [Fact]
    public void FilterByYear_ShortYear_PadsTo8760()
    {
        // Arrange only 100 hours of data for year 2023
        var allData = Enumerable.Range(0, 100)
            .Select(i => new WeatherDataPoint
            {
                Temperature = 5.0,
                RelativeHumidity = 70,
                Index = i,
                Year = 2023
            })
            .ToList();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "2023");

        // Assert should be padded to 8760 by cycling
        Assert.Equal(8760, result.Count);
    }

    // Edge Cases

    [Fact]
    public void FilterByYear_NoDataForRequestedYear_ReturnsEmpty()
    {
        // Arrange data is for 2023 but we request 2024
        var allData = Enumerable.Range(0, 8760)
            .Select(i => new WeatherDataPoint
            {
                Temperature = 5.0,
                RelativeHumidity = 70,
                Index = i,
                Year = 2023
            })
            .ToList();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "2024");

        // Assert no data for 2024, result should be empty (no padding source)
        Assert.Empty(result);
    }

    [Fact]
    public void FilterByYear_EmptyData_ReturnsEmpty()
    {
        // Arrange
        var allData = new List<WeatherDataPoint>();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "2023");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FilterByYear_AllYears_EmptyData_Returns8760Defaults()
    {
        // Arrange empty data, asking for "all"
        var allData = new List<WeatherDataPoint>();

        // Act
        var result = WeatherDataService.FilterByYear(allData, "all");

        // Assert accumulator produces 8760 points with count=0 defaults
        Assert.Equal(8760, result.Count);
        Assert.All(result, p => Assert.Equal(0, p.Temperature));
    }

    [Fact]
    public void ParseCsv_WindowsLineEndings_ParsesCorrectly()
    {
        // Arrange \r\n line endings
        var service = CreateService();
        var csv = "time,temperature_2m,relative_humidity_2m\r\n2024-01-01T00:00,5.2,80\r\n2024-01-01T01:00,4.8,82";

        // Act
        var (data, years) = service.ParseCsv(csv);

        // Assert
        Assert.Equal(2, data.Count);
        Assert.Equal(5.2, data[0].Temperature, precision: 1);
    }

    // Helpers

    private static WeatherDataService CreateService()
    {
        return new WeatherDataService(new HttpClient());
    }
}
