using System.Runtime.CompilerServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

public class TextureObject : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    public readonly uint Width;
    public readonly uint Height;
    public readonly Image Image;
    private readonly DeviceMemory _memory;
    public readonly ImageView ImageView;
    public readonly Sampler Sampler;

    public unsafe TextureObject(
        Vk vk,
        PhysicalDevice physicalDevice,
        Device device,
        uint width,
        uint height,
        ImageUsageFlags usage
    )
    {
        _vk = vk;
        _device = device;
        Width = width;
        Height = height;

        CreateImage(
            _vk,
            physicalDevice,
            _device,
            width,
            height,
            Format.R8G8B8A8Unorm,
            ImageTiling.Optimal,
            usage, //ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            out Image,
            out _memory
        );

        ImageView = CreateImageView(
            _vk,
            _device,
            Image,
            Format.R8G8B8A8Unorm,
            ImageAspectFlags.ColorBit
        );

        var info = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MinLod = -1000,
            MaxLod = 1000,
            MaxAnisotropy = 1.0f,
        };
        if (vk.CreateSampler(_device, in info, default, out Sampler) != Result.Success)
        {
            throw new Exception($"Unable to create sampler");
        }
    }

    public static unsafe ImageView CreateImageView(
        Vk vk,
        Device device,
        Image image,
        Format format,
        ImageAspectFlags aspectFlags
    )
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = format,
            //Components =
            //    {
            //        R = ComponentSwizzle.Identity,
            //        G = ComponentSwizzle.Identity,
            //        B = ComponentSwizzle.Identity,
            //        A = ComponentSwizzle.Identity,
            //    },
            SubresourceRange =
            {
                AspectMask = aspectFlags,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        if (
            vk.CreateImageView(device, in createInfo, null, out ImageView imageView)
            != Result.Success
        )
        {
            throw new Exception("failed to create image views!");
        }

        return imageView;
    }

    public static unsafe void CreateImage(
        Vk vk,
        PhysicalDevice physicalDevice,
        Device device,
        uint width,
        uint height,
        Format format,
        ImageTiling tiling,
        ImageUsageFlags usage,
        MemoryPropertyFlags properties,
        out Image image,
        out DeviceMemory imageMemory
    )
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Extent =
            {
                Width = width,
                Height = height,
                Depth = 1,
            },
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
        };

        fixed (Image* imagePtr = &image)
        {
            if (vk.CreateImage(device, in imageInfo, null, imagePtr) != Result.Success)
            {
                throw new Exception("failed to create image!");
            }
        }

        vk.GetImageMemoryRequirements(device, image, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                vk,
                physicalDevice,
                memRequirements.MemoryTypeBits,
                properties
            ),
        };

        fixed (DeviceMemory* imageMemoryPtr = &imageMemory)
        {
            if (vk.AllocateMemory(device, in allocInfo, null, imageMemoryPtr) != Result.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }
        }

        vk.BindImageMemory(device, image, imageMemory, 0);
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

    public unsafe TextureObject(
        Vk vk,
        PhysicalDevice physicalDevice,
        Device device,
        uint width,
        uint height,
        uint graphicsQueueFamilyIndex,
        ReadOnlySpan<byte> pixels
    )
        : this(
            vk,
            physicalDevice,
            device,
            width,
            height,
            ImageUsageFlags.SampledBit | ImageUsageFlags.TransferDstBit
        )
    {
        fixed (void* p = pixels)
        {
            Upload(physicalDevice, graphicsQueueFamilyIndex, new nint(p));
        }
    }

    public unsafe void Dispose()
    {
        _vk.DestroySampler(_device, Sampler, default);
        _vk.DestroyImageView(_device, ImageView, default);
        _vk.DestroyImage(_device, Image, default);
        _vk.FreeMemory(_device, _memory, default);
    }

    public unsafe void Upload(
        PhysicalDevice physicalDevice,
        uint graphicsQueueFamilyIndex,
        IntPtr pixels
    )
    {
        var upload_size = (ulong)(Width * Height * 4 * sizeof(byte));

        CreateBuffer(
            _vk,
            physicalDevice,
            _device,
            upload_size,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit,
            out var uploadBuffer,
            out var uploadBufferMemory
        );
        void* map = null;
        if (
            _vk.MapMemory(_device, uploadBufferMemory, 0, upload_size, 0, (void**)(&map))
            != Result.Success
        )
        {
            throw new Exception($"Failed to map device memory");
        }
        Unsafe.CopyBlock(map, pixels.ToPointer(), (uint)upload_size);
        var range = new MappedMemoryRange
        {
            SType = StructureType.MappedMemoryRange,
            Memory = uploadBufferMemory,
            Size = upload_size,
        };
        if (_vk.FlushMappedMemoryRanges(_device, 1, in range) != Result.Success)
        {
            throw new Exception($"Failed to flush memory to device");
        }
        _vk.UnmapMemory(_device, uploadBufferMemory);

        using var ot = new OneTimeCommandBuffer(_vk, _device, graphicsQueueFamilyIndex);
        ot.Execute(commandBuffer =>
        {
            TransitionImageLayout(_vk, commandBuffer, Image, ImageLayout.TransferDstOptimal);

            var region = new BufferImageCopy
            {
                ImageSubresource = new ImageSubresourceLayers
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    LayerCount = 1,
                },
                ImageExtent = new Extent3D
                {
                    Width = Width,
                    Height = Height,
                    Depth = 1,
                },
            };
            _vk.CmdCopyBufferToImage(
                commandBuffer,
                uploadBuffer,
                Image,
                ImageLayout.TransferDstOptimal,
                1,
                &region
            );

            TransitionImageLayout(
                _vk,
                commandBuffer,
                Image,
                ImageLayout.ShaderReadOnlyOptimal
            );
        });
        _vk.DestroyBuffer(_device, uploadBuffer, default);
        _vk.FreeMemory(_device, uploadBufferMemory, default);
    }

    public static unsafe void TransitionImageLayout(
        Vk vk,
        CommandBuffer commandBuffer,
        Image image,
        ImageLayout newLayout
    )
    {
        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = ImageLayout.Undefined,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1,
            },
        };

        vk.CmdPipelineBarrier(
            commandBuffer,
            PipelineStageFlags.BottomOfPipeBit,
            PipelineStageFlags.TopOfPipeBit,
            0,
            0,
            null,
            0,
            null,
            1,
            in barrier
        );
    }

    public static unsafe void CreateBuffer(
        Vk vk,
        PhysicalDevice physicalDevice,
        Device device,
        ulong size,
        BufferUsageFlags usage,
        MemoryPropertyFlags properties,
        out Buffer buffer,
        out DeviceMemory bufferMemory
    )
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
        };

        fixed (Buffer* bufferPtr = &buffer)
        {
            if (vk.CreateBuffer(device, in bufferInfo, null, bufferPtr) != Result.Success)
            {
                throw new Exception("failed to create vertex buffer!");
            }
        }

        MemoryRequirements memRequirements = new();
        vk.GetBufferMemoryRequirements(device, buffer, out memRequirements);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(
                vk,
                physicalDevice,
                memRequirements.MemoryTypeBits,
                properties
            ),
        };

        fixed (DeviceMemory* bufferMemoryPtr = &bufferMemory)
        {
            if (vk.AllocateMemory(device, in allocateInfo, null, bufferMemoryPtr) != Result.Success)
            {
                throw new Exception("failed to allocate vertex buffer memory!");
            }
        }

        vk.BindBufferMemory(device, buffer, bufferMemory, 0);
    }
}
