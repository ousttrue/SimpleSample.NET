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
            window.GetExtent()
        );
        using var renderTarget = new RenderTarget(
            device.Api,
            indices.GraphicsFamily,
            swapchain.Format,
            swapchain.Extent,
            swapchain.Images
        );
        using var pipeline = new PipelineObject(device.Api, renderTarget.RenderPass);

        while (true)
        {
            if (!window.NextFrame())
            {
                break;
            }
            var (imageIndex, imageAvailableSemaphore, inFlightFence) = swapchain.Acquire();
            var (commandBuffer, renderFinishedSemaphore) = renderTarget.BeginRenderPass(imageIndex);
            {
                pipeline.RecordCommandBuffer(commandBuffer, renderTarget.Extent);
            }
            renderTarget.EndRenderPass(imageAvailableSemaphore, inFlightFence);
            swapchain.Present(imageIndex, renderFinishedSemaphore);
        }

        device.Api.vkDeviceWaitIdle();
    }
}
