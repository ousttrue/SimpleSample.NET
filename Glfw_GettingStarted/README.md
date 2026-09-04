# Silk.NET.GLFW を使う

Silk.NET の Glfw は `IWindow` interface でラップされている。
直接 Glfw を使う用例。
glfw3.dll も含まれており簡単に使える。

```
$ dotnet add package Silk.NET.GLFW
```

https://www.glfw.org/docs/latest/quick_guide.html#quick_example

を移植。

## GLFW

関数ポインターをゲットする感じ？

```cs
var glfw = GlfwProvider.GLFW.Value;
```

## OpenGL

`c` では `gladLoadGL(glfwGetProcAddress)` に相当するところ。

```
$ dotnet add package Silk.NET.OpenGL
```

```cs
        glfw.MakeContextCurrent(window);
        gl = GL.GetApi(new GlfwContext(glfw, window));
```

### unsafe のとりまわし

`fixed` で `void*` を取得するか、 `ref vertices[0]` のように参照を渡す。
`stackalloc` で `T*` を作って渡すのもあり。

## System.Numerics

`c` の `#include "linmath.h"` に相当するところ。
`Silk.NET.Maths` もあるのだけど `System.Numerics` を勧めたい。
`Vector2`, `Vector3`, `Matrix4x4` など完備。
仕様は、`DirectXMath` に似ている。

メモリーレイアウトは `row major` で

```
[M11(00), M12(01), M13(02), M14(03)]
[M21(04), M22(05), M23(06), M24(07)]
[M31(08), M32(09), M33(10), M34(11)]
[M41(12), M42(13), M43(14), M44(15)]
```

```
[行ベクトル][m][v][p]
```

の乗算順。

### UnityEngine.Matrix4x4 と違う

`UnityEngine.Matrix4x4` は `column major` で

```
[M11(00), M12(04), M13(08), M14(12)]
[M21(01), M22(05), M23(09), M24(13)]
[M31(02), M32(06), M33(10), M34(14)]
[M41(03), M42(07), M43(11), M44(15)]
```

```
[p][v][m][列]
         [ベ]
         [ク]
         [ト]
         [ル]
```

の乗算順。

> (12,13,14,15) が Translation になるので結果として同じ？なのである。
