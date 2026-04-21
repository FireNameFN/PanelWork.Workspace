using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using GreenPng;
using PanelWork;
using SDL3;
using Thermal.Bindings;
using Thermal.Core;
using Thermal.Extensions;
using Thermal.Fonts;
using Thermal.Meshes;
using Thermal.Models;
using Thermal.Primitives;
using Thermal.Shaders;
using Thermal.ThVk;
using Vortice.Vulkan;

//BenchmarkRunner.Run<FontBenchmark>();

//return;

SDL.SetHint("SDL_VIDEO_DRIVER", "wayland,x11,cocoa,windows");

SDL.Init(SDL.InitFlags.Video);

Console.WriteLine(SDL.GetCurrentVideoDriver());

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
    Usage = VkImageUsageFlags.TransferDst,
    PresentMode = VkPresentModeKHR.Mailbox
};

ThCommandPool pool = device.CreateCommmandPool(queue.QueueFamily);

ThCommandBuffer commandBuffer = pool.AllocateCommandBuffer(VkCommandBufferLevel.Primary);

Command command = new(device, queue);

ThFence fence = device.CreateFence();

ThDeviceImage renderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment);

ThImageView view = renderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

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

ShaderBuilder shaderBuilder = new(device);

VertexShaderLayout vertexShader = shaderBuilder.BuildVertex();

ShaderLayout testShader = shaderBuilder.BuildTest();

ShaderLayout solidShader = shaderBuilder.BuildSolid();

ShaderLayout textureShader = shaderBuilder.BuildTexture();

PipelineLayout testLayout = PipelineLayout.Create(device, vertexShader, testShader);

PipelineLayout solidLayout = PipelineLayout.Create(device, vertexShader, solidShader);

PipelineLayout textureLayout = PipelineLayout.Create(device, vertexShader, textureShader);

Pipeline testPipeline = testLayout.CreatePipeline(renderPass.Handle);

Pipeline solidPipeline = solidLayout.CreatePipeline(renderPass.Handle);

Pipeline texturePipeline = textureLayout.CreatePipeline(renderPass.Handle);

ThSampler sampler = device.CreateSampler(VkFilter.Nearest, VkSamplerAddressMode.Repeat);

Stream trayStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Test.Resources.tray.png");

Span<byte> traySpan = stackalloc byte[(int)trayStream.Length];

trayStream.ReadExactly(traySpan);

Icon icon = Icon.CreateFromPng(traySpan);

nint tray = SDL.CreateTray(icon.Handle, "PanelWork");

nint menu = SDL.CreateTrayMenu(tray);

nint entry = SDL.InsertTrayEntryAt(menu, 0, "Toggle", SDL.TrayEntryFlags.Button);

SDL.SetTrayEntryCallback(entry, (entry, user) => {
    Console.WriteLine("Click");
}, 0);

Atlas atlas = new(512, 512);

foreach(string name in Assembly.GetExecutingAssembly().GetManifestResourceNames()) {
    if(!name.StartsWith("PanelWork.Test.Resources.textures."))
        continue;

    using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);

    byte[] data = new byte[stream.Length];

    stream.ReadExactly(data);

    byte[] pixels = PngDecoder.Decode(data, out PngHeader header);

    atlas.Add(pixels, header.Width, header.Height);
}

ThDeviceImage texture = atlas.CreateTexture(command, physicalDevice);

Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Test.Resources.inter.ttf");

byte[] fontData = new byte[fontStream.Length];

fontStream.ReadExactly(fontData);

FontFactory fontFactory = new();

Font font = fontFactory.CreateFont(fontData);

FontMap map = font.Render(command, physicalDevice, 24);

//ThImageView fontTexture = map.Image.Image.CreateImageView(VkFormat.R8Srgb, new(VkComponentSwizzle.R, VkComponentSwizzle.G, VkComponentSwizzle.B, VkComponentSwizzle.One));

ThImageView fontTexture = map.Image.Image.CreateImageView(VkFormat.R8Srgb, new(VkComponentSwizzle.One, VkComponentSwizzle.One, VkComponentSwizzle.One, VkComponentSwizzle.R));

ThImageView textureView = texture.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

DescriptorStorage storage = new(device);

VertexBuffer<Vertex> vertexBuffer = new(device, physicalDevice);

Rect testRect = Rect.Create(320, 60, 600 + 320, 600 + 60);

Rect solidRect = Rect.Create(0, 0, 100, 100);

Viewport viewport = Viewport.Create(0, 0, 1280, 720);

presenter.SetSize(1280, 720);

DrawContext context = new(device, storage.CreateContext(), commandBuffer.Handle);

DrawHandle<Vertex> handle = new(vertexBuffer, commandBuffer.Handle);

Span<Vertex> vertices = stackalloc Vertex[200];

int width = 1280;

int height = 720;

float angle = 0;

bool running = true;

while(running) {
    SDL.WaitEvent(out SDL.Event e);

    //SDL.PollEvent(out SDL.Event e);

    do {
        SDL.EventType type = (SDL.EventType)e.Type;

        if(type == SDL.EventType.WindowResized) {
            Console.WriteLine("Resize");

            SDL.GetWindowSizeInPixels(window.Handle, out width, out height);

            framebuffer.Dispose();

            view.Dispose();

            renderTarget.Dispose();

            presenter.SetSize(width, height);

            width = presenter.Width;
            height = presenter.Height;

            renderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(width, height), 1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment);

            view = renderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

            framebuffer = renderPass.CreateFramebuffer([view.Handle], (uint)width, (uint)height);
        }

        if(type == SDL.EventType.Quit || type == SDL.EventType.WindowCloseRequested) {
            running = false;
        }
    } while(SDL.PollEvent(out e));

    presenter.Acquire(ulong.MaxValue, out uint index, out ThImage image, out ThSemaphore semaphore);

    device.Handle.vkBeginCommandBuffer(commandBuffer.Handle, VkCommandBufferUsageFlags.OneTimeSubmit);

    commandBuffer.BeginRenderPass(renderPass.Handle, framebuffer.Handle, new(0, 0, (uint)width, (uint)height), new(0, 1, 0), VkSubpassContents.Inline);

    device.Handle.vkCmdSetViewport(commandBuffer.Handle, 0, new VkViewport(width, height));

    device.Handle.vkCmdSetScissor(commandBuffer.Handle, 0, new VkRect2D(0, 0, (uint)width, (uint)height));

    //

    device.Handle.vkCmdSetPrimitiveTopologyEXT(commandBuffer.Handle, VkPrimitiveTopology.TriangleStrip);

    //

    context.BindPipeline(testPipeline);

    context.Push(viewport);

    handle.Draw(testRect);

    //

    context.BindPipeline(solidPipeline);

    context.Push(new SolidPush() {
        Viewport = viewport,
        Color = new Vector4(1, 0, 1, 1)
    });

    handle.Draw(solidRect);

    //

    context.BindPipeline(texturePipeline);

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, textureView.Handle));

    context.NextDescriptorSet();

    context.Bind();

    handle.Draw(Rect.Create(320, 60, 600 + 320, 600 + 60, angle * MathF.Tau / 360));

    //

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, fontTexture.Handle));

    context.NextDescriptorSet();

    context.Bind();

    //handle.Draw(testRect);

    //

    int length = TextModel.CreateModel(map, "London is the capital of Great Britain!", vertices);

    context.BindPipeline(texturePipeline);

    context.Push(new Vector4(viewport.X, viewport.Y, 0, 0));

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, fontTexture.Handle));

    context.NextDescriptorSet();

    context.Bind();

    device.Handle.vkCmdSetPrimitiveTopologyEXT(commandBuffer.Handle, VkPrimitiveTopology.TriangleList);

    handle.Draw(vertices[..length]);

    //

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

    commandBuffer.CopyImage(renderTarget.Image.Handle, VkImageLayout.TransferSrcOptimal, image.Handle, VkImageLayout.TransferDstOptimal, copy);

    commandBuffer.ImageBarrier(image.Handle, new() {
        SrcAccess = VkAccessFlags.TransferWrite,
        DstAccess = VkAccessFlags.None,
        OldLayout = VkImageLayout.TransferDstOptimal,
        NewLayout = VkImageLayout.PresentSrcKHR,
        SrcStage = VkPipelineStageFlags.Transfer,
        DstStage = VkPipelineStageFlags.BottomOfPipe
    });

    device.Handle.vkEndCommandBuffer(commandBuffer.Handle);

    vertexBuffer.Flush();

    queue.Submit(fence.Handle, [presenter.Semaphore.Handle], [VkPipelineStageFlags.Transfer], [commandBuffer.Handle], [semaphore.Handle]);

    presenter.Present(index);

    fence.Wait();

    fence.Reset();

    pool.Reset();

    storage.Clear();

    vertexBuffer.Clear();

    angle += 0.5f;

    Console.WriteLine("Frame");
}

Console.WriteLine("End");

struct SolidPush {
    public Viewport Viewport;

    public Vector4 Color;
}

[MemoryDiagnoser(false)]
public class FontBenchmark {
    FontFactory factory;

    ThDevice device;

    byte[] fontData;

    Font font;

    Command command;

    ThPhysicalDevice physicalDevice;

    FontMap map;

    Vertex[] vertices = new Vertex[200];

    [GlobalSetup]
    public void Setup() {
        Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Test.Resources.inter.ttf");

        fontData = new byte[fontStream.Length];

        fontStream.ReadExactly(fontData);

        factory = new();

        font = factory.CreateFont(fontData);

        factory.CreateFont(fontData);

        ThInstance instance = ThInstance.Create(VkVersion.Version_1_2, [], []);

        instance.TryCreateDevicePreferDiscrete((_, _, _, _) => true, [], new(), out physicalDevice, out device, out ThQueue queue);

        command = new(device, queue);

        map = font.Render(command, physicalDevice, 24);
    }

    [Benchmark]
    public FontMap RenderFont() {
        return font.Render(command, physicalDevice, 24);
    }

    [Benchmark]
    public Vertex CreateModel() {
        int length = TextModel.CreateModel(map, "London is the capital of Great Britain!", vertices);

        return vertices[length - 1];
    }
}
