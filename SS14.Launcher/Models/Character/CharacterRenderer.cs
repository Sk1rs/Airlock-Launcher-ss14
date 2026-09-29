using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using SkiaSharp;

namespace SS14.Launcher.Models.Character;

/// <summary>
/// Draws a character the way the game stacks its sprite layers.
/// </summary>
/// <remarks>
/// The layer order is lifted from the game's own <c>species_appearance.yml</c>, so hair lands in
/// front of the head and tails behind everything. Compositing is done by hand on raw pixels rather
/// than through the UI toolkit: layers have to be tinted (skin tone, hair colour), and the result
/// has to be exportable to a file whether or not a window is on screen.
/// </remarks>
public sealed class CharacterRenderer
{
    /// <summary>
    /// Back to front, copied from the game's own layer list so clothing lands where it should.
    /// Equipment slots are prefixed to keep them apart from body layers of the same name.
    /// </summary>
    public static readonly string[] LayerOrder =
    [
        "Chest",
        "Head",
        "Snout",
        "Eyes",
        "RArm",
        "LArm",
        "RLeg",
        "LLeg",
        "UndergarmentBottom",
        "UndergarmentTop",
        Slot("jumpsuit"),
        "LFoot",
        "RFoot",
        "LHand",
        "RHand",
        "Overlay",
        Slot("gloves"),
        Slot("shoes"),
        Slot("ears"),
        Slot("eyes"),
        Slot("belt"),
        Slot("outerClothing"),
        Slot("back"),
        Slot("neck"),
        "SnoutCover",
        "FacialHair",
        "Hair",
        "HeadSide",
        "HeadTop",
        "Tail",
        "TailOverlay",
        Slot("mask"),
        Slot("head"),
    ];

    /// <summary>
    /// Equipment slots, and the suffix the game gives their worn sprite.
    /// </summary>
    /// <remarks>
    /// Slots without a worn sprite (an ID card, pocket contents) are simply absent.
    /// </remarks>
    private static readonly Dictionary<string, string> SlotStates = new()
    {
        ["jumpsuit"] = "INNERCLOTHING",
        ["outerClothing"] = "OUTERCLOTHING",
        ["shoes"] = "FEET",
        ["head"] = "HELMET",
        ["eyes"] = "EYES",
        ["mask"] = "MASK",
        ["gloves"] = "HAND",
        ["ears"] = "EARS",
        ["neck"] = "NECK",
        ["belt"] = "BELT",
        ["back"] = "BACKPACK",
    };

    /// <summary>
    /// Layer key for an equipment slot.
    /// </summary>
    public static string Slot(string slot) => $"slot:{slot}";

    /// <summary>
    /// Body layers and the sprite state they use, <c>{s}</c> standing in for the sex suffix.
    /// </summary>
    private static readonly (string Layer, string State)[] BodyLayers =
    [
        ("Chest", "torso_{s}"),
        ("Head", "head_{s}"),
        ("RArm", "r_arm"),
        ("LArm", "l_arm"),
        ("RLeg", "r_leg"),
        ("LLeg", "l_leg"),
        ("LFoot", "l_foot"),
        ("RFoot", "r_foot"),
        ("LHand", "l_hand"),
        ("RHand", "r_hand"),
    ];

    private const string EyesRsi = "Mobs/Customization/eyes.rsi";
    private const string EyesState = "eyes";

    private readonly CharacterCatalog _catalog;

    public CharacterRenderer(CharacterCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// Builds the finished character image.
    /// </summary>
    public async Task<RenderedCharacter?> RenderAsync(CharacterConfig config, CancellationToken cancel = default)
    {
        var species = config.Species;
        var width = Math.Max(1, species.Parts.Size.X);
        var height = Math.Max(1, species.Parts.Size.Y);

        var canvas = new byte[width * height * 4];
        var drewAnything = false;

        foreach (var layer in await ResolveLayersAsync(config, cancel))
        {
            var sprite = await LoadFrameAsync(layer.Rsi, layer.State, config.Direction, cancel);
            if (sprite == null)
                continue;

            // Sprites of the wrong size (a marking sheet from another species) are skipped rather
            // than stretched: better a missing layer than a smeared one.
            if (sprite.Width != width || sprite.Height != height)
            {
                Log.Debug("Skipping {Rsi}/{State}: {W}x{H} does not fit {CW}x{CH}",
                    layer.Rsi, layer.State, sprite.Width, sprite.Height, width, height);
                continue;
            }

            Composite(canvas, sprite.Pixels, layer.Color);
            drewAnything = true;
        }

        if (!drewAnything)
            return null;

        return new RenderedCharacter(canvas, width, height);
    }

    /// <summary>
    /// Works out every sprite layer the character is made of, in drawing order.
    /// </summary>
    public async Task<List<ResolvedLayer>> ResolveLayersAsync(CharacterConfig config, CancellationToken cancel = default)
    {
        var species = config.Species;
        var sex = config.Female ? "f" : "m";
        var byLayer = new Dictionary<string, List<ResolvedLayer>>();

        void Add(string layer, ResolvedLayer resolved)
        {
            if (!byLayer.TryGetValue(layer, out var list))
                byLayer[layer] = list = [];

            list.Add(resolved);
        }

        foreach (var (layer, template) in BodyLayers)
        {
            var state = template.Replace("{s}", sex);

            // Some species only ship one version of a part; fall back to the other sex, then to a
            // suffix-less name, before giving up on the layer.
            var resolvedState = FirstExistingState(species.Parts, state,
                template.Replace("{s}", config.Female ? "m" : "f"),
                template.Replace("_{s}", ""));

            if (resolvedState != null)
                Add(layer, new ResolvedLayer(species.PartsRsi, resolvedState, config.SkinColor));
        }

        var eyes = await _catalog.LoadMetaAsync(EyesRsi, cancel);
        if (eyes?.HasState(EyesState) == true)
            Add("Eyes", new ResolvedLayer(EyesRsi, EyesState, config.EyeColor));

        foreach (var (bodyPart, selection) in config.Markings)
        {
            foreach (var sprite in selection.Marking.Sprites)
            {
                Add(bodyPart, new ResolvedLayer(
                    sprite.Rsi,
                    sprite.State,
                    selection.Marking.FollowsSkinColor ? config.SkinColor : selection.Color));
            }
        }

        foreach (var (slot, item) in config.Outfit)
        {
            if (!SlotStates.TryGetValue(slot, out var tag))
                continue;

            if (_catalog.Clothing.ResolveSprite(item) is not { } rsi)
            {
                Log.Debug("No sprite for {Item} in slot {Slot}", item, slot);
                continue;
            }

            // Non-human species get their own cut of a garment where one was drawn.
            var meta = await _catalog.LoadMetaAsync(rsi, cancel);
            if (meta == null)
                continue;

            var speciesState = $"equipped-{tag}-{species.Name.ToLowerInvariant()}";
            var state = meta.HasState(speciesState) ? speciesState : $"equipped-{tag}";

            if (meta.HasState(state))
                Add(Slot(slot), new ResolvedLayer(rsi, state, CharacterColor.White));
        }

        var ordered = new List<ResolvedLayer>();

        foreach (var layer in LayerOrder)
        {
            if (byLayer.Remove(layer, out var list))
                ordered.AddRange(list);
        }

        // Anything on a layer the game grew since this list was written still gets drawn, on top.
        foreach (var leftover in byLayer.Values)
        {
            ordered.AddRange(leftover);
        }

        return ordered;
    }

    private static string? FirstExistingState(RsiMeta meta, params string[] candidates)
    {
        return candidates.FirstOrDefault(meta.HasState);
    }

    private async Task<SpriteFrame?> LoadFrameAsync(
        string rsiPath,
        string stateName,
        RsiDirection direction,
        CancellationToken cancel)
    {
        var meta = await _catalog.LoadMetaAsync(rsiPath, cancel);
        if (meta?.GetState(stateName) is not { } state)
            return null;

        var png = await _catalog.Resources.GetFileAsync(
            $"Resources/Textures/{CharacterCatalog.Strip(rsiPath)}/{stateName}.png",
            cancel);

        if (png == null)
            return null;

        try
        {
            return ExtractFrame(png, meta, state, direction);
        }
        catch (Exception e)
        {
            Log.Warning(e, "Failed to read sprite {Rsi}/{State}", rsiPath, stateName);
            return null;
        }
    }

    /// <summary>
    /// Cuts a single frame out of a sprite sheet.
    /// </summary>
    /// <remarks>
    /// Decoded through Skia rather than the toolkit's own bitmap: this runs before (and without) a
    /// graphics backend, and it pins down the pixel format instead of taking whatever the platform
    /// hands back.
    /// </remarks>
    private static SpriteFrame ExtractFrame(byte[] png, RsiMeta meta, RsiMeta.RsiState state, RsiDirection direction)
    {
        var (sheet, sheetWidth, sheetHeight) = DecodeBgra(png);

        var frameWidth = meta.Size.X;
        var frameHeight = meta.Size.Y;
        var columns = Math.Max(1, sheetWidth / frameWidth);

        var index = RsiMeta.FrameIndex(state, direction);
        var originX = index % columns * frameWidth;
        var originY = index / columns * frameHeight;

        var frame = new byte[frameWidth * frameHeight * 4];

        for (var y = 0; y < frameHeight; y++)
        {
            var sourceY = originY + y;
            if (sourceY >= sheetHeight)
                break;

            for (var x = 0; x < frameWidth; x++)
            {
                var sourceX = originX + x;
                if (sourceX >= sheetWidth)
                    break;

                var source = (sourceY * sheetWidth + sourceX) * 4;
                var target = (y * frameWidth + x) * 4;

                frame[target] = sheet[source];
                frame[target + 1] = sheet[source + 1];
                frame[target + 2] = sheet[source + 2];
                frame[target + 3] = sheet[source + 3];
            }
        }

        return new SpriteFrame(frame, frameWidth, frameHeight);
    }

    /// <summary>
    /// Decodes a PNG to straight (unpremultiplied) BGRA pixels.
    /// </summary>
    public static (byte[] Pixels, int Width, int Height) DecodeBgra(byte[] png)
    {
        using var image = SKImage.FromEncodedData(png)
                          ?? throw new InvalidDataException("Not a readable image");

        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];

        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                throw new InvalidDataException("Could not read image pixels");
        }
        finally
        {
            handle.Free();
        }

        return (pixels, image.Width, image.Height);
    }

    /// <summary>
    /// Draws a tinted layer over the canvas, straight alpha, in place.
    /// </summary>
    /// <remarks>
    /// The tint multiplies, which is what the game does with a sprite's colour: a white sprite comes
    /// out the tint colour and a grey one comes out a darker shade of it.
    /// </remarks>
    public static void Composite(byte[] canvas, byte[] layer, CharacterColor tint)
    {
        for (var i = 0; i < canvas.Length; i += 4)
        {
            var alpha = layer[i + 3] / 255f;
            if (alpha <= 0)
                continue;

            // Pixels arrive as BGRA.
            var blue = layer[i] * tint.B / 255f;
            var green = layer[i + 1] * tint.G / 255f;
            var red = layer[i + 2] * tint.R / 255f;

            var inverse = 1f - alpha;

            canvas[i] = (byte) Math.Clamp(blue * alpha + canvas[i] * inverse, 0, 255);
            canvas[i + 1] = (byte) Math.Clamp(green * alpha + canvas[i + 1] * inverse, 0, 255);
            canvas[i + 2] = (byte) Math.Clamp(red * alpha + canvas[i + 2] * inverse, 0, 255);
            canvas[i + 3] = (byte) Math.Clamp(alpha * 255 + canvas[i + 3] * inverse, 0, 255);
        }
    }

    private sealed record SpriteFrame(byte[] Pixels, int Width, int Height);
}

/// <summary>
/// One sprite to draw, and the colour to draw it in.
/// </summary>
public sealed record ResolvedLayer(string Rsi, string State, CharacterColor Color);

/// <summary>
/// A finished character image, as raw BGRA pixels.
/// </summary>
public sealed record RenderedCharacter(byte[] Pixels, int Width, int Height)
{
    /// <summary>
    /// Blows the image up by a whole-number factor, keeping the pixels sharp.
    /// </summary>
    public RenderedCharacter Scale(int factor)
    {
        if (factor <= 1)
            return this;

        var width = Width * factor;
        var height = Height * factor;
        var scaled = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var source = (y / factor * Width + x / factor) * 4;
                var target = (y * width + x) * 4;

                Array.Copy(Pixels, source, scaled, target, 4);
            }
        }

        return new RenderedCharacter(scaled, width, height);
    }

    /// <summary>
    /// Writes the image out as a PNG.
    /// </summary>
    public void SavePng(string path)
    {
        var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

        using var bitmap = new SKBitmap(info);
        Marshal.Copy(Pixels, 0, bitmap.GetPixels(), Pixels.Length);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(path);

        data.SaveTo(file);
    }

    /// <summary>
    /// Turns the pixels into something that can be put on screen.
    /// </summary>
    public WriteableBitmap ToBitmap()
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(Width, Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);

        using var buffer = bitmap.Lock();

        for (var y = 0; y < Height; y++)
        {
            System.Runtime.InteropServices.Marshal.Copy(
                Pixels,
                y * Width * 4,
                buffer.Address + y * buffer.RowBytes,
                Width * 4);
        }

        return bitmap;
    }
}
