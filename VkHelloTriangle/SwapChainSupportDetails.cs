// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using Vortice.Vulkan;

record struct SwapChainSupportDetails(
    VkSurfaceCapabilitiesKHR capabilities,
    VkSurfaceFormatKHR[] formats,
    VkPresentModeKHR[] presentModes
)
{
    public static SwapChainSupportDetails querySwapChainSupport(
        VkInstanceApi vki,
        VkPhysicalDevice physicalDevice,
        VkSurfaceKHR surface
    )
    {
        vki.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(
            physicalDevice,
            surface,
            out var capabilities
        );

        vki.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, out var formatCount);
        if (formatCount == 0)
        {
            throw new Exception("No surface format");
        }
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[(int)formatCount];
        vki.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, formats);

        vki.vkGetPhysicalDeviceSurfacePresentModesKHR(
            physicalDevice,
            surface,
            out var presentModeCount
        );
        if (presentModeCount == 0)
        {
            throw new Exception("No present mode");
        }
        Span<VkPresentModeKHR> presentModes = stackalloc VkPresentModeKHR[(int)presentModeCount];
        vki.vkGetPhysicalDeviceSurfacePresentModesKHR(
            physicalDevice,
            surface,
            presentModes
        );

        return new(capabilities, formats.ToArray(), presentModes.ToArray());
    }
};
