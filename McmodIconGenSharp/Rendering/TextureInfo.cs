using System.Collections.Immutable;

namespace McmodIconGenSharp.Rendering;

public readonly struct TextureInfo(uint width, uint height, ImmutableArray<byte> rgba)
{
    public readonly uint Width = width, Height = height;
    public readonly ImmutableArray<byte> Rgba = rgba;
}