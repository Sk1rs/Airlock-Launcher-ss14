using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// The <c>meta.json</c> that describes a Robust sprite sheet folder (an <c>.rsi</c>).
/// </summary>
/// <remarks>
/// Every state in an RSI is one PNG named after it. Inside that PNG the frames run left to right,
/// top to bottom: all frames of the first direction, then the second, and so on. Directions are
/// always in the order south, north, east, west.
/// </remarks>
public sealed class RsiMeta
{
    [JsonPropertyName("size")] public RsiSize Size { get; set; } = new();
    [JsonPropertyName("states")] public List<RsiState> States { get; set; } = new();

    public static RsiMeta? Parse(string json)
    {
        return JsonSerializer.Deserialize<RsiMeta>(json);
    }

    public RsiState? GetState(string name)
    {
        return States.FirstOrDefault(state => state.Name == name);
    }

    public bool HasState(string name) => GetState(name) != null;

    /// <summary>
    /// Index of a state's first frame for the given direction, within its sheet.
    /// </summary>
    /// <remarks>
    /// A state with fewer directions than asked for falls back to its first one, which is what a
    /// one-directional sprite (a marking drawn the same from every side) needs.
    /// </remarks>
    public static int FrameIndex(RsiState state, RsiDirection direction)
    {
        var wanted = (int) direction;
        if (wanted >= state.Directions)
            return 0;

        // Animated states have one delay list per direction; the frame we want is the first of the
        // direction's own run.
        if (state.Delays is { Count: > 0 })
        {
            var index = 0;
            for (var dir = 0; dir < wanted && dir < state.Delays.Count; dir++)
            {
                index += Math.Max(1, state.Delays[dir].Count);
            }

            return index;
        }

        return wanted;
    }

    public sealed class RsiSize
    {
        [JsonPropertyName("x")] public int X { get; set; } = 32;
        [JsonPropertyName("y")] public int Y { get; set; } = 32;
    }

    public sealed class RsiState
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("directions")] public int Directions { get; set; } = 1;

        /// <summary>
        /// Frame delays, one list per direction. Absent on still sprites.
        /// </summary>
        [JsonPropertyName("delays")] public List<List<float>>? Delays { get; set; }
    }
}

/// <summary>
/// Sprite facing, in the order Robust stores them in.
/// </summary>
public enum RsiDirection
{
    South = 0,
    North = 1,
    East = 2,
    West = 3,
}
