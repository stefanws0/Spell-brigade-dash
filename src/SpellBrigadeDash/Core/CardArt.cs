namespace SpellBrigadeDash.Core;

using System.Text;

/// <summary>
/// File names of the cards' own icons: <c>art/icons/&lt;name&gt;.png</c> in the repo (embedded in the DLL) and, as an
/// override for trying new art without a rebuild, <c>icons/&lt;name&gt;.png</c> next to the mod's DLL.
/// </summary>
public static class CardArt
{
    /// <summary>e.g. ExtraCharge → "extra_charge.png".</summary>
    public static string IconFile(DashCardId card)
    {
        var name = card.ToString();
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.Append(".png").ToString();
    }
}
