namespace Genesis.Core;

public static class CognitionValidation
{
    public static void Options(CognitionOptions o)
    {
        Validation.Require(o is not null && Enum.IsDefined(o.Mode) && o.Policy == "survival-v1"
            && o.MemoryCapacity is >= 1 and <= 8 && o.MemoryMaxAge is >= 1 and <= 120
            && double.IsFinite(o.AdequateNutrition) && o.AdequateNutrition is > 0 and <= 1, "Invalid cognition options.");
    }
    public static void Memory(MindState m, CognitionOptions o, int tick)
    {
        Validation.Require(m is not null && m.Memory is not null && m.Memory.Length > 0 && m.Memory.Length <= o.MemoryCapacity,
            "Invalid bounded memory.");
        int previous = 0;
        foreach (var item in m!.Memory)
        {
            Validation.Require(item is not null && item.Tick > previous && item.Tick <= tick && item.Region.Value >= 0
                && double.IsFinite(item.Nutrition) && item.Nutrition is >= 0 and <= 1, "Invalid dated memory observation.");
            previous = item!.Tick;
        }
        Validation.Require(m.LastObservedTick == previous && (m.LastDecisionTick is null || m.LastDecisionTick > 0 && m.LastDecisionTick <= previous), "Invalid mind observation/decision cursor.");
    }
    public static void State(WorldState w)
    {
        var c = w.Cognition;
        Validation.Require(c is not null && c.Options is not null && c.Minds is not null && c.EventHash is not null, "Incomplete cognition state.");
        Options(c!.Options);
        Validation.Require(c.EventCount >= 0 && c.EventHash.Length == 64 && c.EventHash.All(Uri.IsHexDigit), "Invalid cognition history cursor.");
        Validation.Require(c.EventCount != 0 || c.EventHash == Persistence.EmptyEventHash, "Empty cognition history must have an empty cursor.");
        Validation.Require(c.Minds.Length <= w.Identity.Living.Length, "Mind has no living population owner.");
        if (c.Minds.Length == 0) return;
        var people = w.Identity.Living.ToDictionary(p => p.Id); long previous = 0;
        foreach (var m in c.Minds)
        {
            Memory(m, c.Options, w.CurrentTick);
            Validation.Require(m.Person.Value > previous && people.TryGetValue(m.Person, out var p)
                && m.Memory.All(o => o.Tick > p.PromotedTick && o.Region.Value < w.Regions.Length)
                && (m.LastDecisionTick is null || m.LastDecisionTick > p.PromotedTick), "Mind ownership/order/provenance failed.");
            previous = m.Person.Value;
        }
        Validation.Require(c.EventCount >= c.Minds.Sum(m => (long)m.Memory.Length), "Memory has no recorded observation history.");
    }
}
