using Genesis.Core;

namespace Genesis.Cli;

public static class CognitionDemo
{
    public static Scenario Scenario(Scenario baseline) => baseline with
    {
        Name = "first-local-exploration", InitialStageCounts = [0, 20, 0], InitialVariantWeights = [1, 0, 0], InitialStockFraction = 0,
        InitialPopulations = [new(new(0), new(0), [0, 20, 0])],
        Species = [baseline.Species[0] with { FoodDemand = 1, AnnualBirthRate = 0, NaturalMortality = 0,
            ColdTolerance = -100, HeatTolerance = 100, MigrationRate = 0, AdultMonths = 12000, MaxTravelCost = 3 }],
        Rules = baseline.Rules with { Productivity = 100, StarvationMortality = 0, ClimateMortality = 0, MutationRate = 0, ResistanceFoodCost = 0 },
        Regions = [baseline.Regions[0] with { Area = 1, Fertility = 1, BaseTemperature = 25, BaseRainfall = 1000, Capacity = 10, Neighbors = [new(new(1), 1)] },
            baseline.Regions[1] with { Area = 1, Fertility = 1, BaseTemperature = 25, BaseRainfall = 1000, Capacity = 100, Neighbors = [new(new(0), 1)] }]
    };
    public static object Run(Scenario baseline, string output)
    {
        Validation.Require(!Directory.Exists(output) && !File.Exists(output), "Demo directory already exists.");
        var scenario = Scenario(baseline);
        foreach (var mode in new[] { CognitionMode.ObserveOnly, CognitionMode.Decide })
        {
            var initial = Simulation.Create(scenario, 1, cognitionOptions: new(mode));
            var commands = PopulationOwnership.PlanPromotions(initial, 1, 1);
            var path = Path.Combine(output, mode.ToString());
            RunArchive.Run(initial, 3, path, 1, 1, 2, 0, commands); RunArchive.Verify(path);
        }
        var observer = Persistence.Load(Path.Combine(output, "ObserveOnly", "snapshot-00000002.json"));
        var active = Persistence.Load(Path.Combine(output, "Decide", "snapshot-00000002.json"));
        var decision = File.ReadLines(Path.Combine(output, "Decide", "cognition-events.jsonl"))
            .Select(Persistence.Decode<CognitionEvent>).Single(e => e.Tick == 2);
        Validation.Require(decision.Reason == "explore-unfamiliar-exit" && active.Identity.Living[0].Cohort.Region.Value == 1
            && observer.Identity.Living[0].Cohort.Region.Value == 0 && active.Population == 20 && observer.Population == 20, "Demonstration failed its causal assertions.");
        var report = new { status = "complete", decision, activePopulation = active.Population,
            activeRegion = active.Identity.Living[0].Cohort.Region, controlRegion = observer.Identity.Living[0].Cohort.Region,
            knowledge = "Destination conditions remained unknown; only its legal exit and travel cost were provided.",
            actualFirstNutrition = decision.PriorMemory.Single().Nutrition, actualSecondNutrition = decision.Observation.Feeding.Nutrition };
        File.WriteAllText(Path.Combine(output, "demo.json"), Persistence.Encode(report));
        return report;
    }
}
