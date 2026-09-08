# ImGui_Glfw_vulkan

https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp

を c# に移植しようと試みたのだけど、途中で挫けた。
自己流の簡単バージョンに縮小。

- https://github.com/stymee/SilkVulkanTutorial/tree/master/Source/Sandbox02ImGui/Systems/ImGui

## Note

- Silk.NET.Windowing は使わずに Silk.NET.Glfw を直接使う
- `Silk.NET.Vulkan` を `Vortice.Vulkan` に置き換えた
- vulkan-1.3 の DynamicRending を使用し、RenderPass と FrameBuffer を作らない

window の resize による swapchain の再作成は Image の更新を引き起こして、
Image に依存するリソースの再作成を連鎖させる。
Image に依存する ImageView, FrameBuffer, RenderPass, Pipeline の再作成へと波及する。

Dynamic Rendering を使うと FrameBuffer と RenderPass が消滅し、Pipeline の RenderPass への依存が無くなる。
Pipeline の Image (ColorAttachment) への依存が、Create 時から BeginRendering 時へと移動する。

