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
    Features = new() {
        sampleRateShading = true
    }
};

instance.TryCreateDevicePreferDiscrete((instance, physicalDevice, queueFamily, flags) => SDL.VulkanGetPresentationSupport(instance, physicalDevice, queueFamily), ["VK_KHR_swapchain"], features, out ThPhysicalDevice physicalDevice, out ThDevice device, out ThQueue queue);

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

ThDeviceImage colorRenderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkSampleCountFlags.Count8, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment);

ThImageView colorView = colorRenderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

ThDeviceImage resolveRenderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkSampleCountFlags.Count1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment);

ThImageView resolveView = resolveRenderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

VkAttachmentDescription colorAttachment = new() {
    format = VkFormat.B8G8R8A8Srgb,
    samples = VkSampleCountFlags.Count8,
    loadOp = VkAttachmentLoadOp.Clear,
    storeOp = VkAttachmentStoreOp.DontCare,
    stencilLoadOp = VkAttachmentLoadOp.DontCare,
    stencilStoreOp = VkAttachmentStoreOp.DontCare,
    initialLayout = VkImageLayout.Undefined,
    finalLayout = VkImageLayout.ColorAttachmentOptimal
};

VkAttachmentDescription resolveAttachment = new() {
    format = VkFormat.B8G8R8A8Srgb,
    samples = VkSampleCountFlags.Count1,
    loadOp = VkAttachmentLoadOp.DontCare,
    storeOp = VkAttachmentStoreOp.Store,
    stencilLoadOp = VkAttachmentLoadOp.DontCare,
    stencilStoreOp = VkAttachmentStoreOp.DontCare,
    initialLayout = VkImageLayout.Undefined,
    finalLayout = VkImageLayout.TransferSrcOptimal
};

ThRenderPass.SubpassDescriptionSpan subpassDescriptionSpan = new() {
    PipelineBindPoints = [VkPipelineBindPoint.Graphics],
    Input = new([0], []),
    Color = new([1], [new VkAttachmentReference(0, VkImageLayout.ColorAttachmentOptimal)]),
    Resolve = new([1], [new VkAttachmentReference(1, VkImageLayout.ColorAttachmentOptimal)]),
    Depth = new([0], []),
    Preserve = new([0], [])
};

ThRenderPass renderPass = device.CreateRenderPass([colorAttachment, resolveAttachment], subpassDescriptionSpan);

ThFramebuffer framebuffer = renderPass.CreateFramebuffer([colorView.Handle, resolveView.Handle], 1280, 720);

ShaderBuilder shaderBuilder = new(device);

VertexShaderLayout vertexShader = shaderBuilder.BuildVertex();

ShaderLayout testShader = shaderBuilder.BuildTest();

ShaderLayout solidShader = shaderBuilder.BuildSolid();

ShaderLayout textureShader = shaderBuilder.BuildTexture();

ShaderLayout roundedShader = shaderBuilder.BuildRounded();

PipelineLayout testLayout = PipelineLayout.Create(device, vertexShader, testShader);

PipelineLayout solidLayout = PipelineLayout.Create(device, vertexShader, solidShader);

PipelineLayout textureLayout = PipelineLayout.Create(device, vertexShader, textureShader);

PipelineLayout roundedLayout = PipelineLayout.Create(device, vertexShader, roundedShader);

Pipeline testPipeline = testLayout.CreatePipeline(renderPass.Handle, VkSampleCountFlags.Count8, -1);

Pipeline solidPipeline = solidLayout.CreatePipeline(renderPass.Handle, VkSampleCountFlags.Count8, -1);

Pipeline texturePipeline = textureLayout.CreatePipeline(renderPass.Handle, VkSampleCountFlags.Count8, 1);

Pipeline roundedPipeline = roundedLayout.CreatePipeline(renderPass.Handle, VkSampleCountFlags.Count8, 1);

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

Rect solidRect = Rect.Create(0, 0, 100, 100, new Vector4(1, 0, 1, 1));

RoundedRect roundedRect = RoundedRect.Create(270, 130, 470, 230, 25, new Vector4(0, 0, 1, 1));

RoundedRect roundedRect2 = RoundedRect.Create(500, 130, 700, 280, 20, new Vector4(0.2f, 0.2f, 1, 1));

Viewport viewport = Viewport.Create(0, 0, 1280, 720);

presenter.SetSize(1280, 720);

DrawContext context = new(device, storage.CreateContext(), commandBuffer.Handle);

DrawHandle<Vertex> handle = new(vertexBuffer, commandBuffer.Handle);

int width = 1280;

int height = 720;

float angle = 0;

bool running = true;

long time = Stopwatch.GetTimestamp();

int frames = 0;

while(running) {
    SDL.WaitEvent(out SDL.Event e);

    //SDL.PollEvent(out SDL.Event e);

    do {
        SDL.EventType type = (SDL.EventType)e.Type;

        if(type == SDL.EventType.WindowResized) {
            Console.WriteLine("Resize");

            SDL.GetWindowSizeInPixels(window.Handle, out width, out height);

            framebuffer.Dispose();

            resolveView.Dispose();

            resolveRenderTarget.Dispose();

            presenter.SetSize(width, height);

            width = presenter.Width;
            height = presenter.Height;

            colorRenderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkSampleCountFlags.Count8, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment);

            colorView = colorRenderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

            resolveRenderTarget = device.AllocateImage(physicalDevice, VkFormat.B8G8R8A8Srgb, new(1280, 720), 1, VkSampleCountFlags.Count1, VkImageUsageFlags.TransferSrc | VkImageUsageFlags.ColorAttachment);

            resolveView = resolveRenderTarget.Image.CreateImageView(VkFormat.B8G8R8A8Srgb, VkComponentMapping.Rgba);

            framebuffer = renderPass.CreateFramebuffer([resolveView.Handle], (uint)width, (uint)height);
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

    context.BindPipeline(testPipeline);

    context.Push(viewport);

    handle.AddDraw(testRect);

    handle.AddDraw([
        new(0, 0, 0, 1),
        new(1280, 0, 1, 1),
        new(640, 720, 0.9f, 0),
    ]);

    handle.Flush();

    //

    context.BindPipeline(solidPipeline);

    handle.AddDraw(solidRect);

    handle.Flush();

    //

    context.BindPipeline(texturePipeline);

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, textureView.Handle));

    context.NextDescriptorSet();

    context.Bind();

    handle.AddDraw(Rect.Create(320, 60, 600 + 320, 600 + 60, angle * MathF.Tau / 360));

    handle.Flush();

    //

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, fontTexture.Handle));

    context.NextDescriptorSet();

    context.Bind();

    //handle.Draw(testRect);

    context.BindPipeline(roundedPipeline);

    handle.AddDraw(roundedRect);

    handle.AddDraw(roundedRect2);

    handle.Flush();

    //

    context.BindPipeline(texturePipeline);

    context.Push(new Vector4(viewport.X, viewport.Y, 0, 0));

    context.AddBinding(new ThImageSamplerBinding(sampler.Handle, fontTexture.Handle));

    context.NextDescriptorSet();

    context.Bind();

    TextModel.CreateModel(map, "London is the capital of Great Britain!", handle);

    handle.Flush();

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

    commandBuffer.CopyImage(resolveRenderTarget.Image.Handle, VkImageLayout.TransferSrcOptimal, image.Handle, VkImageLayout.TransferDstOptimal, copy);

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

    if(frames++ >= 1000) {
        long t2 = Stopwatch.GetTimestamp();

        Console.WriteLine((t2 - time) * 1000 / Stopwatch.Frequency);

        time = t2;

        frames = 0;
    }

    //Console.WriteLine("Frame");
}

Console.WriteLine("End");

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

    DrawHandle<Vertex> handle = new(null, default, 200);

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
    public void CreateModel() {
        TextModel.CreateModel(map, "London is the capital of Great Britain!", handle);
    }
}
