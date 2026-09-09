// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Silk.NET.GLFW;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

class SwapchainObject : IDisposable
{
    static VkSurfaceFormatKHR chooseSwapSurfaceFormat(
        ReadOnlySpan<VkSurfaceFormatKHR> availableFormats
    )
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

    static VkPresentModeKHR chooseSwapPresentMode(
        ReadOnlySpan<VkPresentModeKHR> availablePresentModes
    )
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

    private readonly VkDeviceApi _vkd;
    public readonly VkSwapchainKHR SwapChain;
    private readonly VkFormat ImageFormat;
    public readonly VkExtent2D Extent;
    public readonly VkRenderPass RenderPass;

    private VkSemaphore imageAvailableSemaphore;
    private VkFence inFlightFence;
    public readonly VkQueue PresentQueue;

    private readonly VkImage[] _images;
    private readonly VkImageView[] _imageViews;
    public readonly VkFramebuffer[] _framebuffers;

    private VkCommandPool commandPool;
    private VkCommandBuffer commandBuffer;
    private VkSemaphore renderFinishedSemaphore;

    public unsafe SwapchainObject(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface,
        VkDeviceApi vkd,
        VkExtent2D windowExtent
    )
    {
        _vkd = vkd;

        var swapChainSupport = SwapchainSupportDetails.querySwapchainSupport(
            vki,
            physicalDevice,
            surface
        );

        var surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
        var presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);

        var extent = swapChainSupport.CalcExtent(windowExtent);

        var imageCount = swapChainSupport.capabilities.minImageCount + 1;
        if (
            swapChainSupport.capabilities.maxImageCount > 0
            && imageCount > swapChainSupport.capabilities.maxImageCount
        )
        {
            imageCount = swapChainSupport.capabilities.maxImageCount;
        }
        var indices = QueueFamilyIndices.findQueueFamilies(vki, physicalDevice, surface);
        vkd.vkGetDeviceQueue(indices.PresentFamily, 0, out PresentQueue);

        {
            var createInfo = new VkSwapchainCreateInfoKHR
            {
                sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                surface = surface,
                minImageCount = imageCount,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment,
                preTransform = swapChainSupport.capabilities.currentTransform,
                // compositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
                compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
                presentMode = presentMode,
                clipped = true,
                oldSwapchain = default,
            };

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

            if (vkd.vkCreateSwapchainKHR(&createInfo, null, out SwapChain) != VK_SUCCESS)
            {
                throw new Exception("failed to create swap chain!");
            }
        }

        ImageFormat = surfaceFormat.format;
        Extent = extent;

        vkd.vkGetSwapchainImagesKHR(SwapChain, out imageCount);
        Span<VkImage> swapchainImages = stackalloc VkImage[(int)imageCount];
        vkd.vkGetSwapchainImagesKHR(SwapChain, swapchainImages);
        _images = swapchainImages.ToArray();

        {
            _imageViews = new VkImageView[_images.Length];
            for (int i = 0; i < _images.Length; ++i)
            {
                var createInfo = new VkImageViewCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                    image = _images[i],
                    viewType = VkImageViewType.Image2D,
                    format = ImageFormat,
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

                if (_vkd.vkCreateImageView(&createInfo, null, out _imageViews[i]) != VK_SUCCESS)
                {
                    throw new Exception("failed to create image views!");
                }
            }
        }
        {
            var colorAttachment = new VkAttachmentDescription
            {
                format = ImageFormat,
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

            if (_vkd.vkCreateRenderPass(&renderPassInfo, null, out RenderPass) != VK_SUCCESS)
            {
                throw new Exception("failed to create render pass!");
            }
        }
        {
            _framebuffers = new VkFramebuffer[_imageViews.Length];

            for (int i = 0; i < _imageViews.Length; i++)
            {
                var attachment = _imageViews[i];

                var framebufferInfo = new VkFramebufferCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_FRAMEBUFFER_CREATE_INFO,
                    renderPass = RenderPass,
                    attachmentCount = 1,
                    pAttachments = &attachment,
                    width = Extent.width,
                    height = Extent.height,
                    layers = 1,
                };

                if (
                    _vkd.vkCreateFramebuffer(&framebufferInfo, null, out _framebuffers[i])
                    != VK_SUCCESS
                )
                {
                    throw new Exception("failed to create framebuffer!");
                }
            }
        }

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
            vkd.vkCreateSemaphore(&semaphoreInfo, null, out imageAvailableSemaphore) != VK_SUCCESS
            || vkd.vkCreateSemaphore(&semaphoreInfo, null, out renderFinishedSemaphore)
                != VK_SUCCESS
            || vkd.vkCreateFence(&fenceInfo, null, out inFlightFence) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = indices.GraphicsFamily,
        };

        if (_vkd.vkCreateCommandPool(&poolInfo, null, out commandPool) != VK_SUCCESS)
        {
            throw new Exception("failed to create command pool!");
        }

        var allocInfo = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1,
        };

        VkCommandBuffer _commandBuffer;
        if (_vkd.vkAllocateCommandBuffers(&allocInfo, &_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to allocate command buffers!");
        }
        commandBuffer = _commandBuffer;
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroySemaphore(renderFinishedSemaphore, null);
        _vkd.vkDestroySemaphore(imageAvailableSemaphore, null);
        _vkd.vkDestroyFence(inFlightFence, null);
        _vkd.vkDestroyCommandPool(commandPool, null);

        foreach (var framebuffer in _framebuffers)
        {
            _vkd.vkDestroyFramebuffer(framebuffer, null);
        }
        foreach (var imageView in _imageViews)
        {
            _vkd.vkDestroyImageView(imageView, null);
        }
        _vkd.vkDestroyRenderPass(RenderPass, null);
        _vkd.vkDestroySwapchainKHR(SwapChain, null);
    }

    public unsafe (uint, VkSemaphore, VkFence, VkCommandBuffer, VkSemaphore) Acquire()
    {
        _vkd.vkDeviceWaitIdle();

        var _inFlightFence = inFlightFence;
        _vkd.vkWaitForFences(1, &_inFlightFence, true, ulong.MaxValue);
        _vkd.vkResetFences(1, &_inFlightFence);

        _vkd.vkAcquireNextImageKHR(
            SwapChain,
            ulong.MaxValue,
            imageAvailableSemaphore,
            default,
            out var imageIndex
        );

        _vkd.vkResetCommandBuffer(
            commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );

        return (
            imageIndex,
            imageAvailableSemaphore,
            inFlightFence,
            commandBuffer,
            renderFinishedSemaphore
        );
    }

    public unsafe void Present(uint imageIndex)
    {
        var signalSemaphores = stackalloc VkSemaphore[] { renderFinishedSemaphore };
        var swapChains = stackalloc VkSwapchainKHR[] { SwapChain };
        var presentInfo = new VkPresentInfoKHR
        {
            sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
            waitSemaphoreCount = 1,
            pWaitSemaphores = signalSemaphores,
            swapchainCount = 1,
            pSwapchains = swapChains,
            pImageIndices = &imageIndex,
        };

        _vkd.vkQueuePresentKHR(PresentQueue, &presentInfo);
    }
}
