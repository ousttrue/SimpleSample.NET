// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

public class DeviceObject : IDisposable
{
    public static readonly string[] DeviceExtensions =
    [
        Encoding.ASCII.GetString(VK_KHR_SWAPCHAIN_EXTENSION_NAME),
    ];

    private readonly VkDevice device;
    public readonly VkDeviceApi Api;
    public readonly VkQueue GraphicsQueue;
    public readonly VkQueue PresentQueue;

    public unsafe DeviceObject(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        uint graphicsFamily,
        uint presentFamily
    )
    {
        var uniqueQueueFamilies = new HashSet<uint>() { graphicsFamily, presentFamily };
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

        ByteStringArrayAllocator extensions = [.. DeviceExtensions];
        (createInfo.enabledExtensionCount, createInfo.ppEnabledExtensionNames) = extensions;

        ByteStringArrayAllocator layers = [.. InstanceObject.ValidationLayers];
        if (InstanceObject.EnableValidationLayers)
        {
            (createInfo.enabledLayerCount, createInfo.ppEnabledLayerNames) = layers;
        }

        if (vki.vkCreateDevice(physicalDevice, &createInfo, null, out device) != VK_SUCCESS)
        {
            throw new Exception("failed to create logical device!");
        }
        Api = new VkDeviceApi(vki, device);

        Api.vkGetDeviceQueue(graphicsFamily, 0, out GraphicsQueue);
        Api.vkGetDeviceQueue(presentFamily, 0, out PresentQueue);
    }

    public unsafe void Dispose()
    {
        Api.vkDestroyDevice(null);
    }
}
