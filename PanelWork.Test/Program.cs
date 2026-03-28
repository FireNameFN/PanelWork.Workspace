using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using GreenPng;
using PanelWork;
using SDL3;
using Thermal.Bindings;
using Thermal.Core;
using Thermal.Extensions;
using Thermal.Shaders;
using Thermal.ThVk;
using Vortice.Vulkan;

Console.WriteLine("Hello, World!");

SDL.Init(SDL.InitFlags.Video);

SDL.VulkanLoadLibrary(null);

string[] extensions = SDL.VulkanGetInstanceExtensions(out _);

ThInstance instance = ThInstance.Create(VkVersion.Version_1_2, ["VK_LAYER_KHRONOS_validation"], extensions);

ThDeviceFeatures features = new() {
    ExtendedDynamicState = true
};

instance.TryCreateDevicePreferDiscrete((instance, physicalDevice, queueFamily, flags) => SDL.VulkanGetPresentationSupport(instance, physicalDevice, queueFamily), ["VK_KHR_swapchain", "VK_EXT_extended_dynamic_state"], features, out ThPhysicalDevice physicalDevice, out ThDevice device, out ThQueue queue);

Window window = new(flags: SDL.WindowFlags.Resizable);

VkSurfaceKHR surface = window.CreateSurface(instance.Handle.Instance);

Presenter presenter = new(device, physicalDevice, queue, surface) {
    Usage = VkImageUsageFlags.TransferDst
};

ThCommandPool pool = device.CreateCommmandPool(VkCommandPoolCreateFlags.ResetCommandBuffer, queue.QueueFamily);

ThCommandBuffer commandBuffer = pool.AllocateCommandBuffer(VkCommandBufferLevel.Primary);

ThFence fence = device.CreateFence();

device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment, VkImageLayout.Undefined, VkMemoryPropertyFlags.DeviceLocal, out ThImage renderTarget, out ThDeviceMemory memory);

ThImageView view = renderTarget.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba, new(VkImageAspectFlags.Color));

ThRenderPass renderPass = device.CreateRenderPass(new() {
    format = VkFormat.B8G8R8A8Srgb,
    samples = VkSampleCountFlags.Count1,
    loadOp = VkAttachmentLoadOp.Clear,
    storeOp = VkAttachmentStoreOp.Store,
    stencilLoadOp = VkAttachmentLoadOp.DontCare,
    stencilStoreOp = VkAttachmentStoreOp.DontCare,
    initialLayout = VkImageLayout.Undefined,
    finalLayout = VkImageLayout.TransferSrcOptimal
});

ThFramebuffer framebuffer = renderPass.CreateFramebuffer([view.Handle], 1280, 720);

ShaderCompiler shaderCompiler = new(device.Handle);

ShaderObject vertexShader = shaderCompiler.CompileVertex();

ShaderObject testShader = shaderCompiler.CompileTest();

ShaderObject solidShader = shaderCompiler.CompileSolid();

ShaderObject textureShader = shaderCompiler.CompileTexture();

ThPipeline testPipeline = new ThPipelineLayout(testShader.PipelineLayout, device.Handle).CreateGraphicsPipeline(renderPass.Handle, new() {
    VertexShader = vertexShader.ShaderModule,
    FragmentShader = testShader.ShaderModule,
    VertexSize = 16,
    VertexDescription = [new(0, VkFormat.R32G32Sfloat, 0), new(1, VkFormat.R32G32Sfloat, 8)]
});

ThPipeline solidPipeline = new ThPipelineLayout(solidShader.PipelineLayout, device.Handle).CreateGraphicsPipeline(renderPass.Handle, new() {
    VertexShader = vertexShader.ShaderModule,
    FragmentShader = solidShader.ShaderModule,
    VertexSize = 16,
    VertexDescription = [new(0, VkFormat.R32G32Sfloat, 0), new(1, VkFormat.R32G32Sfloat, 8)]
});

ThPipeline texturePipeline = new ThPipelineLayout(textureShader.PipelineLayout, device.Handle).CreateGraphicsPipeline(renderPass.Handle, new() {
    VertexShader = vertexShader.ShaderModule,
    FragmentShader = textureShader.ShaderModule,
    VertexSize = 16,
    VertexDescription = [new(0, VkFormat.R32G32Sfloat, 0), new(1, VkFormat.R32G32Sfloat, 8)]
});

ThSampler sampler = device.CreateSampler(VkFilter.Nearest, VkSamplerAddressMode.Repeat);

Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Test.Resources.OverGreen.png");

Span<byte> span = stackalloc byte[(int)stream.Length];

stream.ReadExactly(span);

Stream trayStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Test.Resources.tray.png");

Span<byte> traySpan = stackalloc byte[(int)trayStream.Length];

trayStream.ReadExactly(traySpan);

PngDecoder.TryDecodeHeader(span, out PngHeader header);

Span<byte> pixels = stackalloc byte[header.ByteSize];

PngDecoder.TryDecode(span, header, pixels);

Icon icon = Icon.CreateFromPng(traySpan);

nint tray = SDL.CreateTray(icon.Handle, "PanelWork");

nint menu = SDL.CreateTrayMenu(tray);

nint entry = SDL.InsertTrayEntryAt(menu, 0, "Toggle", SDL.TrayEntryFlags.Button);

SDL.SetTrayEntryCallback(entry, (entry, user) => {
    Console.WriteLine("Click");
}, 0);

ThDeviceImage texture = pool.CreateTexture(device, physicalDevice, queue, fence, pixels, header.Width, header.Height);

ThImageView textureView = texture.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba, new(VkImageAspectFlags.Color));

DescriptorStorage storage = new(device);

DescriptorStorageContext storageContext = storage.CreateContext();

VertexBuffer<Vertex> vertexBuffer = new(device, physicalDevice);

Vertex v1 = new() { Position = new(-0.5f, -0.5f), Offset = new(0, 0) };
Vertex v2 = new() { Position = new(-0.5f, 0.5f), Offset = new(0, 1) };
Vertex v3 = new() { Position = new(0.5f, -0.5f), Offset = new(1, 0) };
Vertex v4 = new() { Position = new(0.5f, 0.5f), Offset = new(1, 1) };

vertexBuffer.AddVertex(v1);
vertexBuffer.AddVertex(v2);
vertexBuffer.AddVertex(v3);
vertexBuffer.AddVertex(v4);

uint testIndex = vertexBuffer.Push();

v1 = new() { Position = new(-1f, -1f), Offset = new(0, 0) };
v2 = new() { Position = new(-1f, -0.9f), Offset = new(0, 1) };
v3 = new() { Position = new(-0.9f, -1f), Offset = new(1, 0) };
v4 = new() { Position = new(-0.9f, -0.9f), Offset = new(1, 1) };

vertexBuffer.AddVertex(v1);
vertexBuffer.AddVertex(v2);
vertexBuffer.AddVertex(v3);
vertexBuffer.AddVertex(v4);

uint solidIndex = vertexBuffer.Push();

v1 = new() { Position = new(-0.20f, -0.20f), Offset = new(0, 0) };
v2 = new() { Position = new(-0.20f, 0.20f), Offset = new(0, 1) };
v3 = new() { Position = new(0.20f, -0.20f), Offset = new(1, 0) };
v4 = new() { Position = new(0.20f, 0.20f), Offset = new(1, 1) };

vertexBuffer.AddVertex(v1);
vertexBuffer.AddVertex(v2);
vertexBuffer.AddVertex(v3);
vertexBuffer.AddVertex(v4);

uint textureIndex = vertexBuffer.Push();

vertexBuffer.Flush();

presenter.SetSize(1280, 720);

int width = 1280;

int height = 720;

bool running = true;

while(running) {
    SDL.WaitEvent(out SDL.Event e);

    do {
        SDL.EventType type = (SDL.EventType)e.Type;

        if(type == SDL.EventType.WindowResized) {
            Console.WriteLine("Resize");

            SDL.GetWindowSizeInPixels(window.Handle, out width, out height);

            framebuffer.Dispose();

            view.Dispose();

            renderTarget.Dispose();

            memory.Dispose();

            device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(width, height), 1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment, VkImageLayout.Undefined, VkMemoryPropertyFlags.DeviceLocal, out renderTarget, out memory);

            view = renderTarget.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba, new(VkImageAspectFlags.Color));

            framebuffer = renderPass.CreateFramebuffer([view.Handle], (uint)width, (uint)height);

            presenter.SetSize(width, height);
        }

        if(type == SDL.EventType.Quit || type == SDL.EventType.WindowCloseRequested) {
            running = false;
        }
    } while(SDL.PollEvent(out e));

    VkResult result = presenter.Acquire(ulong.MaxValue, out uint index, out ThImage image, out ThSemaphore semaphore);

    if(result == VkResult.ErrorOutOfDateKHR) {
        Console.WriteLine("Weh");

        continue;
    }

    device.Handle.vkBeginCommandBuffer(commandBuffer.Handle, VkCommandBufferUsageFlags.OneTimeSubmit);

    commandBuffer.BeginRenderPass(renderPass.Handle, framebuffer.Handle, new(0, 0, (uint)width, (uint)height), new(0, 1, 0), VkSubpassContents.Inline);

    device.Handle.vkCmdSetViewport(commandBuffer.Handle, 0, new VkViewport(width, height));

    device.Handle.vkCmdSetScissor(commandBuffer.Handle, 0, new VkRect2D(0, 0, (uint)width, (uint)height));

    device.Handle.vkCmdSetPrimitiveTopologyEXT(commandBuffer.Handle, VkPrimitiveTopology.TriangleStrip);

    device.Handle.vkCmdBindVertexBuffer(commandBuffer.Handle, 0, vertexBuffer.LastBuffer.BufferHandle);

    device.Handle.vkCmdBindPipeline(commandBuffer.Handle, VkPipelineBindPoint.Graphics, testPipeline.Handle);

    device.Handle.vkCmdDraw(commandBuffer.Handle, 4, 1, testIndex, 0);

    device.Handle.vkCmdBindPipeline(commandBuffer.Handle, VkPipelineBindPoint.Graphics, solidPipeline.Handle);

    Vector4 solidColor = new(1, 0, 1, 1);

    commandBuffer.PushConstants(solidShader.PipelineLayout, VkShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes([solidColor]));

    device.Handle.vkCmdDraw(commandBuffer.Handle, 4, 1, solidIndex, 0);

    device.Handle.vkCmdBindPipeline(commandBuffer.Handle, VkPipelineBindPoint.Graphics, texturePipeline.Handle);

    long t1 = Stopwatch.GetTimestamp();

    storageContext.ClearBindings();

    storageContext.AddBinding(new ThImageSamplerBinding(sampler.Handle, textureView.Handle, VkImageLayout.ShaderReadOnlyOptimal));

    VkDescriptorSet textureSet = storageContext.CreateDescriptorSet(textureShader.SetLayouts[0]);

    long t2 = Stopwatch.GetTimestamp();

    Console.WriteLine($"Storage {(t2 - t1) * 1000000d / Stopwatch.Frequency}");

    device.Handle.vkCmdBindDescriptorSets(commandBuffer.Handle, VkPipelineBindPoint.Graphics, textureShader.PipelineLayout, 0, textureSet);

    device.Handle.vkCmdDraw(commandBuffer.Handle, 4, 1, textureIndex, 0);

    device.Handle.vkCmdEndRenderPass(commandBuffer.Handle);

    commandBuffer.ImageBarrier(image.Handle, new() {
        SrcAccess = VkAccessFlags.None,
        DstAccess = VkAccessFlags.TransferWrite,
        OldLayout = VkImageLayout.Undefined,
        NewLayout = VkImageLayout.TransferDstOptimal,
        SrcStage = VkPipelineStageFlags.ColorAttachmentOutput,
        DstStage = VkPipelineStageFlags.Transfer
    });

    VkImageCopy copy = new() {
        srcSubresource = new(VkImageAspectFlags.Color, 0, 0, 1),
        dstSubresource = new(VkImageAspectFlags.Color, 0, 0, 1),
        extent = new(width, height, 1)
    };

    commandBuffer.CopyImage(renderTarget.Handle, VkImageLayout.TransferSrcOptimal, image.Handle, VkImageLayout.TransferDstOptimal, copy);

    commandBuffer.ImageBarrier(image.Handle, new() {
        SrcAccess = VkAccessFlags.TransferWrite,
        DstAccess = VkAccessFlags.None,
        OldLayout = VkImageLayout.TransferDstOptimal,
        NewLayout = VkImageLayout.PresentSrcKHR,
        SrcStage = VkPipelineStageFlags.Transfer,
        DstStage = VkPipelineStageFlags.BottomOfPipe
    });

    device.Handle.vkEndCommandBuffer(commandBuffer.Handle);

    queue.Submit(fence.Handle, [presenter.Semaphore.Handle], [VkPipelineStageFlags.Transfer], [commandBuffer.Handle], [semaphore.Handle]);

    presenter.Present(index);

    fence.Wait();

    fence.Reset();

    storage.Clear();

    Console.WriteLine("Frame");
}

Console.WriteLine("End");

struct Vertex {
    public Vector2 Position;

    public Vector2 Offset;
}
