using System.Numerics;
using System.Runtime.CompilerServices;

using Veldrid;

namespace McmodIconGenSharp.RenderingInfos;

public record struct VertexInfo(Vector3 Position, Vector3 Normal, Vector2 TexCoord, byte TextureIndex)
{
    public static readonly uint ByteSize = (uint)Unsafe.SizeOf<VertexInfo>();

    public static VertexLayoutDescription LayoutDescription => new VertexLayoutDescription(GetVertexElements());

    public static VertexElementDescription[] GetVertexElements() =>
    [
        new VertexElementDescription("Position",     VertexElementSemantic.Position,          VertexElementFormat.Float3), // 0
        new VertexElementDescription("Normal",       VertexElementSemantic.Normal,            VertexElementFormat.Float3), // 12
        new VertexElementDescription("TexCoord",     VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2), // 24
        new VertexElementDescription("TextureIndex", VertexElementSemantic.TextureCoordinate, VertexElementFormat.UInt1),  // 32
    ];
}