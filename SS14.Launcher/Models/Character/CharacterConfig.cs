using System;
using System.Collections.Generic;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// A plain colour, kept separate from the UI toolkit so the renderer can run without one.
/// </summary>
public readonly record struct CharacterColor(byte R, byte G, byte B)
{
    public static readonly CharacterColor White = new(255, 255, 255);

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    /// <summary>
    /// The form character files use, which carries an alpha channel we always leave opaque.
    /// </summary>
    public string ToProfileHex() => $"#{R:X2}{G:X2}{B:X2}FF";

    /// <summary>
    /// Reads a colour written as <c>#RRGGBB</c> or <c>#RRGGBBAA</c>; alpha is ignored.
    /// </summary>
    public static CharacterColor FromHex(string hex)
    {
        var text = hex.Trim().TrimStart('#');

        if (text.Length == 8)
            text = text[..6];

        if (text.Length != 6
            || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return White;
        }

        return new CharacterColor((byte) (value >> 16), (byte) (value >> 8 & 0xFF), (byte) (value & 0xFF));
    }

    /// <summary>
    /// The game's human skin tone scale, from pale at 0 to dark at 100.
    /// </summary>
    /// <remarks>
    /// Mirrors the game's own formula: a fixed orange hue whose saturation rises below the midpoint
    /// and whose brightness falls above it, so the whole range stays a believable skin colour.
    /// </remarks>
    public static CharacterColor FromSkinTone(int tone)
    {
        tone = Math.Clamp(tone, 0, 100);

        const float hue = 25f / 360f;
        var saturation = 20f;
        var value = 100f;

        var offset = tone - 20;
        if (offset <= 0)
            saturation += Math.Abs(offset);
        else
            value -= Math.Abs(offset);

        return FromHsv(hue, saturation / 100f, value / 100f);
    }

    private static CharacterColor FromHsv(float hue, float saturation, float value)
    {
        var sector = (int) Math.Floor(hue * 6f) % 6;
        var fraction = hue * 6f - (float) Math.Floor(hue * 6f);

        var p = value * (1f - saturation);
        var q = value * (1f - fraction * saturation);
        var t = value * (1f - (1f - fraction) * saturation);

        var (r, g, b) = sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };

        return new CharacterColor(Channel(r), Channel(g), Channel(b));

        static byte Channel(float component) => (byte) Math.Clamp(MathF.Round(component * 255f), 0, 255);
    }
}

/// <summary>
/// A marking the character is wearing, and the colour it is drawn in.
/// </summary>
public sealed record MarkingSelection(MarkingPrototype Marking, CharacterColor Color);

/// <summary>
/// Everything that decides what the character looks like.
/// </summary>
public sealed class CharacterConfig
{
    public required SpeciesEntry Species { get; init; }

    public bool Female { get; set; }

    public CharacterColor SkinColor { get; set; } = CharacterColor.FromSkinTone(20);

    public CharacterColor EyeColor { get; set; } = new(120, 80, 60);

    public RsiDirection Direction { get; set; } = RsiDirection.South;

    /// <summary>
    /// At most one marking per body layer, which is all the editor offers.
    /// </summary>
    public Dictionary<string, MarkingSelection> Markings { get; } = new();

    /// <summary>
    /// What the character is wearing: equipment slot to item prototype id. Empty means naked.
    /// </summary>
    public Dictionary<string, string> Outfit { get; } = new();
}
