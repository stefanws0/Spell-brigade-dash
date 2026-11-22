namespace SpellBrigadeDash.Game;

using System;
using SpellBrigadeDash.Core;
using UnityEngine;

/// <summary>
/// Reads and lowers the wizard's HP for the dash's health cost, on the game's <c>PlayerHealthContainer</c>.
///
/// <c>CurrentHealth</c> / <c>MaxHealth</c> are read-only (they read network-synced copies), so the cost is written to
/// <c>SyncedCurrentHealth</c> (a NetworkVariable): the health bar updates and other players see the new HP. That
/// doesn't go through the game's damage path (no hit flash, and our own i-frames can't block it). Who may write it in
/// co-op is still unchecked.
/// </summary>
internal static class HealthBridge
{
    private static bool _firstCostLogged;
    private static bool _writeBroken;

    public static string Status { get; private set; } = "not used yet";

    /// <summary>Take the dash's HP cost. Returns the HP taken (0 if nothing was taken or HP couldn't be read).</summary>
    public static float ApplyCost(PlayerInfo p, float fraction)
    {
        if (fraction <= 0f || _writeBroken) return 0f;
        try
        {
            var health = p.Health;
            if (health == null)
            {
                Status = "the wizard has no PlayerHealthContainer; dash HP cost not applied";
                ModLog.WarnOnce("health-missing", "Dash HP cost: " + Status);
                return 0f;
            }

            var current = health.CurrentHealth;
            var max = health.MaxHealth;
            Status = $"PlayerHealthContainer.CurrentHealth ({current:0.#}) / MaxHealth ({max:0.#})";

            // health not set up yet (just spawned) or nonsense: take nothing
            if (max < 1f || current <= 0f || float.IsNaN(current) || float.IsInfinity(max)) return 0f;

            var cost = DashTuning.HealthCost(fraction, current, max);
            if (cost <= 0f) return 0f;

            var target = current - cost;
            try
            {
                health.SyncedCurrentHealth.Value = target;
            }
            catch (Exception ex)
            {
                _writeBroken = true; // e.g. only the server may write it
                Status = $"couldn't write SyncedCurrentHealth ({ex.Message}); dash HP cost off";
                ModLog.Warn("Dash HP cost: " + Status);
                return 0f;
            }

            if (!_firstCostLogged)
            {
                _firstCostLogged = true;
                ModLog.Info($"Dash HP cost: first dash took {cost:0.#} HP ({current:0.#} -> {target:0.#} written); " +
                            $"the game now reads {health.CurrentHealth:0.#} / {health.MaxHealth:0.#}.");
            }

            return cost;
        }
        catch (Exception ex)
        {
            ModLog.ErrorThrottled("HealthBridge", ex, Time.unscaledTime, 30f);
            return 0f;
        }
    }
}
