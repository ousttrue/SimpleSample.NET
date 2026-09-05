# SimpleSample.NET

Examples of usage such as Silk.NET

`.NET10` で 3D graphics を扱う用例集。

Silk.NET の Glfw と OpenGL や Vulkan を使う簡単なサンプル集の予定。

Silk.NET はアクティブにメンテナンスされていて使いやすいのだけど、
ドキュメントはあまり無くて、ラップされた C のライブラリのドキュメントから読み解く必要がある。
取っ掛かりになる動くコードがあると開発が捗る。

`Vulkan + ImGui` する場合にコード片を参照できるようにしたい。
`vvvv` とか `StereoKit` も視野に入れていきたいのだが届くかな？

## dirs

### [Glfw_GettingStarted](./Glfw_GettingStarted/README.md)

https://www.glfw.org/docs/latest/quick.html

### [ImGui_Glfw_opengl3](./ImGui_Glfw_opengl3/README.md)

- https://github.com/ocornut/imgui/tree/master/examples/example_glfw_opengl3

### [VkHelloTriangle](./VkHelloTriangle/README.md)

`glfw3` + `vulkan`

- https://vulkan-tutorial.com/Drawing_a_triangle/Setup/Base_code
- https://github.com/Overv/VulkanTutorial/blob/main/code/15_hello_triangle.cpp

### TODO: ImGui_Glfw_vulkan

- vulkan-1.3 により RenderPass の作成を回避する。

## slnx

C# ソリューション。たぶん、無くても動くが Editor の language server の
動きに影響がありそう。
