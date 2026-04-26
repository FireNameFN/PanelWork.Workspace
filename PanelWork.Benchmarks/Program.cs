using System.IO;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Thermal.Core;
using Thermal.Extensions;
using Thermal.Fonts;
using Thermal.Models;
using Thermal.Primitives;
using Thermal.ThVk;
using Vortice.Vulkan;

BenchmarkRunner.Run<FontBenchmark>();

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

    DrawHandle<Vertex, Matrix> handle = new(null, null, default, 200);

    [GlobalSetup]
    public void Setup() {
        Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PanelWork.Benchmarks.Resources.inter.ttf");

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
