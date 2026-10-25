namespace Genesis.Core;

public static class EnvironmentModel
{
    public static double MeanTemperature(RegionDefinition region, Rules rules, int tick) =>
        region.BaseTemperature + rules.TemperatureOffset + 8 * Math.Sin(2 * Math.PI * ((tick - 1) % 12) / 12);

    public static double MeanRainfall(RegionDefinition region, Rules rules, int tick) =>
        region.BaseRainfall * rules.RainMultiplier *
        (tick > rules.DroughtStart && tick <= rules.DroughtEnd ? rules.DroughtMultiplier : 1);

    public static double Production(RegionDefinition region, Rules rules, double temperature, double rainfall) =>
        region.Area * region.Fertility * rules.Productivity * Math.Min(1.5, rainfall / 100)
        * Math.Clamp((temperature + 10) / 25, 0, 1);

    // One-month plug-in estimate using mean weather, not actual future random draws.
    // Stock already includes this month's regrowth and consumption. Only NEW production is added.
    public static FoodForecast Forecast(RegionDefinition region, Rules rules, RegionState current, int nextTick)
    {
        double temperature = MeanTemperature(region, rules, nextTick);
        double rainfall = MeanRainfall(region, rules, nextTick);
        double growth = Math.Min(region.Capacity - current.Forage, Production(region, rules, temperature, rainfall));
        return new(region.Id, nextTick, current.Forage, temperature, rainfall, growth, current.Forage + growth);
    }
}
