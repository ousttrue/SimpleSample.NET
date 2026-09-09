// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

unsafe class HelloTriangleApplication : IDisposable
{
    private readonly GlfwWindow _window;

    // const int MAX_FRAMES_IN_FLIGHT = 2;

    private readonly InstanceObject _instance;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly DeviceObject _device;
    private readonly SwapchainObject _swapchain;

    private VkPipelineLayout pipelineLayout;

    private VkPipeline graphicsPipeline;

    public HelloTriangleApplication()
    {
        _window = new GlfwWindow();

        _instance = new InstanceObject(_window);
        _physicalDevice = _instance.pickPhysicalDevice(DeviceObject.DeviceExtensions);

        var deviceProperties = new VkPhysicalDeviceProperties2
        {
            sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
        };
        _instance.Api.vkGetPhysicalDeviceProperties2(_physicalDevice, &deviceProperties);
        Console.Error.WriteLine(
            $"Selected device: {Marshal.PtrToStringAnsi((nint)deviceProperties.properties.deviceName) ?? throw new Exception()}"
        );

        var indices = QueueFamilyIndices.findQueueFamilies(
            _instance.Api,
            _physicalDevice,
            _instance.Surface
        );
        _device = new DeviceObject(
            _instance.Api,
            _physicalDevice,
            indices.GraphicsFamily,
            indices.PresentFamily
        );
        _swapchain = new SwapchainObject(
            _instance.Api,
            _physicalDevice,
            _instance.Surface,
            _device.Api,
            new()
        );
        createGraphicsPipeline();

        while (true)
        {
            if (!_window.NextFrame())
            {
                break;
            }
            drawFrame();
        }

        _device.Api.vkDeviceWaitIdle();
    }

    public void Dispose()
    {
        _device.Api.vkDestroyPipeline(graphicsPipeline, null);
        _device.Api.vkDestroyPipelineLayout(pipelineLayout, null);

        _swapchain.Dispose();
        _device.Dispose();
        _instance.Dispose();
        _window.Dispose();
    }

    void createGraphicsPipeline()
    {
        var vertShaderCode = ShaderResource.FromAssembly("shader.vert.spv");
        var vertShaderModule = createShaderModule(vertShaderCode);

        var fragShaderCode = ShaderResource.FromAssembly("shader.frag.spv");
        var fragShaderModule = createShaderModule(fragShaderCode);

        fixed (byte* main = "main"u8)
        {
            var vertShaderStageInfo = new VkPipelineShaderStageCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlags.Vertex,
                module = vertShaderModule,
                pName = main,
            };
            var fragShaderStageInfo = new VkPipelineShaderStageCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_SHADER_STAGE_CREATE_INFO,
                stage = VkShaderStageFlags.Fragment,
                module = fragShaderModule,
                pName = main,
            };
            var shaderStages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                vertShaderStageInfo,
                fragShaderStageInfo,
            };

            var vertexInputInfo = new VkPipelineVertexInputStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO,
                vertexBindingDescriptionCount = 0,
                vertexAttributeDescriptionCount = 0,
            };

            var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO,
                topology = VkPrimitiveTopology.TriangleList,
                primitiveRestartEnable = false,
            };
            var viewportState = new VkPipelineViewportStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_VIEWPORT_STATE_CREATE_INFO,
                viewportCount = 1,
                scissorCount = 1,
            };
            var rasterizer = new VkPipelineRasterizationStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_RASTERIZATION_STATE_CREATE_INFO,
                depthClampEnable = false,
                rasterizerDiscardEnable = false,
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1.0f,
                cullMode = VkCullModeFlags.Back,
                frontFace = VkFrontFace.Clockwise,
                depthBiasEnable = false,
            };
            var multisampling = new VkPipelineMultisampleStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_MULTISAMPLE_STATE_CREATE_INFO,
                sampleShadingEnable = false,
                rasterizationSamples = VkSampleCountFlags.Count1,
            };
            var colorBlendAttachment = new VkPipelineColorBlendAttachmentState
            {
                colorWriteMask =
                    VkColorComponentFlags.R
                    | VkColorComponentFlags.G
                    | VkColorComponentFlags.B
                    | VkColorComponentFlags.A,
                blendEnable = false,
            };
            var colorBlending = new VkPipelineColorBlendStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_COLOR_BLEND_STATE_CREATE_INFO,
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy,
                attachmentCount = 1,
                pAttachments = &colorBlendAttachment,
            };
            colorBlending.blendConstants[0] = 0.0f;
            colorBlending.blendConstants[1] = 0.0f;
            colorBlending.blendConstants[2] = 0.0f;
            colorBlending.blendConstants[3] = 0.0f;

            var dynamicStates = stackalloc VkDynamicState[]
            {
                VkDynamicState.Viewport,
                VkDynamicState.Scissor,
            };
            var dynamicState = new VkPipelineDynamicStateCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_DYNAMIC_STATE_CREATE_INFO,
                dynamicStateCount = 2, //(uint)dynamicStates.Length,
                pDynamicStates = dynamicStates,
            };

            var pipelineLayoutInfo = new VkPipelineLayoutCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_PIPELINE_LAYOUT_CREATE_INFO,
                setLayoutCount = 0,
                pushConstantRangeCount = 0,
            };

            if (
                _device.Api.vkCreatePipelineLayout(&pipelineLayoutInfo, null, out pipelineLayout)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create pipeline layout!");
            }

            var pipelineInfo = new VkGraphicsPipelineCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_GRAPHICS_PIPELINE_CREATE_INFO,
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputInfo,
                pInputAssemblyState = &inputAssembly,
                pViewportState = &viewportState,
                pRasterizationState = &rasterizer,
                pMultisampleState = &multisampling,
                pColorBlendState = &colorBlending,
                pDynamicState = &dynamicState,
                layout = pipelineLayout,
                renderPass = _swapchain.RenderPass,
                subpass = 0,
                basePipelineHandle = default,
            };

            VkPipeline _graphicsPipeline;
            if (
                _device.Api.vkCreateGraphicsPipelines(
                    default,
                    1,
                    &pipelineInfo,
                    null,
                    &_graphicsPipeline
                ) != VK_SUCCESS
            )
            {
                throw new Exception("failed to create graphics pipeline!");
            }
            graphicsPipeline = _graphicsPipeline;

            _device.Api.vkDestroyShaderModule(fragShaderModule, null);
            _device.Api.vkDestroyShaderModule(vertShaderModule, null);
        }
    }

    void recordCommandBuffer(VkCommandBuffer commandBuffer, uint imageIndex)
    {
        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_device.Api.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VK_SUCCESS)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        var renderPassInfo = new VkRenderPassBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
            renderPass = _swapchain.RenderPass,
            framebuffer = _swapchain._framebuffers[imageIndex],
        };
        renderPassInfo.renderArea.offset = new(0, 0);
        renderPassInfo.renderArea.extent = _swapchain.Extent;

        var clearColor = new VkClearValue { };
        clearColor.color.float32[0] = 0.0f;
        clearColor.color.float32[1] = 0.0f;
        clearColor.color.float32[2] = 0.0f;
        clearColor.color.float32[2] = 1.0f;
        renderPassInfo.clearValueCount = 1;
        renderPassInfo.pClearValues = &clearColor;

        _device.Api.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

        _device.Api.vkCmdBindPipeline(
            commandBuffer,
            VkPipelineBindPoint.Graphics,
            graphicsPipeline
        );

        var viewport = new VkViewport
        {
            x = 0.0f,
            y = 0.0f,
            width = _swapchain.Extent.width,
            height = _swapchain.Extent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _device.Api.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = _swapchain.Extent };
        _device.Api.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        _device.Api.vkCmdDraw(commandBuffer, 3, 1, 0, 0);

        _device.Api.vkCmdEndRenderPass(commandBuffer);
        if (_device.Api.vkEndCommandBuffer(commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to record command buffer!");
        }
    }

    void drawFrame()
    {
        var (
            imageIndex,
            imageAvailableSemaphore,
            inFlightFence,
            commandBuffer,
            renderFinishedSemaphore
        ) = _swapchain.Acquire();

        recordCommandBuffer(commandBuffer, imageIndex);

        var waitSemaphores = stackalloc VkSemaphore[] { imageAvailableSemaphore };
        var waitStages = stackalloc VkPipelineStageFlags[]
        {
            VkPipelineStageFlags.ColorAttachmentOutput,
        };
        var signalSemaphores = stackalloc VkSemaphore[] { renderFinishedSemaphore };
        var cmd = commandBuffer;
        var submitInfo = new VkSubmitInfo
        {
            sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
            waitSemaphoreCount = 1,
            pWaitSemaphores = waitSemaphores,
            pWaitDstStageMask = waitStages,
            commandBufferCount = 1,
            pCommandBuffers = &cmd,
            signalSemaphoreCount = 1,
            pSignalSemaphores = signalSemaphores,
        };
        if (
            _device.Api.vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, inFlightFence)
            != VK_SUCCESS
        )
        {
            throw new Exception("failed to submit draw command buffer!");
        }

        _swapchain.Present(imageIndex);
    }

    VkShaderModule createShaderModule(ReadOnlySpan<byte> code)
    {
        fixed (byte* pCode = code)
        {
            var createInfo = new VkShaderModuleCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_SHADER_MODULE_CREATE_INFO,
                codeSize = (uint)code.Length,
                pCode = (uint*)pCode,
            };

            if (
                _device.Api.vkCreateShaderModule(&createInfo, null, out var shaderModule)
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create shader module!");
            }

            return shaderModule;
        }
    }
}

static class Program
{
    public static void Main()
    {
        using var app = new HelloTriangleApplication();
    }
}
