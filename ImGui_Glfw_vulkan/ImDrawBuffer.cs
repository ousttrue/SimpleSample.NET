using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Vulkan;

class ImDrawVertBuffer : IDisposable
{
    public ArrayBufferObject Vertex;
    public ArrayBufferObject Index;

    public ImDrawVertBuffer(Vk vk, Device device)
    {
        Vertex = new ArrayBufferObject(
            vk,
            device,
            BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.HostVisibleBit,
            (uint)Marshal.SizeOf<ImDrawVert>()
        );
        Index = new ArrayBufferObject(
            vk,
            device,
            BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.HostVisibleBit,
            (uint)Marshal.SizeOf<ushort>()
        );
    }

    public void Dispose()
    {
        Vertex.Dispose();
        Index.Dispose();
    }

    public unsafe void UploadDrawData(
        Vk vk,
        PhysicalDevice physicalDevice,
        Device device,
        ImDrawDataPtr drawDataPtr
    )
    {
        var drawData = *drawDataPtr.NativePtr;
        if (drawData.TotalVtxCount <= 0)
        {
            return;
        }

        // Create or resize the vertex/index buffers
        ulong vertex_size = (ulong)drawData.TotalVtxCount;
        Vertex.Grow(physicalDevice, vertex_size);

        ulong index_size = (ulong)drawData.TotalIdxCount;
        Index.Grow(physicalDevice, index_size);

        // Upload vertex/index data into a single contiguous GPU buffer
        using var vertexMap = Vertex.Map();
        var vtx_dst = vertexMap.ToPointer<ImDrawVert>();
        using var indexMap = Index.Map();
        var idx_dst = indexMap.ToPointer<ushort>();
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawList* cmd_list = drawDataPtr.CmdLists[n];
            Unsafe.CopyBlock(
                vtx_dst,
                cmd_list->VtxBuffer.Data.ToPointer(),
                (uint)cmd_list->VtxBuffer.Size * (uint)sizeof(ImDrawVert)
            );
            Unsafe.CopyBlock(
                idx_dst,
                cmd_list->IdxBuffer.Data.ToPointer(),
                (uint)cmd_list->IdxBuffer.Size * (uint)sizeof(ushort)
            );
            vtx_dst += cmd_list->VtxBuffer.Size;
            idx_dst += cmd_list->IdxBuffer.Size;
        }
    }
};
