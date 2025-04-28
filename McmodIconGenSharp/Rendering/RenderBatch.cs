using KirisameLib.Extensions;

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

    /// <remarks>Note that return bytes should be used or copied before next any another render of same environment</remarks>
    public ReadOnlySpan<byte> Render(TextureInfo[] textures)
    {
        var bTexViews = textures.Select(t => Environment.GetTexture(t.Width, t.Height, t.Rgba.AsSpan())).ToArray();
        var texViews = bTexViews.Select(t => t.Value).ToArray();
        var result = Environment.Render(this, texViews);

        bTexViews.ForEach(t => t.Dispose());
        return result;
    }

    #endregion
}