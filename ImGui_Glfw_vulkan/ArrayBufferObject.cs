using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

public class ArrayBufferObject(
    Vk vk,
    Device device,
    BufferUsageFlags usage,
    MemoryPropertyFlags memoryProps,
    uint stride
) : IDisposable
{
    private readonly Vk _vk = vk;
    private readonly Device _device = device;
    private readonly BufferUsageFlags _usage = usage;
    private readonly MemoryPropertyFlags _memoryProps = memoryProps;
    private uint _stride = stride;
    private ulong _itemCount = 0;
    public ulong ByteLength => _stride * _itemCount;
    public Buffer Buffer;
    private DeviceMemory _memory;
    private ulong _bufferMemoryAlignment = 256;

    public ArrayBufferObject(
        Vk vk,
        Device device,
        BufferUsageFlags usage,
        MemoryPropertyFlags memoryProps,
        PhysicalDevice physicalDevice,
        uint stride,
        ulong itemCount
    )
        : this(vk, device, usage, memoryProps, stride)
    {
        Grow(physicalDevice, itemCount);
    }

    /// <summary>
    /// use MemoryPropertyFlags.HostVisibleBit and map
    /// </summary>
    public static unsafe ArrayBufferObject Create<T>(
        Vk vk,
        Device device,
        BufferUsageFlags usage,
        PhysicalDevice physicalDevice,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        var self = new ArrayBufferObject(
            vk,
            device,
            usage,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            physicalDevice,
            (uint)Marshal.SizeOf<T>(),
            (uint)values.Length
        );
        using (var map = self.Map())
        {
            values.CopyTo(new Span<T>(map.ToPointer<T>(), values.Length));
        }
        return self;
    }

    public static unsafe ArrayBufferObject Create(
        Vk vk,
        Device device,
        BufferUsageFlags usage,
        PhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        uint stride,
        ReadOnlySpan<byte> data
    )
    {
        var self = new ArrayBufferObject(
            vk,
            device,
            usage,
            MemoryPropertyFlags.DeviceLocalBit,
            physicalDevice,
            stride,
            (uint)(data.Length / stride)
        );
        using var singleTimeCommand = new OneTimeCommandBuffer(
            vk,
            device,
            graphicsQueueFamilyIndex
        );
        using (
            var staging = Create(vk, device, BufferUsageFlags.TransferSrcBit, physicalDevice, data)
        )
        {
            singleTimeCommand.Execute(commandBuffer =>
            {
                CopyBuffer(vk, commandBuffer, staging.Buffer, self.Buffer, staging.ByteLength);
            });
        }
        return self;
    }

    public static void CopyBuffer(
        Vk vk,
        CommandBuffer commandBuffer,
        Buffer srcBuffer,
        Buffer dstBuffer,
        ulong size
    )
    {
        BufferCopy copyRegion = new() { Size = size };
        vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, in copyRegion);
    }

    public static unsafe ArrayBufferObject Create<T>(
        Vk vk,
        Device device,
        BufferUsageFlags usage,
        PhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<T> values
    )
        where T : unmanaged
    {
        return Create(
            vk,
            device,
            usage,
            physicalDevice,
            graphicsQueueFamilyIndex,
            (uint)Marshal.SizeOf<T>(),
            MemoryMarshal.Cast<T, byte>(values)
        );
    }

    public unsafe void Dispose()
    {
        _vk.DestroyBuffer(_device, Buffer, default);
        _vk.FreeMemory(_device, _memory, default);
    }

    public unsafe void Grow(PhysicalDevice physicalDevice, ulong itemCount)
    {
        if (Buffer.Handle != default && _itemCount >= itemCount)
        {
            return;
        }

        if (Buffer.Handle != default)
        {
            _vk.DestroyBuffer(_device, Buffer, default);
        }
        if (_memory.Handle != default)
        {
            _vk.FreeMemory(_device, _memory, default);
        }

        // VkHelper.CreateBuffer(
        //     _vk,
        //     physicalDevice,
        //     _device,
        //     bufferSize,
        //     ,
        //     MemoryPropertyFlags.DeviceLocalBit,
        //     ref VertexBuffer,
        //     ref vertexBufferMemory
        // );

        ulong sizeAlignedVertexBuffer =
            ((itemCount * _stride - 1) / _bufferMemoryAlignment + 1) * _bufferMemoryAlignment;
        var bufferInfo = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = sizeAlignedVertexBuffer,
            Usage = _usage,
            SharingMode = SharingMode.Exclusive,
        };
        if (_vk.CreateBuffer(_device, in bufferInfo, default, out Buffer) != Result.Success)
        {
            throw new Exception($"Unable to create a device buffer");
        }

        _vk.GetBufferMemoryRequirements(_device, Buffer, out var req);
        _bufferMemoryAlignment =
            (_bufferMemoryAlignment > req.Alignment) ? _bufferMemoryAlignment : req.Alignment;
        MemoryAllocateInfo allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = req.Size,
            MemoryTypeIndex = FindMemoryType(_vk, physicalDevice, req.MemoryTypeBits, _memoryProps),
        };
        if (_vk.AllocateMemory(_device, &allocInfo, default, out _memory) != Result.Success)
        {
            throw new Exception($"Unable to allocate device memory");
        }

        if (_vk.BindBufferMemory(_device, Buffer, _memory, 0) != Result.Success)
        {
            throw new Exception($"Unable to bind device memory");
        }
        _itemCount = req.Size / _stride;
    }

    public static uint FindMemoryType(
        Vk vk,
        PhysicalDevice physicalDevice,
        uint typeFilter,
        MemoryPropertyFlags properties
    )
    {
        vk.GetPhysicalDeviceMemoryProperties(
            physicalDevice,
            out PhysicalDeviceMemoryProperties memProperties
        );

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if (
                (typeFilter & (1 << i)) != 0
                && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties
            )
            {
                return (uint)i;
            }
        }

        throw new Exception("failed to find suitable memory type!");
    }

    public class MemoryMap(Vk vk, Device device, DeviceMemory Memory, IntPtr Ptr) : IDisposable
    {
        private readonly Vk _vk = vk;
        private readonly Device _device = device;
        private readonly DeviceMemory _memory = Memory;
        private readonly nint _ptr = Ptr;

        public unsafe T* ToPointer<T>()
            where T : unmanaged
        {
            return (T*)_ptr.ToPointer();
        }

        public unsafe void Dispose()
        {
            // Span<MappedMemoryRange> range = stackalloc MappedMemoryRange[2];
            // range[0].SType = StructureType.MappedMemoryRange;
            // range[0].Memory = Vertex.Memory;
            // range[0].Size = Vk.WholeSize;
            // range[1].SType = StructureType.MappedMemoryRange;
            // range[1].Memory = Index.Memory;
            // range[1].Size = Vk.WholeSize;
            var range = new MappedMemoryRange
            {
                SType = StructureType.MappedMemoryRange,
                Memory = _memory,
                Size = Vk.WholeSize,
            };
            if (_vk.FlushMappedMemoryRanges(_device, 1, &range) != Result.Success)
            {
                throw new Exception($"Unable to flush memory to device");
            }
            _vk.UnmapMemory(_device, _memory);
        }
    }

    public unsafe MemoryMap Map()
    {
        void* p;
        if (_vk.MapMemory(_device, _memory, 0, Vk.WholeSize, 0, (void**)(&p)) != Result.Success)
        {
            throw new Exception($"Unable to map device memory");
        }
        return new MemoryMap(_vk, _device, _memory, new IntPtr(p));
    }

    public unsafe void Bind(CommandBuffer commandBuffer)
    {
        if (_usage.HasFlag(BufferUsageFlags.VertexBufferBit))
        {
            ulong vertex_offset = 0;
            var buffer = Buffer;
            _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &buffer, &vertex_offset);
        }
        else if (_usage.HasFlag(BufferUsageFlags.IndexBufferBit))
        {
            IndexType indexType;
            switch (_stride)
            {
                case 2:
                    indexType = IndexType.Uint16;
                    break;
                case 4:
                    indexType = IndexType.Uint32;
                    break;
                default:
                    throw new Exception();
            }

            _vk.CmdBindIndexBuffer(commandBuffer, Buffer, 0, indexType);
        }
        else
        {
            throw new Exception();
        }
    }

    public void Draw(CommandBuffer commandBuffer)
    {
        Draw(commandBuffer, 0, (uint)_itemCount);
    }

    public void Draw(CommandBuffer commandBuffer, uint offset, uint count)
    {
        if (_usage.HasFlag(BufferUsageFlags.VertexBufferBit))
        {
            _vk.CmdDraw(commandBuffer, count, 1, offset, 0);
        }
        else if (_usage.HasFlag(BufferUsageFlags.IndexBufferBit))
        {
            _vk.CmdDrawIndexed(commandBuffer, count, 1, offset, 0, 0);
        }
        else
        {
            throw new Exception();
        }
    }
}
