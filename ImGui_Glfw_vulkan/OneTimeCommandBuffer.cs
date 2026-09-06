using Silk.NET.Vulkan;

public class OneTimeCommandBuffer : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Queue _graphicsQueue;
    private readonly CommandPool _pool;

    public OneTimeCommandBuffer(Vk vk, Device device, uint graphicsQueueFamilyIndex)
    {
        _vk = vk;
        _device = device;
        _graphicsQueue = _vk.GetDeviceQueue(_device, graphicsQueueFamilyIndex, 0);
        _pool = CreateCommandPool(vk, device, graphicsQueueFamilyIndex);
    }

    public static unsafe CommandPool CreateCommandPool(
        Vk vk,
        Device device,
        uint graphicsQueueFamilyIndex
    )
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = graphicsQueueFamilyIndex,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
        };
        if (vk.CreateCommandPool(device, in poolInfo, null, out var commandPool) != Result.Success)
        {
            throw new Exception("failed to create command pool!");
        }
        return commandPool;
    }

    public unsafe void Dispose()
    {
        _vk.DestroyCommandPool(_device, _pool, null);
    }

    private CommandBuffer Begin()
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _pool,
            CommandBufferCount = 1,
        };

        _vk.AllocateCommandBuffers(_device, in allocateInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
        };

        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        return commandBuffer;
    }

    private unsafe void End(CommandBuffer commandBuffer)
    {
        _vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer,
        };

        _vk.QueueSubmit(_graphicsQueue, 1, in submitInfo, default);
        _vk.QueueWaitIdle(_graphicsQueue);

        _vk.FreeCommandBuffers(_device, _pool, 1, in commandBuffer);
    }

    public void Execute(Action<CommandBuffer> callback)
    {
        var commandBuffer = Begin();
        callback(commandBuffer);
        End(commandBuffer);
    }
}
