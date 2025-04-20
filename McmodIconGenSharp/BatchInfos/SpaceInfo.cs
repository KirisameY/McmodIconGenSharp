using System.Numerics;

namespace McmodIconGenSharp.BatchInfos;

public record SpaceInfo(Matrix4x4 ModelTransform, Matrix4x4 ViewTransform, Matrix4x4 ProjectionTransform);