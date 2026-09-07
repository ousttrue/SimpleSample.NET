// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using ImGuiNET;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

class ImGui_ImplVulkanH_Window : IDisposable
{
    private readonly Vk vk;
    private readonly Instance Instance;
    private readonly PhysicalDevice PhysicalDevice;
    private readonly uint QueueFamily;
    private readonly Device Device;
    private readonly Queue Queue;
    private readonly KhrSurface khrSurface;
    private readonly KhrSwapchain khrSwapchain;

    // Input
    bool UseDynamicRendering = true;
    SurfaceKHR Surface; // Surface created and destroyed by caller.
    public SurfaceFormatKHR SurfaceFormat;
    PresentModeKHR PresentMode; // Ensure we get an error if user doesn't set this.
    AttachmentDescription AttachmentDesc = new AttachmentDescription
    {
        Format = Format.Undefined, // Will automatically use wd.SurfaceFormat.format.
        Samples = SampleCountFlags.Count1Bit,
        LoadOp = AttachmentLoadOp.Clear,
        StoreOp = AttachmentStoreOp.Store,
        StencilLoadOp = AttachmentLoadOp.DontCare,
        StencilStoreOp = AttachmentStoreOp.DontCare,
        InitialLayout = ImageLayout.Undefined,
        FinalLayout = ImageLayout.PresentSrcKhr,
    }; // RenderPass creation: main attachment description.

    // Internal
    bool g_SwapChainRebuild = false;

    public int Width; // Generally same as passed to ImGui_ImplVulkanH_CreateOrResizeWindow()
    public int Height;
    SwapchainKHR Swapchain;

    // public RenderPass RenderPass;

    // Pipeline Pipeline; // The window pipeline may uses a different VkRenderPass than the one passed in ImGui_ImplVulkan_InitInfo
    public uint FrameIndex; // Current frame being rendered to (0 <= FrameIndex < FrameInFlightCount)
    public uint ImageCount; // Number of simultaneous in-flight frames (returned by vkGetSwapchainImagesKHR, usually derived from min_image_count)
    uint SemaphoreCount; // Number of simultaneous in-flight frames + 1, to be able to use it in vkAcquireNextImageKHR
    uint SemaphoreIndex; // Current set of swapchain wait semaphores we're using (needs to be distinct from per frame data)

    class ImGui_ImplVulkanH_Frame
    {
        public CommandPool CommandPool;
        public CommandBuffer CommandBuffer;
        public Fence Fence;
        public Image Backbuffer;
        public ImageView BackbufferView;
        // public Framebuffer Framebuffer;
    };

    List<ImGui_ImplVulkanH_Frame> Frames = [];

    class ImGui_ImplVulkanH_FrameSemaphores
    {
        public Semaphore ImageAcquiredSemaphore;
        public Semaphore RenderCompleteSemaphore;
    };

    List<ImGui_ImplVulkanH_FrameSemaphores> FrameSemaphores = [];

    private unsafe SurfaceFormatKHR ImGui_ImplVulkanH_SelectSurfaceFormat(
        PhysicalDevice physical_device,
        SurfaceKHR surface,
        ReadOnlySpan<Format> request_formats,
        ColorSpaceKHR request_color_space
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
        khrSurface.GetPhysicalDeviceSurfaceFormats(physical_device, surface, &avail_count, null);
        var avail_format = stackalloc SurfaceFormatKHR[(int)avail_count];
        khrSurface.GetPhysicalDeviceSurfaceFormats(
            physical_device,
            surface,
            &avail_count,
            avail_format
        );

        // First check if only one format, VK_FORMAT_UNDEFINED, is available, which would imply that any format is available
        if (avail_count == 1)
        {
            if (avail_format[0].Format == Format.Undefined)
            {
                return new SurfaceFormatKHR
                {
                    Format = request_formats[0],
                    ColorSpace = request_color_space,
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
            for (uint avail_i = 0; avail_i < avail_count; avail_i++)
                if (
                    avail_format[avail_i].Format == request_formats[request_i]
                    && avail_format[avail_i].ColorSpace == request_color_space
                )
                    return avail_format[avail_i];

            // If none of the requested image formats could be found, use the first available
            return avail_format[0];
        }
    }

    private unsafe PresentModeKHR ImGui_ImplVulkanH_SelectPresentMode(
        PhysicalDevice physical_device,
        SurfaceKHR surface,
        ReadOnlySpan<PresentModeKHR> request_modes
    )
    {
        // IM_ASSERT(g_FunctionsLoaded && "Need to call ImGui_ImplVulkan_LoadFunctions() if IMGUI_IMPL_VULKAN_NO_PROTOTYPES or VK_NO_PROTOTYPES are set!");
        // IM_ASSERT(request_modes != nullptr);
        // IM_ASSERT(request_modes_count > 0);

        // Request a certain mode and confirm that it is available. If not use VK_PRESENT_MODE_FIFO_KHR which is mandatory
        uint avail_count = 0;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(
            physical_device,
            surface,
            &avail_count,
            null
        );
        var avail_modes = stackalloc PresentModeKHR[(int)avail_count];
        khrSurface.GetPhysicalDeviceSurfacePresentModes(
            physical_device,
            surface,
            &avail_count,
            avail_modes
        );
        //for (uint avail_i = 0; avail_i < avail_count; avail_i++)
        //    printf("[vulkan] avail_modes[%d] = %d\n", avail_i, avail_modes[avail_i]);

        for (int request_i = 0; request_i < request_modes.Length; request_i++)
        for (uint avail_i = 0; avail_i < avail_count; avail_i++)
            if (request_modes[request_i] == avail_modes[avail_i])
                return request_modes[request_i];

        return PresentModeKHR.FifoKhr; // Always available
    }

    unsafe void ImGui_ImplVulkanH_DestroyFrame(Device device, ImGui_ImplVulkanH_Frame fd)
    {
        vk.DestroyFence(device, fd.Fence, default);
        var commandBuffer = fd.CommandBuffer;
        vk.FreeCommandBuffers(device, fd.CommandPool, 1, &commandBuffer);
        vk.DestroyCommandPool(device, fd.CommandPool, default);
        fd.Fence = default;
        fd.CommandBuffer = default;
        fd.CommandPool = default;

        vk.DestroyImageView(device, fd.BackbufferView, default);
        // vk.DestroyFramebuffer(device, fd.Framebuffer, default);
    }

    unsafe void ImGui_ImplVulkanH_DestroyFrameSemaphores(
        Device device,
        ImGui_ImplVulkanH_FrameSemaphores fsd
    )
    {
        vk.DestroySemaphore(device, fsd.ImageAcquiredSemaphore, default);
        vk.DestroySemaphore(device, fsd.RenderCompleteSemaphore, default);
        fsd.ImageAcquiredSemaphore = fsd.RenderCompleteSemaphore = default;
    }

    uint ImGui_ImplVulkanH_GetMinImageCountFromPresentMode(PresentModeKHR present_mode)
    {
        if (present_mode == PresentModeKHR.MailboxKhr)
            return 3;
        if (present_mode == PresentModeKHR.FifoKhr || present_mode == PresentModeKHR.FifoRelaxedKhr)
            return 2;
        if (present_mode == PresentModeKHR.ImmediateKhr)
            return 1;

        throw new Exception();
        // IM_ASSERT(0);
        // return 1;
    }

    // Also destroy old swap chain and in-flight frames data, if any.
    unsafe void ImGui_ImplVulkanH_CreateWindowSwapChain(
        PhysicalDevice physical_device,
        Device device,
        int w,
        int h,
        uint min_image_count,
        ImageUsageFlags image_usage
    )
    {
        var old_swapchain = Swapchain;
        Swapchain = default;
        vk.DeviceWaitIdle(device).ThrowIfError();

        // We don't use ImGui_ImplVulkanH_DestroyWindow() because we want to preserve the old swapchain to create the new one.
        // Destroy old Framebuffer
        for (int i = 0; i < ImageCount; i++)
            ImGui_ImplVulkanH_DestroyFrame(device, Frames[i]);
        for (int i = 0; i < SemaphoreCount; i++)
            ImGui_ImplVulkanH_DestroyFrameSemaphores(device, FrameSemaphores[i]);
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
            khrSurface
                .GetPhysicalDeviceSurfaceCapabilities(physical_device, Surface, out var cap)
                .ThrowIfError();

            var info = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = Surface,
                MinImageCount = min_image_count,
                ImageFormat = SurfaceFormat.Format,
                ImageColorSpace = SurfaceFormat.ColorSpace,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit | image_usage,
                ImageSharingMode = SharingMode.Exclusive, // Assume that graphics family == present family
                PreTransform = cap.SupportedTransforms.HasFlag(
                    SurfaceTransformFlagsKHR.IdentityBitKhr
                )
                    ? SurfaceTransformFlagsKHR.IdentityBitKhr
                    : cap.CurrentTransform,
            };
            if (
                cap.SupportedCompositeAlpha.HasFlag(
                    CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr
                )
            )
                info.CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
            else if (cap.SupportedCompositeAlpha.HasFlag(CompositeAlphaFlagsKHR.InheritBitKhr))
                info.CompositeAlpha = CompositeAlphaFlagsKHR.InheritBitKhr;
            else
                throw new Exception("No supported composite alpha mode found!");
            info.PresentMode = PresentMode;
            info.Clipped = Vk.True;
            info.OldSwapchain = old_swapchain;
            if (info.MinImageCount < cap.MinImageCount)
                info.MinImageCount = cap.MinImageCount;
            else if (cap.MaxImageCount != 0 && info.MinImageCount > cap.MaxImageCount)
                info.MinImageCount = cap.MaxImageCount;
            if (cap.CurrentExtent.Width == 0xffffffff)
            {
                Width = w;
                Height = h;
            }
            else
            {
                Width = (int)cap.CurrentExtent.Width;
                Height = (int)cap.CurrentExtent.Height;
            }
            info.ImageExtent.Width = (uint)Width;
            info.ImageExtent.Height = (uint)Height;
            khrSwapchain.CreateSwapchain(device, &info, default, out Swapchain).ThrowIfError();
            uint imageCount;
            khrSwapchain.GetSwapchainImages(device, Swapchain, &imageCount, null).ThrowIfError();
            ImageCount = imageCount;
            var backbuffers = stackalloc Image[16];
            //     IM_ASSERT(wd.ImageCount >= min_image_count);
            //     IM_ASSERT(wd.ImageCount < IM_COUNTOF(backbuffers));
            khrSwapchain
                .GetSwapchainImages(device, Swapchain, &imageCount, backbuffers)
                .ThrowIfError();

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
        if (old_swapchain is SwapchainKHR _old_swapchain)
            khrSwapchain.DestroySwapchain(device, _old_swapchain, default);

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
        //         SType = StructureType.RenderPassCreateInfo,
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
            var info = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                ViewType = ImageViewType.Type2D,
                Format = SurfaceFormat.Format,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.R,
                    G = ComponentSwizzle.G,
                    B = ComponentSwizzle.B,
                    A = ComponentSwizzle.A,
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1,
                },
            };
            for (int i = 0; i < ImageCount; i++)
            {
                var fd = Frames[i];
                info.Image = fd.Backbuffer;
                vk.CreateImageView(device, &info, default, out fd.BackbufferView).ThrowIfError();
            }
        }

        // Create Framebuffer
        // if (UseDynamicRendering == false)
        // {
        //     var attachment = stackalloc ImageView[1];
        //     var info = new FramebufferCreateInfo
        //     {
        //         SType = StructureType.FramebufferCreateInfo,
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

    private unsafe void ImGui_ImplVulkanH_CreateWindowCommandBuffers(
        PhysicalDevice physical_device,
        Device device,
        uint queue_family
    )
    {
        // IM_ASSERT(physical_device != VK_NULL_HANDLE && device != VK_NULL_HANDLE);
        // IM_UNUSED(physical_device);

        // Create Command Buffers
        for (int i = 0; i < ImageCount; i++)
        {
            var fd = Frames[i];
            {
                var info = new CommandPoolCreateInfo
                {
                    SType = StructureType.CommandPoolCreateInfo,
                    Flags = 0,
                    QueueFamilyIndex = queue_family,
                };
                vk.CreateCommandPool(device, &info, default, out fd.CommandPool).ThrowIfError();
            }
            {
                var info = new CommandBufferAllocateInfo
                {
                    SType = StructureType.CommandBufferAllocateInfo,
                    CommandPool = fd.CommandPool,
                    Level = CommandBufferLevel.Primary,
                    CommandBufferCount = 1,
                };
                vk.AllocateCommandBuffers(device, &info, out fd.CommandBuffer).ThrowIfError();
            }
            {
                var info = new FenceCreateInfo
                {
                    SType = StructureType.FenceCreateInfo,
                    Flags = FenceCreateFlags.SignaledBit,
                };
                vk.CreateFence(device, &info, default, out fd.Fence).ThrowIfError();
            }
        }

        for (int i = 0; i < SemaphoreCount; i++)
        {
            var fsd = FrameSemaphores[i];
            {
                var info = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
                vk.CreateSemaphore(device, &info, default, out fsd.ImageAcquiredSemaphore)
                    .ThrowIfError();
                vk.CreateSemaphore(device, &info, default, out fsd.RenderCompleteSemaphore)
                    .ThrowIfError();
            }
        }
    }

    // Create or resize window
    // - 2025/09/26: v1.92.4 added a trailing 'VkImageUsageFlags image_usage' parameter which is usually VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT.
    unsafe void ImGui_ImplVulkanH_CreateOrResizeWindow(
        Instance instance,
        PhysicalDevice physical_device,
        Device device,
        uint queue_family,
        int width,
        int height,
        uint min_image_count,
        ImageUsageFlags image_usage
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
        ImGui_ImplVulkanH_CreateWindowCommandBuffers(physical_device, device, queue_family);

        // FIXME: to submit the command buffer, we need a queue. In the examples folder, the ImGui_ImplVulkanH_CreateOrResizeWindow function is called
        // before the ImGui_ImplVulkan_Init function, so we don't have access to the queue yet. Here we have the queue_family that we can use to grab
        // a queue from the device and submit the command buffer. It would be better to have access to the queue as suggested in the FIXME below.
        CommandPool command_pool;
        var pool_info = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queue_family,
        };
        vk.CreateCommandPool(device, &pool_info, default, &command_pool).ThrowIfError();

        var fence_info = new FenceCreateInfo { SType = StructureType.FenceCreateInfo };
        Fence fence;
        vk.CreateFence(device, &fence_info, default, &fence).ThrowIfError();

        var alloc_info = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = command_pool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };
        CommandBuffer command_buffer;
        vk.AllocateCommandBuffers(device, &alloc_info, &command_buffer).ThrowIfError();

        var begin_info = new CommandBufferBeginInfo
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };
        vk.BeginCommandBuffer(command_buffer, &begin_info).ThrowIfError();

        // Transition the images to the correct layout for rendering
        for (int i = 0; i < ImageCount; i++)
        {
            var barrier = new ImageMemoryBarrier
            {
                SType = StructureType.ImageMemoryBarrier,
                Image = Frames[i].Backbuffer,
                OldLayout = ImageLayout.Undefined,
                NewLayout = ImageLayout.PresentSrcKhr,
                SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
                DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            };
            barrier.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
            barrier.SubresourceRange.LevelCount = 1;
            barrier.SubresourceRange.LayerCount = 1;
            vk.CmdPipelineBarrier(
                command_buffer,
                PipelineStageFlags.BottomOfPipeBit,
                PipelineStageFlags.ColorAttachmentOutputBit,
                0,
                0,
                null,
                0,
                null,
                1,
                &barrier
            );
        }

        vk.EndCommandBuffer(command_buffer).ThrowIfError();
        var submit_info = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &command_buffer,
        };

        Queue queue;
        vk.GetDeviceQueue(device, queue_family, 0, &queue);
        vk.QueueSubmit(queue, 1, &submit_info, fence).ThrowIfError();
        vk.WaitForFences(device, 1, &fence, Vk.True, uint.MaxValue).ThrowIfError();
        vk.ResetFences(device, 1, &fence).ThrowIfError();

        vk.ResetCommandPool(device, command_pool, 0).ThrowIfError();

        // Destroy command buffer and fence and command pool
        vk.FreeCommandBuffers(device, command_pool, 1, &command_buffer);
        vk.DestroyCommandPool(device, command_pool, default);
        vk.DestroyFence(device, fence, default);
    }

    // All the ImGui_ImplVulkanH_XXX structures/functions are optional helpers used by the demo.
    // Your real engine/app may not use them.
    public ImGui_ImplVulkanH_Window(
        Vk _vk,
        Instance instance,
        PhysicalDevice physicalDevice,
        uint queueFamily,
        Device device,
        SurfaceKHR surface,
        int width,
        int height,
        uint minImageCount
    )
    {
        vk = _vk;
        Instance = instance;
        PhysicalDevice = physicalDevice;
        QueueFamily = queueFamily;
        Device = device;
        Queue = vk.GetDeviceQueue(Device, QueueFamily, 0);
        Surface = surface;

        if (!vk.TryGetInstanceExtension(instance, out khrSurface))
        {
            throw new Exception("TryGetInstanceExtension<KhrSurface>");
        }

        if (!vk.TryGetDeviceExtension(instance, device, out khrSwapchain))
        {
            throw new Exception("TryGetInstanceExtension<KhrSwapchain>");
        }

        // Check for WSI support
        khrSurface.GetPhysicalDeviceSurfaceSupport(
            PhysicalDevice,
            QueueFamily,
            Surface,
            out var res
        );
        if (res != Vk.True)
        {
            throw new Exception("Error no WSI support on physical device 0");
        }

        // Select Surface Format
        ReadOnlySpan<Format> requestSurfaceImageFormat =
        [
            Format.B8G8R8A8Unorm,
            Format.R8G8B8A8Unorm,
            Format.B8G8R8Unorm,
            Format.R8G8B8Unorm,
        ];
        var requestSurfaceColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr;
        SurfaceFormat = ImGui_ImplVulkanH_SelectSurfaceFormat(
            PhysicalDevice,
            Surface,
            requestSurfaceImageFormat,
            requestSurfaceColorSpace
        );

        // Select Present Mode
        // #ifdef APP_USE_UNLIMITED_FRAME_RATE
        ReadOnlySpan<PresentModeKHR> present_modes =
        [
            PresentModeKHR.MailboxKhr,
            PresentModeKHR.ImmediateKhr,
            PresentModeKHR.FifoKhr,
        ];
        // #else
        //     VkPresentModeKHR present_modes[] = { VK_PRESENT_MODE_FIFO_KHR };
        // #endif
        PresentMode = ImGui_ImplVulkanH_SelectPresentMode(PhysicalDevice, Surface, present_modes);
        //printf("[vulkan] Selected PresentMode = %d\n", wd.PresentMode);

        CreateOrResizeWindow(width, height, minImageCount, 0);
    }

    public void CreateOrResizeWindow(
        int width,
        int height,
        uint minImageCount,
        ImageUsageFlags imageUsageFlags
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
        khrSurface.DestroySurface(Instance, Surface, default);
    }

    unsafe void ImGui_ImplVulkanH_DestroyWindow(Instance instance, Device device)
    {
        // IM_UNUSED(instance);
        vk.DeviceWaitIdle(device); // FIXME: We could wait on the Queue if we had the queue in  (otherwise VulkanH functions can't use globals)
        //vkQueueWaitIdle(bd->Queue);

        for (int i = 0; i < ImageCount; i++)
            ImGui_ImplVulkanH_DestroyFrame(device, Frames[i]);
        for (int i = 0; i < SemaphoreCount; i++)
            ImGui_ImplVulkanH_DestroyFrameSemaphores(device, FrameSemaphores[i]);
        Frames.Clear();
        FrameSemaphores.Clear();
        khrSwapchain.DestroySwapchain(device, Swapchain, default);
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

    public unsafe (uint, Semaphore, Semaphore, CommandBuffer)? BeginRender(ClearValue clearValue)
    {
        var image_acquired_semaphore = FrameSemaphores[(int)SemaphoreIndex].ImageAcquiredSemaphore;
        var render_complete_semaphore = FrameSemaphores[
            (int)SemaphoreIndex
        ].RenderCompleteSemaphore;
        uint frameIndex;
        var err = khrSwapchain.AcquireNextImage(
            Device,
            Swapchain,
            uint.MaxValue,
            image_acquired_semaphore,
            default,
            &frameIndex
        );
        FrameIndex = frameIndex;
        if (err == Result.ErrorOutOfDateKhr || err == Result.SuboptimalKhr)
            g_SwapChainRebuild = true;

        if (err == Result.ErrorOutOfDateKhr)
            return default;

        if (err != Result.SuboptimalKhr)
            err.ThrowIfError();

        var fd = Frames[(int)FrameIndex];
        {
            var fence = fd.Fence;
            // wait indefinitely instead of periodically checking
            vk.WaitForFences(Device, 1, &fence, Vk.True, uint.MaxValue).ThrowIfError();
            vk.ResetFences(Device, 1, &fence).ThrowIfError();
        }
        {
            vk.ResetCommandPool(Device, fd.CommandPool, 0).ThrowIfError();
            var info = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };
            vk.BeginCommandBuffer(fd.CommandBuffer, &info).ThrowIfError();
        }
        if (UseDynamicRendering)
        {
            var color_attachment_info = new RenderingAttachmentInfo
            {
                SType = StructureType.RenderingAttachmentInfo,
                ImageView = fd.BackbufferView,
                ImageLayout = ImageLayout.ColorAttachmentOptimal,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                ClearValue = clearValue,
            };
            // var depth_attachment_info = new RenderingAttachmentInfo()
            // {
            //     SType = StructureType.RenderingAttachmentInfo,
            //     ImageView = DepthImageView,
            //     ImageLayout = ImageLayout.DepthAttachmentOptimal,
            //     LoadOp = depthLoadOp,
            //     StoreOp = depthStoreOp,
            //     ClearValue = new ClearValue { DepthStencil = clearDepthStencil },
            // };
            var render_info = new RenderingInfo
            {
                SType = StructureType.RenderingInfo,
                RenderArea = new()
                {
                    Extent = new Extent2D { Width = (uint)Width, Height = (uint)Height },
                },
                LayerCount = 1,
                ColorAttachmentCount = 1,
                PColorAttachments = &color_attachment_info,
                // PDepthAttachment = &depth_attachment_info,
                // PStencilAttachment = &depth_attachment_info,
            };

            // TransitionImageLayout(
            //     vk,
            //     fd.CommandBuffer,
            //     fd.Backbuffer,
            //     ImageLayout.ColorAttachmentOptimal
            // );
            vk.CmdBeginRendering(fd.CommandBuffer, &render_info);
        }
        else
        {
            throw new NotImplementedException();
            // var info = new RenderPassBeginInfo
            // {
            //     SType = StructureType.RenderPassBeginInfo,
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
        Semaphore image_acquired_semaphore,
        Semaphore render_complete_semaphore
    )
    {
        var fd = Frames[(int)FrameIndex];
        //     // Record dear imgui primitives into command buffer
        //     ImGui_ImplVulkan_RenderDrawData(draw_data, fd.CommandBuffer);

        // Submit command buffer
        if (UseDynamicRendering)
        {
            vk.CmdEndRendering(fd.CommandBuffer);
            TransitionImageLayout(vk, fd.CommandBuffer, fd.Backbuffer, ImageLayout.PresentSrcKhr);
        }
        else
        {
            throw new NotImplementedException();
            vk.CmdEndRenderPass(fd.CommandBuffer);
        }
        {
            var commandBuffer = fd.CommandBuffer;
            var wait_stage = PipelineStageFlags.ColorAttachmentOutputBit;
            var info = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &image_acquired_semaphore,
                PWaitDstStageMask = &wait_stage,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = &render_complete_semaphore,
            };
            vk.EndCommandBuffer(fd.CommandBuffer).ThrowIfError();
            vk.QueueSubmit(Queue, 1, &info, fd.Fence).ThrowIfError();
        }

        if (!g_SwapChainRebuild)
        {
            // var render_complete_semaphore = FrameSemaphores[
            //     (int)SemaphoreIndex
            // ].RenderCompleteSemaphore;
            var swapchain = Swapchain;
            var frameIndex = FrameIndex;
            var info = new PresentInfoKHR
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = &render_complete_semaphore,
                SwapchainCount = 1,
                PSwapchains = &swapchain,
                PImageIndices = &frameIndex,
            };
            var err = khrSwapchain.QueuePresent(Queue, &info);
            if (err == Result.ErrorOutOfDateKhr || err == Result.SuboptimalKhr)
                g_SwapChainRebuild = true;
            if (err == Result.ErrorOutOfDateKhr)
                return;
            if (err != Result.SuboptimalKhr)
                err.ThrowIfError();
            SemaphoreIndex = (SemaphoreIndex + 1) % SemaphoreCount; // Now we can use the next set of semaphores
        }
    }

    public static unsafe void TransitionImageLayout(
        Vk vk,
        CommandBuffer commandBuffer,
        Image image,
        ImageLayout newLayout
    )
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.BottomOfPipeBit,
            PipelineStageFlags.TopOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }
}
