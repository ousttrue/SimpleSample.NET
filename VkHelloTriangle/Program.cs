// https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

unsafe class HelloTriangleApplication : IDisposable
{
    private readonly GlfwWindow _window;
    private readonly InstanceObject _instance;
    private readonly VkPhysicalDevice _physicalDevice;
    private readonly DeviceObject _device;
    private readonly SwapchainObject _swapchain;
    private readonly PipelineObject _pipeline;

    public HelloTriangleApplication()
    {
        _window = new GlfwWindow();

        _instance = new InstanceObject(_window);
        _physicalDevice = _instance.pickPhysicalDevice(DeviceObject.DeviceExtensions);

        var deviceProperties = new VkPhysicalDeviceProperties2
        {
            sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_PROPERTIES_2,
        };
        _instance.Api.vkGetPhysicalDeviceProperties2(_physicalDevice, &deviceProperties);
        Console.Error.WriteLine(
            $"Selected device: {Marshal.PtrToStringAnsi((nint)deviceProperties.properties.deviceName) ?? throw new Exception()}"
        );

        var indices = QueueFamilyIndices.findQueueFamilies(
            _instance.Api,
            _physicalDevice,
            _instance.Surface
        );
        _device = new DeviceObject(
            _instance.Api,
            _physicalDevice,
            indices.GraphicsFamily,
            indices.PresentFamily
        );
        _swapchain = new SwapchainObject(
            _instance.Api,
            _physicalDevice,
            _instance.Surface,
            _device.Api,
            new()
        );
        _pipeline = new PipelineObject(_device.Api, _swapchain.RenderPass);

        while (true)
        {
            if (!_window.NextFrame())
            {
                break;
            }
            var (imageIndex, commandBuffer) = _swapchain.Acquire();

            _pipeline.RecordCommandBuffer(commandBuffer, _swapchain.Extent);

            _swapchain.Present(imageIndex);
        }

        _device.Api.vkDeviceWaitIdle();
    }

    public void Dispose()
    {
        _swapchain.Dispose();
        _device.Dispose();
        _instance.Dispose();
        _window.Dispose();
    }
}

static class Program
{
    public static void Main()
    {
        using var app = new HelloTriangleApplication();
    }
}
