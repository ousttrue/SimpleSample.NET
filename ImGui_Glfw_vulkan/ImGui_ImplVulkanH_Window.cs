// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using ImGuiNET;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

class ImGui_ImplVulkanH_Window : IDisposable
{
    private readonly VkInstanceApi _vi;
    private readonly VkDeviceApi _vd;
    private readonly VkInstance Instance;
    private readonly VkPhysicalDevice PhysicalDevice;
    private readonly uint QueueFamily;
    private readonly VkDevice Device;
    private readonly VkQueue Queue;

    // private readonly KhrSurface khrSurface;
    // private readonly KhrSwapchain khrSwapchain;

    // Input
    bool UseDynamicRendering = true;
    VkSurfaceKHR Surface; // Surface created and destroyed by caller.
    public VkSurfaceFormatKHR SurfaceFormat;
    VkPresentModeKHR PresentMode; // Ensure we get an error if user doesn't set this.
    VkAttachmentDescription AttachmentDesc = new()
    {
        format = VkFormat.Undefined, // Will automatically use wd.SurfaceFormat.format.
        samples = VkSampleCountFlags.Count1,
        loadOp = VkAttachmentLoadOp.Clear,
        storeOp = VkAttachmentStoreOp.Store,
        stencilLoadOp = VkAttachmentLoadOp.DontCare,
        stencilStoreOp = VkAttachmentStoreOp.DontCare,
        initialLayout = VkImageLayout.Undefined,
        finalLayout = VkImageLayout.PresentSrcKHR,
    }; // RenderPass creation: main attachment description.

    // Internal
    bool g_SwapChainRebuild = false;

    public int Width; // Generally same as passed to ImGui_ImplVulkanH_CreateOrResizeWindow()
    public int Height;
    VkSwapchainKHR Swapchain;

    // public RenderPass RenderPass;

    // Pipeline Pipeline; // The window pipeline may uses a different VkRenderPass than the one passed in ImGui_ImplVulkan_InitInfo
    public uint FrameIndex; // Current frame being rendered to (0 <= FrameIndex < FrameInFlightCount)
    public uint ImageCount; // Number of simultaneous in-flight frames (returned by vkGetSwapchainImagesKHR, usually derived from min_image_count)
    uint SemaphoreCount; // Number of simultaneous in-flight frames + 1, to be able to use it in vkAcquireNextImageKHR
    uint SemaphoreIndex; // Current set of swapchain wait semaphores we're using (needs to be distinct from per frame data)

    class ImGui_ImplVulkanH_Frame
    {
        public VkCommandPool CommandPool;
        public VkCommandBuffer CommandBuffer;
        public VkFence Fence;
        public VkImage Backbuffer;
        public VkImageView BackbufferView;
        // public Framebuffer Framebuffer;
    };

    List<ImGui_ImplVulkanH_Frame> Frames = [];

    class ImGui_ImplVulkanH_FrameSemaphores
    {
        public VkSemaphore ImageAcquiredSemaphore;
        public VkSemaphore RenderCompleteSemaphore;
    };

    List<ImGui_ImplVulkanH_FrameSemaphores> FrameSemaphores = [];

    private unsafe VkSurfaceFormatKHR ImGui_ImplVulkanH_SelectSurfaceFormat(
        VkPhysicalDevice physical_device,
        VkSurfaceKHR surface,
        ReadOnlySpan<VkFormat> request_formats,
        VkColorSpaceKHR request_color_space
    )
    {
        // IM_ASSERT(g_FunctionsLoaded && "Need to call ImGui_ImplVulkan_LoadFunctions() if IMGUI_IMPL_VULKAN_NO_PROTOTYPES or VK_NO_PROTOTYPES are set!");
        // IM_ASSERT(request_formats != nullptr);
        // IM_ASSERT(request_formats_count > 0);

        // Per Spec Format and View Format are expected to be the same unless VK_IMAGE_CREATE_MUTABLE_BIT was set at image creation
        // Assuming that the default behavior is without setting this bit, there is no need for separate Swapchain image and image view format
        // Additionally several new color spaces were introduced with Vulkan Spec v1.0.40,
        // hence we must make sure that a format with the mostly available color space, VK_COLOR_SPACE_SRGB_NONLINEAR_KHR, is found and used.
        uint avail_count;
        _vi.vkGetPhysicalDeviceSurfaceFormatsKHR(physical_device, surface, &avail_count, null);
        Span<VkSurfaceFormatKHR> avail_format = stackalloc VkSurfaceFormatKHR[(int)avail_count];
        _vi.vkGetPhysicalDeviceSurfaceFormatsKHR(physical_device, surface, avail_format);

        // First check if only one format, VK_FORMAT_UNDEFINED, is available, which would imply that any format is available
        if (avail_count == 1)
        {
            if (avail_format[0].format == VkFormat.Undefined)
            {
                return new VkSurfaceFormatKHR
                {
                    format = request_formats[0],
                    colorSpace = request_color_space,
                };
            }
            else
            {
                // No point in searching another format
                return avail_format[0];
            }
        }
        else
        {
            // Request several formats, the first found will be used
            for (int request_i = 0; request_i < request_formats.Length; request_i++)
                foreach (var avail in avail_format)
                    if (
                        avail.format == request_formats[request_i]
                        && avail.colorSpace == request_color_space
                    )
                        return avail;

            // If none of the requested image formats could be found, use the first available
            return avail_format[0];
        }
    }

    private unsafe VkPresentModeKHR ImGui_ImplVulkanH_SelectPresentMode(
        VkPhysicalDevice physical_device,
        VkSurfaceKHR surface,
        ReadOnlySpan<VkPresentModeKHR> request_modes
    )
    {
        // IM_ASSERT(g_FunctionsLoaded && "Need to call ImGui_ImplVulkan_LoadFunctions() if IMGUI_IMPL_VULKAN_NO_PROTOTYPES or VK_NO_PROTOTYPES are set!");
        // IM_ASSERT(request_modes != nullptr);
        // IM_ASSERT(request_modes_count > 0);

        // Request a certain mode and confirm that it is available. If not use VK_PRESENT_MODE_FIFO_KHR which is mandatory
        uint avail_count = 0;
        _vi.vkGetPhysicalDeviceSurfacePresentModesKHR(physical_device, surface, &avail_count, null);
        Span<VkPresentModeKHR> avail_modes = stackalloc VkPresentModeKHR[(int)avail_count];
        _vi.vkGetPhysicalDeviceSurfacePresentModesKHR(physical_device, surface, avail_modes);
        //for (uint avail_i = 0; avail_i < avail_count; avail_i++)
        //    printf("[vulkan] avail_modes[%d] = %d\n", avail_i, avail_modes[avail_i]);

        foreach (var request in request_modes)
        foreach (var avail in avail_modes)
            if (request == avail)
                return request;

        return VkPresentModeKHR.Fifo; // Always available
    }

    unsafe void ImGui_ImplVulkanH_DestroyFrame(ImGui_ImplVulkanH_Frame fd)
    {
        _vd.vkDestroyFence(fd.Fence, default);
        var commandBuffer = fd.CommandBuffer;
        _vd.vkFreeCommandBuffers(fd.CommandPool, 1, &commandBuffer);
        _vd.vkDestroyCommandPool(fd.CommandPool, default);
        fd.Fence = default;
        fd.CommandBuffer = default;
        fd.CommandPool = default;

        _vd.vkDestroyImageView(fd.BackbufferView, default);
        // vk.DestroyFramebuffer(device, fd.Framebuffer, default);
    }

    unsafe void ImGui_ImplVulkanH_DestroyFrameSemaphores(ImGui_ImplVulkanH_FrameSemaphores fsd)
    {
        _vd.vkDestroySemaphore(fsd.ImageAcquiredSemaphore, default);
        _vd.vkDestroySemaphore(fsd.RenderCompleteSemaphore, default);
        fsd.ImageAcquiredSemaphore = fsd.RenderCompleteSemaphore = default;
    }

    uint ImGui_ImplVulkanH_GetMinImageCountFromPresentMode(VkPresentModeKHR present_mode)
    {
        if (present_mode == VkPresentModeKHR.Mailbox)
            return 3;
        if (present_mode == VkPresentModeKHR.Fifo || present_mode == VkPresentModeKHR.FifoRelaxed)
            return 2;
        if (present_mode == VkPresentModeKHR.Immediate)
            return 1;

        throw new Exception();
        // IM_ASSERT(0);
        // return 1;
    }

    // Also destroy old swap chain and in-flight frames data, if any.
    unsafe void ImGui_ImplVulkanH_CreateWindowSwapChain(
        VkPhysicalDevice physical_device,
        VkDevice device,
        int w,
        int h,
        uint min_image_count,
        VkImageUsageFlags image_usage
    )
    {
        var old_swapchain = Swapchain;
        Swapchain = default;
        _vd.vkDeviceWaitIdle().ThrowIfError();

        // We don't use ImGui_ImplVulkanH_DestroyWindow() because we want to preserve the old swapchain to create the new one.
        // Destroy old Framebuffer
        for (int i = 0; i < ImageCount; i++)
            ImGui_ImplVulkanH_DestroyFrame(Frames[i]);
        for (int i = 0; i < SemaphoreCount; i++)
            ImGui_ImplVulkanH_DestroyFrameSemaphores(FrameSemaphores[i]);
        Frames.Clear();
        FrameSemaphores.Clear();
        ImageCount = 0;
        // if (RenderPass is RenderPass renderPass)
        //     vk.DestroyRenderPass(device, renderPass, default);

        // If min image count was not specified, request different count of images dependent on selected present mode
        if (min_image_count == 0)
            min_image_count = ImGui_ImplVulkanH_GetMinImageCountFromPresentMode(PresentMode);

        // Create Swapchain
        {
            _vi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physical_device, Surface, out var cap)
                .ThrowIfError();

            var info = new VkSwapchainCreateInfoKHR
            {
                sType = VK_STRUCTURE_TYPE_SWAPCHAIN_CREATE_INFO_KHR,
                surface = Surface,
                minImageCount = min_image_count,
                imageFormat = SurfaceFormat.format,
                imageColorSpace = SurfaceFormat.colorSpace,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | image_usage,
                imageSharingMode = VkSharingMode.Exclusive, // Assume that graphics family == present family
                preTransform = cap.supportedTransforms.HasFlag(VkSurfaceTransformFlagsKHR.Identity)
                    ? VkSurfaceTransformFlagsKHR.Identity
                    : cap.currentTransform,
            };
            if (cap.supportedCompositeAlpha.HasFlag(VkCompositeAlphaFlagsKHR.Opaque))
                info.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
            else if (cap.supportedCompositeAlpha.HasFlag(VkCompositeAlphaFlagsKHR.Inherit))
                info.compositeAlpha = VkCompositeAlphaFlagsKHR.Inherit;
            else
                throw new Exception("No supported composite alpha mode found!");
            info.presentMode = PresentMode;
            info.clipped = true;
            info.oldSwapchain = old_swapchain;
            if (info.minImageCount < cap.minImageCount)
                info.minImageCount = cap.minImageCount;
            else if (cap.maxImageCount != 0 && info.minImageCount > cap.maxImageCount)
                info.minImageCount = cap.maxImageCount;
            if (cap.currentExtent.width == 0xffffffff)
            {
                Width = w;
                Height = h;
            }
            else
            {
                Width = (int)cap.currentExtent.width;
                Height = (int)cap.currentExtent.height;
            }
            info.imageExtent.width = (uint)Width;
            info.imageExtent.height = (uint)Height;
            _vd.vkCreateSwapchainKHR(&info, default, out Swapchain).ThrowIfError();
            uint imageCount;
            _vd.vkGetSwapchainImagesKHR(Swapchain, &imageCount, null).ThrowIfError();
            ImageCount = imageCount;
            Span<VkImage> backbuffers = stackalloc VkImage[16];
            //     IM_ASSERT(wd.ImageCount >= min_image_count);
            //     IM_ASSERT(wd.ImageCount < IM_COUNTOF(backbuffers));
            _vd.vkGetSwapchainImagesKHR(Swapchain, backbuffers).ThrowIfError();

            SemaphoreCount = ImageCount + 1;
            // Frames.resize(wd.ImageCount);
            // FrameSemaphores.resize(wd.SemaphoreCount);
            //     memset(wd.Frames.Data, 0, wd.Frames.size_in_bytes());
            //     memset(wd.FrameSemaphores.Data, 0, wd.FrameSemaphores.size_in_bytes());
            for (int i = 0; i < ImageCount; i++)
            {
                Frames.Add(new() { Backbuffer = backbuffers[i] });
            }
            for (int i = 0; i < SemaphoreCount; ++i)
            {
                FrameSemaphores.Add(new());
            }
        }
        if (old_swapchain is VkSwapchainKHR _old_swapchain)
            _vd.vkDestroySwapchainKHR(_old_swapchain, default);

        // Create the Render Pass
        // if (UseDynamicRendering == false)
        // {
        //     var attachment = AttachmentDesc;
        //     if (attachment.Format == Format.Undefined)
        //         attachment.Format = SurfaceFormat.Format;
        //     var color_attachment = new AttachmentReference
        //     {
        //         Attachment = 0,
        //         Layout = ImageLayout.ColorAttachmentOptimal,
        //     };
        //     var subpass = new SubpassDescription
        //     {
        //         PipelineBindPoint = PipelineBindPoint.Graphics,
        //         ColorAttachmentCount = 1,
        //         PColorAttachments = &color_attachment,
        //     };
        //     var dependency = new SubpassDependency
        //     {
        //         SrcSubpass = Vk.SubpassExternal,
        //         DstSubpass = 0,
        //         SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
        //         DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
        //         SrcAccessMask = 0,
        //         DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
        //     };
        //     var info = new RenderPassCreateInfo
        //     {
        //         SType = VK_STRUCTURE_TYPE_RenderPassCreateInfo,
        //         AttachmentCount = 1,
        //         PAttachments = &attachment,
        //         SubpassCount = 1,
        //         PSubpasses = &subpass,
        //         DependencyCount = 1,
        //         PDependencies = &dependency,
        //     };
        //     vk.CreateRenderPass(device, &info, default, out RenderPass).ThrowIfError();

        //     // We do not create a pipeline by default as this is also used by examples' main.cpp,
        //     // but secondary viewport in multi-viewport mode may want to create one with:
        //     //ImGui_ImplVulkan_CreatePipeline(device, default, VK_NULL_HANDLE, wd.RenderPass, VK_SAMPLE_COUNT_1_BIT, &wd.Pipeline, v.Subpass);
        // }

        // Create The Image Views
        {
            var info = new VkImageViewCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_IMAGE_VIEW_CREATE_INFO,
                viewType = VkImageViewType.Image2D,
                format = SurfaceFormat.format,
                components = new VkComponentMapping
                {
                    r = VkComponentSwizzle.R,
                    g = VkComponentSwizzle.G,
                    b = VkComponentSwizzle.B,
                    a = VkComponentSwizzle.A,
                },
                subresourceRange = new VkImageSubresourceRange
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 1,
                },
            };
            for (int i = 0; i < ImageCount; i++)
            {
                var fd = Frames[i];
                info.image = fd.Backbuffer;
                _vd.vkCreateImageView(&info, default, out fd.BackbufferView).ThrowIfError();
            }
        }

        // Create Framebuffer
        // if (UseDynamicRendering == false)
        // {
        //     var attachment = stackalloc ImageView[1];
        //     var info = new FramebufferCreateInfo
        //     {
        //         SType = VK_STRUCTURE_TYPE_FramebufferCreateInfo,
        //         RenderPass = RenderPass,
        //         AttachmentCount = 1,
        //         PAttachments = attachment,
        //         Width = (uint)Width,
        //         Height = (uint)Height,
        //         Layers = 1,
        //     };
        //     for (int i = 0; i < ImageCount; i++)
        //     {
        //         var fd = Frames[i];
        //         attachment[0] = fd.BackbufferView;
        //         vk.CreateFramebuffer(device, &info, default, out fd.Framebuffer).ThrowIfError();
        //     }
        // }
    }

    private unsafe void ImGui_ImplVulkanH_CreateWindowCommandBuffers(uint queue_family)
    {
        // IM_ASSERT(physical_device != VK_NULL_HANDLE && device != VK_NULL_HANDLE);
        // IM_UNUSED(physical_device);

        // Create Command Buffers
        for (int i = 0; i < ImageCount; i++)
        {
            var fd = Frames[i];
            {
                var info = new VkCommandPoolCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
                    flags = 0,
                    queueFamilyIndex = queue_family,
                };
                _vd.vkCreateCommandPool(&info, default, out fd.CommandPool).ThrowIfError();
            }
            {
                var info = new VkCommandBufferAllocateInfo
                {
                    sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
                    commandPool = fd.CommandPool,
                    level = VkCommandBufferLevel.Primary,
                    commandBufferCount = 1,
                };
                VkCommandBuffer commandBuffer;
                _vd.vkAllocateCommandBuffers(&info, &commandBuffer).ThrowIfError();
                fd.CommandBuffer = commandBuffer;
            }
            {
                var info = new VkFenceCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO,
                    flags = VkFenceCreateFlags.Signaled,
                };
                _vd.vkCreateFence(&info, default, out fd.Fence).ThrowIfError();
            }
        }

        for (int i = 0; i < SemaphoreCount; i++)
        {
            var fsd = FrameSemaphores[i];
            {
                var info = new VkSemaphoreCreateInfo
                {
                    sType = VK_STRUCTURE_TYPE_SEMAPHORE_CREATE_INFO,
                };
                _vd.vkCreateSemaphore(&info, default, out fsd.ImageAcquiredSemaphore)
                    .ThrowIfError();
                _vd.vkCreateSemaphore(&info, default, out fsd.RenderCompleteSemaphore)
                    .ThrowIfError();
            }
        }
    }

    // Create or resize window
    // - 2025/09/26: v1.92.4 added a trailing 'VkImageUsageFlags image_usage' parameter which is usually VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT.
    unsafe void ImGui_ImplVulkanH_CreateOrResizeWindow(
        VkInstance instance,
        VkPhysicalDevice physical_device,
        VkDevice device,
        uint queue_family,
        int width,
        int height,
        uint min_image_count,
        VkImageUsageFlags image_usage
    )
    {
        // IM_ASSERT(g_FunctionsLoaded && "Need to call ImGui_ImplVulkan_LoadFunctions() if IMGUI_IMPL_VULKAN_NO_PROTOTYPES or VK_NO_PROTOTYPES are set!");
        // IM_ASSERT(wd.Surface != VK_NULL_HANDLE);
        // IM_UNUSED(instance);

        ImGui_ImplVulkanH_CreateWindowSwapChain(
            physical_device,
            device,
            width,
            height,
            min_image_count,
            image_usage
        );
        ImGui_ImplVulkanH_CreateWindowCommandBuffers(queue_family);

        // FIXME: to submit the command buffer, we need a queue. In the examples folder, the ImGui_ImplVulkanH_CreateOrResizeWindow function is called
        // before the ImGui_ImplVulkan_Init function, so we don't have access to the queue yet. Here we have the queue_family that we can use to grab
        // a queue from the device and submit the command buffer. It would be better to have access to the queue as suggested in the FIXME below.
        VkCommandPool command_pool;
        var pool_info = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            queueFamilyIndex = queue_family,
        };
        _vd.vkCreateCommandPool(&pool_info, default, &command_pool).ThrowIfError();

        var fence_info = new VkFenceCreateInfo { sType = VK_STRUCTURE_TYPE_FENCE_CREATE_INFO };
        VkFence fence;
        _vd.vkCreateFence(&fence_info, default, &fence).ThrowIfError();

        var alloc_info = new VkCommandBufferAllocateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_ALLOCATE_INFO,
            commandPool = command_pool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1,
        };
        VkCommandBuffer command_buffer;
        _vd.vkAllocateCommandBuffers(&alloc_info, &command_buffer).ThrowIfError();

        var begin_info = new VkCommandBufferBeginInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
            flags = VkCommandBufferUsageFlags.OneTimeSubmit,
        };
        _vd.vkBeginCommandBuffer(command_buffer, &begin_info).ThrowIfError();

        // Transition the images to the correct layout for rendering
        for (int i = 0; i < ImageCount; i++)
        {
            var barrier = new VkImageMemoryBarrier
            {
                sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER,
                image = Frames[i].Backbuffer,
                oldLayout = VkImageLayout.Undefined,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
            };
            barrier.subresourceRange.aspectMask = VkImageAspectFlags.Color;
            barrier.subresourceRange.levelCount = 1;
            barrier.subresourceRange.layerCount = 1;
            _vd.vkCmdPipelineBarrier(
                command_buffer,
                VkPipelineStageFlags.BottomOfPipe,
                VkPipelineStageFlags.ColorAttachmentOutput,
                0,
                0,
                null,
                0,
                null,
                1,
                &barrier
            );
        }

        _vd.vkEndCommandBuffer(command_buffer).ThrowIfError();
        var submit_info = new VkSubmitInfo
        {
            sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
            commandBufferCount = 1,
            pCommandBuffers = &command_buffer,
        };

        VkQueue queue;
        _vd.vkGetDeviceQueue(queue_family, 0, &queue);
        _vd.vkQueueSubmit(queue, 1, &submit_info, fence).ThrowIfError();
        _vd.vkWaitForFences(1, &fence, true, uint.MaxValue).ThrowIfError();
        _vd.vkResetFences(1, &fence).ThrowIfError();

        _vd.vkResetCommandPool(command_pool, 0).ThrowIfError();

        // Destroy command buffer and fence and command pool
        _vd.vkFreeCommandBuffers(command_pool, 1, &command_buffer);
        _vd.vkDestroyCommandPool(command_pool, default);
        _vd.vkDestroyFence(fence, default);
    }

    // All the ImGui_ImplVulkanH_XXX structures/functions are optional helpers used by the demo.
    // Your real engine/app may not use them.
    public ImGui_ImplVulkanH_Window(
        VkInstanceApi vi,
        VkDeviceApi vd,
        VkInstance instance,
        VkPhysicalDevice physicalDevice,
        uint queueFamily,
        VkDevice device,
        VkSurfaceKHR surface,
        int width,
        int height,
        uint minImageCount
    )
    {
        _vi = vi;
        _vd = vd;
        Instance = instance;
        PhysicalDevice = physicalDevice;
        QueueFamily = queueFamily;
        Device = device;
        _vd.vkGetDeviceQueue(QueueFamily, 0, out Queue);
        Surface = surface;

        // Check for WSI support
        _vi.vkGetPhysicalDeviceSurfaceSupportKHR(PhysicalDevice, QueueFamily, Surface, out var res)
            .CheckResult();

        // Select Surface Format
        ReadOnlySpan<VkFormat> requestSurfaceImageFormat =
        [
            VkFormat.B8G8R8A8Unorm,
            VkFormat.R8G8B8A8Unorm,
            VkFormat.B8G8R8Unorm,
            VkFormat.R8G8B8Unorm,
        ];
        var requestSurfaceColorSpace = VkColorSpaceKHR.SrgbNonLinear;
        SurfaceFormat = ImGui_ImplVulkanH_SelectSurfaceFormat(
            PhysicalDevice,
            Surface,
            requestSurfaceImageFormat,
            requestSurfaceColorSpace
        );

        // Select Present Mode
#if APP_USE_UNLIMITED_FRAME_RATE
        ReadOnlySpan<PresentModeKHR> present_modes =
        [
            PresentModeKHR.MailboxKhr,
            PresentModeKHR.ImmediateKhr,
            PresentModeKHR.FifoKhr,
        ];
#else
        ReadOnlySpan<VkPresentModeKHR> present_modes = [VkPresentModeKHR.Fifo];
#endif
        PresentMode = ImGui_ImplVulkanH_SelectPresentMode(PhysicalDevice, Surface, present_modes);
        //printf("[vulkan] Selected PresentMode = %d\n", wd.PresentMode);

        CreateOrResizeWindow(width, height, minImageCount, 0);
    }

    public void CreateOrResizeWindow(
        int width,
        int height,
        uint minImageCount,
        VkImageUsageFlags imageUsageFlags
    )
    {
        // Create SwapChain, RenderPass, Framebuffer, etc.
        //     IM_ASSERT(g_MinImageCount >= 2);
        ImGui_ImplVulkanH_CreateOrResizeWindow(
            Instance,
            PhysicalDevice,
            Device,
            QueueFamily,
            width,
            height,
            minImageCount,
            imageUsageFlags
        );
    }

    public unsafe void Dispose()
    {
        ImGui_ImplVulkanH_DestroyWindow(Instance, Device);
        _vi.vkDestroySurfaceKHR(Surface, default);
    }

    unsafe void ImGui_ImplVulkanH_DestroyWindow(VkInstance instance, VkDevice device)
    {
        // IM_UNUSED(instance);
        _vd.vkDeviceWaitIdle(); // FIXME: We could wait on the Queue if we had the queue in  (otherwise VulkanH functions can't use globals)
        //vkQueueWaitIdle(bd->Queue);

        for (int i = 0; i < ImageCount; i++)
            ImGui_ImplVulkanH_DestroyFrame(Frames[i]);
        for (int i = 0; i < SemaphoreCount; i++)
            ImGui_ImplVulkanH_DestroyFrameSemaphores(FrameSemaphores[i]);
        Frames.Clear();
        FrameSemaphores.Clear();
        _vd.vkDestroySwapchainKHR(Swapchain, default);
        Swapchain = default;
        // vk.DestroyRenderPass(device, RenderPass, default);
        // RenderPass = default;
        Width = Height = 0;
        FrameIndex = 0;
        ImageCount = 0;
        SemaphoreCount = 0;
        SemaphoreIndex = 0;
        //vkDestroySurfaceKHR(instance, Surface, default); // v1.92.6 (~2026-01-16): because Surface is user provided we don't attempt to destroy it ourself.
    }

    public void RecreateIfResized(int fb_width, int fb_height, uint g_MinImageCount)
    {
        if (
            fb_width > 0
            && fb_height > 0
            && (g_SwapChainRebuild || Width != fb_width || Height != fb_height)
        )
        {
            // Release RenderPass etc... depends on swapchain Image[]
            // ImGui_ImplVulkan_SetMinImageCount(g_MinImageCount);

            CreateOrResizeWindow(fb_width, fb_height, g_MinImageCount, 0);
            FrameIndex = 0;
            g_SwapChainRebuild = false;
        }
    }

    public unsafe (uint, VkSemaphore, VkSemaphore, VkCommandBuffer)? BeginRender(
        VkClearValue clearValue
    )
    {
        var image_acquired_semaphore = FrameSemaphores[(int)SemaphoreIndex].ImageAcquiredSemaphore;
        var render_complete_semaphore = FrameSemaphores[
            (int)SemaphoreIndex
        ].RenderCompleteSemaphore;
        uint frameIndex;
        var err = _vd.vkAcquireNextImageKHR(
            Swapchain,
            uint.MaxValue,
            image_acquired_semaphore,
            default,
            &frameIndex
        );
        FrameIndex = frameIndex;
        if (err == VK_ERROR_OUT_OF_DATE_KHR || err == VK_SUBOPTIMAL_KHR)
            g_SwapChainRebuild = true;

        if (err == VkResult.ErrorOutOfDateKHR)
            return default;

        if (err != VkResult.SuboptimalKHR)
            err.ThrowIfError();

        var fd = Frames[(int)FrameIndex];
        {
            var fence = fd.Fence;
            // wait indefinitely instead of periodically checking
            _vd.vkWaitForFences(1, &fence, true, uint.MaxValue).ThrowIfError();
            _vd.vkResetFences(1, &fence).ThrowIfError();
        }
        {
            _vd.vkResetCommandPool(fd.CommandPool, 0).ThrowIfError();
            var info = new VkCommandBufferBeginInfo
            {
                sType = VK_STRUCTURE_TYPE_COMMAND_BUFFER_BEGIN_INFO,
                flags = VkCommandBufferUsageFlags.OneTimeSubmit,
            };
            _vd.vkBeginCommandBuffer(fd.CommandBuffer, &info).ThrowIfError();
        }
        if (UseDynamicRendering)
        {
            var b = new VkImageMemoryBarrier2
            {
                sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
                srcStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
                srcAccessMask = 0,
                dstStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
                dstAccessMask = (VkAccessFlags2)(
                    VK_ACCESS_COLOR_ATTACHMENT_READ_BIT | VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT
                ),
                oldLayout = VK_IMAGE_LAYOUT_UNDEFINED,
                newLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
                image = fd.Backbuffer,
                subresourceRange = new()
                {
                    aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
                    levelCount = 1,
                    layerCount = 1,
                },
            };
            var barrierDependencyInfo = new VkDependencyInfo
            {
                sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO,
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &b,
            };
            _vd.vkCmdPipelineBarrier2(fd.CommandBuffer, &barrierDependencyInfo);

            var color_attachment_info = new VkRenderingAttachmentInfo
            {
                sType = VK_STRUCTURE_TYPE_RENDERING_ATTACHMENT_INFO,
                imageView = fd.BackbufferView,
                imageLayout = VkImageLayout.ColorAttachmentOptimal,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValue,
            };
            // var depth_attachment_info = new RenderingAttachmentInfo()
            // {
            //     SType = VK_STRUCTURE_TYPE_RenderingAttachmentInfo,
            //     ImageView = DepthImageView,
            //     ImageLayout = ImageLayout.DepthAttachmentOptimal,
            //     LoadOp = depthLoadOp,
            //     StoreOp = depthStoreOp,
            //     ClearValue = new ClearValue { DepthStencil = clearDepthStencil },
            // };
            var render_info = new VkRenderingInfo
            {
                sType = VK_STRUCTURE_TYPE_RENDERING_INFO,
                renderArea = new()
                {
                    extent = new VkExtent2D { width = (uint)Width, height = (uint)Height },
                },
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &color_attachment_info,
                // PDepthAttachment = &depth_attachment_info,
                // PStencilAttachment = &depth_attachment_info,
            };

            _vd.vkCmdBeginRendering(fd.CommandBuffer, &render_info);
        }
        else
        {
            throw new NotImplementedException();
            // var info = new RenderPassBeginInfo
            // {
            //     SType = VK_STRUCTURE_TYPE_RenderPassBeginInfo,
            //     RenderPass = RenderPass,
            //     Framebuffer = fd.Framebuffer,
            //     RenderArea = new Rect2D
            //     {
            //         Extent = new Extent2D { Width = (uint)Width, Height = (uint)Height },
            //     },
            //     ClearValueCount = 1,
            //     PClearValues = &clearValue,
            // };
            // vk.CmdBeginRenderPass(fd.CommandBuffer, &info, SubpassContents.Inline);
        }
        return (frameIndex, image_acquired_semaphore, render_complete_semaphore, fd.CommandBuffer);
    }

    public unsafe void EndRender(
        VkSemaphore image_acquired_semaphore,
        VkSemaphore render_complete_semaphore
    )
    {
        var fd = Frames[(int)FrameIndex];
        //     // Record dear imgui primitives into command buffer
        //     ImGui_ImplVulkan_RenderDrawData(draw_data, fd.CommandBuffer);

        // Submit command buffer
        if (UseDynamicRendering)
        {
            _vd.vkCmdEndRendering(fd.CommandBuffer);

            var barrierPresent = new VkImageMemoryBarrier2
            {
                sType = VK_STRUCTURE_TYPE_IMAGE_MEMORY_BARRIER_2,
                srcStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
                srcAccessMask = (VkAccessFlags2)VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT,
                dstStageMask = VK_PIPELINE_STAGE_2_COLOR_ATTACHMENT_OUTPUT_BIT,
                dstAccessMask = 0,
                oldLayout = VK_IMAGE_LAYOUT_ATTACHMENT_OPTIMAL,
                newLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR,
                image = fd.Backbuffer,
                subresourceRange = new()
                {
                    aspectMask = VK_IMAGE_ASPECT_COLOR_BIT,
                    levelCount = 1,
                    layerCount = 1,
                },
            };
            var barrierPresentDependencyInfo = new VkDependencyInfo
            {
                sType = VK_STRUCTURE_TYPE_DEPENDENCY_INFO,
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &barrierPresent,
            };
            _vd.vkCmdPipelineBarrier2(fd.CommandBuffer, &barrierPresentDependencyInfo);
        }
        else
        {
            throw new NotImplementedException();
            // _vd.vkCmdEndRenderPass(fd.CommandBuffer);
        }
        {
            var commandBuffer = fd.CommandBuffer;
            var wait_stage = VkPipelineStageFlags.ColorAttachmentOutput;
            var info = new VkSubmitInfo
            {
                sType = VK_STRUCTURE_TYPE_SUBMIT_INFO,
                waitSemaphoreCount = 1,
                pWaitSemaphores = &image_acquired_semaphore,
                pWaitDstStageMask = &wait_stage,
                commandBufferCount = 1,
                pCommandBuffers = &commandBuffer,
                signalSemaphoreCount = 1,
                pSignalSemaphores = &render_complete_semaphore,
            };
            _vd.vkEndCommandBuffer(fd.CommandBuffer).ThrowIfError();
            _vd.vkQueueSubmit(Queue, 1, &info, fd.Fence).ThrowIfError();
        }

        if (!g_SwapChainRebuild)
        {
            // var render_complete_semaphore = FrameSemaphores[
            //     (int)SemaphoreIndex
            // ].RenderCompleteSemaphore;
            var swapchain = Swapchain;
            var frameIndex = FrameIndex;
            var info = new VkPresentInfoKHR
            {
                sType = VK_STRUCTURE_TYPE_PRESENT_INFO_KHR,
                waitSemaphoreCount = 1,
                pWaitSemaphores = &render_complete_semaphore,
                swapchainCount = 1,
                pSwapchains = &swapchain,
                pImageIndices = &frameIndex,
            };
            var err = _vd.vkQueuePresentKHR(Queue, &info);
            if (err == VkResult.ErrorOutOfDateKHR || err == VkResult.SuboptimalKHR)
                g_SwapChainRebuild = true;
            if (err == VkResult.ErrorOutOfDateKHR)
                return;
            if (err != VkResult.SuboptimalKHR)
                err.ThrowIfError();
            SemaphoreIndex = (SemaphoreIndex + 1) % SemaphoreCount; // Now we can use the next set of semaphores
        }
    }
}
