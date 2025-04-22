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

    private const PixelFormat ColorFormat = PixelFormat.R8_G8_B8_A8_SInt;
    private const PixelFormat DepthFormat = PixelFormat.D32_Float_S8_UInt;
    private const TextureSampleCount SampleCount = TextureSampleCount.Count1;

    #endregion


    #region Init&Cleanup

    public MigEnvironment()
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
        _shaders         = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
        _vertexBuffer    = factory.CreateBuffer(new BufferDescription(VertexBufferByteSize, BufferUsage.VertexBuffer));
        _indexBuffer     = factory.CreateBuffer(new BufferDescription(IndexBufferByteSize,  BufferUsage.IndexBuffer));
        _vsUniformBuffer = factory.CreateBuffer(new BufferDescription(Transforms.ByteSize,  BufferUsage.UniformBuffer));
        _fsUniformBuffer = factory.CreateBuffer(new BufferDescription(LightInfos.ByteSize,  BufferUsage.UniformBuffer));

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
                factory.CreateResourceLayout(new ResourceLayoutDescription([
                    Transforms.LayoutElementDescription,
                    LightInfos.LayoutElementDescription,
                ])),
                factory.CreateResourceLayout(new ResourceLayoutDescription([
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
                ]))
            ],
            ShaderSet = new ShaderSetDescription
            {
                VertexLayouts = [VertexInfo.LayoutDescription],
                Shaders       = _shaders,
            },
        });
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        _texturesCache.Values.Flatten().ForEach(t =>
        {
            t.view.Dispose();
            t.tex.Dispose();
        });
        _outputBuffers.Values.ForEach(t =>
        {
            t.buf.Dispose();
            t.color.Dispose();
            t.depth.Dispose();
        });

        _pipeline.Dispose();
        _vsUniformBuffer.Dispose();
        _fsUniformBuffer.Dispose();
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
    private readonly DeviceBuffer _vsUniformBuffer, _fsUniformBuffer;
    private readonly Pipeline _pipeline;

    private readonly Dictionary<(uint, uint), ConcurrentBag<(Texture tex, TextureView view)>> _texturesCache = new();
    private readonly Dictionary<(uint, uint), (Texture color, Texture depth, Framebuffer buf)> _outputBuffers = new();

    #endregion


    #region In/Out Textures

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

    internal Framebuffer GetFramebuffer(uint width, uint height)
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
                Usage       = TextureUsage.RenderTarget,
            };
            var depthTexDescription = colorTexDescription with
            {
                Format = DepthFormat,
                Usage = TextureUsage.DepthStencil
            };

            var colorTex = GraphicsDevice.ResourceFactory.CreateTexture(colorTexDescription);
            var depthTex = GraphicsDevice.ResourceFactory.CreateTexture(depthTexDescription);

            var buffer = GraphicsDevice.ResourceFactory.CreateFramebuffer(new FramebufferDescription
            {
                ColorTargets = [new FramebufferAttachmentDescription(colorTex, 0)],
                DepthTarget  = new FramebufferAttachmentDescription(depthTex, 0),
            });

            _outputBuffers[(width, height)] = (colorTex, depthTex, buffer);
        }

        return frame.buf;
    }

    #endregion


    #region Batch

    public RenderBatch CurrentBatch { get; private set; }

    public RenderBatch CreateRenderBatch()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        throw new NotImplementedException();
    }

    #endregion
}