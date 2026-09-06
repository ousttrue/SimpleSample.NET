// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_vulkan/main.cpp

using Silk.NET.GLFW;

unsafe class GlfwWindow : IDisposable
{
    static readonly Glfw glfw;

    static GlfwWindow()
    {
        glfw = GlfwProvider.GLFW.Value;
    }

    static void glfw_error_callback(ErrorCode error, string description)
    {
        Console.Error.WriteLine($"GLFW Error {error}: {description}");
    }

    private readonly WindowHandle* _window;
    public WindowHandle* Window => _window;

    public GlfwWindow()
    {
        glfw.SetErrorCallback(glfw_error_callback);

        if (!glfw.Init())
            throw new Exception("glfw.Init");

        // Create window with Vulkan context
        glfw.WindowHint(WindowHintClientApi.ClientApi, ClientApi.NoApi);
        // float main_scale = ImGui_ImplGlfw_GetContentScaleForMonitor(glfwGetPrimaryMonitor()); // Valid on GLFW 3.3+ only
        var main_scale = 1f;
        _window = glfw.CreateWindow(
            (int)(1280 * main_scale),
            (int)(800 * main_scale),
            "Dear ImGui GLFW+Vulkan example",
            null,
            null
        );
        if (!glfw.VulkanSupported())
        {
            throw new Exception("GLFW: Vulkan Not Supported");
        }
    }

    public void Dispose()
    {
        glfw.DestroyWindow(_window);
        glfw.Terminate();
    }

    public (int, int)? NewFrame()
    {
        if (glfw.WindowShouldClose(_window))
        {
            return default;
        }
        // Poll and handle events (inputs, window resize, etc.)
        // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
        // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application, or clear/overwrite your copy of the mouse data.
        // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application, or clear/overwrite your copy of the keyboard data.
        // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
        glfw.PollEvents();

        glfw.GetFramebufferSize(_window, out var fb_width, out var fb_height);
        return (fb_width, fb_height);
    }
}
