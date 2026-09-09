using Silk.NET.Core.Native;
using Silk.NET.GLFW;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

unsafe class GlfwWindow : IDisposable
{
    static readonly Glfw glfw;

    static GlfwWindow()
    {
        glfw = GlfwProvider.GLFW.Value ?? throw new NullReferenceException();
    }

    const uint WIDTH = 800;
    const uint HEIGHT = 600;

    private readonly WindowHandle* _window;

    public GlfwWindow()
    {
        glfw.Init();

        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
        glfw.WindowHint(WindowHintBool.Resizable, false);

        _window = glfw.CreateWindow((int)WIDTH, (int)HEIGHT, "Vulkan", null, null);
    }

    public void Dispose()
    {
        glfw.DestroyWindow(_window);

        glfw.Terminate();
    }

    public ReadOnlySpan<IntPtr> GetVkExtensions()
    {
        var glfwExtensions = glfw.GetRequiredInstanceExtensions(out var glfwExtensionCount);
        return new(glfwExtensions, (int)glfwExtensionCount);
    }

    public ulong CreateVkSurface(nint vkInstance)
    {
        VkNonDispatchableHandle _surface;
        if (
            (VkResult)glfw.CreateWindowSurface(new VkHandle(vkInstance), _window, null, &_surface)
            != VK_SUCCESS
        )
        {
            throw new Exception("failed to create window surface!");
        }
        // surface = new(_surface.Handle);
        return _surface.Handle;
    }

    public VkExtent2D GetExtent()
    {
        glfw.GetFramebufferSize(_window, out var width, out var height);
        return new VkExtent2D((uint)width, (uint)height);
    }

    public bool NextFrame()
    {
        if (glfw.WindowShouldClose(_window))
        {
            return false;
        }
        glfw.PollEvents();
        return true;
    }
}
