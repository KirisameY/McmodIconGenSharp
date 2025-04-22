using McmodIconGenSharp.BatchInfos;

using Veldrid;

namespace McmodIconGenSharp.Rendering;

public class RenderBatch : IDisposable
{
    #region Init&Cleanup

    internal RenderBatch(MigEnvironment environment, ModelInfo modelInfo, SpaceInfo spaceInfo, TargetInfo targetInfo)
    {
        Environment = environment;
        var factory = Environment.GraphicsDevice.ResourceFactory;
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;
    }

    #endregion


    #region Resources

    public MigEnvironment Environment { get; }

    #endregion
}