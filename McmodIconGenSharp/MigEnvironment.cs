using System.Text;

using KirisameLib.Extensions;

using Veldrid;
using Veldrid.SPIRV;

namespace McmodIconGenSharp;

public sealed class MigEnvironment : IDisposable
{
    #region Constants

    private const uint VertexBufferByteSize = 2 * 1024 * 1024; // 2MiB
    private const uint IndexBufferByteSize = 2 * 1024 * 1024;  // 2MiB

    private const PixelFormat ColorFormat = PixelFormat.R8_G8_B8_A8_SInt;
    private const PixelFormat DepthFormat = PixelFormat.D32_Float_S8_UInt;
    private const TextureSampleCount SampleCount = TextureSampleCount.Count1;

    #endregion


    #region Init&Cleanup

    public MigEnvironment()
    {
        _graphicsDevice = GraphicsDevice.CreateVulkan(new GraphicsDeviceOptions
        {
            PreferDepthRangeZeroToOne = true,
            PreferStandardClipSpaceYDirection = false,
            ResourceBindingModel = ResourceBindingModel.Improved,
        });
        var factory = _graphicsDevice.ResourceFactory;
        ShaderDescription vertexShaderDesc = new ShaderDescription(
            ShaderStages.Vertex,
            Encoding.UTF8.GetBytes(Resources.Resources.GetShader("vertex")!),
            "main");
        ShaderDescription fragmentShaderDesc = new ShaderDescription(
            ShaderStages.Fragment,
            Encoding.UTF8.GetBytes(Resources.Resources.GetShader("fragment")!),
            "main");
        _shaders = factory.CreateFromSpirv(vertexShaderDesc, fragmentShaderDesc);
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(VertexBufferByteSize, BufferUsage.VertexBuffer));
        _indexBuffer = factory.CreateBuffer(new BufferDescription(IndexBufferByteSize,   BufferUsage.IndexBuffer));
        _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
        _pipeline = _graphicsDevice.ResourceFactory.CreateGraphicsPipeline(new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleOverrideBlend,
            DepthStencilState = new DepthStencilStateDescription
            {
                DepthTestEnabled = true,
                DepthWriteEnabled = true,
                DepthComparison = ComparisonKind.LessEqual,
            },
            Outputs = new OutputDescription
            {
                ColorAttachments = [new OutputAttachmentDescription(ColorFormat)],
                DepthAttachment = new OutputAttachmentDescription(PixelFormat.D32_Float_S8_UInt),
                SampleCount = SampleCount,
            },
            PrimitiveTopology = PrimitiveTopology.TriangleList,
            RasterizerState = RasterizerStateDescription.Default,
            ResourceBindingModel = null, // inherited from GraphicsDevice
            ResourceLayouts = [],
            ShaderSet = new ShaderSetDescription
            {
                VertexLayouts =
                [
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.Position,          VertexElementFormat.Float3),
                        new VertexElementDescription("Normal",   VertexElementSemantic.Normal,            VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2)
                    )
                ],
                Shaders = _shaders,
            },
        });
    }

    public bool Disposed { get; private set; } = false;

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        foreach (var (gpuTexture, buffer, stagingTexture) in _buffers.Values)
        {
            stagingTexture.Dispose();
            buffer.Dispose();
            gpuTexture.Dispose();
        }
        _pipeline.Dispose();
        _commandList.Dispose();
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _shaders.ForEach(s => s.Dispose());
        _graphicsDevice.Dispose();
    }

    #endregion


    #region Rending Resources

    private readonly GraphicsDevice _graphicsDevice;
    private readonly Shader[] _shaders;
    private readonly DeviceBuffer _vertexBuffer, _indexBuffer;
    private readonly CommandList _commandList;
    private readonly Pipeline _pipeline;
    private readonly Dictionary<(uint x, uint y), (Texture gpuTexture, Framebuffer buffer, Texture stagingTexture)> _buffers = new();

    #endregion
}