// https://www.glfw.org/docs/latest/quick.html

using System.Numerics;
using System.Runtime.InteropServices;
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

    struct Vertex(Vector2 pos, Vector3 col)
    {
        public Vector2 pos = pos;
        public Vector3 col = col;
    }

    static readonly Vertex[] vertices =
    [
        new(new(-0.6f, -0.4f), new(1f, 0f, 0f)),
        new(new(0.6f, -0.4f), new(0f, 1f, 0f)),
        new(new(0f, 0.6f), new(0f, 0f, 1f)),
    ];

    const string vertex_shader_text =
        @"#version 330
uniform mat4 MVP;
in vec3 vCol;
in vec2 vPos;
out vec3 color;
void main()
{
    gl_Position = MVP * vec4(vPos, 0.0, 1.0);
    color = vCol;
};
";

    const string fragment_shader_text =
        @"#version 330
in vec3 color;
out vec4 fragment;
void main()
{
    fragment = vec4(color, 1.0);
};
";

    static void error_callback(Silk.NET.GLFW.ErrorCode error, string description)
    {
        Console.Error.WriteLine($"{error}: {description}\n");
    }

    static unsafe void key_callback(
        WindowHandle* window,
        Keys key,
        int scanCode,
        InputAction action,
        KeyModifiers mods
    )
    {
        if (key == Keys.Escape && action == InputAction.Press)
            glfw.SetWindowShouldClose(window, true);
    }

    public static unsafe void Main()
    {
        glfw.SetErrorCallback(error_callback);

        if (!glfw.Init())
            throw new Exception("glfw.Init");

        glfw.WindowHint(WindowHintInt.ContextVersionMajor, 3);
        glfw.WindowHint(WindowHintInt.ContextVersionMinor, 3);
        glfw.WindowHint(WindowHintOpenGlProfile.OpenGlProfile, OpenGlProfile.Core);

        var window = glfw.CreateWindow(640, 480, "OpenGL Triangle", null, null);
        if (window is null)
        {
            glfw.Terminate();
            throw new Exception("glfw.CreateWindow");
        }

        glfw.SetKeyCallback(window, key_callback);

        glfw.MakeContextCurrent(window);
        var gl = GL.GetApi(new GlfwContext(glfw, window));
        glfw.SwapInterval(1);

        // NOTE: OpenGL error checks have been omitted for brevity

        gl.GenBuffers(1, out uint vertex_buffer);
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertex_buffer);
        gl.BufferData(
            BufferTargetARB.ArrayBuffer,
            (nuint)(Marshal.SizeOf<Vertex>() * vertices.Length),
            ref vertices[0],
            BufferUsageARB.StaticDraw
        );

        var vertex_shader = gl.CreateShader(ShaderType.VertexShader);
        gl.ShaderSource(vertex_shader, 1, [vertex_shader_text], null);
        gl.CompileShader(vertex_shader);

        var fragment_shader = gl.CreateShader(ShaderType.FragmentShader);
        gl.ShaderSource(fragment_shader, 1, [fragment_shader_text], null);
        gl.CompileShader(fragment_shader);

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex_shader);
        gl.AttachShader(program, fragment_shader);
        gl.LinkProgram(program);

        var mvp_location = gl.GetUniformLocation(program, "MVP");
        var vpos_location = gl.GetAttribLocation(program, "vPos");
        var vcol_location = gl.GetAttribLocation(program, "vCol");

        uint vertex_array;
        gl.GenVertexArrays(1, &vertex_array);
        gl.BindVertexArray(vertex_array);
        gl.EnableVertexAttribArray((uint)vpos_location);
        gl.VertexAttribPointer(
            (uint)vpos_location,
            2,
            VertexAttribPointerType.Float,
            false,
            (uint)Marshal.SizeOf<Vertex>(),
            new IntPtr(Marshal.OffsetOf<Vertex>(nameof(Vertex.pos)))
        );
        gl.EnableVertexAttribArray((uint)vcol_location);
        gl.VertexAttribPointer(
            (uint)vcol_location,
            3,
            VertexAttribPointerType.Float,
            false,
            (uint)Marshal.SizeOf<Vertex>(),
            new IntPtr(Marshal.OffsetOf<Vertex>(nameof(Vertex.col)))
        );

        while (!glfw.WindowShouldClose(window))
        {
            glfw.GetFramebufferSize(window, out int width, out int height);
            float ratio = width / (float)height;

            gl.Viewport(0, 0, (uint)width, (uint)height);
            gl.Clear(ClearBufferMask.ColorBufferBit);

            var m = Matrix4x4.CreateRotationZ((float)glfw.GetTime());
            var p = Matrix4x4.CreateOrthographicOffCenter(-ratio, ratio, -1f, 1f, 1f, -1f);
            var mvp = Matrix4x4.Multiply(m, p);

            gl.UseProgram(program);
            gl.UniformMatrix4(mvp_location, 1, false, ref mvp.M11);
            gl.BindVertexArray(vertex_array);
            gl.DrawArrays(PrimitiveType.Triangles, 0, 3);

            glfw.SwapBuffers(window);
            glfw.PollEvents();
        }

        glfw.DestroyWindow(window);

        glfw.Terminate();
    }
}
