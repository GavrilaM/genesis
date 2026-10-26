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
public sealed record InitialPopulation(RegionId Region, SpeciesId Species, long[] StageCounts,
    double[]? VariantWeights = null, long[][]? StageVariantCounts = null);
// Zero bins is the legacy geometric comparison. 1 is exact monthly minimum age;
// 12 is the coarse candidate. These switches are persisted for reproducible assays.
public sealed record DemographyOptions(int JuvenileBinMonths = 1, bool BinomialEvents = true,
    bool InitialJuvenilesAreNewborn = false);
public sealed record Scenario(int SchemaVersion, string Name, Rules Rules,
    RegionDefinition[] Regions, SpeciesDefinition[] Species, long[] InitialStageCounts,
    double[] InitialVariantWeights, double InitialStockFraction, InitialPopulation[]? InitialPopulations = null,
    DemographyOptions? Demography = null);
public sealed record RegionState(RegionId Id, double Temperature, double Rainfall, double Forage);
public sealed record Cohort(CohortId Id, long Count, long[]? JuvenileBins = null);
public sealed record GroupMemory(RegionId Region, SpeciesId Species, int HungryMonths, bool Famine);
public sealed record PopulationWindow(RegionId Region, SpeciesId Species, int StartTick,
    long Start, long Births, long Deaths, long Inward, long Outward, double Demand, double Consumed, long Matured = 0);
public sealed record PopulationSummary(PopulationWindow Window, int EndTick, long End);
public sealed record WorldState(int SchemaVersion, string EngineVersion, string RulesetHash,
    ulong Seed, int CurrentTick, string RngAlgorithm, Scenario Scenario, RegionState[] Regions,
    Cohort[] Cohorts, GroupMemory[] Memory, long EventCount, string EventHash, PopulationWindow[] Windows,
    IdentityState Identity)
{
    // Cohorts own anonymous people only. Identities never also belong to a cohort.
    [JsonIgnore] public long Population => Cohorts.Sum(c => c.Count) + Identity.Living.Length;
}
public sealed record Evidence(string Name, double Value);
public sealed record SimEvent(EventId Id, int Tick, string Type, SpeciesId[] ActorRefs,
    RegionId[] RegionRefs, double Before, double After, Evidence[] Contributors,
    string SourceSystem, string ReasonCode, string Severity);
public sealed record PopulationFlow(RegionId Region, SpeciesId Species, long Start, long Births,
    long Deaths, long Inward, long Outward, long End, double Demand, double Consumed,
    double Nutrition, int HungryMonths, long[] TraitCounts, long Matured = 0);
public sealed record FoodFlow(RegionId Region, double Start, double Regrowth, double Consumed,
    double End, double Temperature, double Rainfall);
public sealed record Transfer(CohortId Origin, RegionId Destination, long Count, double Cost,
    double OriginScore, double DestinationScore);
public sealed record CohortAudit(CohortId Cohort, long Start, double ExposureDegrees,
    double DeathProbability, long Deaths, long Survivors);
public sealed record FoodForecast(RegionId Region, int Tick, double Stock, double Temperature,
    double Rainfall, double Regrowth, double Available);
public sealed record TickResult(WorldState State, PopulationFlow[] Populations, FoodFlow[] Food,
    Transfer[] Transfers, SimEvent[] Events, CohortAudit[] Audit, FoodForecast[] Forecasts,
    PopulationSummary[] Summaries, PersonEvent[] PersonEvents);
