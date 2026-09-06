using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

public class VkShaderModule : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    public readonly ShaderModule Module;

    public unsafe VkShaderModule(Vk vk, Device device, ReadOnlySpan<byte> code)
    {
        _vk = vk;
        _device = device;

        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length,
        };
        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;
            if (vk.CreateShaderModule(device, in createInfo, null, out Module) != Result.Success)
            {
                throw new Exception();
            }
        }
    }

    public VkShaderModule(Vk vk, Device device, ReadOnlySpan<uint> code)
        : this(vk, device, MemoryMarshal.Cast<uint, byte>(code)) { }

    public unsafe void Dispose()
    {
        _vk.DestroyShaderModule(_device, Module, null);
    }
}
