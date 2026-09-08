// https://github.com/ocornut/imgui/blob/master/examples/example_glfw_opengl3/main.cpp

using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.GLFW;
using Silk.NET.OpenGL;

#pragma warning disable IDE1006

static class Program
{
    static readonly Glfw glfw;

    static Program()
    {
        glfw = GlfwProvider.GLFW.Value;
    }

    static void glfw_error_callback(Silk.NET.GLFW.ErrorCode error, string description)
    {
        Console.Error.Write($"GLFW Error {error}: {description}");
    }

    class ImGuiDisposer : IDisposable
    {
        public ImGuiDisposer()
        {
            ImGui.CreateContext();
        }

        public void Dispose()
        {
            ImGui.DestroyContext();
        }
    }

    // Main code
    public static unsafe int Main()
    {
        glfw.SetErrorCallback(glfw_error_callback);
        if (!glfw.Init())
            return 1;

        // Select GL version + let the backend select a GLSL version
        // GL 3.0 + generally GLSL 130
        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 4);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core); // 3.2+ only
        //glfwWindowHint(GLFW_OPENGL_FORWARD_COMPAT, GL_TRUE);            // 3.0+ only

        //     // Create window with graphics context
        //     float main_scale = ImGui_ImplGlfw_GetContentScaleForMonitor(glfwGetPrimaryMonitor()); // Valid on GLFW 3.3+ only
        var main_scale = 1f;
        var window = glfw.CreateWindow(
            (int)(1280 * main_scale),
            (int)(800 * main_scale),
            "Dear ImGui GLFW+OpenGL3 example",
            null,
            null
        );
        if (window is null)
            return 1;
        glfw.MakeContextCurrent(window);
        var gl = GL.GetApi(new GlfwContext(glfw, window));
        glfw.SwapInterval(1);

        glfw.SwapInterval(1); // Enable vsync

        // Setup Dear ImGui context
        // IMGUI_CHECKVERSION();
        using var igDisposer = new ImGuiDisposer();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard; // Enable Keyboard Controls
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableGamepad; // Enable Gamepad Controls

        // Setup Dear ImGui style
        ImGui.StyleColorsDark();
        //ImGui.StyleColorsLight();

        // Setup scaling
        var style = ImGui.GetStyle();
        style.ScaleAllSizes(main_scale); // Bake a fixed style scale. (until we have a solution for dynamic style scaling, changing this requires resetting Style + calling this again)
        // style.FontScaleDpi = main_scale;        // Set initial font scale. (in docking branch: using io.ConfigDpiScaleFonts=true automatically overrides this for every window depending on the current monitor)

        // Setup Platform/Renderer backends
        using var implGlfw = ImGuiImplGlfw.InitForOpenGL(window, true);
        using var implOpenGL3 = new ImGuiImplOpenGL3(gl, "#version 430 core");

        // Load Fonts
        // - If fonts are not explicitly loaded, Dear ImGui will select an embedded font: either AddFontDefaultVector() or AddFontDefaultBitmap().
        //   This selection is based on (style.FontSizeBase * style.FontScaleMain * style.FontScaleDpi) reaching a small threshold.
        // - You can load multiple fonts and use ImGui.PushFont()/PopFont() to select them.
        // - If a file cannot be loaded, AddFont functions will return a nullptr. Please handle those errors in your code (e.g. use an assertion, display an error and quit).
        // - Read 'docs/FONTS.md' for more instructions and details.
        // - Use '#define IMGUI_ENABLE_FREETYPE' in your imconfig file to use FreeType for higher quality font rendering.
        // - Remember that in C/C++ if you want to include a backslash \ in a string literal you need to write a double backslash \\ !
        // - Our Emscripten build process allows embedding fonts to be accessible at runtime from the "fonts/" folder. See Makefile.emscripten for details.
        //style.FontSizeBase = 20.0f;
        //io.Fonts.AddFontDefaultVector();
        //io.Fonts.AddFontDefaultBitmap();
        //io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\segoeui.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/DroidSans.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/Roboto-Medium.ttf");
        //io.Fonts.AddFontFromFileTTF("../../misc/fonts/Cousine-Regular.ttf");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var font = io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\MSGothic.ttc", 24f);
            if (font.NativePtr is null)
            {
                throw new Exception("AddFontFromFileTTF");
            }
        }

        // Our state
        bool show_demo_window = true;
        bool show_another_window = false;
        Vector4 clear_color = new(0.45f, 0.55f, 0.60f, 1.00f);
        float f = 0.0f;
        int counter = 0;

        // Main loop
        while (!glfw.WindowShouldClose(window))
        {
            // Poll and handle events (inputs, window resize, etc.)
            // You can read the io.WantCaptureMouse, io.WantCaptureKeyboard flags to tell if dear imgui wants to use your inputs.
            // - When io.WantCaptureMouse is true, do not dispatch mouse input data to your main application, or clear/overwrite your copy of the mouse data.
            // - When io.WantCaptureKeyboard is true, do not dispatch keyboard input data to your main application, or clear/overwrite your copy of the keyboard data.
            // Generally you may always pass all inputs to dear imgui, and hide them from your application based on those two flags.
            glfw.PollEvents();
            if (glfw.GetWindowAttrib(window, WindowAttributeGetter.Iconified))
            {
                Thread.Sleep(10);
                continue;
            }

            // Start the Dear ImGui frame
            implOpenGL3.NewFrame();
            implGlfw.NewFrame();
            ImGui.NewFrame();

            // 1. Show the big demo window (Most of the sample code is in ImGui.ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
            if (show_demo_window)
                ImGui.ShowDemoWindow(ref show_demo_window);

            // 2. Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
            {
                ImGui.Begin("Hello, world!"); // Create a window called "Hello, world!" and append into it.

                ImGui.Text("This is some useful text."); // Display some text (you can use a format strings too)
                ImGui.Checkbox("Demo Window", ref show_demo_window); // Edit bools storing our window open/close state
                ImGui.Checkbox("Another Window", ref show_another_window);

                ImGui.SliderFloat("float", ref f, 0.0f, 1.0f); // Edit 1 float using a slider from 0.0f to 1.0f
                ImGui.ColorEdit4("clear color", ref clear_color); // Edit 3 floats representing a color

                if (ImGui.Button("Button")) // Buttons return true when clicked (most widgets return true when edited/activated)
                    counter++;
                ImGui.SameLine();
                ImGui.Text($"counter = {counter}");

                ImGui.Text(
                    $"Application average {1000.0f / io.Framerate} ms/frame {io.Framerate} FPS)"
                );
                ImGui.End();
            }

            // 3. Show another simple window.
            if (show_another_window)
            {
                ImGui.Begin("Another Window", ref show_another_window); // Pass a pointer to our bool variable (the window will have a closing button that will clear the bool when clicked)
                ImGui.Text("Hello from another window!");
                if (ImGui.Button("Close Me"))
                    show_another_window = false;
                ImGui.End();
            }

            // Rendering
            ImGui.Render();
            glfw.GetFramebufferSize(window, out var display_w, out var display_h);
            gl.Viewport(0, 0, (uint)display_w, (uint)display_h);
            gl.ClearColor(
                clear_color.X * clear_color.W,
                clear_color.Y * clear_color.W,
                clear_color.Z * clear_color.W,
                clear_color.W
            );
            gl.Clear(ClearBufferMask.ColorBufferBit);
            implOpenGL3.RenderDrawData(ImGui.GetDrawData());

            glfw.SwapBuffers(window);
        }

        return 0;
    }
}
