using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Vulkan;

public class ImGuiImplVulkan : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;

    private const int maxSets = 255;
    private readonly DescriptorPool _descriptorPool;
    private readonly DescriptorSet[] _descriptorSets;
    private readonly List<DescriptorSet> _descriptorSetPool = new();

    record struct Constant(Vector2 Scale, Vector2 Translate) { }

    private readonly VkPipeline<Constant> _pipeline;

    public int _vertBufferIndex;
    private readonly ImDrawVertBuffer[] _vertBuffers;

    // DescriptorSetLayout
    private readonly DescriptorSetLayoutBinding[] bindings =
    [
        new DescriptorSetLayoutBinding
        {
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.FragmentBit,
        },
    ];

    // VertexInput
    static readonly VertexInputBindingDescription binding_desc = new VertexInputBindingDescription
    {
        Stride = (uint)Unsafe.SizeOf<ImDrawVert>(),
        InputRate = VertexInputRate.Vertex,
    };
    static readonly VertexInputAttributeDescription[] attribute_desc =
    [
        new VertexInputAttributeDescription
        {
            Location = 0,
            Format = Format.R32G32Sfloat,
            Offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos)),
        },
        new VertexInputAttributeDescription
        {
            Location = 1,
            Format = Format.R32G32Sfloat,
            Offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv)),
        },
        new VertexInputAttributeDescription
        {
            Location = 2,
            Format = Format.R8G8B8A8Unorm,
            Offset = (uint)Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col)),
        },
    ];

    public unsafe ImGuiImplVulkan(
        Vk vk,
        Device device,
        Format colorFormat,
        Format depthFormat,
        uint swapchainImageCount
    )
    {
        _vk = vk;
        _device = device;

        _vertBuffers = new ImDrawVertBuffer[swapchainImageCount];
        for (int i = 0; i < _vertBuffers.Length; ++i)
        {
            _vertBuffers[i] = new(_vk, _device);
        }
        _vertBufferIndex = 0;

        using var vs = new VkShaderModule(_vk, _device, FromAssembly("glsl_shader.vert.spv"));
        using var fs = new VkShaderModule(_vk, _device, FromAssembly("glsl_shader.frag.spv"));
        _pipeline = new(
            _vk,
            _device,
            vs,
            fs,
            PrimitiveTopology.TriangleList,
            binding_desc,
            attribute_desc,
            swapchainImageCount,
            bindings,
            colorFormat,
            depthFormat,
            new PipelineDepthStencilStateCreateInfo
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
            }
        );

        //
        // Create the descriptor pool for ImGui
        //
        var poolSize = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = maxSets,
        };
        var descriptorPoolInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
            MaxSets = maxSets,
        };
        if (
            _vk.CreateDescriptorPool(_device, in descriptorPoolInfo, default, out _descriptorPool)
            != Result.Success
        )
        {
            throw new Exception($"Unable to create descriptor pool");
        }

        AllocateDescriptorSets(
            _vk,
            _device,
            _descriptorPool,
            _pipeline.DescriptorSetLayout,
            maxSets,
            out _descriptorSets
        );
        foreach (var desc in _descriptorSets)
        {
            _descriptorSetPool.Add(desc);
        }
    }

    private static readonly Assembly assm = Assembly.GetExecutingAssembly();

    public static byte[] FromAssembly(string name)
    {
        using var stream =
            assm.GetManifestResourceStream(name)
            ?? throw new Exception($"GetManifestResourceStream: {name}");
        // var reader = new StreamReader(stream);
        // return reader.ReadToEnd();
        using (MemoryStream ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }

    public static unsafe void AllocateDescriptorSets(
        Vk vk,
        Device device,
        DescriptorPool pool,
        DescriptorSetLayout layout,
        uint maxSets,
        out DescriptorSet[] descriptorSets
    )
    {
        descriptorSets = new DescriptorSet[maxSets];

        var layouts = stackalloc DescriptorSetLayout[(int)maxSets];
        new Span<DescriptorSetLayout>(layouts, (int)maxSets).Fill(layout);
        var allocateInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = pool,
            DescriptorSetCount = maxSets,
            PSetLayouts = layouts,
        };
        fixed (DescriptorSet* descriptorSetsPtr = descriptorSets)
        {
            if (
                vk.AllocateDescriptorSets(device, in allocateInfo, descriptorSetsPtr)
                != Result.Success
            )
            {
                throw new Exception("failed to allocate descriptor sets!");
            }
        }
    }

    public unsafe void Dispose()
    {
        foreach (var mesh in _vertBuffers)
        {
            mesh.Dispose();
        }
        _pipeline.Dispose();
        _vk.DestroyDescriptorPool(_device, _descriptorPool, default);
    }

    public unsafe DescriptorSet BindTexture(TextureObject texture)
    {
        var descImageInfo = new DescriptorImageInfo
        {
            Sampler = texture.Sampler,
            ImageView = texture.ImageView,
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        };

        var desc = _descriptorSetPool[0];
        _descriptorSetPool.RemoveAt(0);

        var writeDescriptors = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = desc,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            PImageInfo = &descImageInfo,
        };
        _vk.UpdateDescriptorSets(_device, 1, in writeDescriptors, 0, default);

        return desc;
    }

    // public void UnbindTexture(DescriptorSet texture)
    // {
    //     _descriptorSetPool.Add(texture);
    // }

    public void SetFontTexture(DescriptorSet fontTexture)
    {
        //     SetFontID(fontTexture.Handle);
        // }
        // private void SetFontID(ulong handle)
        // {
        ImGuiNET.ImGui.GetIO().Fonts.SetTexID((IntPtr)fontTexture.Handle);
        // BeginFrame();
    }

    public unsafe void RenderImDrawData(
        PhysicalDevice physicalDevice,
        in ImDrawDataPtr drawDataPtr,
        in CommandBuffer commandBuffer,
        uint imageIndex,
        in Extent2D swapChainExtent
    )
    {
        int framebufferWidth = (int)(drawDataPtr.DisplaySize.X * drawDataPtr.FramebufferScale.X);
        int framebufferHeight = (int)(drawDataPtr.DisplaySize.Y * drawDataPtr.FramebufferScale.Y);
        if (framebufferWidth <= 0 || framebufferHeight <= 0)
        {
            return;
        }

        // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
        var drawData = *drawDataPtr.NativePtr;
        int fb_width = (int)(drawData.DisplaySize.X * drawData.FramebufferScale.X);
        int fb_height = (int)(drawData.DisplaySize.Y * drawData.FramebufferScale.Y);
        if (fb_width <= 0 || fb_height <= 0)
        {
            return;
        }

        // Allocate array to store enough vertex/index buffers

        _vertBufferIndex = (_vertBufferIndex + 1) % _vertBuffers.Length;
        var vertBuffer = _vertBuffers[_vertBufferIndex];
        // update VertexBuffer
        vertBuffer.UploadDrawData(_vk, physicalDevice, _device, drawDataPtr);

        // Bind Vertex And Index Buffer:
        if (drawData.TotalVtxCount > 0)
        {
            vertBuffer.Vertex.Bind(commandBuffer);
            vertBuffer.Index.Bind(commandBuffer);
        }

        // Setup viewport:
        Viewport viewport;
        viewport.X = 0;
        viewport.Y = 0;
        viewport.Width = (float)fb_width;
        viewport.Height = (float)fb_height;
        viewport.MinDepth = 0.0f;
        viewport.MaxDepth = 1.0f;
        _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

        // Setup scale and translation:
        // Our visible imgui space lies from draw_data.DisplayPps (top left) to draw_data.DisplayPos+data_data.DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        // Span<float> scale = stackalloc float[2];
        var scale = new Vector2(2.0f / drawData.DisplaySize.X, 2.0f / drawData.DisplaySize.Y);
        var translate = new Vector2(
            -1.0f - drawData.DisplayPos.X * scale[0],
            -1.0f - drawData.DisplayPos.Y * scale[1]
        );
        _pipeline.PushConstant(commandBuffer, new Constant(scale, translate));

        // Will project scissor/clipping rectangles into framebuffer space
        Vector2 clipOff = drawData.DisplayPos; // (0,0) unless using multi-viewports
        Vector2 clipScale = drawData.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        // (Because we merged all buffers into a single one, we maintain our own offset into them)
        nint last_image_view = -1;
        int vertexOffset = 0;
        int indexOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmd_list = drawDataPtr.CmdLists[n];
            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = cmd_list.CmdBuffer[cmd_i];

                // Project scissor/clipping rectangles into framebuffer space
                Vector4 clipRect;
                clipRect.X = (pcmd.ClipRect.X - clipOff.X) * clipScale.X;
                clipRect.Y = (pcmd.ClipRect.Y - clipOff.Y) * clipScale.Y;
                clipRect.Z = (pcmd.ClipRect.Z - clipOff.X) * clipScale.X;
                clipRect.W = (pcmd.ClipRect.W - clipOff.Y) * clipScale.Y;

                if (
                    clipRect.X < fb_width
                    && clipRect.Y < fb_height
                    && clipRect.Z >= 0.0f
                    && clipRect.W >= 0.0f
                )
                {
                    // Negative offsets are illegal for vkCmdSetScissor
                    if (clipRect.X < 0.0f)
                        clipRect.X = 0.0f;
                    if (clipRect.Y < 0.0f)
                        clipRect.Y = 0.0f;

                    // Apply scissor/clipping rectangle
                    Rect2D scissor = new Rect2D();
                    scissor.Offset.X = (int)clipRect.X;
                    scissor.Offset.Y = (int)clipRect.Y;
                    scissor.Extent.Width = (uint)(clipRect.Z - clipRect.X);
                    scissor.Extent.Height = (uint)(clipRect.W - clipRect.Y);
                    _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

                    // TODO
                    // https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp#L553
                    // Bind DescriptorSets for image view (font or user texture) and samplers
                    var image_view = pcmd.GetTexID();
                    if (image_view != last_image_view)
                    {
                        var descriptorSet = new DescriptorSet { Handle = (ulong)image_view };
                        _pipeline.Bind(commandBuffer, swapChainExtent, descriptorSet);
                    }
                    last_image_view = image_view;

                    // Draw
                    _vk.CmdDrawIndexed(
                        commandBuffer,
                        pcmd.ElemCount,
                        1,
                        pcmd.IdxOffset + (uint)indexOffset,
                        (int)pcmd.VtxOffset + vertexOffset,
                        0
                    );
                }
            }
            indexOffset += cmd_list.IdxBuffer.Size;
            vertexOffset += cmd_list.VtxBuffer.Size;
        }
    }
}
