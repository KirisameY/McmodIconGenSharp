using System.Numerics;
using System.Runtime.InteropServices;

using Veldrid;

namespace McmodIconGenSharp.BatchInfos;

public record SpaceInfo(Transforms Transforms, LightInfos LightInfos);

[StructLayout(LayoutKind.Sequential)]
public record struct Transforms(Matrix4x4 ModelTransform, Matrix4x4 ViewTransform, Matrix4x4 ProjectionTransform)
{
    public const uint ByteSize = 3 * 16 * 4;

    public static readonly ResourceLayoutElementDescription LayoutElementDescription = new(
        name: nameof(Transforms),
        kind: ResourceKind.UniformBuffer,
        stages: ShaderStages.Vertex
    );
}

[StructLayout(LayoutKind.Sequential)]
public record struct LightInfos(Vector4 LightDirection, Vector4 LightColor, Vector4 AmbientLightColor)
{
    public const uint ByteSize = 3 * 4 * 4;

    public static readonly ResourceLayoutElementDescription LayoutElementDescription = new(
        name: nameof(LightInfos),
        kind: ResourceKind.UniformBuffer,
        stages: ShaderStages.Fragment
    );
}