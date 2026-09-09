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
        using var pipeline = new PipelineObject(device.Api, swapchain.Format, 
        // renderTarget.RenderPass
        null
        );

        while (true)
        {
            if (!window.NextFrame())
            {
                break;
            }
            var (imageIndex, imageAvailableSemaphore, renderFinishedSemaphore, inFlightFence) = swapchain.Acquire();

            VkClearColorValue clearColor = default;
            clearColor.float32[0] = 0.0f;
            clearColor.float32[1] = 0.0f;
            clearColor.float32[2] = 0.0f;
            clearColor.float32[2] = 1.0f;
            ReadOnlySpan<VkClearValue> clearValues = [new VkClearValue { color = clearColor }];
            // var (commandBuffer, renderFinishedSemaphore) = renderTarget.BeginRenderPass(
            //     imageIndex,
            //     clearValues
            // );
            var commandBuffer = renderTarget.BeginRendering(
                imageIndex,
                swapchain.Extent,
                clearValues
            );
            {
                pipeline.RecordCommandBuffer(commandBuffer);
            }
            // renderTarget.EndRenderPass();
            renderTarget.EndRendering(swapchain.Images[imageIndex]);
            renderTarget.vkEndSubmitCommandBuffer(imageAvailableSemaphore, renderFinishedSemaphore, inFlightFence);

            swapchain.Present(imageIndex, renderFinishedSemaphore);
        }

        device.Api.vkDeviceWaitIdle();
    }
}
