using McmodIconGenSharp.BatchInfos;

using Veldrid;

namespace McmodIconGenSharp.Rendering;

public sealed class RenderBatch(MigEnvironment environment, ModelInfo modelInfo, SpaceInfo spaceInfo, TargetInfo targetInfo)
{
    #region Properties

    public MigEnvironment Environment { get; } = environment;
    public ModelInfo ModelInfo { get; } = modelInfo;
    public SpaceInfo SpaceInfo { get; } = spaceInfo;
    public TargetInfo TargetInfo { get; } = targetInfo;

    private (Texture color, Texture staging, Framebuffer buf)? RenderTarget => field ??= Environment.GetFramebuffer(TargetInfo.Width, TargetInfo.Height);
    internal Texture TargetTex => RenderTarget!.Value.color;
    internal Texture StagingTex => RenderTarget!.Value.staging;
    internal Framebuffer TargetBuf => RenderTarget!.Value.buf;

    #endregion


    #region Public Methods

    public void Render()
    {

    }

    #endregion
}