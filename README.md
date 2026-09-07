# SimpleSample.NET

`.NET10` で 3D graphics を扱う用例集。

Silk.NET の `Silk.NET.Glfw` と `Silk.NET.OpenGL` や `Silk.NET.Vulkan` を使う簡単なサンプル集の予定。
だったが、
`Silk.NET.Glfw` + `Vortice.Vulkan` に路線変更中。
あと、 `ImGui.NET`

Silk.NET と Vortice 共にいにしえの SharpDX ぽい構成であり、
元ライブラリの C++ の例から類推して使う感じ。
取っ掛かりになる動くコードがあると開発が捗る。

## dirs

### [Glfw_GettingStarted](./Glfw_GettingStarted/README.md)

https://www.glfw.org/docs/latest/quick.html

`Silk.NET.Glfw`, `Silk.NET.OpenGL`

### [ImGui_Glfw_opengl3](./ImGui_Glfw_opengl3/README.md)

`Silk.NET.Glfw`, `Silk.NET.OpenGL`, `ImGui.NET`

- https://github.com/ocornut/imgui/tree/master/examples/example_glfw_opengl3

### [VkHelloTriangle](./VkHelloTriangle/README.md)

`Silk.NET.Glfw`, `Vortice.Vulkan`

- https://vulkan-tutorial.com/Drawing_a_triangle/Setup/Base_code
- https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

### [ImGui_Glfw_vulkan](./ImGui_Glfw_vulkan/README.md)

`Silk.NET.Glfw`, `Silk.NET.Vulkan`, `ImGui.NET`

- vulkan-1.3 により RenderPass の作成を回避する。

window の resize による swapchain の再作成は Image の更新を引き起こして、
Image に依存するリソースの再作成を連鎖させる。
Image や Image サイズなどに依存する ImageView, FrameBuffer, RenderPass, Pipeline の再作成へと波及する。

Dynamic Rendering を使うと FrameBuffer と RenderPass が消滅し、Pipeline の RenderPass への依存が無くなる。
Pipeline の Image (ColorAttachment) への依存が、Create 時から BeginRendering 時へと移動する。

## slnx

C# ソリューション。たぶん、無くても動くが Editor の language server の
動きに影響がありそう。
