using System.Collections;
using System.Collections.Immutable;
using System.Numerics;

using JetBrains.Annotations;

using McmodIconGenSharp.RenderingInfos;

namespace McmodIconGenSharp.BatchInfos;

// I apologize for the tight logic and lack of abstraction that made the code difficult to read.

public record ModelInfo(ImmutableArray<CubeInfo> Cubes);

public record CubeInfo(Vector3 Size, Matrix4x4 Transform, Faces<Vector4> UVs, Faces<byte> TextureIndexes)
{
    private static readonly Faces<Vector3> Normals = new()
    {
        West   = new Vector3(-1, 0,  0),
        East   = new Vector3(1,  0,  0),
        Bottom = new Vector3(0,  -1, 0),
        Top    = new Vector3(0,  1,  0),
        North  = new Vector3(0,  0,  -1),
        South  = new Vector3(0,  0,  1),
    };

    private static class Vertex
    {
        // ReSharper disable InconsistentNaming
        public const byte E = 0b001, T = 0b010, S = 0b100; //三个轴向
        public const byte WBN = 0b000, EBN = 0b001,        //八个顶点
                          WTN = 0b010, ETN = 0b011,
                          WBS = 0b100, EBS = 0b101,
                          WTS = 0b110, ETS = 0b111;
        public const byte XSet = 0b00_000,
                          YSet = 0b01_000,
                          ZSet = 0b10_000;
        // ReSharper restore InconsistentNaming
    }

    public ImmutableArray<VertexInfo> GetVertexes()
    {
        Span<Vector3> vertices = stackalloc Vector3[8];
        var halfSize = Size / 2;
        for (byte i = 0; i < 8; i++) // load 8 vertices position into vertices, half size with direction
        {
            var v = halfSize;
            if ((i & Vertex.E) == 0) v.X *= -1;
            if ((i & Vertex.T) == 0) v.Y *= -1;
            if ((i & Vertex.S) == 0) v.Z *= -1;
            vertices[i] = Vector3.Transform(v, Transform);
        }

        Matrix4x4 normalTransform = Matrix4x4.Invert(Transform, out var invTransform)
            ? Matrix4x4.Transpose(invTransform)
            : Matrix4x4.Identity;
        var worldNormals = Normals.Map(n => Vector3.TransformNormal(n, normalTransform));

        Span<VertexInfo> results = stackalloc VertexInfo[24];
        // I've written so many bugs in this cursed long loop. Thanks to Gemini for helping me find them out.
        for (byte axis = 0; axis < 3; axis++) // 0,1,2 for x,y,z
        {
            for (byte i = 0; i < 8; i++) // repeat load all vertices every axis, make all faces
            {
                var dir = (i & (1 << axis)) == 0 ? (byte)0 : (byte)1;
                var face = (byte)(axis * 2 + dir);

                var normal = worldNormals[face];
                var texIndex = TextureIndexes[face];

                // 0b00, 0 for the origin and 1 for the end, bit0 for u and bit1 for v
                byte uv = 0;

                if (axis == 1) // y axis
                {
                    uv = (byte)((i & Vertex.S) >> 1);     // calc v: zyx -> 0z0 (for top, north(0) is origin)
                    if (dir == 0) uv = (byte)(uv ^ 0b10); // and for bottom, reverse it
                    uv = (byte)(uv | (i & Vertex.E));     // calc u: zyx -> 00x (for top&bottom, west(0) is origin)
                    // (final: 0xz)
                }
                else // x and z
                {
                    if (axis == 0)                         // x axis
                        uv = (byte)((~i & Vertex.S) >> 2); // calc u: for east faces south(1) means origin
                    else                                   // z axis
                        uv = (byte)(i & Vertex.E);         // calc u: for south faces west(0) means origin
                    if (dir == 0) uv = (byte)(uv ^ 0b01);  // and for west or north, reverse it

                    uv = (byte)(uv | (~i & Vertex.T)); // calc v: for side faces up(1) means origin
                }
                Vector4 fullUv = UVs[face];
                Vector2 texCoord = new Vector2(fullUv[(uv & 0b01) << 1], fullUv[(uv & 0b10) + 1]); // find uv
                // ↑ fullUv layout: u0, v0, u1, v1
                // thus 0bxx, bit0 for u-0 or v-1, bit1 for origin-0, end-1

                results[axis * 8 + i] = new(vertices[i], normal, texCoord, texIndex);
            }
        }

        return results.ToImmutableArray();
    }

    public static ushort[] GetIndices(ushort cubeOrdinal)
    {
        if (cubeOrdinal >= 8192)
        {
            Console.Error.WriteLine("Cube ordinal out of range! Exceeded cube will not be rendered!");
            return [];
        }

        var offsetValue = (ushort)(cubeOrdinal * 8);
        // ReSharper disable once ConvertToLocalFunction
        Func<ushort, ushort> offset = v => (ushort)(offsetValue + v);
        return
        [
            // West face
            offset(Vertex.XSet | Vertex.WTN), offset(Vertex.XSet | Vertex.WTS), offset(Vertex.XSet | Vertex.WBS),
            offset(Vertex.XSet | Vertex.WTN), offset(Vertex.XSet | Vertex.WBS), offset(Vertex.XSet | Vertex.WBN),
            // East face
            offset(Vertex.XSet | Vertex.ETS), offset(Vertex.XSet | Vertex.ETN), offset(Vertex.XSet | Vertex.EBN),
            offset(Vertex.XSet | Vertex.ETS), offset(Vertex.XSet | Vertex.EBN), offset(Vertex.XSet | Vertex.EBS),
            // Bottom face
            offset(Vertex.YSet | Vertex.WBS), offset(Vertex.YSet | Vertex.EBS), offset(Vertex.YSet | Vertex.EBN),
            offset(Vertex.YSet | Vertex.WBS), offset(Vertex.YSet | Vertex.EBN), offset(Vertex.YSet | Vertex.WBN),
            // Top face
            offset(Vertex.YSet | Vertex.WTN), offset(Vertex.YSet | Vertex.ETN), offset(Vertex.YSet | Vertex.ETS),
            offset(Vertex.YSet | Vertex.WTN), offset(Vertex.YSet | Vertex.ETS), offset(Vertex.YSet | Vertex.WTS),
            // North face
            offset(Vertex.ZSet | Vertex.ETN), offset(Vertex.ZSet | Vertex.WTN), offset(Vertex.ZSet | Vertex.WBN),
            offset(Vertex.ZSet | Vertex.ETN), offset(Vertex.ZSet | Vertex.WBN), offset(Vertex.ZSet | Vertex.EBN),
            // South face
            offset(Vertex.ZSet | Vertex.WTS), offset(Vertex.ZSet | Vertex.ETS), offset(Vertex.ZSet | Vertex.EBS),
            offset(Vertex.ZSet | Vertex.WTS), offset(Vertex.ZSet | Vertex.EBS), offset(Vertex.ZSet | Vertex.WBS),
        ];
    }
}

public readonly record struct Faces<T>(T West, T East, T Bottom, T Top, T North, T South) : IEnumerable<T>
{
    public T this[byte x] => x switch
    {
        0 => West,   // -x
        1 => East,   // +x
        2 => Bottom, // -y
        3 => Top,    // +y
        4 => North,  // -z
        5 => South,  // +z
        _ => throw new ArgumentOutOfRangeException(nameof(x), x, null)
    };

    [MustDisposeResource]
    public IEnumerator<T> GetEnumerator()
    {
        yield return West;
        yield return East;
        yield return Bottom;
        yield return Top;
        yield return North;
        yield return South;
    }

    [MustDisposeResource]
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public Faces<TResult> Map<TResult>(Func<T, TResult> f) =>
        new(f(West), f(East), f(Bottom), f(Top), f(North), f(South));
}