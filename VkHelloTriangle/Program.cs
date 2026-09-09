// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

unsafe class HelloTriangleApplication : IDisposable
{
    private readonly GlfwWindow _window;

    // const int MAX_FRAMES_IN_FLIGHT = 2;

    private readonly InstanceObject _instance;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly DeviceObject _device;

    // private KhrSwapchain khrSwapchain;
    private VkSwapchainKHR swapChain;

    private VkImage[] swapChainImages;

    private VkFormat swapChainImageFormat;
    private VkExtent2D swapChainExtent;

    private VkImageView[] swapChainImageViews;

    private VkFramebuffer[] swapChainFramebuffers;

    private VkRenderPass renderPass;

    private VkPipelineLayout pipelineLayout;

    private VkPipeline graphicsPipeline;

    private VkCommandPool commandPool;

    private VkCommandBuffer commandBuffer;

    private VkSemaphore imageAvailableSemaphore;
    private VkSemaphore renderFinishedSemaphore;
    private VkFence inFlightFence;

    public HelloTriangleApplication()
    {
        _window = new GlfwWindow();

        _instance = new InstanceObject(_window);
        _physicalDevice = _instance.pickPhysicalDevice(DeviceObject.DeviceExtensions);
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
        createSwapChain();
        createImageViews();
        createRenderPass();
        createGraphicsPipeline();
        createFramebuffers();
        createCommandPool();
        createCommandBuffer();
        createSyncObjects();

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
        _device.Api.vkDestroySemaphore(renderFinishedSemaphore, null);
        _device.Api.vkDestroySemaphore(imageAvailableSemaphore, null);
        _device.Api.vkDestroyFence(inFlightFence, null);

        _device.Api.vkDestroyCommandPool(commandPool, null);

        foreach (var framebuffer in swapChainFramebuffers)
        {
            _device.Api.vkDestroyFramebuffer(framebuffer, null);
        }

        _device.Api.vkDestroyPipeline(graphicsPipeline, null);
        _device.Api.vkDestroyPipelineLayout(pipelineLayout, null);
        _device.Api.vkDestroyRenderPass(renderPass, null);

        foreach (var imageView in swapChainImageViews)
        {
            _device.Api.vkDestroyImageView(imageView, null);
        }

        _device.Api.vkDestroySwapchainKHR(swapChain, null);
        _device.Dispose();
        _instance.Dispose();
        _window.Dispose();
    }

    static void StrCopy(Span<byte> dst, ReadOnlySpan<byte> src)
    {
        src.CopyTo(dst);
    }

    void createSwapChain()
    {
        var swapChainSupport = SwapchainSupportDetails.querySwapchainSupport(
            _instance.Api,
            _physicalDevice,
            _instance.Surface
        );

        var surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
        var presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);

        var extent =
            (swapChainSupport.capabilities.currentExtent.width != uint.MaxValue)
                ? swapChainSupport.capabilities.currentExtent
                : _window.GetExtent(
                    swapChainSupport.capabilities.minImageExtent,
                    swapChainSupport.capabilities.maxImageExtent
                );

        var imageCount = swapChainSupport.capabilities.minImageCount + 1;
        if (
            swapChainSupport.capabilities.maxImageCount > 0
            && imageCount > swapChainSupport.capabilities.maxImageCount
        )
        {
            imageCount = swapChainSupport.capabilities.maxImageCount;
        }

        var createInfo = new VkSwapchainCreateInfoKHR
        {
            sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
            surface = _instance.Surface,
            minImageCount = imageCount,
            imageFormat = surfaceFormat.format,
            imageColorSpace = surfaceFormat.colorSpace,
            imageExtent = extent,
            imageArrayLayers = 1,
            imageUsage = VkImageUsageFlags.ColorAttachment,
        };

        var indices = QueueFamilyIndices.findQueueFamilies(
            _instance.Api,
            _physicalDevice,
            _instance.Surface
        );
        var queueFamilyIndices = stackalloc uint[]
        {
            indices.GraphicsFamily,
            indices.PresentFamily,
        };

        if (indices.GraphicsFamily != indices.PresentFamily)
        {
            createInfo.imageSharingMode = VkSharingMode.Concurrent;
            createInfo.queueFamilyIndexCount = 2;
            createInfo.pQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            createInfo.imageSharingMode = VkSharingMode.Exclusive;
        }

        createInfo.preTransform = swapChainSupport.capabilities.currentTransform;
        // createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
        createInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
        createInfo.presentMode = presentMode;
        createInfo.clipped = true;

        createInfo.oldSwapchain = default;

        if (_device.Api.vkCreateSwapchainKHR(&createInfo, null, out swapChain) != VK_SUCCESS)
        {
            throw new Exception("failed to create swap chain!");
        }

        _device.Api.vkGetSwapchainImagesKHR(swapChain, &imageCount, null);
        swapChainImages = new VkImage[(int)imageCount];
        fixed (VkImage* images = swapChainImages)
        {
            _device.Api.vkGetSwapchainImagesKHR(swapChain, &imageCount, images);
        }

        swapChainImageFormat = surfaceFormat.format;
        swapChainExtent = extent;
    }

    void createImageViews()
    {
        swapChainImageViews = new VkImageView[swapChainImages.Length];

        for (int i = 0; i < swapChainImages.Length; ++i)
        {
            var createInfo = new VkImageViewCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                image = swapChainImages[i],
                viewType = VkImageViewType.Image2D,
                format = swapChainImageFormat,
            };
            createInfo.components.r = VkComponentSwizzle.Identity;
            createInfo.components.g = VkComponentSwizzle.Identity;
            createInfo.components.b = VkComponentSwizzle.Identity;
            createInfo.components.a = VkComponentSwizzle.Identity;
            createInfo.subresourceRange.aspectMask = VkImageAspectFlags.Color;
            createInfo.subresourceRange.baseMipLevel = 0;
            createInfo.subresourceRange.levelCount = 1;
            createInfo.subresourceRange.baseArrayLayer = 0;
            createInfo.subresourceRange.layerCount = 1;

            if (
                _device.Api.vkCreateImageView(&createInfo, null, out swapChainImageViews[i])
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create image views!");
            }
        }
    }

    void createRenderPass()
    {
        var colorAttachment = new VkAttachmentDescription
        {
            format = swapChainImageFormat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.PresentSrcKHR,
        };

        var colorAttachmentRef = new VkAttachmentReference
        {
            attachment = 0,
            layout = VkImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorAttachmentRef,
        };

        var dependency = new VkSubpassDependency
        {
            srcSubpass = VK_SUBPASS_EXTERNAL,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
            srcAccessMask = 0,
            dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
            dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
        };

        var renderPassInfo = new VkRenderPassCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDER_PASS_CREATE_INFO,
            attachmentCount = 1,
            pAttachments = &colorAttachment,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 1,
            pDependencies = &dependency,
        };

        if (_device.Api.vkCreateRenderPass(&renderPassInfo, null, out renderPass) != VK_SUCCESS)
        {
            throw new Exception("failed to create render pass!");
        }
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
                renderPass = renderPass,
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

    void createFramebuffers()
    {
        swapChainFramebuffers = new VkFramebuffer[swapChainImageViews.Length];

        for (int i = 0; i < swapChainImageViews.Length; i++)
        {
            var attachment = swapChainImageViews[i];

            var framebufferInfo = new VkFramebufferCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
                renderPass = renderPass,
                attachmentCount = 1,
                pAttachments = &attachment,
                width = swapChainExtent.width,
                height = swapChainExtent.height,
                layers = 1,
            };

            if (
                _device.Api.vkCreateFramebuffer(&framebufferInfo, null, out swapChainFramebuffers[i])
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create framebuffer!");
            }
        }
    }

    void createCommandPool()
    {
        var queueFamilyIndices = QueueFamilyIndices.findQueueFamilies(
            _instance.Api,
            _physicalDevice,
            _instance.Surface
        );

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = queueFamilyIndices.GraphicsFamily,
        };

        if (_device.Api.vkCreateCommandPool(&poolInfo, null, out commandPool) != VK_SUCCESS)
        {
            throw new Exception("failed to create command pool!");
        }
    }

    void createCommandBuffer()
    {
        var allocInfo = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1,
        };

        VkCommandBuffer _commandBuffer;
        if (_device.Api.vkAllocateCommandBuffers(&allocInfo, &_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to allocate command buffers!");
        }
        commandBuffer = _commandBuffer;
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
            renderPass = renderPass,
            framebuffer = swapChainFramebuffers[imageIndex],
        };
        renderPassInfo.renderArea.offset = new(0, 0);
        renderPassInfo.renderArea.extent = swapChainExtent;

        var clearColor = new VkClearValue { };
        clearColor.color.float32[0] = 0.0f;
        clearColor.color.float32[1] = 0.0f;
        clearColor.color.float32[2] = 0.0f;
        clearColor.color.float32[2] = 1.0f;
        renderPassInfo.clearValueCount = 1;
        renderPassInfo.pClearValues = &clearColor;

        _device.Api.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

        _device.Api.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, graphicsPipeline);

        var viewport = new VkViewport
        {
            x = 0.0f,
            y = 0.0f,
            width = swapChainExtent.width,
            height = swapChainExtent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        _device.Api.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = swapChainExtent };
        _device.Api.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        _device.Api.vkCmdDraw(commandBuffer, 3, 1, 0, 0);

        _device.Api.vkCmdEndRenderPass(commandBuffer);
        if (_device.Api.vkEndCommandBuffer(commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to record command buffer!");
        }
    }

    void createSyncObjects()
    {
        var semaphoreInfo = new VkSemaphoreCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
        };

        var fenceInfo = new VkFenceCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
            flags = VkFenceCreateFlags.Signaled,
        };

        if (
            _device.Api.vkCreateSemaphore(&semaphoreInfo, null, out imageAvailableSemaphore)
                != VK_SUCCESS
            || _device.Api.vkCreateSemaphore(&semaphoreInfo, null, out renderFinishedSemaphore)
                != VK_SUCCESS
            || _device.Api.vkCreateFence(&fenceInfo, null, out inFlightFence) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    void drawFrame()
    {
        var _inFlightFence = inFlightFence;
        _device.Api.vkWaitForFences(1, &_inFlightFence, true, ulong.MaxValue);
        _device.Api.vkResetFences(1, &_inFlightFence);

        uint imageIndex;
        _device.Api.vkAcquireNextImageKHR(
            swapChain,
            ulong.MaxValue,
            imageAvailableSemaphore,
            default,
            &imageIndex
        );

        _device.Api.vkResetCommandBuffer(
            commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );
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
        if (_device.Api.vkQueueSubmit(_device.GraphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS)
        {
            throw new Exception("failed to submit draw command buffer!");
        }

        var swapChains = stackalloc VkSwapchainKHR[] { swapChain };
        var presentInfo = new VkPresentInfoKHR
        {
            sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = 1,
            pWaitSemaphores = signalSemaphores,
            swapchainCount = 1,
            pSwapchains = swapChains,
            pImageIndices = &imageIndex,
        };

        _device.Api.vkQueuePresentKHR(_device.PresentQueue, &presentInfo);
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

    VkSurfaceFormatKHR chooseSwapSurfaceFormat(ReadOnlySpan<VkSurfaceFormatKHR> availableFormats)
    {
        foreach (var availableFormat in availableFormats)
        {
            if (
                availableFormat.format == VkFormat.B8G8R8A8Srgb
                && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonLinear
            )
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    VkPresentModeKHR chooseSwapPresentMode(ReadOnlySpan<VkPresentModeKHR> availablePresentModes)
    {
        foreach (var availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == VkPresentModeKHR.Mailbox)
            {
                return availablePresentMode;
            }
        }

        return VkPresentModeKHR.Fifo;
    }
}

static class Program
{
    public static void Main()
    {
        using var app = new HelloTriangleApplication();
    }
}
