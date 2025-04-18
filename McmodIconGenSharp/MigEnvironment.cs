using Veldrid;

namespace McmodIconGenSharp;

public sealed class MigEnvironment : IDisposable
{
    #region Init&Cleanup

    public MigEnvironment()
    {
        _graphicsDevice = GraphicsDevice.CreateVulkan(new GraphicsDeviceOptions
        {
            PreferDepthRangeZeroToOne = true,
            PreferStandardClipSpaceYDirection = false,
            ResourceBindingModel = ResourceBindingModel.Improved,
        });
        _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        foreach (var (gpuTexture, buffer, cpuTexture) in _buffers.Values)
        {
            cpuTexture.Dispose();
            buffer.Dispose();
            gpuTexture.Dispose();
        }
        _commandList.Dispose();
        _graphicsDevice.Dispose();
    }

    #endregion

    #region Rending Resources

    private readonly GraphicsDevice _graphicsDevice;
    private readonly CommandList _commandList;
    private readonly Dictionary<(uint x, uint y), (Texture gpuTexture, Framebuffer buffer, Texture cpuTexture)> _buffers = new();

    #endregion
}