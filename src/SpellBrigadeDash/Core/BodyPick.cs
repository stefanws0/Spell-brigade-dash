namespace SpellBrigadeDash.Core;

using System.Collections.Generic;

/// <summary>
/// Picks the wizard's body among the renderers on the wizard object. That object also holds its spells, e.g. the Sun
/// Mage's Solar Flare circle under <c>PlayerSpells</c>, which is a bigger mesh than the wizard itself: picked as the
/// body (18 Nov), it made the afterimages copy the flare and scaled the dash effects to its size. So: never anything
/// under the spells, and the animated (skinned) model before sprites before plain meshes; the largest within a kind.
/// Pure, unit-tested.
/// </summary>
public static class BodyPick
{
    public enum Kind
    {
        Mesh,
        Sprite,
        Skinned,
    }

    public readonly struct Candidate
    {
        public Candidate(Kind kind, float size, bool underSpells)
        {
            Kind = kind;
            Size = size;
            UnderSpells = underSpells;
        }

        public Kind Kind { get; }

        /// <summary>Surface of the renderer's bounds box.</summary>
        public float Size { get; }

        public bool UnderSpells { get; }
    }

    /// <summary>Index of the body among <paramref name="candidates"/>, or -1 if none qualifies.</summary>
    public static int Best(IReadOnlyList<Candidate> candidates)
    {
        var best = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.UnderSpells || c.Size <= 0f) continue;
            if (best < 0) best = i;
            else
            {
                var b = candidates[best];
                if (c.Kind > b.Kind || (c.Kind == b.Kind && c.Size > b.Size)) best = i;
            }
        }

        return best;
    }
}
