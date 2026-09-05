// VkResult CreateDebugUtilsMessengerEXT(VkInstance instance, const VkDebugUtilsMessengerCreateInfoEXT* pCreateInfo, const VkAllocationCallbacks* pAllocator, VkDebugUtilsMessengerEXT* pDebugMessenger) {
//     auto func = (PFN_vkCreateDebugUtilsMessengerEXT) vkGetInstanceProcAddr(instance, "vkCreateDebugUtilsMessengerEXT");
//     if (func != null) {
//         return func(instance, pCreateInfo, pAllocator, pDebugMessenger);
//     } else {
//         return VK_ERROR_EXTENSION_NOT_PRESENT;
//     }
// }

// void DestroyDebugUtilsMessengerEXT(VkInstance instance, VkDebugUtilsMessengerEXT debugMessenger, const VkAllocationCallbacks* pAllocator) {
//     auto func = (PFN_vkDestroyDebugUtilsMessengerEXT) vkGetInstanceProcAddr(instance, "vkDestroyDebugUtilsMessengerEXT");
//     if (func != null) {
//         func(instance, debugMessenger, pAllocator);
//     }
// }

// struct QueueFamilyIndices {
//     std::optional<uint32_t> graphicsFamily;
//     std::optional<uint32_t> presentFamily;

//     bool isComplete() {
//         return graphicsFamily.has_value() && presentFamily.has_value();
//     }
// };

// struct SwapChainSupportDetails {
//     VkSurfaceCapabilitiesKHR capabilities;
//     std::vector<VkSurfaceFormatKHR> formats;
//     std::vector<VkPresentModeKHR> presentModes;
// };

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Silk.NET.Vulkan;

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

    static readonly byte[] ExtDebugUtilsName = Encoding.ASCII.GetBytes(
        Silk.NET.Vulkan.Extensions.EXT.ExtDebugUtils.ExtensionName
    );

    // const int MAX_FRAMES_IN_FLIGHT = 2;

    static readonly byte[] validationLayer = "VK_LAYER_KHRONOS_validation"u8.ToArray();

    // const std::vector<const char*> deviceExtensions = {
    //     VK_KHR_SWAPCHAIN_EXTENSION_NAME
    // };

    private WindowHandle* window;

    private Instance instance;

    //     VkDebugUtilsMessengerEXT debugMessenger;
    //     VkSurfaceKHR surface;

    //     VkPhysicalDevice physicalDevice = VK_NULL_HANDLE;
    //     VkDevice device;

    //     VkQueue graphicsQueue;
    //     VkQueue presentQueue;

    //     VkSwapchainKHR swapChain;
    //     std::vector<VkImage> swapChainImages;
    //     VkFormat swapChainImageFormat;
    //     VkExtent2D swapChainExtent;
    //     std::vector<VkImageView> swapChainImageViews;
    //     std::vector<VkFramebuffer> swapChainFramebuffers;

    //     VkRenderPass renderPass;
    //     VkPipelineLayout pipelineLayout;
    //     VkPipeline graphicsPipeline;

    //     VkCommandPool commandPool;
    //     VkCommandBuffer commandBuffer;

    //     VkSemaphore imageAvailableSemaphore;
    //     VkSemaphore renderFinishedSemaphore;
    //     VkFence inFlightFence;

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
        //         setupDebugMessenger();
        //         createSurface();
        //         pickPhysicalDevice();
        //         createLogicalDevice();
        //         createSwapChain();
        //         createImageViews();
        //         createRenderPass();
        //         createGraphicsPipeline();
        //         createFramebuffers();
        //         createCommandPool();
        //         createCommandBuffer();
        //         createSyncObjects();
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

    unsafe void createInstance()
    {
        if (enableValidationLayers && !checkValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        fixed (byte* appName = "Hello Triangle"u8)
        fixed (byte* engineName = "No Engine"u8)
        fixed (byte* EXT_DEBUG_UTILS_NAME = ExtDebugUtilsName)
        fixed (byte* pvalidationLayer = validationLayer)
        {
            var glfwExtensions = glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
            var extensionsCount = (int)glfwExtensionCount;
            if (enableValidationLayers)
            {
                ++extensionsCount;
            }
            var extensions = stackalloc byte*[extensionsCount];
            int i = 0;
            for (; i < glfwExtensionCount; ++i)
            {
                extensions[i] = glfwExtensions[i];
            }
            if (enableValidationLayers)
            {
                extensions[i] = EXT_DEBUG_UTILS_NAME;
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
                EnabledExtensionCount = (uint)extensionsCount,
                PpEnabledExtensionNames = extensions,
                EnabledLayerCount = 0,
                PNext = null,
            };

            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = default;
            if (enableValidationLayers)
            {
                createInfo.EnabledLayerCount = 1;
                createInfo.PpEnabledLayerNames = &pvalidationLayer;

                populateDebugMessengerCreateInfo(ref debugCreateInfo);
                createInfo.PNext = &debugCreateInfo;
            }
            if (vk.CreateInstance(&createInfo, null, out var instance) != Result.Success)
            {
                throw new Exception("failed to create instance!");
            }
        }
    }

    void populateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
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

    //     void setupDebugMessenger() {
    //         if (!enableValidationLayers) return;

    //         VkDebugUtilsMessengerCreateInfoEXT createInfo;
    //         populateDebugMessengerCreateInfo(createInfo);

    //         if (CreateDebugUtilsMessengerEXT(instance, &createInfo, null, &debugMessenger) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to set up debug messenger!");
    //         }
    //     }

    //     void createSurface() {
    //         if (glfwCreateWindowSurface(instance, window, null, &surface) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create window surface!");
    //         }
    //     }

    //     void pickPhysicalDevice() {
    //         uint32_t deviceCount = 0;
    //         vkEnumeratePhysicalDevices(instance, &deviceCount, null);

    //         if (deviceCount == 0) {
    //             throw std::runtime_error("failed to find GPUs with Vulkan support!");
    //         }

    //         std::vector<VkPhysicalDevice> devices(deviceCount);
    //         vkEnumeratePhysicalDevices(instance, &deviceCount, devices.data());

    //         for (const auto& device : devices) {
    //             if (isDeviceSuitable(device)) {
    //                 physicalDevice = device;
    //                 break;
    //             }
    //         }

    //         if (physicalDevice == VK_NULL_HANDLE) {
    //             throw std::runtime_error("failed to find a suitable GPU!");
    //         }
    //     }

    //     void createLogicalDevice() {
    //         QueueFamilyIndices indices = findQueueFamilies(physicalDevice);

    //         std::vector<VkDeviceQueueCreateInfo> queueCreateInfos;
    //         std::set<uint32_t> uniqueQueueFamilies = {indices.graphicsFamily.value(), indices.presentFamily.value()};

    //         float queuePriority = 1.0f;
    //         for (uint32_t queueFamily : uniqueQueueFamilies) {
    //             VkDeviceQueueCreateInfo queueCreateInfo{};
    //             queueCreateInfo.SType = StructureType.DEVICE_QUEUE_CREATE_INFO;
    //             queueCreateInfo.queueFamilyIndex = queueFamily;
    //             queueCreateInfo.queueCount = 1;
    //             queueCreateInfo.pQueuePriorities = &queuePriority;
    //             queueCreateInfos.push_back(queueCreateInfo);
    //         }

    //         VkPhysicalDeviceFeatures deviceFeatures{};

    //         VkDeviceCreateInfo createInfo{};
    //         createInfo.SType = StructureType.DEVICE_CREATE_INFO;

    //         createInfo.queueCreateInfoCount = static_cast<uint32_t>(queueCreateInfos.size());
    //         createInfo.pQueueCreateInfos = queueCreateInfos.data();

    //         createInfo.pEnabledFeatures = &deviceFeatures;

    //         createInfo.enabledExtensionCount = static_cast<uint32_t>(deviceExtensions.size());
    //         createInfo.ppEnabledExtensionNames = deviceExtensions.data();

    //         if (enableValidationLayers) {
    //             createInfo.enabledLayerCount = static_cast<uint32_t>(validationLayers.size());
    //             createInfo.ppEnabledLayerNames = validationLayers.data();
    //         } else {
    //             createInfo.enabledLayerCount = 0;
    //         }

    //         if (vkCreateDevice(physicalDevice, &createInfo, null, &device) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create logical device!");
    //         }

    //         vkGetDeviceQueue(device, indices.graphicsFamily.value(), 0, &graphicsQueue);
    //         vkGetDeviceQueue(device, indices.presentFamily.value(), 0, &presentQueue);
    //     }

    //     void createSwapChain() {
    //         SwapChainSupportDetails swapChainSupport = querySwapChainSupport(physicalDevice);

    //         VkSurfaceFormatKHR surfaceFormat = chooseSwapSurfaceFormat(swapChainSupport.formats);
    //         VkPresentModeKHR presentMode = chooseSwapPresentMode(swapChainSupport.presentModes);
    //         VkExtent2D extent = chooseSwapExtent(swapChainSupport.capabilities);

    //         uint32_t imageCount = swapChainSupport.capabilities.minImageCount + 1;
    //         if (swapChainSupport.capabilities.maxImageCount > 0 && imageCount > swapChainSupport.capabilities.maxImageCount) {
    //             imageCount = swapChainSupport.capabilities.maxImageCount;
    //         }

    //         VkSwapchainCreateInfoKHR createInfo{};
    //         createInfo.SType = StructureType.SWAPCHAIN_CREATE_INFO_KHR;
    //         createInfo.surface = surface;

    //         createInfo.minImageCount = imageCount;
    //         createInfo.imageFormat = surfaceFormat.format;
    //         createInfo.imageColorSpace = surfaceFormat.colorSpace;
    //         createInfo.imageExtent = extent;
    //         createInfo.imageArrayLayers = 1;
    //         createInfo.imageUsage = VK_IMAGE_USAGE_COLOR_ATTACHMENT_BIT;

    //         QueueFamilyIndices indices = findQueueFamilies(physicalDevice);
    //         uint32_t queueFamilyIndices[] = {indices.graphicsFamily.value(), indices.presentFamily.value()};

    //         if (indices.graphicsFamily != indices.presentFamily) {
    //             createInfo.imageSharingMode = VK_SHARING_MODE_CONCURRENT;
    //             createInfo.queueFamilyIndexCount = 2;
    //             createInfo.pQueueFamilyIndices = queueFamilyIndices;
    //         } else {
    //             createInfo.imageSharingMode = VK_SHARING_MODE_EXCLUSIVE;
    //         }

    //         createInfo.preTransform = swapChainSupport.capabilities.currentTransform;
    //         createInfo.compositeAlpha = VK_COMPOSITE_ALPHA_OPAQUE_BIT_KHR;
    //         createInfo.presentMode = presentMode;
    //         createInfo.clipped = VK_TRUE;

    //         createInfo.oldSwapchain = VK_NULL_HANDLE;

    //         if (vkCreateSwapchainKHR(device, &createInfo, null, &swapChain) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create swap chain!");
    //         }

    //         vkGetSwapchainImagesKHR(device, swapChain, &imageCount, null);
    //         swapChainImages.resize(imageCount);
    //         vkGetSwapchainImagesKHR(device, swapChain, &imageCount, swapChainImages.data());

    //         swapChainImageFormat = surfaceFormat.format;
    //         swapChainExtent = extent;
    //     }

    //     void createImageViews() {
    //         swapChainImageViews.resize(swapChainImages.size());

    //         for (size_t i = 0; i < swapChainImages.size(); i++) {
    //             VkImageViewCreateInfo createInfo{};
    //             createInfo.SType = StructureType.IMAGE_VIEW_CREATE_INFO;
    //             createInfo.image = swapChainImages[i];
    //             createInfo.viewType = VK_IMAGE_VIEW_TYPE_2D;
    //             createInfo.format = swapChainImageFormat;
    //             createInfo.components.r = VK_COMPONENT_SWIZZLE_IDENTITY;
    //             createInfo.components.g = VK_COMPONENT_SWIZZLE_IDENTITY;
    //             createInfo.components.b = VK_COMPONENT_SWIZZLE_IDENTITY;
    //             createInfo.components.a = VK_COMPONENT_SWIZZLE_IDENTITY;
    //             createInfo.subresourceRange.aspectMask = VK_IMAGE_ASPECT_COLOR_BIT;
    //             createInfo.subresourceRange.baseMipLevel = 0;
    //             createInfo.subresourceRange.levelCount = 1;
    //             createInfo.subresourceRange.baseArrayLayer = 0;
    //             createInfo.subresourceRange.layerCount = 1;

    //             if (vkCreateImageView(device, &createInfo, null, &swapChainImageViews[i]) != VK_SUCCESS) {
    //                 throw std::runtime_error("failed to create image views!");
    //             }
    //         }
    //     }

    //     void createRenderPass() {
    //         VkAttachmentDescription colorAttachment{};
    //         colorAttachment.format = swapChainImageFormat;
    //         colorAttachment.samples = VK_SAMPLE_COUNT_1_BIT;
    //         colorAttachment.loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR;
    //         colorAttachment.storeOp = VK_ATTACHMENT_STORE_OP_STORE;
    //         colorAttachment.stencilLoadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE;
    //         colorAttachment.stencilStoreOp = VK_ATTACHMENT_STORE_OP_DONT_CARE;
    //         colorAttachment.initialLayout = VK_IMAGE_LAYOUT_UNDEFINED;
    //         colorAttachment.finalLayout = VK_IMAGE_LAYOUT_PRESENT_SRC_KHR;

    //         VkAttachmentReference colorAttachmentRef{};
    //         colorAttachmentRef.attachment = 0;
    //         colorAttachmentRef.layout = VK_IMAGE_LAYOUT_COLOR_ATTACHMENT_OPTIMAL;

    //         VkSubpassDescription subpass{};
    //         subpass.pipelineBindPoint = VK_PIPELINE_BIND_POINT_GRAPHICS;
    //         subpass.colorAttachmentCount = 1;
    //         subpass.pColorAttachments = &colorAttachmentRef;

    //         VkSubpassDependency dependency{};
    //         dependency.srcSubpass = VK_SUBPASS_EXTERNAL;
    //         dependency.dstSubpass = 0;
    //         dependency.srcStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    //         dependency.srcAccessMask = 0;
    //         dependency.dstStageMask = VK_PIPELINE_STAGE_COLOR_ATTACHMENT_OUTPUT_BIT;
    //         dependency.dstAccessMask = VK_ACCESS_COLOR_ATTACHMENT_WRITE_BIT;

    //         VkRenderPassCreateInfo renderPassInfo{};
    //         renderPassInfo.SType = StructureType.RENDER_PASS_CREATE_INFO;
    //         renderPassInfo.attachmentCount = 1;
    //         renderPassInfo.pAttachments = &colorAttachment;
    //         renderPassInfo.subpassCount = 1;
    //         renderPassInfo.pSubpasses = &subpass;
    //         renderPassInfo.dependencyCount = 1;
    //         renderPassInfo.pDependencies = &dependency;

    //         if (vkCreateRenderPass(device, &renderPassInfo, null, &renderPass) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create render pass!");
    //         }
    //     }

    //     void createGraphicsPipeline() {
    //         auto vertShaderCode = readFile("shaders/vert.spv");
    //         auto fragShaderCode = readFile("shaders/frag.spv");

    //         VkShaderModule vertShaderModule = createShaderModule(vertShaderCode);
    //         VkShaderModule fragShaderModule = createShaderModule(fragShaderCode);

    //         VkPipelineShaderStageCreateInfo vertShaderStageInfo{};
    //         vertShaderStageInfo.SType = StructureType.PIPELINE_SHADER_STAGE_CREATE_INFO;
    //         vertShaderStageInfo.stage = VK_SHADER_STAGE_VERTEX_BIT;
    //         vertShaderStageInfo.module = vertShaderModule;
    //         vertShaderStageInfo.pName = "main";

    //         VkPipelineShaderStageCreateInfo fragShaderStageInfo{};
    //         fragShaderStageInfo.SType = StructureType.PIPELINE_SHADER_STAGE_CREATE_INFO;
    //         fragShaderStageInfo.stage = VK_SHADER_STAGE_FRAGMENT_BIT;
    //         fragShaderStageInfo.module = fragShaderModule;
    //         fragShaderStageInfo.pName = "main";

    //         VkPipelineShaderStageCreateInfo shaderStages[] = {vertShaderStageInfo, fragShaderStageInfo};

    //         VkPipelineVertexInputStateCreateInfo vertexInputInfo{};
    //         vertexInputInfo.SType = StructureType.PIPELINE_VERTEX_INPUT_STATE_CREATE_INFO;
    //         vertexInputInfo.vertexBindingDescriptionCount = 0;
    //         vertexInputInfo.vertexAttributeDescriptionCount = 0;

    //         VkPipelineInputAssemblyStateCreateInfo inputAssembly{};
    //         inputAssembly.SType = StructureType.PIPELINE_INPUT_ASSEMBLY_STATE_CREATE_INFO;
    //         inputAssembly.topology = VK_PRIMITIVE_TOPOLOGY_TRIANGLE_LIST;
    //         inputAssembly.primitiveRestartEnable = VK_FALSE;

    //         VkPipelineViewportStateCreateInfo viewportState{};
    //         viewportState.SType = StructureType.PIPELINE_VIEWPORT_STATE_CREATE_INFO;
    //         viewportState.viewportCount = 1;
    //         viewportState.scissorCount = 1;

    //         VkPipelineRasterizationStateCreateInfo rasterizer{};
    //         rasterizer.SType = StructureType.PIPELINE_RASTERIZATION_STATE_CREATE_INFO;
    //         rasterizer.depthClampEnable = VK_FALSE;
    //         rasterizer.rasterizerDiscardEnable = VK_FALSE;
    //         rasterizer.polygonMode = VK_POLYGON_MODE_FILL;
    //         rasterizer.lineWidth = 1.0f;
    //         rasterizer.cullMode = VK_CULL_MODE_BACK_BIT;
    //         rasterizer.frontFace = VK_FRONT_FACE_CLOCKWISE;
    //         rasterizer.depthBiasEnable = VK_FALSE;

    //         VkPipelineMultisampleStateCreateInfo multisampling{};
    //         multisampling.SType = StructureType.PIPELINE_MULTISAMPLE_STATE_CREATE_INFO;
    //         multisampling.sampleShadingEnable = VK_FALSE;
    //         multisampling.rasterizationSamples = VK_SAMPLE_COUNT_1_BIT;

    //         VkPipelineColorBlendAttachmentState colorBlendAttachment{};
    //         colorBlendAttachment.colorWriteMask = VK_COLOR_COMPONENT_R_BIT | VK_COLOR_COMPONENT_G_BIT | VK_COLOR_COMPONENT_B_BIT | VK_COLOR_COMPONENT_A_BIT;
    //         colorBlendAttachment.blendEnable = VK_FALSE;

    //         VkPipelineColorBlendStateCreateInfo colorBlending{};
    //         colorBlending.SType = StructureType.PIPELINE_COLOR_BLEND_STATE_CREATE_INFO;
    //         colorBlending.logicOpEnable = VK_FALSE;
    //         colorBlending.logicOp = VK_LOGIC_OP_COPY;
    //         colorBlending.attachmentCount = 1;
    //         colorBlending.pAttachments = &colorBlendAttachment;
    //         colorBlending.blendConstants[0] = 0.0f;
    //         colorBlending.blendConstants[1] = 0.0f;
    //         colorBlending.blendConstants[2] = 0.0f;
    //         colorBlending.blendConstants[3] = 0.0f;

    //         std::vector<VkDynamicState> dynamicStates = {
    //             VK_DYNAMIC_STATE_VIEWPORT,
    //             VK_DYNAMIC_STATE_SCISSOR
    //         };
    //         VkPipelineDynamicStateCreateInfo dynamicState{};
    //         dynamicState.SType = StructureType.PIPELINE_DYNAMIC_STATE_CREATE_INFO;
    //         dynamicState.dynamicStateCount = static_cast<uint32_t>(dynamicStates.size());
    //         dynamicState.pDynamicStates = dynamicStates.data();

    //         VkPipelineLayoutCreateInfo pipelineLayoutInfo{};
    //         pipelineLayoutInfo.SType = StructureType.PIPELINE_LAYOUT_CREATE_INFO;
    //         pipelineLayoutInfo.setLayoutCount = 0;
    //         pipelineLayoutInfo.pushConstantRangeCount = 0;

    //         if (vkCreatePipelineLayout(device, &pipelineLayoutInfo, null, &pipelineLayout) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create pipeline layout!");
    //         }

    //         VkGraphicsPipelineCreateInfo pipelineInfo{};
    //         pipelineInfo.SType = StructureType.GRAPHICS_PIPELINE_CREATE_INFO;
    //         pipelineInfo.stageCount = 2;
    //         pipelineInfo.pStages = shaderStages;
    //         pipelineInfo.pVertexInputState = &vertexInputInfo;
    //         pipelineInfo.pInputAssemblyState = &inputAssembly;
    //         pipelineInfo.pViewportState = &viewportState;
    //         pipelineInfo.pRasterizationState = &rasterizer;
    //         pipelineInfo.pMultisampleState = &multisampling;
    //         pipelineInfo.pColorBlendState = &colorBlending;
    //         pipelineInfo.pDynamicState = &dynamicState;
    //         pipelineInfo.layout = pipelineLayout;
    //         pipelineInfo.renderPass = renderPass;
    //         pipelineInfo.subpass = 0;
    //         pipelineInfo.basePipelineHandle = VK_NULL_HANDLE;

    //         if (vkCreateGraphicsPipelines(device, VK_NULL_HANDLE, 1, &pipelineInfo, null, &graphicsPipeline) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create graphics pipeline!");
    //         }

    //         vkDestroyShaderModule(device, fragShaderModule, null);
    //         vkDestroyShaderModule(device, vertShaderModule, null);
    //     }

    //     void createFramebuffers() {
    //         swapChainFramebuffers.resize(swapChainImageViews.size());

    //         for (size_t i = 0; i < swapChainImageViews.size(); i++) {
    //             VkImageView attachments[] = {
    //                 swapChainImageViews[i]
    //             };

    //             VkFramebufferCreateInfo framebufferInfo{};
    //             framebufferInfo.SType = StructureType.FRAMEBUFFER_CREATE_INFO;
    //             framebufferInfo.renderPass = renderPass;
    //             framebufferInfo.attachmentCount = 1;
    //             framebufferInfo.pAttachments = attachments;
    //             framebufferInfo.width = swapChainExtent.width;
    //             framebufferInfo.height = swapChainExtent.height;
    //             framebufferInfo.layers = 1;

    //             if (vkCreateFramebuffer(device, &framebufferInfo, null, &swapChainFramebuffers[i]) != VK_SUCCESS) {
    //                 throw std::runtime_error("failed to create framebuffer!");
    //             }
    //         }
    //     }

    //     void createCommandPool() {
    //         QueueFamilyIndices queueFamilyIndices = findQueueFamilies(physicalDevice);

    //         VkCommandPoolCreateInfo poolInfo{};
    //         poolInfo.SType = StructureType.COMMAND_POOL_CREATE_INFO;
    //         poolInfo.flags = VK_COMMAND_POOL_CREATE_RESET_COMMAND_BUFFER_BIT;
    //         poolInfo.queueFamilyIndex = queueFamilyIndices.graphicsFamily.value();

    //         if (vkCreateCommandPool(device, &poolInfo, null, &commandPool) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create command pool!");
    //         }
    //     }

    //     void createCommandBuffer() {
    //         VkCommandBufferAllocateInfo allocInfo{};
    //         allocInfo.SType = StructureType.COMMAND_BUFFER_ALLOCATE_INFO;
    //         allocInfo.commandPool = commandPool;
    //         allocInfo.level = VK_COMMAND_BUFFER_LEVEL_PRIMARY;
    //         allocInfo.commandBufferCount = 1;

    //         if (vkAllocateCommandBuffers(device, &allocInfo, &commandBuffer) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to allocate command buffers!");
    //         }
    //     }

    //     void recordCommandBuffer(VkCommandBuffer commandBuffer, uint32_t imageIndex) {
    //         VkCommandBufferBeginInfo beginInfo{};
    //         beginInfo.SType = StructureType.COMMAND_BUFFER_BEGIN_INFO;

    //         if (vkBeginCommandBuffer(commandBuffer, &beginInfo) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to begin recording command buffer!");
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

    //         if (vkEndCommandBuffer(commandBuffer) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to record command buffer!");
    //         }
    //     }

    //     void createSyncObjects() {
    //         VkSemaphoreCreateInfo semaphoreInfo{};
    //         semaphoreInfo.SType = StructureType.SEMAPHORE_CREATE_INFO;

    //         VkFenceCreateInfo fenceInfo{};
    //         fenceInfo.SType = StructureType.FENCE_CREATE_INFO;
    //         fenceInfo.flags = VK_FENCE_CREATE_SIGNALED_BIT;

    //         if (vkCreateSemaphore(device, &semaphoreInfo, null, &imageAvailableSemaphore) != VK_SUCCESS ||
    //             vkCreateSemaphore(device, &semaphoreInfo, null, &renderFinishedSemaphore) != VK_SUCCESS ||
    //             vkCreateFence(device, &fenceInfo, null, &inFlightFence) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create synchronization objects for a frame!");
    //         }

    //     }

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

    //         if (vkQueueSubmit(graphicsQueue, 1, &submitInfo, inFlightFence) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to submit draw command buffer!");
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

    //     VkShaderModule createShaderModule(const std::vector<char>& code) {
    //         VkShaderModuleCreateInfo createInfo{};
    //         createInfo.SType = StructureType.SHADER_MODULE_CREATE_INFO;
    //         createInfo.codeSize = code.size();
    //         createInfo.pCode = reinterpret_cast<const uint32_t*>(code.data());

    //         VkShaderModule shaderModule;
    //         if (vkCreateShaderModule(device, &createInfo, null, &shaderModule) != VK_SUCCESS) {
    //             throw std::runtime_error("failed to create shader module!");
    //         }

    //         return shaderModule;
    //     }

    //     VkSurfaceFormatKHR chooseSwapSurfaceFormat(const std::vector<VkSurfaceFormatKHR>& availableFormats) {
    //         for (const auto& availableFormat : availableFormats) {
    //             if (availableFormat.format == VK_FORMAT_B8G8R8A8_SRGB && availableFormat.colorSpace == VK_COLOR_SPACE_SRGB_NONLINEAR_KHR) {
    //                 return availableFormat;
    //             }
    //         }

    //         return availableFormats[0];
    //     }

    //     VkPresentModeKHR chooseSwapPresentMode(const std::vector<VkPresentModeKHR>& availablePresentModes) {
    //         for (const auto& availablePresentMode : availablePresentModes) {
    //             if (availablePresentMode == VK_PRESENT_MODE_MAILBOX_KHR) {
    //                 return availablePresentMode;
    //             }
    //         }

    //         return VK_PRESENT_MODE_FIFO_KHR;
    //     }

    //     VkExtent2D chooseSwapExtent(const VkSurfaceCapabilitiesKHR& capabilities) {
    //         if (capabilities.currentExtent.width != std::numeric_limits<uint32_t>::max()) {
    //             return capabilities.currentExtent;
    //         } else {
    //             int width, height;
    //             glfwGetFramebufferSize(window, &width, &height);

    //             VkExtent2D actualExtent = {
    //                 static_cast<uint32_t>(width),
    //                 static_cast<uint32_t>(height)
    //             };

    //             actualExtent.width = std::clamp(actualExtent.width, capabilities.minImageExtent.width, capabilities.maxImageExtent.width);
    //             actualExtent.height = std::clamp(actualExtent.height, capabilities.minImageExtent.height, capabilities.maxImageExtent.height);

    //             return actualExtent;
    //         }
    //     }

    //     SwapChainSupportDetails querySwapChainSupport(VkPhysicalDevice device) {
    //         SwapChainSupportDetails details;

    //         vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, surface, &details.capabilities);

    //         uint32_t formatCount;
    //         vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, null);

    //         if (formatCount != 0) {
    //             details.formats.resize(formatCount);
    //             vkGetPhysicalDeviceSurfaceFormatsKHR(device, surface, &formatCount, details.formats.data());
    //         }

    //         uint32_t presentModeCount;
    //         vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModeCount, null);

    //         if (presentModeCount != 0) {
    //             details.presentModes.resize(presentModeCount);
    //             vkGetPhysicalDeviceSurfacePresentModesKHR(device, surface, &presentModeCount, details.presentModes.data());
    //         }

    //         return details;
    //     }

    //     bool isDeviceSuitable(VkPhysicalDevice device) {
    //         QueueFamilyIndices indices = findQueueFamilies(device);

    //         bool extensionsSupported = checkDeviceExtensionSupport(device);

    //         bool swapChainAdequate = false;
    //         if (extensionsSupported) {
    //             SwapChainSupportDetails swapChainSupport = querySwapChainSupport(device);
    //             swapChainAdequate = !swapChainSupport.formats.empty() && !swapChainSupport.presentModes.empty();
    //         }

    //         return indices.isComplete() && extensionsSupported && swapChainAdequate;
    //     }

    //     bool checkDeviceExtensionSupport(VkPhysicalDevice device) {
    //         uint32_t extensionCount;
    //         vkEnumerateDeviceExtensionProperties(device, null, &extensionCount, null);

    //         std::vector<VkExtensionProperties> availableExtensions(extensionCount);
    //         vkEnumerateDeviceExtensionProperties(device, null, &extensionCount, availableExtensions.data());

    //         std::set<std::string> requiredExtensions(deviceExtensions.begin(), deviceExtensions.end());

    //         for (const auto& extension : availableExtensions) {
    //             requiredExtensions.erase(extension.extensionName);
    //         }

    //         return requiredExtensions.empty();
    //     }

    //     QueueFamilyIndices findQueueFamilies(VkPhysicalDevice device) {
    //         QueueFamilyIndices indices;

    //         uint32_t queueFamilyCount = 0;
    //         vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

    //         std::vector<VkQueueFamilyProperties> queueFamilies(queueFamilyCount);
    //         vkGetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies.data());

    //         int i = 0;
    //         for (const auto& queueFamily : queueFamilies) {
    //             if (queueFamily.queueFlags & VK_QUEUE_GRAPHICS_BIT) {
    //                 indices.graphicsFamily = i;
    //             }

    //             VkBool32 presentSupport = false;
    //             vkGetPhysicalDeviceSurfaceSupportKHR(device, i, surface, &presentSupport);

    //             if (presentSupport) {
    //                 indices.presentFamily = i;
    //             }

    //             if (indices.isComplete()) {
    //                 break;
    //             }

    //             i++;
    //         }

    //         return indices;
    //     }

    bool checkValidationLayerSupport()
    {
        uint layerCount;
        vk.EnumerateInstanceLayerProperties(&layerCount, null);

        var availableLayers = stackalloc LayerProperties[(int)layerCount];
        vk.EnumerateInstanceLayerProperties(&layerCount, availableLayers);

        // foreach (var layerName in validationLayers)
        {
            bool layerFound = false;

            for (uint i = 0; i < layerCount; ++i)
            {
                // find zero
                int j=0;
                for(; j<256; ++j)
                {
                    if (availableLayers[i].LayerName[j] == 0)
                    {
                        break;
                    }
                }
                var availableLayerName = new ReadOnlySpan<byte>(
                    availableLayers[i].LayerName,
                    j
                );
                if (validationLayer.AsSpan().SequenceEqual<byte>(availableLayerName))
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
    //             throw std::runtime_error("failed to open file!");
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
