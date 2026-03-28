using System;
using GreenPng;
using SDL3;

namespace PanelWork;

public sealed class Icon(nint handle, byte[] buffer) : IDisposable {
    public nint Handle { get; } = handle;

    readonly byte[] buffer = buffer;

    public static unsafe Icon CreateFromPixels(int width, int height, byte[] buffer) {
        fixed(byte* pixels = buffer) {
            nint surface = SDL.CreateSurfaceFrom(width, height, SDL.PixelFormat.ARGB8888, (nint)pixels, width * 4);

            return new(surface, buffer);
        }
    }

    public static Icon CreateFromPng(ReadOnlySpan<byte> png) {
        byte[] buffer = PngDecoder.Decode(png, out PngHeader header);

        return CreateFromPixels(header.Width, header.Height, buffer);
    }

    public void Dispose() {
        SDL.DestroySurface(Handle);
    }
}
