// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

static class Program
{
    public static unsafe void Main()
    {
        using var window = new GlfwWindow();

        using var instance = new InstanceObject(window);
        var physicalDevice = instance.pickPhysicalDevice(DeviceObject.DeviceExtensions);

        var deviceProperties = new VkPhysicalDeviceProperties2
        {
            sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
        };
        instance.Api.vkGetPhysicalDeviceProperties2(physicalDevice, &deviceProperties);
        Console.Error.WriteLine(
            $"Selected device: {Marshal.PtrToStringAnsi((nint)deviceProperties.properties.deviceName) ?? throw new Exception()}"
        );

        var indices = QueueFamilyIndices.findQueueFamilies(
            instance.Api,
            physicalDevice,
            instance.Surface
        );
        using var device = new DeviceObject(
            instance.Api,
            physicalDevice,
            indices.GraphicsFamily,
            indices.PresentFamily
        );
        using var swapchain = new SwapchainObject(
            instance.Api,
            physicalDevice,
            instance.Surface,
            device.Api,
            new()
        );
        using var pipeline = new PipelineObject(device.Api, swapchain.RenderPass);

        while (true)
        {
            if (!window.NextFrame())
            {
                break;
            }
            var (imageIndex, commandBuffer) = swapchain.Acquire();
            pipeline.RecordCommandBuffer(commandBuffer, swapchain.Extent);
            swapchain.Present(imageIndex);
        }

        device.Api.vkDeviceWaitIdle();
    }
}
