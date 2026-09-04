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

### Glfw_GettingStarted

https://www.glfw.org/docs/latest/quick.html

### ImGui_Glfw_opengl3

- https://github.com/ocornut/imgui/tree/master/examples/example_glfw_opengl3
- https://github.com/ImGuiNET/ImGui.NET `v1.91.6.1`

Silk.NET 版があるのだけど、
改めて `c++` の `v1.91.6-docking` から移植。

- https://github.com/dotnet/Silk.NET/tree/57e0f8643c07702a16e14c027b1f15d60809d15b/src/OpenGL/Extensions/Silk.NET.OpenGL.Extensions.ImGui

`ImGui.NET` のバージョンと同じタグの `c++` を参照しないと微妙に変化していることがある。

## slnx

C# ソリューション。たぶん、無くても動くが Editor の language server の
動きに影響がありそう。
