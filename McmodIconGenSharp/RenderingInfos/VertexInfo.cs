using System.Numerics;
using System.Runtime.CompilerServices;

using Veldrid;

namespace McmodIconGenSharp.RenderingInfos;

public readonly record struct VertexInfo(Vector3 Position, Vector3 Normal, Vector2 TexCoord, uint TextureIndex)
{
    // public static readonly uint ByteSize = (uint)Unsafe.SizeOf<VertexInfo>();
    public const uint ByteSize = 36;

    public static VertexLayoutDescription LayoutDescription => new VertexLayoutDescription(GetVertexElements());

    public static VertexElementDescription[] GetVertexElements() =>
    [
        new VertexElementDescription("Position",     VertexElementSemantic.Position,          VertexElementFormat.Float3), // 00-11
        new VertexElementDescription("Normal",       VertexElementSemantic.Normal,            VertexElementFormat.Float3), // 12-23
        new VertexElementDescription("TexCoord",     VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2), // 24-31
        new VertexElementDescription("TextureIndex", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),  // 32-35
    ];
}