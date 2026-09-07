using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

public class VkPipeline<CONSTANT> : IDisposable
    where CONSTANT : unmanaged
{
    Vk _vk;
    Device _device;

    public readonly DescriptorSetLayout DescriptorSetLayout;
    public readonly DescriptorPoolObject[] DescriptorPools;

    public readonly PipelineLayout PipelieLayout;
    private readonly Pipeline _graphicsPipeline;

    public record struct RenderPassArgs(
        Extent2D extent,
        ImageView[] imageViews,
        ImageView depthImageView
    ) { }

    public record struct DepthStencilInfo(
        Format depthFormat,
        PipelineDepthStencilStateCreateInfo depthStencil
    ) { }

    public unsafe VkPipeline(
        Vk vk,
        Device device,
        VkShaderModule vs,
        VkShaderModule fs,
        PrimitiveTopology topology,
        VertexInputBindingDescription vertexInputBindingDescription,
        VertexInputAttributeDescription[] vertexInputAttributeDescriptions,
        uint maxFlightCount,
        ReadOnlySpan<DescriptorSetLayoutBinding> descriptorSetLayoutBindings,
        Format colorFormat,
        DepthStencilInfo? depthStencil
    )
    {
        _vk = vk;
        _device = device;

        //
        // descriptorSetLayout
        //
        fixed (DescriptorSetLayoutBinding* bindingsPtr = descriptorSetLayoutBindings)
        {
            DescriptorSetLayoutCreateInfo layoutInfo = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)descriptorSetLayoutBindings.Length,
                PBindings = bindingsPtr,
            };
            if (
                vk.CreateDescriptorSetLayout(device, in layoutInfo, null, out DescriptorSetLayout)
                != Result.Success
            )
            {
                throw new Exception("failed to create descriptor set layout!");
            }
        }

        //
        // descriptorPool
        //
        DescriptorPools = new DescriptorPoolObject[maxFlightCount];
        for (int i = 0; i < DescriptorPools.Length; ++i)
        {
            DescriptorPools[i] = new DescriptorPoolObject(
                _vk,
                _device,
                DescriptorSetLayout,
                descriptorSetLayoutBindings,
                255
            );
        }

        var constantRange = new PushConstantRange
        {
            Offset = 0,
            Size = (uint)Marshal.SizeOf<CONSTANT>(),
            StageFlags = ShaderStageFlags.VertexBit,
        };

        //
        // pipeline
        //
        var descriptorSetLayout = DescriptorSetLayout;
        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &descriptorSetLayout,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &constantRange,
        };
        if (
            vk.CreatePipelineLayout(device, in pipelineLayoutInfo, null, out PipelieLayout)
            != Result.Success
        )
        {
            throw new Exception("failed to create pipeline layout!");
        }

        //
        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vs.Module,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };
        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fs.Module,
            PName = (byte*)SilkMarshal.StringToPtr("main"),
        };
        var shaderStages = stackalloc[] { vertShaderStageInfo, fragShaderStageInfo };

        fixed (
            VertexInputAttributeDescription* attributeDescriptionsPtr =
                vertexInputAttributeDescriptions
        )
        {
            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &vertexInputBindingDescription,
                VertexAttributeDescriptionCount = (uint)vertexInputAttributeDescriptions.Length,
                PVertexAttributeDescriptions = attributeDescriptionsPtr,
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = topology,
            };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = default,
                ScissorCount = 1,
                PScissors = default,
            };

            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                PolygonMode = PolygonMode.Fill,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                LineWidth = 1,
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };

            // PipelineColorBlendAttachmentState colorBlendAttachment = new()
            // {
            //     ColorWriteMask =
            //         ColorComponentFlags.RBit
            //         | ColorComponentFlags.GBit
            //         | ColorComponentFlags.BBit
            //         | ColorComponentFlags.ABit,
            //     BlendEnable = false,
            // };
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                BlendEnable = new Silk.NET.Core.Bool32(true),
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.OneMinusSrcAlpha,
                AlphaBlendOp = BlendOp.Add,
                ColorWriteMask =
                    ColorComponentFlags.RBit
                    | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit
                    | ColorComponentFlags.ABit,
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                AttachmentCount = 1,
                PAttachments = (PipelineColorBlendAttachmentState*)
                    Unsafe.AsPointer(ref colorBlendAttachment),
            };
            // PipelineColorBlendStateCreateInfo colorBlending = new()
            // {
            //     SType = StructureType.PipelineColorBlendStateCreateInfo,
            //     LogicOpEnable = false,
            //     LogicOp = LogicOp.Copy,
            //     AttachmentCount = 1,
            //     PAttachments = &colorBlendAttachment,
            // };
            // colorBlending.BlendConstants[0] = 0;
            // colorBlending.BlendConstants[1] = 0;
            // colorBlending.BlendConstants[2] = 0;
            // colorBlending.BlendConstants[3] = 0;

            var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
            PipelineDynamicStateCreateInfo dynamicState = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2,
                PDynamicStates = dynamicStates,
            };

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                Layout = PipelieLayout,
                Subpass = 0,
                BasePipelineHandle = default,
            };
            var pipelineRenderingCreate = new PipelineRenderingCreateInfo
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &colorFormat,
            };
            if (
                depthStencil is
                (Format depthFormat, PipelineDepthStencilStateCreateInfo depthStencilInfo)
            )
            {
                pipelineInfo.PDepthStencilState = &depthStencilInfo;
                pipelineRenderingCreate.DepthAttachmentFormat = depthFormat;
                pipelineRenderingCreate.StencilAttachmentFormat = depthFormat;
            }
            {
                // vulkan-1.3 dynamic rendering(without RenderPass and FrameBuffer)
                pipelineInfo.PNext = &pipelineRenderingCreate;
            }
            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    in pipelineInfo,
                    null,
                    out _graphicsPipeline
                ) != Result.Success
            )
            {
                throw new Exception("failed to create graphics pipeline!");
            }
        }

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }

    public unsafe void Dispose()
    {
        foreach (var pool in DescriptorPools)
        {
            pool.Dispose();
        }
        _vk.DestroyPipeline(_device, _graphicsPipeline, null);
        _vk.DestroyPipelineLayout(_device, PipelieLayout, null);
        _vk.DestroyDescriptorSetLayout(_device, DescriptorSetLayout, null);
    }

    public DescriptorSet Bind(
        uint frameCount,
        CommandBuffer commandBuffer,
        Extent2D extent,
        uint imageIndex
    )
    {
        var descSet = DescriptorPools[imageIndex].Get(frameCount);
        Bind(commandBuffer, extent, descSet);
        return descSet;
    }

    public unsafe void Bind(
        CommandBuffer commandBuffer,
        Extent2D extent,
        DescriptorSet descriptorSet
    )
    {
        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = extent.Width,
            Height = extent.Height,
            MinDepth = 0,
            MaxDepth = 1,
        };
        _vk.CmdSetViewport(commandBuffer, 0, 1, &viewport);

        Rect2D scissor = new() { Offset = { X = 0, Y = 0 }, Extent = extent };
        _vk.CmdSetScissor(commandBuffer, 0, 1, &scissor);

        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, _graphicsPipeline);

        _vk.CmdBindDescriptorSets(
            commandBuffer,
            PipelineBindPoint.Graphics,
            PipelieLayout,
            0,
            1,
            in descriptorSet,
            0,
            null
        );
    }

    public unsafe void PushConstant(CommandBuffer commandBuffer, CONSTANT value)
    {
        _vk.CmdPushConstants(
            commandBuffer,
            PipelieLayout,
            ShaderStageFlags.VertexBit,
            0,
            (uint)Marshal.SizeOf<CONSTANT>(),
            &value
        );
    }
}
