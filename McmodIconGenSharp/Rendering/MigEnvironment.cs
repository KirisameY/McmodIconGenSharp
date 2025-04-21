using System.Runtime.CompilerServices;
using System.Text;

using KirisameLib.Extensions;

using McmodIconGenSharp.BatchInfos;
using McmodIconGenSharp.RenderingInfos;

using Veldrid;
using Veldrid.SPIRV;

namespace McmodIconGenSharp.Rendering;

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
        GraphicsDevice = GraphicsDevice.CreateVulkan(new GraphicsDeviceOptions
        {
            PreferDepthRangeZeroToOne         = true,
            PreferStandardClipSpaceYDirection = false,
            ResourceBindingModel              = ResourceBindingModel.Improved,
        });
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

        _pipeline.Dispose();
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

    #endregion


    #region Public Methods

    public RenderBatch CreateRenderBatch()
    {
        ObjectDisposedException.ThrowIf(Disposed, this);

        throw new NotImplementedException();
    }

    #endregion
}