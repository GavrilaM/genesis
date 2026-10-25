using Genesis.Core;
using Xunit;

namespace Genesis.Tests;

public class MigrationForecastTests
{
    public static Scenario CapacityCase()
    {
        var b = MechanicsTests.Baseline();
        return b with
        {
            InitialStageCounts = [0, 80, 0], InitialVariantWeights = [1, 0, 0], InitialStockFraction = 0,
            Rules = b.Rules with { Productivity = 100, StarvationMortality = 0, ClimateMortality = 0, ResistanceFoodCost = 0 },
            Species = [b.Species[0] with { FoodDemand = 1, AnnualBirthRate = 0, NaturalMortality = 0,
                ColdTolerance = -100, HeatTolerance = 100, MigrationRate = 1, AdultMonths = 12000 }],
            Regions = [
                b.Regions[0] with { Area = 1, Fertility = 1, BaseTemperature = 25, BaseRainfall = 1000, Capacity = 0, Neighbors = [new(new(1), .01)] },
                b.Regions[1] with { Area = 1, Fertility = 1, BaseTemperature = 25, BaseRainfall = 1000, Capacity = 100, Neighbors = [new(new(0), .01)] }]
        };
    }
    [Fact] public void MigrationCannotForecastMoreFoodThanStorageCanHold()
    {
        var s = CapacityCase(); var step = Simulation.Step(Simulation.Create(s, 1));
        var transfer = Assert.Single(step.Transfers, t => t.Origin.Region.Value == 0 && t.Origin.Stage == 1);
        // Destination begins with 80 consumers, produces 100, and consumes 80.
        // Next month it can hold at most 100, not leftover 20 + last regrowth 100.
        Assert.True(transfer.DestinationScore <= 100.0 / 80 - .01 * s.Rules.TravelPenalty + 1e-10,
            $"Impossible capacity forecast score {transfer.DestinationScore}");
    }
    [Fact] public void ForecastUsesNextMonthsDroughtAndRegrowsNewlyFreedStorage()
    {
        var s = CapacityCase(); var r = s.Regions[1];
        var depleted = new RegionState(r.Id, 25, 1000, 20);
        var full = new RegionState(r.Id, 25, 1000, 100);
        var growing = EnvironmentModel.Forecast(r, s.Rules, depleted, 2);
        Assert.Equal(80, growing.Regrowth); Assert.Equal(100, growing.Available);
        Assert.Equal(0, EnvironmentModel.Forecast(r, s.Rules, full, 2).Regrowth);
        var drought = s.Rules with { DroughtStart = 1, DroughtEnd = 12, DroughtMultiplier = 0 };
        var dry = EnvironmentModel.Forecast(r, drought, depleted, 2);
        Assert.Equal(0, dry.Regrowth); Assert.Equal(20, dry.Available);
        Assert.Equal(100, EnvironmentModel.Forecast(r, drought, depleted, 13).Available);
    }
    [Fact] public void ForecastIsAnEstimateAndDoesNotReadFutureRandomWeather()
    {
        var s = CapacityCase(); var region = new RegionState(new(1), 0, 0, 20);
        var forecast = EnvironmentModel.Forecast(s.Regions[1], s.Rules, region, 3);
        Assert.Equal(3, forecast.Tick);
        Assert.Equal(s.Regions[1].BaseRainfall, forecast.Rainfall);
        var step = Simulation.Step(Simulation.Create(s, 1), audit: true);
        Assert.All(step.Forecasts, f => Assert.InRange(f.Available, 0, s.Regions[f.Region.Value].Capacity));
    }
}
