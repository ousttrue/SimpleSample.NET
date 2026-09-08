// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

unsafe class VulkanInstanceAndDevice : IDisposable
{
    static bool IsExtensionAvailable(
        ReadOnlySpan<VkExtensionProperties> properties,
        string extension
    )
    {
        foreach (var p in properties)
            if (Marshal.PtrToStringAnsi((nint)p.extensionName) == extension)
                return true;
        return false;
    }

    [UnmanagedCallersOnly]
    private static uint debug_report(
        uint flags,
        VkDebugReportObjectTypeEXT objectType,
        ulong _object,
        nuint location,
        int messageCode,
        byte* pLayerPrefix,
        byte* pMessage,
        void* pUserData
    )
    {
        // (void)flags; (void)object; (void)location; (void)messageCode; (void)pUserData; (void)pLayerPrefix; // Unused arguments
        Console.Error.WriteLine($"[vulkan] Debug report from ObjectType: {objectType}");
        Console.Error.WriteLine(
            $"Message: {Marshal.PtrToStringAnsi((nint)pMessage) ?? throw new Exception()}"
        );
        return VK_FALSE;
    }

    [UnmanagedCallersOnly]
    private static unsafe uint DebugCallback(
        VkDebugUtilsMessageSeverityFlagsEXT messageSeverity,
        VkDebugUtilsMessageTypeFlagsEXT messageTypes,
        VkDebugUtilsMessengerCallbackDataEXT* pCallbackData,
        void* pUserData
    )
    {
        System.Diagnostics.Debug.WriteLine(
            $"validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->pMessage)
        );

        return VK_FALSE;
    }

    static VkPhysicalDevice ImGui_ImplVulkanH_SelectPhysicalDevice(
        VkInstanceApi api,
        VkInstance instance
    )
    {
        uint gpu_count;
        api.vkEnumeratePhysicalDevices(&gpu_count, null).ThrowIfError();
        if (gpu_count == 0)
        {
            throw new Exception("no gpu");
        }

        Span<VkPhysicalDevice> gpus = stackalloc VkPhysicalDevice[(int)gpu_count];
        api.vkEnumeratePhysicalDevices(gpus).ThrowIfError();

        // If a number >1 of GPUs got reported, find discrete GPU if present, or use first one available. This covers
        // most common cases (multi-gpu/integrated+dedicated graphics). Handling more complicated setups (multiple
        // dedicated GPUs) is out of scope of this sample.
        for (int i = 0; i < gpu_count; ++i)
        {
            var gpu = gpus[i];
            api.vkGetPhysicalDeviceProperties(gpu, out var properties);
            if (properties.deviceType == VkPhysicalDeviceType.DiscreteGpu)
                return gpu;
        }

        // Use first GPU (Integrated) is a Discrete one is not available.
        if (gpu_count > 0)
            return gpus[0];

        throw new Exception("ImGui_ImplVulkanH_SelectPhysicalDevice");
    }

    static uint ImGui_ImplVulkanH_SelectQueueFamilyIndex(
        VkInstanceApi api,
        VkPhysicalDevice physical_device
    )
    {
        uint count;
        api.vkGetPhysicalDeviceQueueFamilyProperties(physical_device, &count, null);
        Span<VkQueueFamilyProperties> queues_properties =
            stackalloc VkQueueFamilyProperties[(int)count];
        api.vkGetPhysicalDeviceQueueFamilyProperties(physical_device, queues_properties);
        for (int i = 0; i < count; i++)
            if (queues_properties[i].queueFlags.HasFlag(VkQueueFlags.Graphics))
                return (uint)i;

        throw new Exception("graphics queue family is not found");
    }

    private readonly VkInstanceApi _vi;
    private readonly VkDeviceApi _vd;
    public (VkInstanceApi Instance, VkDeviceApi Device) Api => (_vi, _vd);

    public readonly VkInstance Instance;

    // private readonly ExtDebugReport extDebugReport;
    // private readonly DebugReportCallbackEXT g_DebugReport;
    // private readonly ExtDebugUtils extDebugUtils;
    private readonly VkDebugUtilsMessengerEXT debugMessenger;

    public readonly VkPhysicalDevice PhysicalDevice;
    public readonly uint QueueFamily = uint.MaxValue;
    public readonly VkQueue Queue;

    public readonly VkDevice Device;

    // Backend uses a small number of descriptors per font atlas + as many as additional calls done to ImGui_ImplVulkan_AddTexture().
    const uint IMGUI_IMPL_VULKAN_MINIMUM_SAMPLED_IMAGE_POOL_SIZE = 8; // Minimum per atlas
    public const uint IMGUI_IMPL_VULKAN_MINIMUM_SAMPLER_POOL_SIZE = 2; // Minimum for linear + nearest
    public readonly VkDescriptorPool DescriptorPool;

    public VulkanInstanceAndDevice(
        ByteStringArrayAllocator instance_extensions,
        bool useDynamicRendering
    )
    {
        var layers = new ByteStringArrayAllocator() { "VK_LAYER_KHRONOS_validation" };

        // Create Vulkan Instance
        {
            VkApplicationInfo appInfo = new()
            {
                sType = VK_STRUCTURE_TYPE_APPLICATION_INFO,
                pApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
                applicationVersion = new VkVersion(1, 0, 0),
                pEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
                engineVersion = new VkVersion(1, 0, 0),
                apiVersion = VK_API_VERSION_1_3, // requierd for DynamicRendering
            };

            var create_info = new VkInstanceCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO,
                pApplicationInfo = &appInfo,
            };

            // Enumerate available extensions
            uint properties_count;
            vkEnumerateInstanceExtensionProperties((byte*)null, &properties_count, null);
            Span<VkExtensionProperties> properties =
                stackalloc VkExtensionProperties[(int)properties_count];
            vkEnumerateInstanceExtensionProperties(properties).ThrowIfError();

            // Enable required extensions
            if (
                IsExtensionAvailable(
                    properties,
                    Encoding.ASCII.GetString(VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME)
                        ?? throw new Exception()
                )
            )
                instance_extensions.Add(VK_KHR_GET_PHYSICAL_DEVICE_PROPERTIES_2_EXTENSION_NAME);
            // #ifdef VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME
            //         if (IsExtensionAvailable(properties, VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME))
            //         {
            //             instance_extensions.push_back(VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME);
            //             create_info.flags |= VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR;
            //         }
            // #endif

            // Enabling validation layers
            (create_info.enabledLayerCount, create_info.ppEnabledLayerNames) = layers;
            instance_extensions.Add(VK_EXT_DEBUG_REPORT_EXTENSION_NAME);
            instance_extensions.Add(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);

            // Create Vulkan Instance
            (create_info.enabledExtensionCount, create_info.ppEnabledExtensionNames) =
                instance_extensions;
            vkCreateInstance(&create_info, default, out Instance).ThrowIfError();
            _vi = new VkInstanceApi(Instance);

#if false
            if (!vk.TryGetInstanceExtension(Instance, out extDebugReport))
            {
                throw new Exception("TryGetInstanceExtension<ExtDebugReport>");
            }
            // Setup the debug report callback
            var debug_report_ci = new DebugReportCallbackCreateInfoEXT
            {
                SType = VK_STRUCTURE_TYPE_DebugReportCallbackCreateInfoExt,
                Flags =
                    DebugReportFlagsEXT.ErrorBitExt
                    | DebugReportFlagsEXT.WarningBitExt
                    | DebugReportFlagsEXT.PerformanceWarningBitExt,
                PfnCallback = (DebugReportCallbackFunctionEXT)debug_report,
                PUserData = null,
            };
            extDebugReport
                .CreateDebugReportCallback(Instance, &debug_report_ci, default, out g_DebugReport)
                .ThrowIfError();
#endif

            var createInfo = new VkDebugUtilsMessengerCreateInfoEXT
            {
                sType = VK_STRUCTURE_TYPE_DEBUG_UTILS_MESSENGER_CREATE_INFO_EXT,
                messageSeverity =
                    VkDebugUtilsMessageSeverityFlagsEXT.Verbose
                    | VkDebugUtilsMessageSeverityFlagsEXT.Warning
                    | VkDebugUtilsMessageSeverityFlagsEXT.Error,
                messageType =
                    VkDebugUtilsMessageTypeFlagsEXT.General
                    | VkDebugUtilsMessageTypeFlagsEXT.Performance
                    | VkDebugUtilsMessageTypeFlagsEXT.Validation,
                pfnUserCallback = &DebugCallback,
            };
            _vi.vkCreateDebugUtilsMessengerEXT(&createInfo, default, out debugMessenger);
        }

        // Select Physical Device (GPU)
        PhysicalDevice = ImGui_ImplVulkanH_SelectPhysicalDevice(_vi, Instance);

        // Select graphics queue family
        QueueFamily = ImGui_ImplVulkanH_SelectQueueFamilyIndex(_vi, PhysicalDevice);

        // Create Logical Device (with 1 queue)
        {
            var device_extensions = new ByteStringArrayAllocator() { "VK_KHR_swapchain" };

            // Enumerate physical device extension
            uint properties_count;
            _vi.vkEnumerateDeviceExtensionProperties(
                PhysicalDevice,
                (byte*)null,
                &properties_count,
                null
            );
            Span<VkExtensionProperties> properties =
                stackalloc VkExtensionProperties[(int)properties_count];
            _vi.vkEnumerateDeviceExtensionProperties(PhysicalDevice, properties);
            // #ifdef VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME
            //         if (IsExtensionAvailable(properties, VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME))
            //             device_extensions.push_back(VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME);
            // #endif

            var queue_priority = stackalloc float[] { 1.0f };
            var queue_info = stackalloc VkDeviceQueueCreateInfo[1];
            queue_info[0].sType = VK_STRUCTURE_TYPE_DEVICE_QUEUE_CREATE_INFO;
            queue_info[0].queueFamilyIndex = QueueFamily;
            queue_info[0].queueCount = 1;
            queue_info[0].pQueuePriorities = queue_priority;
            var create_info = new VkDeviceCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_DEVICE_CREATE_INFO,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = queue_info,
            };
            // (create_info.EnabledLayerCount, create_info.PpEnabledLayerNames) = layers;
            (create_info.enabledExtensionCount, create_info.ppEnabledExtensionNames) =
                device_extensions;

            if (useDynamicRendering)
            {
                var ext_feature = new VkPhysicalDeviceDynamicRenderingFeatures()
                {
                    sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DYNAMIC_RENDERING_FEATURES,
                };
                var physical_features2 = new VkPhysicalDeviceFeatures2
                {
                    sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2,
                    pNext = &ext_feature,
                };
                _vi.vkGetPhysicalDeviceFeatures2(PhysicalDevice, &physical_features2);
                if (!ext_feature.dynamicRendering)
                {
                    throw new Exception();
                }
                create_info.pNext = &physical_features2;
            }

            _vi.vkCreateDevice(PhysicalDevice, &create_info, default, out Device).ThrowIfError();
            _vd = new VkDeviceApi(_vi, Device);
            _vd.vkGetDeviceQueue(QueueFamily, 0, out Queue);
        }

        // Create Descriptor Pool
        // If you wish to load e.g. additional textures you may need to alter pools sizes and maxSets.
        {
            var pool_sizes = stackalloc VkDescriptorPoolSize[]
            {
                new VkDescriptorPoolSize
                {
                    type = VkDescriptorType.SampledImage,
                    descriptorCount = IMGUI_IMPL_VULKAN_MINIMUM_SAMPLED_IMAGE_POOL_SIZE,
                },
                new VkDescriptorPoolSize
                {
                    type = VkDescriptorType.Sampler,
                    descriptorCount = IMGUI_IMPL_VULKAN_MINIMUM_SAMPLER_POOL_SIZE,
                },
            };
            var pool_info = new VkDescriptorPoolCreateInfo
            {
                sType = VK_STRUCTURE_TYPE_DESCRIPTOR_POOL_CREATE_INFO,
                flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet,
                maxSets = 0,
            };
            for (int i = 0; i < 2; ++i)
                pool_info.maxSets += pool_sizes[i].descriptorCount;
            pool_info.poolSizeCount = 2;
            pool_info.pPoolSizes = pool_sizes;
            _vd.vkCreateDescriptorPool(&pool_info, default, out DescriptorPool).ThrowIfError();
        }
    }

    public void Dispose()
    {
        _vd.vkDestroyDescriptorPool(DescriptorPool, default);

        // Remove the debug report callback
        // extDebugReport.DestroyDebugReportCallback(Instance, g_DebugReport, default);
        _vi.vkDestroyDebugUtilsMessengerEXT(debugMessenger, default);

        _vd.vkDestroyDevice(default);
        _vi.vkDestroyInstance(default);
    }
}
