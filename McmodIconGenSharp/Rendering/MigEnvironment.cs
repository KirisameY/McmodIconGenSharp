using System.Collections.Concurrent;
using System.Text;

using KirisameLib.Extensions;

using McmodIconGenSharp.BatchInfos;
using McmodIconGenSharp.RenderingInfos;

using Veldrid;
using Veldrid.SPIRV;

namespace McmodIconGenSharp.Rendering;

/// <summary>
/// Working environment of 3d rendering, is not async-safe. <br/>
/// (Actually, nothing in <c> McmodIconGenSharp.Rendering </c> is async-safe)
/// </summary>
public sealed class MigEnvironment : IDisposable
{
    #region Constants

    private const uint VertexBufferByteSize = CubeInfo.MaxCubeAmount * 24 * VertexInfo.ByteSize; // max * 3 * 8 * size of vertex
    private const uint IndexBufferByteSize = CubeInfo.MaxCubeAmount * 36 * 2;                    // max * 4 * 6 * size of ushort

    private const PixelFormat ColorFormat = PixelFormat.R8_G8_B8_A8_UInt;
    private const PixelFormat DepthFormat = PixelFormat.D32_Float_S8_UInt;
    private const TextureSampleCount SampleCount = TextureSampleCount.Count1;

    #endregion


    #region Init&Cleanup

    public MigEnvironment(uint maxOutPixels = 144 * 144)
    {
        GraphicsDevice = GraphicsDevice.CreateVulkan(
            new GraphicsDeviceOptions
            (
                swapchainDepthFormat: null,
            #if DEBUG
                debug: true,
            #else
                debug: false,
            #endif
                syncToVerticalBlank: false,
                resourceBindingModel: ResourceBindingModel.Improved,
                preferDepthRangeZeroToOne: true,
                preferStandardClipSpaceYDirection: false
            )
        );
        var factory = GraphicsDevice.ResourceFactory;
        ShaderDescription vertexShaderDesc = new ShaderDescription(
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(Resources.Resources.GetShader("vertex")!),
            "main");
        ShaderDescription fragmentShaderDesc = new ShaderDescription(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(Resources.Resources.GetShader("fragment")!),
            "main");
        _shaders      = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(VertexBufferByteSize, BufferUsage.VertexBuffer));
        _indexBuffer  = factory.CreateBuffer(new BufferDescription(IndexBufferByteSize,  BufferUsage.IndexBuffer));

        _spaceResLayout = factory.CreateResourceLayout(new ResourceLayoutDescription([
            Transforms.LayoutElementDescription,
            LightInfos.LayoutElementDescription,
        ]));
        _texResLayout = factory.CreateResourceLayout(new ResourceLayoutDescription([
            ..Enumerable.Range(0, 6).Select(i => new ResourceLayoutElementDescription(
                                                name: $"Texture{i}",
                                                kind: ResourceKind.TextureReadOnly,
                                                stages: ShaderStages.Fragment
                                            )),
            new ResourceLayoutElementDescription(
                name: "SharedSampler",
                kind: ResourceKind.Sampler,
                stages: ShaderStages.Fragment
            )
        ]));

        _spaceUniformBuffer = factory.CreateBuffer(new BufferDescription(Transforms.ByteSize + LightInfos.ByteSize, BufferUsage.UniformBuffer));
        _spaceResourceSet   = factory.CreateResourceSet(new ResourceSetDescription(_spaceResLayout, _spaceUniformBuffer));

        _pipeline = GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleAlphaBlend,
            DepthStencilState = new DepthStencilStateDescription
            {
                DepthTestEnabled  = true,
                DepthWriteEnabled = true,
                DepthComparison   = ComparisonKind.LessEqual,
            },
            //todo: to done with transparent, i need make 2 pipelines,
            //      first for alpha test and make a non-alpha depth
            //      second for use that depth to draw correct texture
            Outputs = new OutputDescription
            {
                ColorAttachments = [new OutputAttachmentDescription(ColorFormat)],
                DepthAttachment  = new OutputAttachmentDescription(DepthFormat),
                SampleCount      = SampleCount,
            },
            PrimitiveTopology    = PrimitiveTopology.TriangleList,
            RasterizerState      = RasterizerStateDescription.Default with { DepthClipEnabled = false },
            ResourceBindingModel = null, // inherited from GraphicsDevice
            ResourceLayouts =
            [
                _spaceResLayout,
                _texResLayout
            ],
            ShaderSet = new ShaderSetDescription
            {
                VertexLayouts = [VertexInfo.LayoutDescription],
                Shaders       = _shaders,
            },
        });

        _commandList = factory.CreateCommandList();
        _sampler     = factory.CreateSampler(SamplerDescription.Point);

        _placeHolderTexture = factory.CreateTexture(new TextureDescription
        {
            Width       = 2,
            Height      = 2,
            Depth       = 1,
            MipLevels   = 1,
            ArrayLayers = 1,
            Format      = ColorFormat,
            SampleCount = SampleCount,
            Type        = TextureType.Texture2D,
            Usage       = TextureUsage.Sampled,
        });
        _placeHolderTextureView = factory.CreateTextureView(_placeHolderTexture);
        GraphicsDevice.UpdateTexture(_placeHolderTexture, stackalloc uint[] { 0xff00ffff, 0x000000ff, 0xff00ffff, 0x000000ff },
                                     0, 0, 0, 2, 2, 1, 0, 0);
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        _placeHolderTextureView.Dispose();
        _placeHolderTexture.Dispose();

        _outputBuffers.Values.ForEach(t =>
        {
            t.buf.Dispose();
            t.staging.Dispose();
            t.color.Dispose();
            t.depth.Dispose();
        });
        _singleTexResSetsCache.Values.ForEach(s => s.Dispose());
        _texturesCache.Values.Flatten().ForEach(t =>
        {
            t.view.Dispose();
            t.tex.Dispose();
        });

        _sampler.Dispose();
        _commandList.Dispose();
        _pipeline.Dispose();
        _spaceResourceSet.Dispose();
        _spaceUniformBuffer.Dispose();
        _spaceResLayout.Dispose();
        _texResLayout.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _shaders.ForEach(s => s.Dispose());
        GraphicsDevice.Dispose();
    }

    #endregion


    #region Rending Resources

    internal GraphicsDevice GraphicsDevice { get; }
    private readonly Shader[] _shaders;
    private readonly DeviceBuffer _vertexBuffer, _indexBuffer;
    private readonly ResourceLayout _spaceResLayout, _texResLayout;
    private readonly DeviceBuffer _spaceUniformBuffer;
    private readonly ResourceSet _spaceResourceSet;
    private readonly Pipeline _pipeline;
    private readonly CommandList _commandList;
    private readonly Sampler _sampler;

    private readonly Dictionary<(uint, uint), ConcurrentBag<(Texture tex, TextureView view)>> _texturesCache = new();
    private readonly Dictionary<TextureView, ResourceSet> _singleTexResSetsCache = new();
    private readonly Dictionary<(uint, uint), (Texture color, Texture depth, Texture staging, Framebuffer buf)> _outputBuffers = new();

    #endregion


    #region In/Out Textures

    private readonly Texture _placeHolderTexture;
    private readonly TextureView _placeHolderTextureView;

    internal BorrowRes<TextureView> GetTexture(uint width, uint height, ReadOnlySpan<byte> rgba)
    {
        if (!_texturesCache.TryGetValue((width, height), out var textureBag))
            _texturesCache[(width, height)] = textureBag = new();

        if (!textureBag.TryTake(out var tex))
        {
            var texDescription = new TextureDescription
            {
                Width       = width,
                Height      = height,
                Depth       = 1,
                MipLevels   = 1,
                ArrayLayers = 1,
                Format      = ColorFormat,
                SampleCount = SampleCount,
                Type        = TextureType.Texture2D,
                Usage       = TextureUsage.Sampled,
            };
            var texture = GraphicsDevice.ResourceFactory.CreateTexture(texDescription);
            var view = GraphicsDevice.ResourceFactory.CreateTextureView(texture);
            tex = (texture, view);
        }

        GraphicsDevice.UpdateTexture(tex.tex, rgba, 0, 0, 0, width, height, 1, 0, 0);
        return new(tex.view, () => textureBag.Add(tex));
    }

    private BorrowRes<ResourceSet> GetTexResSet(ReadOnlySpan<TextureView> textures)
    {
        if (textures.Length == 1 && _singleTexResSetsCache.TryGetValue(textures[0], out var result))
            return new(result, () => { });

        var texs = new TextureView[6];
        for (int i = 0; i < 6; i++)
        {
            texs[i] = i < textures.Length ? textures[i] : _placeHolderTextureView;
        }
        result = GraphicsDevice.ResourceFactory.CreateResourceSet(new ResourceSetDescription(_texResLayout, [..texs, _sampler]));
        if (textures.Length == 1)
        {
            _singleTexResSetsCache[textures[0]] = result;
            return new(result, () => { });
        }

        return new(result, () => result.Dispose());
    }

    internal (Texture color, Texture staging, Framebuffer buf) GetFramebuffer(uint width, uint height)
    {
        if (!_outputBuffers.TryGetValue((width, height), out var frame))
        {
            var colorTexDescription = new TextureDescription
            {
                Width       = width,
                Height      = height,
                Depth       = 1,
                MipLevels   = 1,
                ArrayLayers = 1,
                Format      = ColorFormat,
                SampleCount = SampleCount,
                Type        = TextureType.Texture2D,
                Usage       = TextureUsage.RenderTarget | TextureUsage.Sampled,
            };
            var depthTexDescription = colorTexDescription with
            {
                Format = DepthFormat,
                Usage = TextureUsage.DepthStencil
            };
            var stagingDescription = colorTexDescription with
            {
                Usage = TextureUsage.Staging
            };

            var colorTex = GraphicsDevice.ResourceFactory.CreateTexture(colorTexDescription);
            var depthTex = GraphicsDevice.ResourceFactory.CreateTexture(depthTexDescription);
            var stagingTex = GraphicsDevice.ResourceFactory.CreateTexture(stagingDescription);

            var buffer = GraphicsDevice.ResourceFactory.CreateFramebuffer(new FramebufferDescription
            {
                ColorTargets = [new FramebufferAttachmentDescription(colorTex, 0)],
                DepthTarget  = new FramebufferAttachmentDescription(depthTex, 0),
            });

            _outputBuffers[(width, height)] = (colorTex, depthTex, stagingTex, buffer);
        }

        return (frame.color, frame.staging, frame.buf);
    }

    #endregion


    #region Batch & Rendering

    public RenderBatch? CurrentBatch { get; private set; }
    public ModelInfo? CurrentModel { get; private set; }
    public SpaceInfo? CurrentSpace { get; private set; }

    public RenderBatch CreateRenderBatch()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        throw new NotImplementedException();
    }

    private void SwitchToBatch(RenderBatch batch)
    {
        CurrentBatch = batch;

        if (CurrentModel != batch.ModelInfo)
        {
            CurrentModel = batch.ModelInfo;
            _commandList.UpdateBuffer(_vertexBuffer, 0, batch.ModelInfo.GetVertexes());
            _commandList.UpdateBuffer(_indexBuffer,  0, batch.ModelInfo.GetIndices());
        }
        if (CurrentSpace != batch.SpaceInfo)
        {
            CurrentSpace = batch.SpaceInfo;
            _commandList.UpdateBuffer(_spaceUniformBuffer, 0, batch.SpaceInfo);
        }
    }

    internal void Render(RenderBatch batch, ReadOnlySpan<TextureView> textures)
    {
        using var texResSet = GetTexResSet(textures);

        _commandList.Begin();

        if (!ReferenceEquals(CurrentBatch, batch)) SwitchToBatch(batch);

        _commandList.SetPipeline(_pipeline);
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _commandList.SetGraphicsResourceSet(0, _spaceResourceSet);
        _commandList.SetGraphicsResourceSet(1, texResSet.Value);
        _commandList.SetViewport(0, new Viewport(0, 0, 16, 16, 0, 1));
        _commandList.SetFramebuffer(batch.TargetBuf);

        _commandList.ClearColorTarget(0, new(0, 0, 0, 0));
        _commandList.ClearDepthStencil(1f);
        _commandList.DrawIndexed(batch.ModelInfo.IndexCount);

        _commandList.CopyTexture(batch.TargetTex, batch.StagingTex);

        _commandList.End();
        GraphicsDevice.SubmitCommands(_commandList);
        GraphicsDevice.WaitForIdle();
    }

    #endregion
}