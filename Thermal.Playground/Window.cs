using System;
using SDL3;
using Vortice.Vulkan;

namespace PanelWork;

public sealed class Window : IDisposable {
    public nint Handle { get; }

    public VkInstance Instance { get; private set; }

    public VkSurfaceKHR Surface { get; private set; }

    public Window(nint handle) {
        Handle = handle;
    }

    public Window(string title = "Thermal", int width = 1280, int height = 720, SDL.WindowFlags flags = 0) {
        Handle = SDL.CreateWindow(title, width, height, flags | SDL.WindowFlags.Vulkan);
    }

    public VkSurfaceKHR CreateSurface(VkInstance instance) {
        if(Surface.IsNull) {
            SDL.VulkanCreateSurface(Handle, instance, 0, out nint surface);

            Instance = instance;

            Surface = new((ulong)surface);
        }

        return Surface;
    }

    public void Dispose() {
        if(Surface.IsNotNull)
            SDL.VulkanDestroySurface(Instance, (nint)Surface.Handle, 0);

        SDL.DestroyWindow(Handle);
    }
}
