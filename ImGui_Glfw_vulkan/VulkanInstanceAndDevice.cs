// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;

unsafe class VulkanInstanceAndDevice : IDisposable
{
    static bool IsExtensionAvailable(ReadOnlySpan<ExtensionProperties> properties, string extension)
    {
        foreach (var p in properties)
            if (Marshal.PtrToStringAnsi((nint)p.ExtensionName) == extension)
                return true;
        return false;
    }

    private static uint debug_report(
        uint flags,
        DebugReportObjectTypeEXT objectType,
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
        return Vk.False;
    }

    static PhysicalDevice ImGui_ImplVulkanH_SelectPhysicalDevice(Vk vk, Instance instance)
    {
        uint gpu_count;
        vk.EnumeratePhysicalDevices(instance, &gpu_count, null).ThrowIfError();
        if (gpu_count == 0)
        {
            throw new Exception("no gpu");
        }

        var gpus = stackalloc PhysicalDevice[(int)gpu_count];
        vk.EnumeratePhysicalDevices(instance, &gpu_count, gpus).ThrowIfError();

        // If a number >1 of GPUs got reported, find discrete GPU if present, or use first one available. This covers
        // most common cases (multi-gpu/integrated+dedicated graphics). Handling more complicated setups (multiple
        // dedicated GPUs) is out of scope of this sample.
        for (int i = 0; i < gpu_count; ++i)
        {
            var device = gpus[i];
            vk.GetPhysicalDeviceProperties(device, out var properties);
            if (properties.DeviceType == PhysicalDeviceType.DiscreteGpu)
                return device;
        }

        // Use first GPU (Integrated) is a Discrete one is not available.
        if (gpu_count > 0)
            return gpus[0];

        throw new Exception("ImGui_ImplVulkanH_SelectPhysicalDevice");
    }

    static uint ImGui_ImplVulkanH_SelectQueueFamilyIndex(Vk vk, PhysicalDevice physical_device)
    {
        uint count;
        vk.GetPhysicalDeviceQueueFamilyProperties(physical_device, &count, null);
        var queues_properties = stackalloc QueueFamilyProperties[(int)count];
        vk.GetPhysicalDeviceQueueFamilyProperties(physical_device, &count, queues_properties);
        for (uint i = 0; i < count; i++)
            if (queues_properties[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                return i;

        throw new Exception("graphics queue family is not found");
    }

    private readonly Vk vk;

    public readonly Instance Instance;

    private readonly ExtDebugReport extDebugReport;
    private readonly DebugReportCallbackEXT g_DebugReport;

    public readonly PhysicalDevice PhysicalDevice;
    public readonly uint QueueFamily = uint.MaxValue;
    public readonly Queue Queue;

    public readonly Device Device;

    // Backend uses a small number of descriptors per font atlas + as many as additional calls done to ImGui_ImplVulkan_AddTexture().
    const uint IMGUI_IMPL_VULKAN_MINIMUM_SAMPLED_IMAGE_POOL_SIZE = 8; // Minimum per atlas
    public const uint IMGUI_IMPL_VULKAN_MINIMUM_SAMPLER_POOL_SIZE = 2; // Minimum for linear + nearest
    public readonly DescriptorPool DescriptorPool;

    public VulkanInstanceAndDevice(Vk _vk, ByteStringArrayAllocator instance_extensions)
    {
        vk = _vk;

        // Create Vulkan Instance
        {
            var create_info = new InstanceCreateInfo { SType = StructureType.InstanceCreateInfo };

            // Enumerate available extensions
            uint properties_count;
            vk.EnumerateInstanceExtensionProperties((byte*)null, &properties_count, null);
            var properties = stackalloc ExtensionProperties[(int)properties_count];
            vk.EnumerateInstanceExtensionProperties((byte*)null, &properties_count, properties)
                .ThrowIfError();

            // Enable required extensions
            if (
                IsExtensionAvailable(
                    new ReadOnlySpan<ExtensionProperties>(properties, (int)properties_count),
                    KhrGetPhysicalDeviceProperties2.ExtensionName
                )
            )
                instance_extensions.Add(KhrGetPhysicalDeviceProperties2.ExtensionName);
            // #ifdef VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME
            //         if (IsExtensionAvailable(properties, VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME))
            //         {
            //             instance_extensions.push_back(VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME);
            //             create_info.flags |= VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR;
            //         }
            // #endif

            // Enabling validation layers
            var layers = new ByteStringArrayAllocator() { "VK_LAYER_KHRONOS_validation" };
            (create_info.EnabledLayerCount, create_info.PpEnabledLayerNames) = layers;
            instance_extensions.Add("VK_EXT_debug_report");

            // Create Vulkan Instance
            (create_info.EnabledExtensionCount, create_info.PpEnabledExtensionNames) =
                instance_extensions;
            vk.CreateInstance(&create_info, default, out Instance).ThrowIfError();

            if (!vk.TryGetInstanceExtension(Instance, out extDebugReport))
            {
                throw new Exception("TryGetInstanceExtension<ExtDebugReport>");
            }
            // Setup the debug report callback
            var debug_report_ci = new DebugReportCallbackCreateInfoEXT
            {
                SType = StructureType.DebugReportCallbackCreateInfoExt,
                Flags =
                    DebugReportFlagsEXT.ErrorBitExt
                    | DebugReportFlagsEXT.WarningBitExt
                    | DebugReportFlagsEXT.PerformanceWarningBitExt,
                PfnCallback = (DebugReportCallbackFunctionEXT)debug_report,
                PUserData = null,
            };
            extDebugReport
                .CreateDebugReportCallback(
                    Instance,
                    &debug_report_ci,
                    default,
                    out g_DebugReport
                )
                .ThrowIfError();
        }

        // Select Physical Device (GPU)
        PhysicalDevice = ImGui_ImplVulkanH_SelectPhysicalDevice(vk, Instance);

        // Select graphics queue family
        QueueFamily = ImGui_ImplVulkanH_SelectQueueFamilyIndex(vk, PhysicalDevice);

        // Create Logical Device (with 1 queue)
        {
            var device_extensions = new ByteStringArrayAllocator() { "VK_KHR_swapchain" };

            // Enumerate physical device extension
            uint properties_count;
            vk.EnumerateDeviceExtensionProperties(
                PhysicalDevice,
                (byte*)null,
                &properties_count,
                null
            );
            var properties = stackalloc ExtensionProperties[(int)properties_count];
            vk.EnumerateDeviceExtensionProperties(
                PhysicalDevice,
                (byte*)null,
                &properties_count,
                properties
            );
            // #ifdef VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME
            //         if (IsExtensionAvailable(properties, VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME))
            //             device_extensions.push_back(VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME);
            // #endif

            var queue_priority = stackalloc float[] { 1.0f };
            var queue_info = stackalloc DeviceQueueCreateInfo[1];
            queue_info[0].SType = StructureType.DeviceQueueCreateInfo;
            queue_info[0].QueueFamilyIndex = QueueFamily;
            queue_info[0].QueueCount = 1;
            queue_info[0].PQueuePriorities = queue_priority;
            var create_info = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = 1,
                PQueueCreateInfos = queue_info,
            };
            (create_info.EnabledExtensionCount, create_info.PpEnabledExtensionNames) =
                device_extensions;

            vk.CreateDevice(PhysicalDevice, &create_info, default, out Device).ThrowIfError();
            vk.GetDeviceQueue(Device, QueueFamily, 0, out Queue);
        }

        // Create Descriptor Pool
        // If you wish to load e.g. additional textures you may need to alter pools sizes and maxSets.
        {
            var pool_sizes = stackalloc DescriptorPoolSize[]
            {
                new DescriptorPoolSize
                {
                    Type = DescriptorType.SampledImage,
                    DescriptorCount = IMGUI_IMPL_VULKAN_MINIMUM_SAMPLED_IMAGE_POOL_SIZE,
                },
                new DescriptorPoolSize
                {
                    Type = DescriptorType.Sampler,
                    DescriptorCount = IMGUI_IMPL_VULKAN_MINIMUM_SAMPLER_POOL_SIZE,
                },
            };
            var pool_info = new DescriptorPoolCreateInfo
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                Flags = DescriptorPoolCreateFlags.FreeDescriptorSetBit,
                MaxSets = 0,
            };
            for (int i = 0; i < 2; ++i)
                pool_info.MaxSets += pool_sizes[i].DescriptorCount;
            pool_info.PoolSizeCount = 2;
            pool_info.PPoolSizes = pool_sizes;
            vk.CreateDescriptorPool(Device, &pool_info, default, out DescriptorPool)
                .ThrowIfError();
        }
    }

    public void Dispose()
    {
        vk.DestroyDescriptorPool(Device, DescriptorPool, default);

        // Remove the debug report callback
        extDebugReport.DestroyDebugReportCallback(Instance, g_DebugReport, default);

        vk.DestroyDevice(Device, default);
        vk.DestroyInstance(Instance, default);
    }
}
