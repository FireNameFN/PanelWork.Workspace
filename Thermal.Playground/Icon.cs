using System;
using System.Runtime.InteropServices;
using GreenPng;
using SDL3;

namespace PanelWork;

public sealed unsafe class Icon : IDisposable {
    public nint Handle { get; }

    readonly void* memory;

    private Icon(nint handle, void* memory) {
        Handle = handle;
        this.memory = memory;
    }

    public static Icon CreateFromPixels(int width, int height, ReadOnlySpan<byte> span) {
        void* memory = NativeMemory.Alloc((nuint)span.Length);

        fixed(byte* spanPointer = span)
            NativeMemory.Copy(spanPointer, memory, (nuint)span.Length);

        nint surface = SDL.CreateSurfaceFrom(width, height, SDL.PixelFormat.ARGB8888, (nint)memory, width * 4);

        return new(surface, memory);
    }

    public static bool TryCreateFromPng(ReadOnlySpan<byte> png, out Icon icon) {
        icon = null;

        if(!PngDecoder.TryDecodeHeader(png, out PngHeader header))
            return false;

        if(!PngDecoder.IsHeaderSupported(header))
            return false;

        void* memory = NativeMemory.Alloc((nuint)header.ByteSize);

        if(!PngDecoder.TryDecode(png, header, new(memory, header.ByteSize)))
            return false;

        nint surface = SDL.CreateSurfaceFrom(header.Width, header.Height, SDL.PixelFormat.ARGB8888, (nint)memory, header.Width * 4);

        icon = new(surface, memory);

        return true;
    }

    public void Dispose() {
        SDL.DestroySurface(Handle);

        NativeMemory.Free(memory);
    }
}
