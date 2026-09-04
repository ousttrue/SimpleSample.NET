// https://github.com/ocornut/imgui/blob/v1.91.6-docking/backends/imgui_impl_opengl3.cpp

using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.OpenGL;
using ImDrawIdx = ushort;

public class ImGuiImplOpenGL3 : IDisposable
{
    private readonly GL gl;

    // OpenGL Data
    private readonly uint GlVersion; // Extracted at runtime using GL_MAJOR_VERSION, GL_MINOR_VERSION queries (e.g. 320 for GL 3.2)

    private readonly string GlslVersionString; // Specified by user or detected based on compile time GL settings.

    private uint FontTexture;
    private uint ShaderHandle;

    private int AttribLocationTex; // Uniforms location
    private int AttribLocationProjMtx;
    private uint AttribLocationVtxPos; // Vertex attributes location
    private uint AttribLocationVtxUV;
    private uint AttribLocationVtxColor;

    private uint VboHandle;
    private uint ElementsHandle;

    /// <summary>
    ///----------------------------------------<br/>
    /// OpenGL    GLSL      GLSL<br/>
    /// version   version   string<br/>
    ///----------------------------------------<br/>
    ///  2.0       110       "#version 110"<br/>
    ///  2.1       120       "#version 120"<br/>
    ///  3.0       130       "#version 130"<br/>
    ///  3.1       140       "#version 140"<br/>
    ///  3.2       150       "#version 150"<br/>
    ///  3.3       330       "#version 330 core"<br/>
    ///  4.0       400       "#version 400 core"<br/>
    ///  4.1       410       "#version 410 core"<br/>
    ///  4.2       420       "#version 410 core"<br/>
    ///  4.3       430       "#version 430 core"<br/>
    ///  ES 2.0    100       "#version 100"      = WebGL 1.0<br/>
    ///  ES 3.0    300       "#version 300 es"   = WebGL 2.0<br/>
    ///----------------------------------------    <br/>
    /// </summary>
    public ImGuiImplOpenGL3(GL _gl, string glsl_version)
    {
        gl = _gl;
        var io = ImGui.GetIO();
        //     IMGUI_CHECKVERSION();
        if (io.BackendRendererUserData != default)
        {
            throw new Exception("Already initialized a renderer backend!");
        }

        // Setup backend capabilities flags
        // ImGui_ImplOpenGL3_Data* bd = IM_NEW(ImGui_ImplOpenGL3_Data)();
        // io.BackendRendererUserData = (void*)bd;
        // io.BackendRendererName = "imgui_impl_opengl3";

        // Query for GL version (e.g. 320 for GL 3.2)
        var gl_version_str = gl.GetStringS(GLEnum.Version);
        // Desktop or GLES 3
        var major = gl.GetInteger(GLEnum.MajorVersion);
        var minor = gl.GetInteger(GLEnum.MinorVersion);
        if (major == 0 && minor == 0)
        {
            throw new Exception("no OpenGL version");
            // sscanf(gl_version_str, "%d.%d", &major, &minor); // Query GL_VERSION in desktop GL 2.X, the string will start with "<major>.<minor>"
        }
        GlVersion = (uint)(major * 100 + minor * 10);

        if (gl_version_str.StartsWith("OpenGL ES 3"))
        {
            // GlProfileIsES3 = true;
        }

        Console.Out.WriteLine($"GlVersion = {GlVersion}, \"{gl_version_str}\"");
        // Console.Out.WriteLine($"GlProfileIsES2/IsEs3 = {GlProfileIsES2}/{GlProfileIsES3}");
        Console.Out.WriteLine($"GL_VENDOR = '{gl.GetStringS(GLEnum.Vendor)}'");
        Console.Out.WriteLine($"GL_RENDERER = '{gl.GetStringS(GLEnum.Renderer)}'");

        if (GlVersion >= 320)
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset; // We can honor the ImDrawCmd.VtxOffset field, allowing for large meshes.
        io.BackendFlags |= ImGuiBackendFlags.RendererHasViewports; // We can create multi-viewports on the Renderer side (optional)

        // Store GLSL version string so we can refer to it later in case we recreate shaders.
        // Note: GLSL version is NOT the same as GL version. Leave this to nullptr if unsure.
        if (string.IsNullOrEmpty(glsl_version))
        {
            // #if defined(IMGUI_IMPL_OPENGL_ES2)
            //         glsl_version = "#version 100";
            // #elif defined(IMGUI_IMPL_OPENGL_ES3)
            //         glsl_version = "#version 300 es";
            // #elif defined(__APPLE__)
            //         glsl_version = "#version 150";
            // #else
            glsl_version = "#version 130";
            // #endif
        }
        //     IM_ASSERT((int)strlen(glsl_version) + 2 < IM_ARRAYSIZE(GlslVersionString));
        //     strcpy(GlslVersionString, glsl_version);
        //     strcat(GlslVersionString, "\n");
        GlslVersionString = glsl_version;
        if (!GlslVersionString.EndsWith("\n"))
        {
            GlslVersionString += "\n";
        }

        // Make an arbitrary GL call (we don't actually need the result)
        // IF YOU GET A CRASH HERE: it probably means the OpenGL function loader didn't do its job. Let us know!
        var current_texture = gl.GetInteger(GLEnum.TextureBinding2D);

        // Detect extensions we support
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_POLYGON_MODE
        //     HasPolygonMode = (!GlProfileIsES2 && !GlProfileIsES3);
        // #endif
        //     HasClipOrigin = (GlVersion >= 450);
        // #ifdef IMGUI_IMPL_OPENGL_HAS_EXTENSIONS
        //     GLint num_extensions = 0;
        //     glGetIntegerv(GL_NUM_EXTENSIONS, &num_extensions);
        //     for (GLint i = 0; i < num_extensions; i++)
        //     {
        //         const char* extension = (const char*)glGetStringi(GL_EXTENSIONS, i);
        //         if (extension != nullptr && strcmp(extension, "GL_ARB_clip_control") == 0)
        //             HasClipOrigin = true;
        //     }
        // #endif
    }

    public void Dispose()
    {
        //     ImGui_ImplOpenGL3_Data* bd = ImGui_ImplOpenGL3_GetBackendData();
        //     IM_ASSERT(bd != nullptr && "No renderer backend to shutdown, or already shutdown?");
        var io = ImGui.GetIO();

        ImGui_ImplOpenGL3_DestroyDeviceObjects();
        //     io.BackendRendererName = nullptr;
        //     io.BackendRendererUserData = nullptr;
        io.BackendFlags &= ~(
            ImGuiBackendFlags.RendererHasVtxOffset | ImGuiBackendFlags.RendererHasViewports
        );
    }

    public void NewFrame()
    {
        if (ShaderHandle == default)
            ImGui_ImplOpenGL3_CreateDeviceObjects();
        if (FontTexture == default)
            ImGui_ImplOpenGL3_CreateFontsTexture();
    }

    void ImGui_ImplOpenGL3_SetupRenderState(
        ImDrawDataPtr draw_data,
        int fb_width,
        int fb_height,
        uint vertex_array_object
    )
    {
        // Setup render state: alpha-blending enabled, no face culling, no depth testing, scissor enabled, polygon fill
        gl.Enable(GLEnum.Blend);
        gl.BlendEquation(GLEnum.FuncAdd);
        gl.BlendFuncSeparate(
            GLEnum.SrcAlpha,
            GLEnum.OneMinusSrcAlpha,
            GLEnum.One,
            GLEnum.OneMinusSrcAlpha
        );
        gl.Disable(GLEnum.CullFace);
        gl.Disable(GLEnum.DepthTest);
        gl.Disable(GLEnum.StencilTest);
        gl.Enable(GLEnum.ScissorTest);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_PRIMITIVE_RESTART
        if (GlVersion >= 310)
            gl.Disable(GLEnum.PrimitiveRestart);
        // #endif
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_POLYGON_MODE
        //     if (HasPolygonMode)
        //         glPolygonMode(GL_FRONT_AND_BACK, GL_FILL);
        // #endif

        //     // Support for GL 4.5 rarely used glClipControl(GL_UPPER_LEFT)
        // #if defined(GL_CLIP_ORIGIN)
        //     bool clip_origin_lower_left = true;
        //     if (HasClipOrigin)
        //     {
        //         GLenum current_clip_origin = 0; glGetIntegerv(GL_CLIP_ORIGIN, (GLint*)&current_clip_origin);
        //         if (current_clip_origin == GL_UPPER_LEFT)
        //             clip_origin_lower_left = false;
        //     }
        // #endif

        // Setup viewport, orthographic projection matrix
        // Our visible imgui space lies from draw_data.DisplayPos (top left) to draw_data.DisplayPos+data_data.DisplaySize (bottom right). DisplayPos is (0,0) for single viewport apps.
        gl.Viewport(0, 0, (uint)fb_width, (uint)fb_height);
        var L = draw_data.DisplayPos.X;
        var R = draw_data.DisplayPos.X + draw_data.DisplaySize.X;
        var T = draw_data.DisplayPos.Y;
        var B = draw_data.DisplayPos.Y + draw_data.DisplaySize.Y;
        // #if defined(GL_CLIP_ORIGIN)
        //     if (!clip_origin_lower_left) { float tmp = T; T = B; B = tmp; } // Swap top and bottom if origin is upper left
        // #endif

        // csharpier-ignore
        ReadOnlySpan<float> ortho_projection =
        [
            2.0f / (R - L), 0.0f, 0.0f, 0.0f,
            0.0f, 2.0f / (T - B), 0.0f, 0.0f,
            0.0f, 0.0f, -1.0f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T), 0.0f, 1.0f,
        ];
        gl.UseProgram(ShaderHandle);
        gl.Uniform1(AttribLocationTex, 0);
        gl.UniformMatrix4(AttribLocationProjMtx, 1, false, ortho_projection);

        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_BIND_SAMPLER
        if (GlVersion >= 330
        //|| GlProfileIsES3
        )
            gl.BindSampler(0, 0); // We use combined texture/sampler state. Applications using GL 3.3 and GL ES 3.0 may set that otherwise.
        // #endif

        gl.BindVertexArray(vertex_array_object);

        // Bind vertex/index buffers and setup attributes for ImDrawVert
        gl.BindBuffer(GLEnum.ArrayBuffer, VboHandle);
        gl.BindBuffer(GLEnum.ElementArrayBuffer, ElementsHandle);
        gl.EnableVertexAttribArray(AttribLocationVtxPos);
        gl.EnableVertexAttribArray(AttribLocationVtxUV);
        gl.EnableVertexAttribArray(AttribLocationVtxColor);
        gl.VertexAttribPointer(
            AttribLocationVtxPos,
            2,
            GLEnum.Float,
            false,
            (uint)Marshal.SizeOf<ImDrawVert>(),
            Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.pos))
        );
        gl.VertexAttribPointer(
            AttribLocationVtxUV,
            2,
            GLEnum.Float,
            false,
            (uint)Marshal.SizeOf<ImDrawVert>(),
            Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.uv))
        );
        gl.VertexAttribPointer(
            AttribLocationVtxColor,
            4,
            GLEnum.UnsignedByte,
            true,
            (uint)Marshal.SizeOf<ImDrawVert>(),
            Marshal.OffsetOf<ImDrawVert>(nameof(ImDrawVert.col))
        );
    }

    // OpenGL3 Render function.
    // Note that this implementation is little overcomplicated because we are saving/setting up/restoring every OpenGL state explicitly.
    // This is in order to be able to run within an OpenGL engine that doesn't do so.
    public unsafe void RenderDrawData(ImDrawDataPtr draw_data)
    {
        // Avoid rendering when minimized, scale coordinates for retina displays (screen coordinates != framebuffer coordinates)
        int fb_width = (int)(draw_data.DisplaySize.X * draw_data.FramebufferScale.X);
        int fb_height = (int)(draw_data.DisplaySize.Y * draw_data.FramebufferScale.Y);
        if (fb_width <= 0 || fb_height <= 0)
            return;

        // Backup GL state
        var last_active_texture = gl.GetInteger(GLEnum.ActiveTexture);
        gl.ActiveTexture(GLEnum.Texture0);
        var last_program = gl.GetInteger(GLEnum.CurrentProgram);
        var last_texture = gl.GetInteger(GLEnum.TextureBinding2D);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_BIND_SAMPLER
        uint last_sampler;
        if (GlVersion >= 330
        // || GlProfileIsES3
        )
        {
            last_sampler = (uint)gl.GetInteger(GLEnum.SamplerBinding);
        }
        else
        {
            last_sampler = 0;
        }
        // #endif
        var last_array_buffer = gl.GetInteger(GLEnum.ArrayBufferBinding);
        // #ifndef IMGUI_IMPL_OPENGL_USE_VERTEX_ARRAY
        //     // This is part of VAO on OpenGL 3.0+ and OpenGL ES 3.0+.
        //     GLint last_element_array_buffer; glGetIntegerv(GL_ELEMENT_ARRAY_BUFFER_BINDING, &last_element_array_buffer);
        //     ImGui_ImplOpenGL3_VtxAttribState last_vtx_attrib_state_pos; last_vtx_attrib_state_pos.GetState(AttribLocationVtxPos);
        //     ImGui_ImplOpenGL3_VtxAttribState last_vtx_attrib_state_uv; last_vtx_attrib_state_uv.GetState(AttribLocationVtxUV);
        //     ImGui_ImplOpenGL3_VtxAttribState last_vtx_attrib_state_color; last_vtx_attrib_state_color.GetState(AttribLocationVtxColor);
        // #endif
        // #ifdef IMGUI_IMPL_OPENGL_USE_VERTEX_ARRAY
        var last_vertex_array_object = gl.GetInteger(GLEnum.VertexArrayBinding);
        // #endif
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_POLYGON_MODE
        //     GLint last_polygon_mode[2]; if (HasPolygonMode) { glGetIntegerv(GL_POLYGON_MODE, last_polygon_mode); }
        // #endif
        var last_viewport = stackalloc int[4];
        gl.GetInteger(GLEnum.Viewport, last_viewport);
        var last_scissor_box = stackalloc int[4];
        gl.GetInteger(GLEnum.ScissorBox, last_scissor_box);
        //     GLenum last_blend_src_rgb; glGetIntegerv(GL_BLEND_SRC_RGB, (GLint*)&last_blend_src_rgb);
        //     GLenum last_blend_dst_rgb; glGetIntegerv(GL_BLEND_DST_RGB, (GLint*)&last_blend_dst_rgb);
        //     GLenum last_blend_src_alpha; glGetIntegerv(GL_BLEND_SRC_ALPHA, (GLint*)&last_blend_src_alpha);
        //     GLenum last_blend_dst_alpha; glGetIntegerv(GL_BLEND_DST_ALPHA, (GLint*)&last_blend_dst_alpha);
        //     GLenum last_blend_equation_rgb; glGetIntegerv(GL_BLEND_EQUATION_RGB, (GLint*)&last_blend_equation_rgb);
        //     GLenum last_blend_equation_alpha; glGetIntegerv(GL_BLEND_EQUATION_ALPHA, (GLint*)&last_blend_equation_alpha);
        var last_enable_blend = gl.IsEnabled(GLEnum.Blend);
        //     GLboolean last_enable_cull_face = glIsEnabled(GL_CULL_FACE);
        //     GLboolean last_enable_depth_test = glIsEnabled(GL_DEPTH_TEST);
        //     GLboolean last_enable_stencil_test = glIsEnabled(GL_STENCIL_TEST);
        //     GLboolean last_enable_scissor_test = glIsEnabled(GL_SCISSOR_TEST);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_PRIMITIVE_RESTART
        //     GLboolean last_enable_primitive_restart = (GlVersion >= 310) ? glIsEnabled(GL_PRIMITIVE_RESTART) : GL_FALSE;
        // #endif

        // Setup desired GL state
        // Recreate the VAO every time (this is to easily allow multiple GL contexts to be rendered to. VAO are not shared among GL contexts)
        // The renderer would actually work without any VAO bound, but then our VertexAttrib calls would overwrite the default one currently bound.
        uint vertex_array_object = gl.GenVertexArrays(1);
        ImGui_ImplOpenGL3_SetupRenderState(draw_data, fb_width, fb_height, vertex_array_object);

        // Will project scissor/clipping rectangles into framebuffer space
        var clip_off = draw_data.DisplayPos; // (0,0) unless using multi-viewports
        var clip_scale = draw_data.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

        // Render command lists
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            var draw_list = draw_data.CmdLists[n];

            // Upload vertex/index buffers
            // - OpenGL drivers are in a very sorry state nowadays....
            //   During 2021 we attempted to switch from glBufferData() to orphaning+glBufferSubData() following reports
            //   of leaks on Intel GPU when using multi-viewports on Windows.
            // - After this we kept hearing of various display corruptions issues. We started disabling on non-Intel GPU, but issues still got reported on Intel.
            // - We are now back to using exclusively glBufferData(). So UseBufferSubData IS ALWAYS FALSE in this code.
            //   We are keeping the old code path for a while in case people finding new issues may want to test the UseBufferSubData path.
            // - See https://github.com/ocornut/imgui/issues/4468 and please report any corruption issues.
            var vtx_buffer_size = (nuint)(draw_list.VtxBuffer.Size * Marshal.SizeOf<ImDrawVert>());
            var idx_buffer_size = (nuint)(draw_list.IdxBuffer.Size * Marshal.SizeOf<ImDrawIdx>());
            gl.BufferData(
                GLEnum.ArrayBuffer,
                vtx_buffer_size,
                draw_list.VtxBuffer.Data.ToPointer(),
                GLEnum.StreamDraw
            );
            gl.BufferData(
                GLEnum.ElementArrayBuffer,
                idx_buffer_size,
                draw_list.IdxBuffer.Data.ToPointer(),
                GLEnum.StreamDraw
            );

            for (int cmd_i = 0; cmd_i < draw_list.CmdBuffer.Size; cmd_i++)
            {
                var pcmd = draw_list.CmdBuffer[cmd_i];
                // Project scissor/clipping rectangles into framebuffer space
                var clip_min = new Vector2(
                    (pcmd.ClipRect.X - clip_off.X) * clip_scale.X,
                    (pcmd.ClipRect.Y - clip_off.Y) * clip_scale.Y
                );
                var clip_max = new Vector2(
                    (pcmd.ClipRect.Z - clip_off.X) * clip_scale.X,
                    (pcmd.ClipRect.W - clip_off.Y) * clip_scale.Y
                );
                if (clip_max.X <= clip_min.X || clip_max.Y <= clip_min.Y)
                    continue;

                // Apply scissor/clipping rectangle (Y is inverted in OpenGL)
                gl.Scissor(
                    (int)clip_min.X,
                    (int)((float)fb_height - clip_max.Y),
                    (uint)(clip_max.X - clip_min.X),
                    (uint)(clip_max.Y - clip_min.Y)
                );

                // Bind texture, Draw
                gl.BindTexture(GLEnum.Texture2D, (uint)pcmd.GetTexID());
                if (GlVersion >= 320)
                    gl.DrawElementsBaseVertex(
                        GLEnum.Triangles,
                        pcmd.ElemCount,
                        sizeof(ImDrawIdx) == 2 ? GLEnum.UnsignedShort : GLEnum.UnsignedInt,
                        new IntPtr(pcmd.IdxOffset * sizeof(ImDrawIdx)).ToPointer(),
                        (int)pcmd.VtxOffset
                    );
                else
                    gl.DrawElements(
                        GLEnum.Triangles,
                        pcmd.ElemCount,
                        sizeof(ImDrawIdx) == 2 ? GLEnum.UnsignedShort : GLEnum.UnsignedInt,
                        new IntPtr(pcmd.IdxOffset * sizeof(ImDrawIdx)).ToPointer()
                    );
            }
        }

        // Destroy the temporary VAO
        gl.DeleteVertexArrays(1, in vertex_array_object);

        //     // Restore modified GL state
        //     // This "glIsProgram()" check is required because if the program is "pending deletion" at the time of binding backup, it will have been deleted by now and will cause an OpenGL error. See #6220.
        //     if (last_program == 0 || glIsProgram(last_program)) glUseProgram(last_program);
        //     glBindTexture(GL_TEXTURE_2D, last_texture);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_BIND_SAMPLER
        //     if (GlVersion >= 330 || GlProfileIsES3)
        //         glBindSampler(0, last_sampler);
        // #endif
        //     glActiveTexture(last_active_texture);
        // #ifdef IMGUI_IMPL_OPENGL_USE_VERTEX_ARRAY
        //     glBindVertexArray(last_vertex_array_object);
        // #endif
        //     glBindBuffer(GL_ARRAY_BUFFER, last_array_buffer);
        // #ifndef IMGUI_IMPL_OPENGL_USE_VERTEX_ARRAY
        //     glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, last_element_array_buffer);
        //     last_vtx_attrib_state_pos.SetState(AttribLocationVtxPos);
        //     last_vtx_attrib_state_uv.SetState(AttribLocationVtxUV);
        //     last_vtx_attrib_state_color.SetState(AttribLocationVtxColor);
        // #endif
        //     glBlendEquationSeparate(last_blend_equation_rgb, last_blend_equation_alpha);
        //     glBlendFuncSeparate(last_blend_src_rgb, last_blend_dst_rgb, last_blend_src_alpha, last_blend_dst_alpha);
        //     if (last_enable_blend) glEnable(GL_BLEND); else glDisable(GL_BLEND);
        //     if (last_enable_cull_face) glEnable(GL_CULL_FACE); else glDisable(GL_CULL_FACE);
        //     if (last_enable_depth_test) glEnable(GL_DEPTH_TEST); else glDisable(GL_DEPTH_TEST);
        //     if (last_enable_stencil_test) glEnable(GL_STENCIL_TEST); else glDisable(GL_STENCIL_TEST);
        //     if (last_enable_scissor_test) glEnable(GL_SCISSOR_TEST); else glDisable(GL_SCISSOR_TEST);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_PRIMITIVE_RESTART
        //     if (GlVersion >= 310) { if (last_enable_primitive_restart) glEnable(GL_PRIMITIVE_RESTART); else glDisable(GL_PRIMITIVE_RESTART); }
        // #endif

        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_POLYGON_MODE
        //     // Desktop OpenGL 3.0 and OpenGL 3.1 had separate polygon draw modes for front-facing and back-facing faces of polygons
        //     if (HasPolygonMode) { if (GlVersion <= 310 || GlProfileIsCompat) { glPolygonMode(GL_FRONT, (GLenum)last_polygon_mode[0]); glPolygonMode(GL_BACK, (GLenum)last_polygon_mode[1]); } else { glPolygonMode(GL_FRONT_AND_BACK, (GLenum)last_polygon_mode[0]); } }
        // #endif // IMGUI_IMPL_OPENGL_MAY_HAVE_POLYGON_MODE

        gl.Viewport(
            last_viewport[0],
            last_viewport[1],
            (uint)last_viewport[2],
            (uint)last_viewport[3]
        );
        gl.Scissor(
            last_scissor_box[0],
            last_scissor_box[1],
            (uint)last_scissor_box[2],
            (uint)last_scissor_box[3]
        );
    }

    unsafe bool ImGui_ImplOpenGL3_CreateFontsTexture()
    {
        var io = ImGui.GetIO();

        // Build texture atlas
        IntPtr pixels;
        io.Fonts.GetTexDataAsRGBA32(out pixels, out var width, out var height); // Load as RGBA 32-bit (75% of the memory is wasted, but default font is so small) because it is more likely to be compatible with user's existing shaders. If your ImTextureId represent a higher-level concept than just a GL texture id, consider calling GetTexDataAsAlpha8() instead to save on GPU memory.

        // Upload texture to graphics system
        // (Bilinear sampling is required by default. Set 'io.Fonts.Flags |= ImFontAtlasFlags_NoBakedLines' or 'style.AntiAliasedLinesUseTex = false' to allow point/nearest sampling)
        var last_texture = gl.GetInteger(GLEnum.TextureBinding2D);
        FontTexture = gl.GenTextures(1);
        gl.BindTexture(GLEnum.Texture2D, FontTexture);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)GLEnum.Linear);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)GLEnum.ClampToEdge);
        // #ifdef GL_UNPACK_ROW_LENGTH // Not on WebGL/ES
        gl.PixelStore(GLEnum.UnpackRowLength, 0);
        // #endif
        gl.TexImage2D(
            GLEnum.Texture2D,
            0,
            (int)GLEnum.Rgba,
            (uint)width,
            (uint)height,
            0,
            GLEnum.Rgba,
            GLEnum.UnsignedByte,
            pixels.ToPointer()
        );

        // Store identifier
        io.Fonts.SetTexID(new nint(FontTexture));

        // Restore state
        gl.BindTexture(GLEnum.Texture2D, (uint)last_texture);

        return true;
    }

    void ImGui_ImplOpenGL3_DestroyFontsTexture()
    {
        var io = ImGui.GetIO();
        if (FontTexture != default)
        {
            gl.DeleteTextures(1, ref FontTexture);
            io.Fonts.SetTexID(0);
            FontTexture = 0;
        }
    }

    // If you get an error please report on github. You may try different GL context version or GLSL version. See GL<>GLSL version table at the top of this file.
    bool CheckShader(uint handle, string desc)
    {
        var status = gl.GetShader(handle, GLEnum.CompileStatus);
        var log_length = gl.GetShader(handle, GLEnum.InfoLogLength);
        if (status == (int)GLEnum.False)
        {
            Console.Error.WriteLine(
                $"ERROR: ImGui_ImplOpenGL3_CreateDeviceObjects: failed to compile {desc}! With GLSL: {GlslVersionString}"
            );
        }
        if (log_length > 1)
        {
            var buf = gl.GetShaderInfoLog(handle);
            Console.Error.WriteLine(buf);
        }
        return status != (int)GLEnum.False;
    }

    // If you get an error please report on GitHub. You may try different GL context version or GLSL version.
    bool CheckProgram(uint handle, string desc)
    {
        var status = gl.GetProgram(handle, GLEnum.LinkStatus);
        var log_length = gl.GetProgram(handle, GLEnum.InfoLogLength);
        if (status == (int)GLEnum.False)
        {
            Console.Error.WriteLine(
                $"ERROR: ImGui_ImplOpenGL3_CreateDeviceObjects: failed to link {desc}! With GLSL {GlslVersionString}"
            );
        }
        if (log_length > 1)
        {
            var buf = gl.GetProgramInfoLog(handle);
            Console.Error.WriteLine(buf);
        }
        return status != (int)GLEnum.False;
    }

    const string vertex_shader_glsl_120 =
        @"uniform mat4 ProjMtx;
attribute vec2 Position;
attribute vec2 UV;
attribute vec4 Color;
varying vec2 Frag_UV;
varying vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy,0,1);
};
";

    const string vertex_shader_glsl_130 =
        @"uniform mat4 ProjMtx;
in vec2 Position;
in vec2 UV;
in vec4 Color;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy,0,1);
};
";

    const string vertex_shader_glsl_300_es =
        @"precision highp float;
layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy,0,1);
};
";

    const string vertex_shader_glsl_410_core =
        @"layout (location = 0) in vec2 Position;
layout (location = 1) in vec2 UV;
layout (location = 2) in vec4 Color;
uniform mat4 ProjMtx;
out vec2 Frag_UV;
out vec4 Frag_Color;
void main()
{
    Frag_UV = UV;
    Frag_Color = Color;
    gl_Position = ProjMtx * vec4(Position.xy,0,1);
};
";

    const string fragment_shader_glsl_120 =
        @"#ifdef GL_ES
    precision mediump float;
#endif
uniform sampler2D Texture;
varying vec2 Frag_UV;
varying vec4 Frag_Color;
void main()
{
    gl_FragColor = Frag_Color * texture2D(Texture, Frag_UV.st);
};
";

    const string fragment_shader_glsl_130 =
        @"uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
out vec4 Out_Color;
void main()
{
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
};
";

    const string fragment_shader_glsl_300_es =
        @"precision mediump float;
uniform sampler2D Texture;
in vec2 Frag_UV;
in vec4 Frag_Color;
layout (location = 0) out vec4 Out_Color;
void main()
{
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
};
";

    const string fragment_shader_glsl_410_core =
        @"in vec2 Frag_UV;
in vec4 Frag_Color;
uniform sampler2D Texture;
layout (location = 0) out vec4 Out_Color;
void main()
{
    Out_Color = Frag_Color * texture(Texture, Frag_UV.st);
};
";

    unsafe bool ImGui_ImplOpenGL3_CreateDeviceObjects()
    {
        // Backup GL state
        var last_texture = gl.GetInteger(GLEnum.TextureBinding2D);
        var last_array_buffer = gl.GetInteger(GLEnum.ArrayBufferBinding);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_BIND_BUFFER_PIXEL_UNPACK
        var last_pixel_unpack_buffer = 0;
        if (GlVersion >= 210)
        {
            last_pixel_unpack_buffer = gl.GetInteger(GLEnum.PixelUnpackBufferBinding);
            gl.BindBuffer(GLEnum.PixelUnpackBuffer, 0);
        }
        // #endif
        var last_vertex_array = gl.GetInteger(GLEnum.VertexArrayBinding);

        // Parse GLSL version string
        var glsl_version = 130;
        //     sscanf(GlslVersionString, "#version %d", &glsl_version);
        if (int.TryParse(GlslVersionString.Substring(9, 3), out var parsed))
        {
            glsl_version = parsed;
        }
        else
        {
            throw new Exception(GlslVersionString);
        }

        // Select shaders matching our GLSL versions
        string? vertex_shader = default;
        string? fragment_shader = default;
        if (glsl_version < 130)
        {
            vertex_shader = vertex_shader_glsl_120;
            fragment_shader = fragment_shader_glsl_120;
        }
        else if (glsl_version >= 410)
        {
            vertex_shader = vertex_shader_glsl_410_core;
            fragment_shader = fragment_shader_glsl_410_core;
        }
        else if (glsl_version == 300)
        {
            vertex_shader = vertex_shader_glsl_300_es;
            fragment_shader = fragment_shader_glsl_300_es;
        }
        else
        {
            vertex_shader = vertex_shader_glsl_130;
            fragment_shader = fragment_shader_glsl_130;
        }

        // Create shaders
        var vert_handle = gl.CreateShader(GLEnum.VertexShader);
        gl.ShaderSource(vert_handle, 2, [GlslVersionString, vertex_shader], null);
        gl.CompileShader(vert_handle);
        CheckShader(vert_handle, "vertex shader");

        var frag_handle = gl.CreateShader(GLEnum.FragmentShader);
        gl.ShaderSource(frag_handle, 2, [GlslVersionString, fragment_shader], null);
        gl.CompileShader(frag_handle);
        CheckShader(frag_handle, "fragment shader");

        // Link
        ShaderHandle = gl.CreateProgram();
        gl.AttachShader(ShaderHandle, vert_handle);
        gl.AttachShader(ShaderHandle, frag_handle);
        gl.LinkProgram(ShaderHandle);
        CheckProgram(ShaderHandle, "shader program");

        gl.DetachShader(ShaderHandle, vert_handle);
        gl.DetachShader(ShaderHandle, frag_handle);
        gl.DeleteShader(vert_handle);
        gl.DeleteShader(frag_handle);

        AttribLocationTex = gl.GetUniformLocation(ShaderHandle, "Texture");
        AttribLocationProjMtx = gl.GetUniformLocation(ShaderHandle, "ProjMtx");
        AttribLocationVtxPos = (uint)gl.GetAttribLocation(ShaderHandle, "Position");
        AttribLocationVtxUV = (uint)gl.GetAttribLocation(ShaderHandle, "UV");
        AttribLocationVtxColor = (uint)gl.GetAttribLocation(ShaderHandle, "Color");

        // Create buffers
        VboHandle = gl.GenBuffers(1);
        ElementsHandle = gl.GenBuffers(1);

        ImGui_ImplOpenGL3_CreateFontsTexture();

        // Restore modified GL state
        gl.BindTexture(GLEnum.Texture2D, (uint)last_texture);
        gl.BindBuffer(GLEnum.ArrayBuffer, (uint)last_array_buffer);
        // #ifdef IMGUI_IMPL_OPENGL_MAY_HAVE_BIND_BUFFER_PIXEL_UNPACK
        if (GlVersion >= 210)
        {
            gl.BindBuffer(GLEnum.PixelUnpackBuffer, (uint)last_pixel_unpack_buffer);
        }
        // #endif
        // #ifdef IMGUI_IMPL_OPENGL_USE_VERTEX_ARRAY
        gl.BindVertexArray((uint)last_vertex_array);
        // #endif

        return true;
    }

    void ImGui_ImplOpenGL3_DestroyDeviceObjects()
    {
        if (VboHandle != default)
        {
            gl.DeleteBuffers(1, ref VboHandle);
            VboHandle = 0;
        }
        if (ElementsHandle != default)
        {
            gl.DeleteBuffers(1, ref ElementsHandle);
            ElementsHandle = 0;
        }
        if (ShaderHandle != default)
        {
            gl.DeleteProgram(ShaderHandle);
            ShaderHandle = 0;
        }
        ImGui_ImplOpenGL3_DestroyFontsTexture();
    }
}
