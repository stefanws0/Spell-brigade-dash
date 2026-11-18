namespace SpellBrigadeDash.Core;

using System.Collections.Generic;
using System.Globalization;

/// <summary>One dash row in the game's stats list (Escape screen and the hold-to-show stats panel).</summary>
public sealed class DashStatRow
{
    public DashStatRow(string key, DashCardId icon, string title, string value, bool visible)
    {
        Key = key;
        Icon = icon;
        Title = title;
        Value = value;
        Visible = visible;
    }

    /// <summary>Stable id of the row; the game-side row object is created once per key.</summary>
    public string Key { get; }

    /// <summary>Whose card icon the row shows.</summary>
    public DashCardId Icon { get; }

    public string Title { get; }
    public string Value { get; }
    public bool Visible { get; }
}

/// <summary>
/// What the dash rows in the game's stats list say. Always the same rows in the same order (see <see cref="Keys"/>),
/// so the game-side objects are made once and only shown, hidden and relabelled. Pure, unit-tested.
/// </summary>
public static class DashStatRows
{
    public static readonly IReadOnlyList<string> Keys = new[] { "charges", "recharge", "distance", "cost", "blazing" };

    public static IReadOnlyList<DashStatRow> Build(DashBaseStats b, DashProgress p)
    {
        var on = p.Unlocked;
        var cost = DashTuning.HealthCostFraction(b, p);
        return new[]
        {
            new DashStatRow("charges", DashCardId.ExtraCharge, "Dash Charges", DashTuning.MaxCharges(b, p).ToString(CultureInfo.InvariantCulture), on),
            new DashStatRow("recharge", DashCardId.QuickRecovery, "Dash Recharge", Number(DashTuning.RechargeSeconds(b, p)) + "s", on),
            new DashStatRow("distance", DashCardId.LongStride, "Dash Distance", Number(DashTuning.Distance(b, p)), on),
            new DashStatRow("cost", DashCardId.EtherealStep, "Dash HP Cost", cost > 0f ? Number(cost * 100f) + "%" : "None", on),
            new DashStatRow("blazing", DashCardId.BlazingDash, "Blazing Dash", "On", on && DashTuning.Blazing(p)),
        };
    }

    private static string Number(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
