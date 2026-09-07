https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_vulkan.cpp

を c# に移植しようと試みたのだけど、途中で挫けた。
自己流の簡単バージョンに縮小。

- https://github.com/stymee/SilkVulkanTutorial/tree/master/Source/Sandbox02ImGui/Systems/ImGui

を参考にした。
本実装が `Silk.NET.GLFW` を使うのに対して、ImGuiController は `Silk.NET.Windowing.IWindow` を使うのが違い。
`Silk.NET.Windowing.IWindow` は SDL と GLFW をラップして隠ぺいするインターフェースぽい。
