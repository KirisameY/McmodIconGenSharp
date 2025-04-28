using System.Numerics;

using McmodIconGenSharp.BatchInfos;

namespace McmodIconGenSharp.Rendering;

public class RenderBatchBuilder(MigEnvironment env)
{
    private ModelInfo? _modelInfo;
    private TargetInfo _targetInfo = new(256, 256);

    private Matrix4x4 _modelTransform = Matrix4x4.Identity;
    private Matrix4x4 _viewTransform = Matrix4x4.Identity;
    private Matrix4x4 _projectionTransform = Matrix4x4.CreateOrthographic(16, 16, 0, 32);

    private Vector3 _lightDirection = Vector3.Normalize(new(0.5f, -1f, 0f));
    private Vector3 _lightColor = new(1f, 1f, 1f);
    private Vector3 _ambientLightColor = new(0.2f, 0.2f, 0.2f);


    public RenderBatchBuilder WithModelInfo(ModelInfo model)
    {
        _modelInfo = model;
        return this;
    }

    public RenderBatchBuilder WithOutputSize(TargetInfo target)
    {
        _targetInfo = target;
        return this;
    }


    public RenderBatchBuilder WithModelTransform(Matrix4x4 transform)
    {
        _modelTransform = transform;
        return this;
    }

    public RenderBatchBuilder WithViewTransform(Matrix4x4 transform)
    {
        _viewTransform = transform;
        return this;
    }

    public RenderBatchBuilder WithOrthographicProjection(float width, float height, float near, float far)
    {
        _projectionTransform = Matrix4x4.CreateOrthographic(width, height, near, far);
        return this;
    }

    public RenderBatchBuilder WithPerspectiveProjection(float fov, float aspect, float near, float far)
    {
        _projectionTransform = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
        return this;
    }


    public RenderBatchBuilder WithLightDirection(Vector3 direction)
    {
        _lightDirection = direction;
        return this;
    }

    public RenderBatchBuilder WithLightDirection(float rx, float ry)
    {
        var dir = -Vector3.UnitZ;
        dir = Vector3.TransformNormal(dir, Matrix4x4.CreateRotationY(ry));
        dir = Vector3.TransformNormal(dir, Matrix4x4.CreateRotationX(rx));

        _lightDirection = dir;
        return this;
    }

    public RenderBatchBuilder WithLightColor(Vector3 color)
    {
        _lightColor = color;
        return this;
    }

    public RenderBatchBuilder WithAmbientLightColor(Vector3 color)
    {
        _ambientLightColor = color;
        return this;
    }


    public RenderBatch Build()
    {
        if (_modelInfo is null) throw new ArgumentNullException(nameof(_modelInfo));

        var transforms = new Transforms(_modelTransform, _viewTransform, _projectionTransform);
        var lightInfos = new LightInfos(new Vector4(_lightDirection,    0),
                                        new Vector4(_lightColor,        0),
                                        new Vector4(_ambientLightColor, 0));
        var spaceInfo = new SpaceInfo(transforms, lightInfos);

        return new RenderBatch(env, _modelInfo, spaceInfo, _targetInfo);
    }
}