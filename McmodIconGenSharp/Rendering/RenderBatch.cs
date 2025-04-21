using McmodIconGenSharp.BatchInfos;

using Veldrid;

namespace McmodIconGenSharp.Rendering;

public class RenderBatch : IDisposable
{
    public RenderBatch(MigEnvironment environment, ModelInfo modelInfo, SpaceInfo spaceInfo, TargetInfo targetInfo)
    {
        Environment = environment;
        var factory = Environment.GraphicsDevice.ResourceFactory;
    }

    public void Dispose()
    {
        _pipeline.Dispose();
    }


    #region Environment Settings

    public MigEnvironment Environment { get; }

    private Pipeline _pipeline;

    #endregion
}