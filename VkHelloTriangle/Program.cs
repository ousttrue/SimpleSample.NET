// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

public unsafe class ByteStringArrayAllocator : IDisposable, IEnumerable
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

    public void Add(nint p)
    {
        _list.Add(Marshal.PtrToStringAnsi(p) ?? throw new Exception());
    }

    public void Add(ReadOnlySpan<byte> p)
    {
        Add(Encoding.UTF8.GetString(p));
    }

    public void AddSpan(ReadOnlySpan<IntPtr> pp)
    {
        foreach (var p in pp)
        {
            Add(p);
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
    public VkSurfaceCapabilitiesKHR capabilities;
    public VkSurfaceFormatKHR[] formats;
    public VkPresentModeKHR[] presentModes;
};

unsafe class HelloTriangleApplication
{
    static readonly Glfw glfw;

    static HelloTriangleApplication()
    {
        glfw = GlfwProvider.GLFW.Value ?? throw new NullReferenceException();
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

    static readonly string[] validationLayers =
    [
        Encoding.ASCII.GetString(VK_LAYER_KHRONOS_VALIDATION_EXTENSION_NAME),
    ];
    static readonly string[] deviceExtensions =
    [
        Encoding.ASCII.GetString(VK_KHR_SWAPCHAIN_EXTENSION_NAME),
    ];

    private WindowHandle* window;
    private VkInstance instance;
    private VkInstanceApi instanceApi;

    // private DebugUtils extDebugUtils;
    private VkDebugUtilsMessengerEXT debugMessenger;

    // private KhrSurface khrSurface;
    private VkSurfaceKHR surface;

    private VkPhysicalDevice physicalDevice;
    private VkDevice device;
    private VkDeviceApi deviceApi;

    private VkQueue graphicsQueue;
    private VkQueue presentQueue;

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
        // if (!vk.TryGetInstanceExtension(instance, out khrSurface))
        // {
        //     throw new Exception("TryGetInstanceExtension");
        // }
        // if (!vk.TryGetInstanceExtension(instance, out extDebugUtils))
        // {
        //     throw new Exception("TryGetInstanceExtension");
        // }

        setupDebugMessenger();
        createSurface();
        pickPhysicalDevice();
        createLogicalDevice();
        // if (!vk.TryGetDeviceExtension(instance, device, out khrSwapchain))
        // {
        //     throw new Exception("TryGetDeviceExtension");
        // }

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
        while (!glfw.WindowShouldClose(window))
        {
            glfw.PollEvents();
            drawFrame();
        }

        deviceApi.vkDeviceWaitIdle();
    }

    void cleanup()
    {
        deviceApi.vkDestroySemaphore(renderFinishedSemaphore, null);
        deviceApi.vkDestroySemaphore(imageAvailableSemaphore, null);
        deviceApi.vkDestroyFence(inFlightFence, null);

        deviceApi.vkDestroyCommandPool(commandPool, null);

        foreach (var framebuffer in swapChainFramebuffers)
        {
            deviceApi.vkDestroyFramebuffer(framebuffer, null);
        }

        deviceApi.vkDestroyPipeline(graphicsPipeline, null);
        deviceApi.vkDestroyPipelineLayout(pipelineLayout, null);
        deviceApi.vkDestroyRenderPass(renderPass, null);

        foreach (var imageView in swapChainImageViews)
        {
            deviceApi.vkDestroyImageView(imageView, null);
        }

        deviceApi.vkDestroySwapchainKHR(swapChain, null);
        deviceApi.vkDestroyDevice(null);

        if (enableValidationLayers)
        {
            instanceApi.vkDestroyDebugUtilsMessengerEXT(debugMessenger, null);
        }

        instanceApi.vkDestroySurfaceKHR(surface, null);
        instanceApi.vkDestroyInstance(null);

        vkShutdown();

        glfw.DestroyWindow(window);

        glfw.Terminate();
    }

    static void StrCopy(Span<byte> dst, ReadOnlySpan<byte> src)
    {
        src.CopyTo(dst);
    }

    void createInstance()
    {
        vkInitialize();
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
                extensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
            }

            var appInfo = new VkApplicationInfo
            {
                sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
                pApplicationName = appName,
                // applicationVersion = Vk.MakeVersion(1, 0, 0),
                pEngineName = engineName,
                // engineVersion = Vk.MakeVersion(1, 0, 0),
                apiVersion = VK_API_VERSION_1_0,
            };
            var createInfo = new VkInstanceCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &appInfo,
                enabledLayerCount = 0,
                pNext = null,
            };
            (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = extensions;

            VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = default;
            ByteStringArrayAllocator layers = [.. validationLayers];
            if (enableValidationLayers)
            {
                (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;

                populateDebugMessengerCreateInfo(out debugCreateInfo);
                createInfo.pNext = &debugCreateInfo;
            }
            if (vkCreateInstance(&createInfo, null, out instance) != VK_SUCCESS)
            {
                throw new Exception("failed to create instance!");
            }
            instanceApi = new VkInstanceApi(instance);
        }
    }

    void populateDebugMessengerCreateInfo(out VkDebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo = new VkDebugUtilsMessengerCreateInfoEXT
        {
            sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
            messageSeverity =
                VkDebugUtilsMessageSeverityFlagsEXT.Verbose
                | VkDebugUtilsMessageSeverityFlagsEXT.Warning
                | VkDebugUtilsMessageSeverityFlagsEXT.Error,
            messageType =
                VkDebugUtilsMessageTypeFlagsEXT.General
                | VkDebugUtilsMessageTypeFlagsEXT.Validation
                | VkDebugUtilsMessageTypeFlagsEXT.Performance,
            pfnUserCallback = &debugCallback,
        };
    }

    void setupDebugMessenger()
    {
        if (!enableValidationLayers)
            return;

        populateDebugMessengerCreateInfo(out var createInfo);

        if (
            instanceApi.vkCreateDebugUtilsMessengerEXT(&createInfo, null, out debugMessenger)
            != VK_SUCCESS
        )
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    void createSurface()
    {
        VkNonDispatchableHandle _surface;
        if (
            (VkResult)
                glfw.CreateWindowSurface(new VkHandle(instance.Handle), window, null, &_surface)
            != VK_SUCCESS
        )
        {
            throw new Exception("failed to create window surface!");
        }
        surface = new(_surface.Handle);
    }

    void pickPhysicalDevice()
    {
        uint physicalDeviceCount = 0;
        instanceApi.vkEnumeratePhysicalDevices(&physicalDeviceCount, null);

        if (physicalDeviceCount == 0)
        {
            throw new Exception("failed to find GPUs with Vulkan support!");
        }

        Span<VkPhysicalDevice> physicalDevices =
            stackalloc VkPhysicalDevice[(int)physicalDeviceCount];
        instanceApi.vkEnumeratePhysicalDevices(physicalDevices);

        foreach (var physicalDevice in physicalDevices)
        {
            if (isDeviceSuitable(physicalDevice))
            {
                this.physicalDevice = physicalDevice;
                break;
            }
        }

        if (physicalDevice.Handle == default)
        {
            throw new Exception("failed to find a suitable GPU!");
        }
    }

    void createLogicalDevice()
    {
        var indices = findQueueFamilies(physicalDevice);

        var uniqueQueueFamilies = indices.ToUniqueSet();
        var queueCreateInfos = stackalloc VkDeviceQueueCreateInfo[2];
        float queuePriority = 1.0f;

        uint uniq = 0;
        foreach (var queueFamily in uniqueQueueFamilies)
        {
            queueCreateInfos[uniq].sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
            queueCreateInfos[uniq].queueFamilyIndex = queueFamily;
            queueCreateInfos[uniq].queueCount = 1;
            queueCreateInfos[uniq].pQueuePriorities = &queuePriority;
            ++uniq;
        }

        VkPhysicalDeviceFeatures deviceFeatures = default;

        var createInfo = new VkDeviceCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
            queueCreateInfoCount = uniq,
            pQueueCreateInfos = queueCreateInfos,
            pEnabledFeatures = &deviceFeatures,
        };

        ByteStringArrayAllocator extensions = [.. deviceExtensions];
        (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = extensions;

        ByteStringArrayAllocator layers = [.. validationLayers];
        if (enableValidationLayers)
        {
            (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;
        }

        if (instanceApi.vkCreateDevice(physicalDevice, &createInfo, null, out device) != VK_SUCCESS)
        {
            throw new Exception("failed to create logical device!");
        }
        deviceApi = new VkDeviceApi(instanceApi, device);

        deviceApi.vkGetDeviceQueue(
            indices.graphicsFamily ?? throw new Exception(),
            0,
            out graphicsQueue
        );
        deviceApi.vkGetDeviceQueue(
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
            surface = surface,
            minImageCount = imageCount,
            imageFormat = surfaceFormat.format,
            imageColorSpace = surfaceFormat.colorSpace,
            imageExtent = extent,
            imageArrayLayers = 1,
            imageUsage = VkImageUsageFlags.ColorAttachment,
        };

        var indices = findQueueFamilies(physicalDevice);
        var queueFamilyIndices = stackalloc uint[]
        {
            indices.graphicsFamily ?? throw new Exception(),
            indices.presentFamily ?? throw new Exception(),
        };

        if (indices.graphicsFamily != indices.presentFamily)
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

        if (deviceApi.vkCreateSwapchainKHR(&createInfo, null, out swapChain) != VK_SUCCESS)
        {
            throw new Exception("failed to create swap chain!");
        }

        deviceApi.vkGetSwapchainImagesKHR(swapChain, &imageCount, null);
        swapChainImages = new VkImage[(int)imageCount];
        fixed (VkImage* images = swapChainImages)
        {
            deviceApi.vkGetSwapchainImagesKHR(swapChain, &imageCount, images);
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
                deviceApi.vkCreateImageView(&createInfo, null, out swapChainImageViews[i])
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

        if (deviceApi.vkCreateRenderPass(&renderPassInfo, null, out renderPass) != VK_SUCCESS)
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
                deviceApi.vkCreatePipelineLayout(&pipelineLayoutInfo, null, out pipelineLayout)
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
                deviceApi.vkCreateGraphicsPipelines(
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

            deviceApi.vkDestroyShaderModule(fragShaderModule, null);
            deviceApi.vkDestroyShaderModule(vertShaderModule, null);
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
                deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out swapChainFramebuffers[i])
                != VK_SUCCESS
            )
            {
                throw new Exception("failed to create framebuffer!");
            }
        }
    }

    void createCommandPool()
    {
        var queueFamilyIndices = findQueueFamilies(physicalDevice);

        var poolInfo = new VkCommandPoolCreateInfo
        {
            sType = VK_STRUCTURE_TYPE_COMMAND_POOL_CREATE_INFO,
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = queueFamilyIndices.graphicsFamily ?? throw new Exception(),
        };

        if (deviceApi.vkCreateCommandPool(&poolInfo, null, out commandPool) != VK_SUCCESS)
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
        if (deviceApi.vkAllocateCommandBuffers(&allocInfo, &_commandBuffer) != VK_SUCCESS)
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
        if (deviceApi.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VK_SUCCESS)
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

        deviceApi.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

        deviceApi.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, graphicsPipeline);

        var viewport = new VkViewport
        {
            x = 0.0f,
            y = 0.0f,
            width = swapChainExtent.width,
            height = swapChainExtent.height,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };
        deviceApi.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);

        var scissor = new VkRect2D { offset = new(0, 0), extent = swapChainExtent };
        deviceApi.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);

        deviceApi.vkCmdDraw(commandBuffer, 3, 1, 0, 0);

        deviceApi.vkCmdEndRenderPass(commandBuffer);
        if (deviceApi.vkEndCommandBuffer(commandBuffer) != VK_SUCCESS)
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
            deviceApi.vkCreateSemaphore(&semaphoreInfo, null, out imageAvailableSemaphore)
                != VK_SUCCESS
            || deviceApi.vkCreateSemaphore(&semaphoreInfo, null, out renderFinishedSemaphore)
                != VK_SUCCESS
            || deviceApi.vkCreateFence(&fenceInfo, null, out inFlightFence) != VK_SUCCESS
        )
        {
            throw new Exception("failed to create synchronization objects for a frame!");
        }
    }

    void drawFrame()
    {
        var _inFlightFence = inFlightFence;
        deviceApi.vkWaitForFences(1, &_inFlightFence, true, ulong.MaxValue);
        deviceApi.vkResetFences(1, &_inFlightFence);

        uint imageIndex;
        deviceApi.vkAcquireNextImageKHR(
            swapChain,
            ulong.MaxValue,
            imageAvailableSemaphore,
            default,
            &imageIndex
        );

        deviceApi.vkResetCommandBuffer(
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
        if (deviceApi.vkQueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS)
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

        deviceApi.vkQueuePresentKHR(presentQueue, &presentInfo);
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
                deviceApi.vkCreateShaderModule(&createInfo, null, out var shaderModule)
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

    VkExtent2D chooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.currentExtent.width != uint.MaxValue)
        {
            return capabilities.currentExtent;
        }
        else
        {
            glfw.GetFramebufferSize(window, out var width, out var height);

            var actualExtent = new VkExtent2D((uint)width, (uint)height);

            actualExtent.width = Math.Clamp(
                actualExtent.width,
                capabilities.minImageExtent.width,
                capabilities.maxImageExtent.width
            );
            actualExtent.height = Math.Clamp(
                actualExtent.height,
                capabilities.minImageExtent.height,
                capabilities.maxImageExtent.height
            );

            return actualExtent;
        }
    }

    SwapChainSupportDetails querySwapChainSupport(VkPhysicalDevice physicalDevice)
    {
        SwapChainSupportDetails details = default;
        instanceApi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            physicalDevice,
            surface,
            &details.capabilities
        );

        uint formatCount;
        instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(
            physicalDevice,
            surface,
            &formatCount,
            null
        );

        if (formatCount != 0)
        {
            // details.formats.resize(formatCount);
            details.formats = new VkSurfaceFormatKHR[(int)formatCount];
            fixed (VkSurfaceFormatKHR* formats = details.formats)
            {
                instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(
                    physicalDevice,
                    surface,
                    &formatCount,
                    formats
                );
            }
        }
        else
        {
            details.formats = [];
        }

        uint presentModeCount;
        instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(
            physicalDevice,
            surface,
            &presentModeCount,
            null
        );

        if (presentModeCount != 0)
        {
            details.presentModes = new VkPresentModeKHR[(int)presentModeCount];
            fixed (VkPresentModeKHR* presentModes = details.presentModes)
            {
                instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(
                    physicalDevice,
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

    bool isDeviceSuitable(VkPhysicalDevice physicalDevice)
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

    bool checkDeviceExtensionSupport(VkPhysicalDevice physicalDevice)
    {
        uint extensionCount;
        instanceApi.vkEnumerateDeviceExtensionProperties(
            physicalDevice,
            null,
            &extensionCount,
            null
        );

        var availableExtensions = stackalloc VkExtensionProperties[(int)extensionCount];
        instanceApi.vkEnumerateDeviceExtensionProperties(
            physicalDevice,
            (byte*)null,
            &extensionCount,
            availableExtensions
        );

        HashSet<string> requiredExtensions = [.. deviceExtensions];
        for (int i = 0; i < extensionCount; ++i)
        {
            var extensionName =
                Marshal.PtrToStringAnsi((nint)availableExtensions[i].extensionName)
                ?? throw new Exception();
            requiredExtensions.Remove(extensionName);
        }

        return requiredExtensions.Count == 0;
    }

    QueueFamilyIndices findQueueFamilies(VkPhysicalDevice physicalDevice)
    {
        QueueFamilyIndices indices = default;

        uint queueFamilyCount = 0;
        instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(
            physicalDevice,
            &queueFamilyCount,
            null
        );

        var queueFamilies = stackalloc VkQueueFamilyProperties[(int)queueFamilyCount];
        instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(
            physicalDevice,
            &queueFamilyCount,
            queueFamilies
        );

        uint i = 0;
        for (int j = 0; j < queueFamilyCount; ++j)
        {
            var queueFamily = queueFamilies[j];
            if (queueFamily.queueFlags.HasFlag(VkQueueFlags.Graphics))
            {
                indices.graphicsFamily = i;
            }

            VkBool32 presentSupport = false;
            instanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(
                physicalDevice,
                i,
                surface,
                &presentSupport
            );

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
        vkEnumerateInstanceLayerProperties(out var layerCount);
        Span<VkLayerProperties> availableLayers = stackalloc VkLayerProperties[(int)layerCount];
        vkEnumerateInstanceLayerProperties(availableLayers);

        foreach (var layerName in validationLayers)
        {
            bool layerFound = false;

            foreach (var available in availableLayers)
            {
                var availableLayerName = Marshal.PtrToStringAnsi((nint)available.layerName);
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

    [UnmanagedCallersOnly]
    private static uint debugCallback(
        VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
        VkDebugUtilsMessageTypeFlagsEXT messageTypes,
        VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* userData
    )
    {
        var msg = Marshal.PtrToStringAnsi((nint)pCallbackData->pMessage);
        Console.Error.WriteLine($"validation layer: {msg}");

        return VK_FALSE;
    }
}

static class Program
{
    public static void Main()
    {
        new HelloTriangleApplication().Run();
    }
}
