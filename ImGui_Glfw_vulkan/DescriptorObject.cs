using Silk.NET.Vulkan;

public class DescriptorPoolObject : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    public readonly DescriptorSetLayout _layout;
    private readonly DescriptorPool _descriptorPool;
    public readonly DescriptorSet[] DescriptorSets;
    int _pos;
    uint _lastFrameCount = uint.MaxValue;

    public unsafe DescriptorPoolObject(
        Vk vk,
        Device device,
        DescriptorSetLayout layout,
        ReadOnlySpan<DescriptorSetLayoutBinding> binds,
        // scene 内での primitive 数必要
        uint maxSets
    )
    {
        _vk = vk;
        _device = device;
        _layout = layout;

        Dictionary<DescriptorType, uint> counter = new();
        foreach (var bind in binds)
        {
            if (counter.TryGetValue(bind.DescriptorType, out var value))
            {
                counter[bind.DescriptorType] = value + maxSets;
            }
            else
            {
                counter[bind.DescriptorType] = maxSets;
            }
        }
        var poolSizes = counter
            .Select(x => new DescriptorPoolSize { Type = x.Key, DescriptorCount = x.Value })
            .ToArray();

        fixed (DescriptorPoolSize* ppoolSizes = poolSizes)
        {
            DescriptorPoolCreateInfo poolInfo = new()
            {
                SType = StructureType.DescriptorPoolCreateInfo,
                PoolSizeCount = (uint)poolSizes.Length,
                PPoolSizes = ppoolSizes,
                MaxSets = maxSets,
            };
            if (
                vk.CreateDescriptorPool(device, in poolInfo, null, out _descriptorPool)
                != Result.Success
            )
            {
                throw new Exception("failed to create descriptor pool!");
            }
        }

        AllocateDescriptorSets(vk, device, _descriptorPool, _layout, maxSets, out DescriptorSets);
    }

    public static unsafe void AllocateDescriptorSets(
        Vk vk,
        Device device,
        DescriptorPool pool,
        DescriptorSetLayout layout,
        uint maxSets,
        out DescriptorSet[] descriptorSets
    )
    {
        descriptorSets = new DescriptorSet[maxSets];

        var layouts = stackalloc DescriptorSetLayout[(int)maxSets];
        new Span<DescriptorSetLayout>(layouts, (int)maxSets).Fill(layout);
        var allocateInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = pool,
            DescriptorSetCount = maxSets,
            PSetLayouts = layouts,
        };
        fixed (DescriptorSet* descriptorSetsPtr = descriptorSets)
        {
            if (
                vk.AllocateDescriptorSets(device, in allocateInfo, descriptorSetsPtr)
                != Result.Success
            )
            {
                throw new Exception("failed to allocate descriptor sets!");
            }
        }
    }

    public unsafe void Dispose()
    {
        _vk.DestroyDescriptorPool(_device, _descriptorPool, null);
    }

    public DescriptorSet Get(uint frameCount)
    {
        if (_lastFrameCount != frameCount)
        {
            _pos = 0;
        }
        _lastFrameCount = frameCount;
        return DescriptorSets[_pos++];
    }
}
