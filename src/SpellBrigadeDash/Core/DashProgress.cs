namespace SpellBrigadeDash.Core;

using System;
using System.Collections.Generic;

public enum DashCardId
{
    /// <summary>Unlocks the dash itself. The run starts without a dash.</summary>
    Dash,

    /// <summary>+1 segment in the dash bar.</summary>
    ExtraCharge,

    /// <summary>Charges refill faster.</summary>
    QuickRecovery,

    /// <summary>The dash travels farther in the same time.</summary>
    LongStride,

    /// <summary>The dash path leaves a damaging trail.</summary>
    BlazingDash,

    /// <summary>The dash no longer costs health.</summary>
    EtherealStep,
}

/// <summary>
/// Per-run upgrade state: which dash cards the local player picked and how often.
/// Reset at the start of each run.
/// </summary>
public sealed class DashProgress
{
    private readonly Dictionary<DashCardId, int> _stacks = new();

    /// <summary>Raised after a card is applied or the progress is reset.</summary>
    public event Action? Changed;

    public bool Unlocked => Stacks(DashCardId.Dash) > 0;

    public int Stacks(DashCardId id) => _stacks.TryGetValue(id, out var n) ? n : 0;

    public void Apply(DashCardId id)
    {
        _stacks[id] = Stacks(id) + 1;
        Changed?.Invoke();
    }

    public void Lock()
    {
        _stacks.Remove(DashCardId.Dash);
        Changed?.Invoke();
    }

    public void Reset()
    {
        _stacks.Clear();
        Changed?.Invoke();
    }

    public override string ToString()
    {
        var parts = new List<string>();
        foreach (DashCardId id in Enum.GetValues(typeof(DashCardId)))
        {
            var n = Stacks(id);
            if (n > 0) parts.Add($"{id}x{n}");
        }

        return parts.Count == 0 ? "(none)" : string.Join(", ", parts);
    }
}
