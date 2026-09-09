// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

class RenderTarget : IDisposable
{
    private readonly VkDeviceApi _vkd;
    private readonly VkFormat _imageFormat;
    public readonly VkExtent2D Extent;
    public readonly VkRenderPass RenderPass;

    private readonly VkImageView[] _imageViews;
    private readonly VkFramebuffer[] _framebuffers;

    private VkCommandPool _commandPool;
    private VkCommandBuffer _commandBuffer;
    private VkSemaphore _renderFinishedSemaphore;
    private readonly VkQueue _graphicsQueue;

    public unsafe RenderTarget(
        VkDeviceApi vkd,
        uint graphicsFamily,
        VkFormat format,
        VkExtent2D extent,
        VkImage[] images
    )
    {
        _vkd = vkd;
        _imageFormat = format;
        Extent = extent;
        vkd.vkGetDeviceQueue(graphicsFamily, 0, out _graphicsQueue);

        {
            _imageViews = new VkImageView[images.Length];
            for (int i = 0; i < images.Length; ++i)
            {
                var createInfo = new VkImageViewCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                    image = images[i],
                    viewType = VkImageViewType.Image2D,
                    format = _imageFormat,
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
                format = _imageFormat,
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

        if (vkd.vkCreateSemaphore(&semaphoreInfo, null, out _renderFinishedSemaphore) != VK_SUCCESS)
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = graphicsFamily,
        };

        if (_vkd.vkCreateCommandPool(&poolInfo, null, out _commandPool) != VK_SUCCESS)
        {
            throw new Exception("failed to create command pool!");
        }

        var allocInfo = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1,
        };

        VkCommandBuffer _commandBuffer;
        if (_vkd.vkAllocateCommandBuffers(&allocInfo, &_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to allocate command buffers!");
        }
        this._commandBuffer = _commandBuffer;
    }

    public unsafe void Dispose()
    {
        _vkd.vkDestroySemaphore(_renderFinishedSemaphore, null);
        _vkd.vkDestroyCommandPool(_commandPool, null);

        foreach (var framebuffer in _framebuffers)
        {
            _vkd.vkDestroyFramebuffer(framebuffer, null);
        }
        foreach (var imageView in _imageViews)
        {
            _vkd.vkDestroyImageView(imageView, null);
        }
        _vkd.vkDestroyRenderPass(RenderPass, null);
    }

    public unsafe (VkCommandBuffer, VkSemaphore) BeginRenderPass(uint imageIndex)
    {
        _vkd.vkResetCommandBuffer(
            _commandBuffer, /*VkCommandBufferResetFlagBits*/
            0
        );

        var beginInfo = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
        };
        if (_vkd.vkBeginCommandBuffer(_commandBuffer, &beginInfo) != VK_SUCCESS)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        var renderPassInfo = new VkRenderPassBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_RENDER_PASS_BEGIN_INFO,
            renderPass = RenderPass,
            framebuffer = _framebuffers[imageIndex],
        };
        renderPassInfo.renderArea.offset = new(0, 0);
        renderPassInfo.renderArea.extent = Extent;

        var clearColor = new VkClearValue { };
        clearColor.color.float32[0] = 0.0f;
        clearColor.color.float32[1] = 0.0f;
        clearColor.color.float32[2] = 0.0f;
        clearColor.color.float32[2] = 1.0f;
        renderPassInfo.clearValueCount = 1;
        renderPassInfo.pClearValues = &clearColor;

        _vkd.vkCmdBeginRenderPass(_commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

        return (_commandBuffer, _renderFinishedSemaphore);
    }

    public unsafe void EndRenderPass(VkSemaphore imageAvailableSemaphore, VkFence inFlightFence)
    {
        _vkd.vkCmdEndRenderPass(_commandBuffer);
        if (_vkd.vkEndCommandBuffer(_commandBuffer) != VK_SUCCESS)
        {
            throw new Exception("failed to record command buffer!");
        }

        var waitSemaphores = stackalloc VkSemaphore[] { imageAvailableSemaphore };
        var waitStages = stackalloc VkPipelineStageFlags[]
        {
            VkPipelineStageFlags.ColorAttachmentOutput,
        };
        var signalSemaphores = stackalloc VkSemaphore[] { _renderFinishedSemaphore };
        var cmd = _commandBuffer;
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
        if (_vkd.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS)
        {
            throw new Exception("failed to submit draw command buffer!");
        }
    }
}
