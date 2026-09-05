// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Collections;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using VkImage = Silk.NET.Vulkan.Image;
using VkQueue = Silk.NET.Vulkan.Queue;
using VkSemaphore = Silk.NET.Vulkan.Semaphore;

unsafe class ByteStringArrayAllocator : IDisposable, IEnumerable
{
    List<string> _list = [];
    byte** _array;

    public IEnumerator GetEnumerator()
    {
        return _list.GetEnumerator();
    }

    public void Deconstruct(out uint x, out byte** y)
    {
        Dispose();

        _array = (byte**)Marshal.AllocHGlobal(sizeof(byte*) * _list.Count);
        for (int i = 0; i < _list.Count; ++i)
        {
            _array[i] = (byte*)Marshal.StringToHGlobalAnsi(_list[i]);
        }
        x = (uint)_list.Count;
        y = _array;
    }

    public void Dispose()
    {
        if (_array != null)
        {
            for (int i = 0; i < _list.Count; ++i)
            {
                Marshal.FreeHGlobal((nint)_array[i]);
            }
            Marshal.FreeHGlobal((nint)_array);
        }
    }

    public void Add(string p)
    {
        _list.Add(p);
    }

    public void AddSpan(ReadOnlySpan<IntPtr> pp)
    {
        foreach (var p in pp)
        {
            _list.Add(Marshal.PtrToStringAnsi(p) ?? throw new Exception());
        }
    }

    public void AddSpan(byte** _pp, uint count)
    {
        var pp = (IntPtr*)_pp;
        AddSpan(new ReadOnlySpan<nint>(pp, (int)count));
    }
}

struct QueueFamilyIndices
{
    public uint? graphicsFamily;
    public uint? presentFamily;

    public bool isComplete()
    {
        return graphicsFamily is not null && presentFamily is not null;
    }

    public HashSet<uint> ToUniqueSet()
    {
        var set = new HashSet<uint>();
        if (graphicsFamily is uint g)
        {
            set.Add(g);
        }
        if (presentFamily is uint p)
        {
            set.Add(p);
        }
        return set;
    }
}

struct SwapChainSupportDetails
{
    public SurfaceCapabilitiesKHR capabilities;
    public SurfaceFormatKHR[] formats;
    public PresentModeKHR[] presentModes;
};

unsafe class HelloTriangleApplication
{
    static readonly Glfw glfw;
    static readonly Vk vk;

    static HelloTriangleApplication()
    {
        glfw = GlfwProvider.GLFW.Value ?? throw new NullReferenceException();
        vk = Vk.GetApi() ?? throw new NullReferenceException();
    }

    const uint WIDTH = 800;
    const uint HEIGHT = 600;
    const bool enableValidationLayers =
#if DEBUG
        true;
#else
        false;
#endif

    // const int MAX_FRAMES_IN_FLIGHT = 2;

    static readonly string[] validationLayers = ["VK_LAYER_KHRONOS_validation"];
    static readonly string[] deviceExtensions = [KhrSwapchain.ExtensionName];

    private WindowHandle* window;
    private Instance instance;

    private ExtDebugUtils extDebugUtils;
    private DebugUtilsMessengerEXT debugMessenger;

    private KhrSurface khrSurface;
    private SurfaceKHR surface;

    private PhysicalDevice physicalDevice;
    private Device device;

    private VkQueue graphicsQueue;
    private VkQueue presentQueue;

    private KhrSwapchain khrSwapchain;
    private SwapchainKHR swapChain;

    private VkImage[] swapChainImages;

    private Format swapChainImageFormat;
    private Extent2D swapChainExtent;

    private ImageView[] swapChainImageViews;

    private Framebuffer[] swapChainFramebuffers;

    private RenderPass renderPass;

    private PipelineLayout pipelineLayout;

    private Pipeline graphicsPipeline;

    private CommandPool commandPool;

    private CommandBuffer commandBuffer;

    private VkSemaphore imageAvailableSemaphore;
    private VkSemaphore renderFinishedSemaphore;
    private Fence inFlightFence;

    public void Run()
    {
        initWindow();
        initVulkan();
        mainLoop();
        cleanup();
    }

    void initWindow()
    {
        glfw.Init();

        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
        glfw.WindowHint(WindowHintBool.Resizable, false);

        window = glfw.CreateWindow((int)WIDTH, (int)HEIGHT, "Vulkan", null, null);
    }

    void initVulkan()
    {
        createInstance();

        // get api
        if (!vk.TryGetInstanceExtension(instance, out khrSurface))
        {
            throw new Exception("TryGetInstanceExtension");
        }
        if (!vk.TryGetInstanceExtension(instance, out extDebugUtils))
        {
            throw new Exception("TryGetInstanceExtension");
        }

        setupDebugMessenger();
        createSurface();
        pickPhysicalDevice();
        createLogicalDevice();
        if (!vk.TryGetDeviceExtension(instance, device, out khrSwapchain))
        {
            throw new Exception("TryGetDeviceExtension");
        }

        createSwapChain();
        createImageViews();
        createRenderPass();
        createGraphicsPipeline();
        createFramebuffers();
        createCommandPool();
        createCommandBuffer();
        createSyncObjects();
    }

    void mainLoop()
    {
        //         while (!glfwWindowShouldClose(window)) {
        //             glfwPollEvents();
        //             drawFrame();
        //         }

        //         vkDeviceWaitIdle(device);
    }

    void cleanup()
    {
        //         vkDestroySemaphore(device, renderFinishedSemaphore, null);
        //         vkDestroySemaphore(device, imageAvailableSemaphore, null);
        //         vkDestroyFence(device, inFlightFence, null);

        //         vkDestroyCommandPool(device, commandPool, null);

        //         for (auto framebuffer : swapChainFramebuffers) {
        //             vkDestroyFramebuffer(device, framebuffer, null);
        //         }

        //         vkDestroyPipeline(device, graphicsPipeline, null);
        //         vkDestroyPipelineLayout(device, pipelineLayout, null);
        //         vkDestroyRenderPass(device, renderPass, null);

        //         for (auto imageView : swapChainImageViews) {
        //             vkDestroyImageView(device, imageView, null);
        //         }

        //         vkDestroySwapchainKHR(device, swapChain, null);
        //         vkDestroyDevice(device, null);

        //         if (enableValidationLayers) {
        //             DestroyDebugUtilsMessengerEXT(instance, debugMessenger, null);
        //         }

        //         vkDestroySurfaceKHR(instance, surface, null);
        //         vkDestroyInstance(instance, null);

        //         glfwDestroyWindow(window);

        //         glfwTerminate();
    }

    static void StrCopy(Span<byte> dst, ReadOnlySpan<byte> src)
    {
        src.CopyTo(dst);
    }

    void createInstance()
    {
        if (enableValidationLayers && !checkValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        fixed (byte* appName = "Hello Triangle"u8)
        fixed (byte* engineName = "No Engine"u8)
        {
            var glfwExtensions = glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);

            using var extensions = new ByteStringArrayAllocator();
            extensions.AddSpan(glfwExtensions, glfwExtensionCount);
            if (enableValidationLayers)
            {
                extensions.Add(ExtDebugUtils.ExtensionName);
            }

            var appInfo = new ApplicationInfo
            {
                SType = StructureType.ApplicationInfo,
                PApplicationName = appName,
                ApplicationVersion = Vk.MakeVersion(1, 0, 0),
                PEngineName = engineName,
                EngineVersion = Vk.MakeVersion(1, 0, 0),
                ApiVersion = Vk.Version10,
            };
            var createInfo = new InstanceCreateInfo
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledLayerCount = 0,
                PNext = null,
            };
            (createInfo.EnabledExtensionCount, createInfo.PpEnabledExtensionNames) = extensions;

            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = default;
            ByteStringArrayAllocator layers = [.. validationLayers];
            if (enableValidationLayers)
            {
                (createInfo.EnabledLayerCount, createInfo.PpEnabledLayerNames) = layers;

                populateDebugMessengerCreateInfo(out debugCreateInfo);
                createInfo.PNext = &debugCreateInfo;
            }
            if (vk.CreateInstance(&createInfo, null, out instance) != Result.Success)
            {
                throw new Exception("failed to create instance!");
            }
        }
    }

    void populateDebugMessengerCreateInfo(out DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity =
                DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
                | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType =
                DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)debugCallback,
        };
    }

    void setupDebugMessenger()
    {
        if (!enableValidationLayers)
            return;

        populateDebugMessengerCreateInfo(out var createInfo);

        if (
            extDebugUtils.CreateDebugUtilsMessenger(instance, &createInfo, null, out debugMessenger)
            != Result.Success
        )
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    void createSurface()
    {
        VkNonDispatchableHandle _surface;
        if (
            (Result)glfw.CreateWindowSurface(new VkHandle(instance.Handle), window, null, &_surface)
            != Result.Success
        )
        {
            throw new Exception("failed to create window surface!");
        }
        surface = new(_surface.Handle);
    }

    void pickPhysicalDevice()
    {
        uint physicalDeviceCount = 0;
        vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, null);

        if (physicalDeviceCount == 0)
        {
            throw new Exception("failed to find GPUs with Vulkan support!");
        }

        var physicalDevices = stackalloc PhysicalDevice[(int)physicalDeviceCount];
        vk.EnumeratePhysicalDevices(instance, &physicalDeviceCount, physicalDevices);

        for (int i = 0; i < physicalDeviceCount; ++i)
        {
            var _physicalDevice = physicalDevices[i];
            if (isDeviceSuitable(_physicalDevice))
            {
                physicalDevice = _physicalDevice;
                break;
            }
        }

        //         if (physicalDevice == VK_NULL_HANDLE) {
        //             throw new Exception("failed to find a suitable GPU!");
        //         }
    }

    void createLogicalDevice()
    {
        var indices = findQueueFamilies(physicalDevice);

        var uniqueQueueFamilies = indices.ToUniqueSet();
        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[2];
        float queuePriority = 1.0f;

        uint uniq = 0;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            queueCreateInfos[uniq].SType = StructureType.DeviceQueueCreateInfo;
            queueCreateInfos[uniq].QueueFamilyIndex = queueFamily;
            queueCreateInfos[uniq].QueueCount = 1;
            queueCreateInfos[uniq].PQueuePriorities = &queuePriority;
            ++uniq;
        }

        PhysicalDeviceFeatures deviceFeatures = default;

        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = uniq,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
        };

        ByteStringArrayAllocator extensions = [.. deviceExtensions];
        (createInfo.EnabledExtensionCount, createInfo.PpEnabledExtensionNames) = extensions;

        ByteStringArrayAllocator layers = [.. validationLayers];
        if (enableValidationLayers)
        {
            (createInfo.EnabledLayerCount, createInfo.PpEnabledLayerNames) = layers;
        }

        if (vk.CreateDevice(physicalDevice, &createInfo, null, out device) != Result.Success)
        {
            throw new Exception("failed to create logical device!");
        }

        vk.GetDeviceQueue(
            device,
            indices.graphicsFamily ?? throw new Exception(),
            0,
            out graphicsQueue
        );
        vk.GetDeviceQueue(
            device,
            indices.presentFamily ?? throw new Exception(),
            0,
            out presentQueue
        );
    }

    void createSwapChain()
    {
        var swapChainSupport = querySwapChainSupport(physicalDevice);

        var surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
        var presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);
        var extent = chooseSwapExtent(swapChainSupport.capabilities);

        var imageCount = swapChainSupport.capabilities.MinImageCount + 1;
        if (
            swapChainSupport.capabilities.MaxImageCount > 0
            && imageCount > swapChainSupport.capabilities.MaxImageCount
        )
        {
            imageCount = swapChainSupport.capabilities.MaxImageCount;
        }

        var createInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
        };

        var indices = findQueueFamilies(physicalDevice);
        var queueFamilyIndices = stackalloc uint[]
        {
            indices.graphicsFamily ?? throw new Exception(),
            indices.presentFamily ?? throw new Exception(),
        };

        if (indices.graphicsFamily != indices.presentFamily)
        {
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
            createInfo.PQueueFamilyIndices = queueFamilyIndices;
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        createInfo.PreTransform = swapChainSupport.capabilities.CurrentTransform;
        createInfo.CompositeAlpha = CompositeAlphaFlagsKHR.CompositeAlphaOpaqueBitKhr;
        createInfo.PresentMode = presentMode;
        createInfo.Clipped = true;

        createInfo.OldSwapchain = default;

        if (
            khrSwapchain.CreateSwapchain(device, &createInfo, null, out swapChain) != Result.Success
        )
        {
            throw new Exception("failed to create swap chain!");
        }

        khrSwapchain.GetSwapchainImages(device, swapChain, &imageCount, null);
        swapChainImages = new VkImage[(int)imageCount];
        khrSwapchain.GetSwapchainImages(device, swapChain, &imageCount, swapChainImages);

        swapChainImageFormat = surfaceFormat.Format;
        swapChainExtent = extent;
    }

    void createImageViews()
    {
        swapChainImageViews = new ImageView[swapChainImages.Length];

        for (int i = 0; i < swapChainImages.Length; ++i)
        {
            var createInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = swapChainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = swapChainImageFormat,
            };
            createInfo.Components.R = ComponentSwizzle.Identity;
            createInfo.Components.G = ComponentSwizzle.Identity;
            createInfo.Components.B = ComponentSwizzle.Identity;
            createInfo.Components.A = ComponentSwizzle.Identity;
            createInfo.SubresourceRange.AspectMask = ImageAspectFlags.ColorBit;
            createInfo.SubresourceRange.BaseMipLevel = 0;
            createInfo.SubresourceRange.LevelCount = 1;
            createInfo.SubresourceRange.BaseArrayLayer = 0;
            createInfo.SubresourceRange.LayerCount = 1;

            if (
                vk.CreateImageView(device, &createInfo, null, out swapChainImageViews[i])
                != Result.Success
            )
            {
                throw new Exception("failed to create image views!");
            }
        }
    }

    void createRenderPass()
    {
        var colorAttachment = new AttachmentDescription
        {
            Format = swapChainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr,
        };

        var colorAttachmentRef = new AttachmentReference
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal,
        };

        var subpass = new SubpassDescription
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
        };

        var dependency = new SubpassDependency { };
        dependency.SrcSubpass = Vk.SubpassExternal;
        dependency.DstSubpass = 0;
        dependency.SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
        dependency.SrcAccessMask = 0;
        dependency.DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
        dependency.DstAccessMask = AccessFlags.ColorAttachmentWriteBit;

        var renderPassInfo = new RenderPassCreateInfo
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency,
        };

        if (vk.CreateRenderPass(device, &renderPassInfo, null, out renderPass) != Result.Success)
        {
            throw new Exception("failed to create render pass!");
        }
    }

    void createGraphicsPipeline()
    {
        var vertShaderCode = File.ReadAllBytes("shaders/vert.spv");
        var vertShaderModule = createShaderModule(vertShaderCode);

        var fragShaderCode = File.ReadAllBytes("shaders/frag.spv");
        var fragShaderModule = createShaderModule(fragShaderCode);

        fixed (byte* main = "main"u8)
        {
            var vertShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.VertexBit,
                Module = vertShaderModule,
                PName = main,
            };
            var fragShaderStageInfo = new PipelineShaderStageCreateInfo
            {
                SType = StructureType.PipelineShaderStageCreateInfo,
                Stage = ShaderStageFlags.FragmentBit,
                Module = fragShaderModule,
                PName = main,
            };
            var shaderStages = stackalloc PipelineShaderStageCreateInfo[]
            {
                vertShaderStageInfo,
                fragShaderStageInfo,
            };

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 0,
                VertexAttributeDescriptionCount = 0,
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = Vk.False,
            };
            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1,
            };
            var rasterizer = new PipelineRasterizationStateCreateInfo
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = Vk.False,
                RasterizerDiscardEnable = Vk.False,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = Vk.False,
            };
            var multisampling = new PipelineMultisampleStateCreateInfo
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = Vk.False,
                RasterizationSamples = SampleCountFlags.Count1Bit,
            };
            var colorBlendAttachment = new PipelineColorBlendAttachmentState
            {
                ColorWriteMask =
                    ColorComponentFlags.RBit
                    | ColorComponentFlags.GBit
                    | ColorComponentFlags.BBit
                    | ColorComponentFlags.ABit,
                BlendEnable = Vk.False,
            };
            var colorBlending = new PipelineColorBlendStateCreateInfo
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = Vk.False,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };
            colorBlending.BlendConstants[0] = 0.0f;
            colorBlending.BlendConstants[1] = 0.0f;
            colorBlending.BlendConstants[2] = 0.0f;
            colorBlending.BlendConstants[3] = 0.0f;

            var dynamicStates = stackalloc DynamicState[]
            {
                DynamicState.Viewport,
                DynamicState.Scissor,
            };
            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = 2, //(uint)dynamicStates.Length,
                PDynamicStates = dynamicStates,
            };

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 0,
                PushConstantRangeCount = 0,
            };

            if (
                vk.CreatePipelineLayout(device, &pipelineLayoutInfo, null, out pipelineLayout)
                != Result.Success
            )
            {
                throw new Exception("failed to create pipeline layout!");
            }

            var pipelineInfo = new GraphicsPipelineCreateInfo
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
                Layout = pipelineLayout,
                RenderPass = renderPass,
                Subpass = 0,
                BasePipelineHandle = default,
            };

            if (
                vk.CreateGraphicsPipelines(
                    device,
                    default,
                    1,
                    &pipelineInfo,
                    null,
                    out graphicsPipeline
                ) != Result.Success
            )
            {
                throw new Exception("failed to create graphics pipeline!");
            }

            vk.DestroyShaderModule(device, fragShaderModule, null);
            vk.DestroyShaderModule(device, vertShaderModule, null);
        }
    }

    void createFramebuffers()
    {
        swapChainFramebuffers = new Framebuffer[swapChainImageViews.Length];

        for (int i = 0; i < swapChainImageViews.Length; i++)
        {
            var attachment = swapChainImageViews[i];

            var framebufferInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = swapChainExtent.Width,
                Height = swapChainExtent.Height,
                Layers = 1,
            };

            if (
                vk.CreateFramebuffer(device, &framebufferInfo, null, out swapChainFramebuffers[i])
                != Result.Success
            )
            {
                throw new Exception("failed to create framebuffer!");
            }
        }
    }

    void createCommandPool()
    {
        var queueFamilyIndices = findQueueFamilies(physicalDevice);

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = queueFamilyIndices.graphicsFamily ?? throw new Exception(),
        };

        if (vk.CreateCommandPool(device, &poolInfo, null, out commandPool) != Result.Success)
        {
            throw new Exception("failed to create command pool!");
        }
    }

    void createCommandBuffer()
    {
        var allocInfo = new CommandBufferAllocateInfo
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = 1,
        };

        if (vk.AllocateCommandBuffers(device, &allocInfo, out commandBuffer) != Result.Success)
        {
            throw new Exception("failed to allocate command buffers!");
        }
    }

    //     void recordCommandBuffer(VkCommandBuffer commandBuffer, uint32_t imageIndex) {
    //         VkCommandBufferBeginInfo beginInfo{};
    //         beginInfo.SType = StructureType.COMMAND_BUFFER_BEGIN_INFO;

    //         if (vkBeginCommandBuffer(commandBuffer, &beginInfo) != Result.Success) {
    //             throw new Exception("failed to begin recording command buffer!");
    //         }

    //         VkRenderPassBeginInfo renderPassInfo{};
    //         renderPassInfo.SType = StructureType.RENDER_PASS_BEGIN_INFO;
    //         renderPassInfo.renderPass = renderPass;
    //         renderPassInfo.framebuffer = swapChainFramebuffers[imageIndex];
    //         renderPassInfo.renderArea.offset = {0, 0};
    //         renderPassInfo.renderArea.extent = swapChainExtent;

    //         VkClearValue clearColor = {{{0.0f, 0.0f, 0.0f, 1.0f}}};
    //         renderPassInfo.clearValueCount = 1;
    //         renderPassInfo.pClearValues = &clearColor;

    //         vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VK_SUBPASS_CONTENTS_INLINE);

    //         vkCmdBindPipeline(commandBuffer, VK_PIPELINE_BIND_POINT_GRAPHICS, graphicsPipeline);

    //         VkViewport viewport{};
    //         viewport.x = 0.0f;
    //         viewport.y = 0.0f;
    //         viewport.width = static_cast<float>(swapChainExtent.width);
    //         viewport.height = static_cast<float>(swapChainExtent.height);
    //         viewport.minDepth = 0.0f;
    //         viewport.maxDepth = 1.0f;
    //         vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

    //         VkRect2D scissor{};
    //         scissor.offset = {0, 0};
    //         scissor.extent = swapChainExtent;
    //         vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

    //         vkCmdDraw(commandBuffer, 3, 1, 0, 0);

    //         vkCmdEndRenderPass(commandBuffer);

    //         if (vkEndCommandBuffer(commandBuffer) != Result.Success) {
    //             throw new Exception("failed to record command buffer!");
    //         }
    //     }

    void createSyncObjects()
    {
        var semaphoreInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };

        var fenceInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };

        if (
            vk.CreateSemaphore(device, &semaphoreInfo, null, out imageAvailableSemaphore)
                != Result.Success
            || vk.CreateSemaphore(device, &semaphoreInfo, null, out renderFinishedSemaphore)
                != Result.Success
            || vk.CreateFence(device, &fenceInfo, null, out inFlightFence) != Result.Success
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    //     void drawFrame() {
    //         vkWaitForFences(device, 1, &inFlightFence, VK_TRUE, UINT64_MAX);
    //         vkResetFences(device, 1, &inFlightFence);

    //         uint32_t imageIndex;
    //         vkAcquireNextImageKHR(device, swapChain, UINT64_MAX, imageAvailableSemaphore, VK_NULL_HANDLE, &imageIndex);

    //         vkResetCommandBuffer(commandBuffer, /*VkCommandBufferResetFlagBits*/ 0);
    //         recordCommandBuffer(commandBuffer, imageIndex);

    //         VkSubmitInfo submitInfo{};
    //         submitInfo.SType = StructureType.SUBMIT_INFO;

    //         VkSemaphore waitSemaphores[] = {imageAvailableSemaphore};
    //         VkPipelineStageFlags waitStages[] = {VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT};
    //         submitInfo.waitSemaphoreCount = 1;
    //         submitInfo.pWaitSemaphores = waitSemaphores;
    //         submitInfo.pWaitDstStageMask = waitStages;

    //         submitInfo.commandBufferCount = 1;
    //         submitInfo.pCommandBuffers = &commandBuffer;

    //         VkSemaphore signalSemaphores[] = {renderFinishedSemaphore};
    //         submitInfo.signalSemaphoreCount = 1;
    //         submitInfo.pSignalSemaphores = signalSemaphores;

    //         if (vkQueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFence) != Result.Success) {
    //             throw new Exception("failed to submit draw command buffer!");
    //         }

    //         VkPresentInfoKHR presentInfo{};
    //         presentInfo.SType = StructureType.PRESENT_INFO_KHR;

    //         presentInfo.waitSemaphoreCount = 1;
    //         presentInfo.pWaitSemaphores = signalSemaphores;

    //         VkSwapchainKHR swapChains[] = {swapChain};
    //         presentInfo.swapchainCount = 1;
    //         presentInfo.pSwapchains = swapChains;

    //         presentInfo.pImageIndices = &imageIndex;

    //         vkQueuePresentKHR(presentQueue, &presentInfo);
    //     }

    ShaderModule createShaderModule(ReadOnlySpan<byte> code)
    {
        fixed (byte* pCode = code)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (uint)code.Length,
                PCode = (uint*)pCode,
            };

            if (
                vk.CreateShaderModule(device, &createInfo, null, out var shaderModule)
                != Result.Success
            )
            {
                throw new Exception("failed to create shader module!");
            }

            return shaderModule;
        }
    }

    SurfaceFormatKHR chooseSwapSurfaceFormat(ReadOnlySpan<SurfaceFormatKHR> availableFormats)
    {
        foreach (var availableFormat in availableFormats)
        {
            if (
                availableFormat.Format == Format.B8G8R8A8Srgb
                && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr
            )
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    PresentModeKHR chooseSwapPresentMode(ReadOnlySpan<PresentModeKHR> availablePresentModes)
    {
        foreach (var availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    Extent2D chooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        else
        {
            glfw.GetFramebufferSize(window, out var width, out var height);

            var actualExtent = new Extent2D((uint)width, (uint)height);

            actualExtent.Width = Math.Clamp(
                actualExtent.Width,
                capabilities.MinImageExtent.Width,
                capabilities.MaxImageExtent.Width
            );
            actualExtent.Height = Math.Clamp(
                actualExtent.Height,
                capabilities.MinImageExtent.Height,
                capabilities.MaxImageExtent.Height
            );

            return actualExtent;
        }
    }

    SwapChainSupportDetails querySwapChainSupport(PhysicalDevice device)
    {
        SwapChainSupportDetails details = default;
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, &details.capabilities);

        uint formatCount;
        khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, null);

        if (formatCount != 0)
        {
            // details.formats.resize(formatCount);
            details.formats = new SurfaceFormatKHR[(int)formatCount];
            fixed (SurfaceFormatKHR* formats = details.formats)
            {
                khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, &formatCount, formats);
            }
        }
        else
        {
            details.formats = [];
        }

        uint presentModeCount;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, &presentModeCount, null);

        if (presentModeCount != 0)
        {
            details.presentModes = new PresentModeKHR[(int)presentModeCount];
            fixed (PresentModeKHR* presentModes = details.presentModes)
            {
                khrSurface.GetPhysicalDeviceSurfacePresentModes(
                    device,
                    surface,
                    &presentModeCount,
                    presentModes
                );
            }
        }
        else
        {
            details.presentModes = [];
        }

        return details;
    }

    bool isDeviceSuitable(PhysicalDevice physicalDevice)
    {
        var indices = findQueueFamilies(physicalDevice);

        bool extensionsSupported = checkDeviceExtensionSupport(physicalDevice);

        bool swapChainAdequate = false;
        if (extensionsSupported)
        {
            var swapChainSupport = querySwapChainSupport(physicalDevice);
            swapChainAdequate =
                swapChainSupport.formats.Length > 0 && swapChainSupport.presentModes.Length > 0;
        }

        return indices.isComplete() && extensionsSupported && swapChainAdequate;
    }

    bool checkDeviceExtensionSupport(PhysicalDevice device)
    {
        uint extensionCount;
        vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);

        var availableExtensions = stackalloc ExtensionProperties[(int)extensionCount];
        vk.EnumerateDeviceExtensionProperties(
            device,
            (byte*)null,
            &extensionCount,
            availableExtensions
        );

        HashSet<string> requiredExtensions = [.. deviceExtensions];
        for (int i = 0; i < extensionCount; ++i)
        {
            var extensionName =
                Marshal.PtrToStringAnsi((nint)availableExtensions[i].ExtensionName)
                ?? throw new Exception();
            requiredExtensions.Remove(extensionName);
        }

        return requiredExtensions.Count == 0;
    }

    QueueFamilyIndices findQueueFamilies(PhysicalDevice device)
    {
        QueueFamilyIndices indices = default;

        uint queueFamilyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

        uint i = 0;
        for (int j = 0; j < queueFamilyCount; ++j)
        {
            var queueFamily = queueFamilies[j];
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                indices.graphicsFamily = i;
            }

            Silk.NET.Core.Bool32 presentSupport = false;
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, surface, &presentSupport);

            if (presentSupport)
            {
                indices.presentFamily = i;
            }

            if (indices.isComplete())
            {
                break;
            }

            i++;
        }

        return indices;
    }

    bool checkValidationLayerSupport()
    {
        uint layerCount;
        vk.EnumerateInstanceLayerProperties(&layerCount, null);

        var availableLayers = stackalloc LayerProperties[(int)layerCount];
        vk.EnumerateInstanceLayerProperties(&layerCount, availableLayers);

        foreach (var layerName in validationLayers)
        {
            bool layerFound = false;

            for (uint i = 0; i < layerCount; ++i)
            {
                // find zero
                int j = 0;
                for (; j < 256; ++j)
                {
                    if (availableLayers[i].LayerName[j] == 0)
                    {
                        break;
                    }
                }
                var availableLayerName = Marshal.PtrToStringAnsi(
                    (nint)availableLayers[i].LayerName
                );
                if (layerName == availableLayerName)
                {
                    layerFound = true;
                    break;
                }
            }

            if (!layerFound)
            {
                return false;
            }
        }

        return true;
    }

    //     static std::vector<char> readFile(const std::string& filename) {
    //         std::ifstream file(filename, std::ios::ate | std::ios::binary);

    //         if (!file.is_open()) {
    //             throw new Exception("failed to open file!");
    //         }

    //         size_t fileSize = (size_t) file.tellg();
    //         std::vector<char> buffer(fileSize);

    //         file.seekg(0);
    //         file.read(buffer.data(), fileSize);

    //         file.close();

    //         return buffer;
    //     }

    private static uint debugCallback(
        DebugUtilsMessageSeverityFlagsEXT messageSeverity,
        DebugUtilsMessageTypeFlagsEXT messageTypes,
        DebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        var msg = Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage);
        Console.Error.WriteLine($"validation layer: {msg}");

        return 0; //VK_FALSE;
    }
}

static class Program
{
    public static void Main()
    {
        new HelloTriangleApplication().Run();
    }
}
