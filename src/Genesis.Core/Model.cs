using System.Text.Json.Serialization;

namespace Genesis.Core;

public readonly record struct RegionId(int Value);
public readonly record struct SpeciesId(int Value);
public readonly record struct CohortId(RegionId Region, SpeciesId Species, int Stage, int Variant);
public readonly record struct EventId(long Value);

public sealed record Edge(RegionId Destination, double Cost);
public sealed record RegionDefinition(RegionId Id, string Name, string Terrain, double Area,
    double Fertility, double BaseTemperature, double BaseRainfall, double Capacity, Edge[] Neighbors);
public sealed record SpeciesDefinition(SpeciesId Id, string Name, double FoodDemand,
    int MaturationMonths, int AdultMonths, int ElderMonths, double AnnualBirthRate,
    double NaturalMortality, double ColdTolerance, double HeatTolerance,
    double MigrationRate, double MaxTravelCost);
public sealed record Rules
{
    public double Productivity { get; init; } = 3;
    public double RainMultiplier { get; init; } = 1;
    public int DroughtStart { get; init; } = 120;
    public int DroughtEnd { get; init; } = 240;
    public double DroughtMultiplier { get; init; } = 1;
    public double TemperatureOffset { get; init; }
    public double StarvationMortality { get; init; } = .07;
    public double ClimateMortality { get; init; } = .0015;
    public double MutationRate { get; init; } = .01;
    public double ResistanceDegrees { get; init; } = 5;
    public double ResistanceFoodCost { get; init; } = .04;
    public double FamineStart { get; init; } = .65;
    public double FamineEnd { get; init; } = .9;
    public double MigrationThreshold { get; init; } = .1;
    public double TravelPenalty { get; init; } = .03;
}
public sealed record Scenario(int SchemaVersion, string Name, Rules Rules,
    RegionDefinition[] Regions, SpeciesDefinition[] Species, long[] InitialStageCounts,
    double[] InitialVariantWeights, double InitialStockFraction);
public sealed record RegionState(RegionId Id, double Temperature, double Rainfall, double Forage);
public sealed record Cohort(CohortId Id, long Count);
public sealed record GroupMemory(RegionId Region, SpeciesId Species, int HungryMonths, bool Famine);
public sealed record WorldState(int SchemaVersion, string EngineVersion, string RulesetHash,
    ulong Seed, int CurrentTick, string RngAlgorithm, Scenario Scenario, RegionState[] Regions,
    Cohort[] Cohorts, GroupMemory[] Memory, long EventCount, string EventHash)
{
    [JsonIgnore] public long Population => Cohorts.Sum(c => c.Count);
}
public sealed record Evidence(string Name, double Value);
public sealed record SimEvent(EventId Id, int Tick, string Type, SpeciesId[] ActorRefs,
    RegionId[] RegionRefs, double Before, double After, Evidence[] Contributors,
    string SourceSystem, string ReasonCode, string Severity);
public sealed record PopulationFlow(RegionId Region, SpeciesId Species, long Start, long Births,
    long Deaths, long Inward, long Outward, long End, double Demand, double Consumed,
    double Nutrition, int HungryMonths, long[] TraitCounts);
public sealed record FoodFlow(RegionId Region, double Start, double Regrowth, double Consumed,
    double End, double Temperature, double Rainfall);
public sealed record Transfer(CohortId Origin, RegionId Destination, long Count, double Cost,
    double OriginScore, double DestinationScore);
public sealed record TickResult(WorldState State, PopulationFlow[] Populations, FoodFlow[] Food,
    Transfer[] Transfers, SimEvent[] Events);
