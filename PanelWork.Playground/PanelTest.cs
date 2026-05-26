using System;
using PanelWork.Components;
using PanelWork.Facades;

namespace PanelWork.Playground;

public static class PanelTest {
    public static void Run() {
        using App app = new();

        Window window = app.CreateWindow();

        Panel panel = app.CreatePanel();

        panel
            .MinWidth(500)
            .Max(0, 0)
            .Padding(10)
            .Gap(20)
            .Facade(RectFacade.FromColor(0x3F3F7F))
            .Panels(
                panel.Fork()
                    .Min(100, 100)
                    .GrowWidth()
                    .Facade(RectFacade.FromColor(0xFF00FF)),
                panel.Fork()
                    .Min(150, 150)
                    .GrowWidth()
                    .Facade(RectFacade.FromColor(0xFFFF00))
                    .AddPanel(panel.Fork()
                        .Min(20, 20)
                        .Facade(RectFacade.FromColor(0x3FAF3F))));

        window.Panel = panel.Entity;

        Console.WriteLine(SDL.SDL3.SDL_GetError());

        app.Run();

        Console.WriteLine("Done");
    }
}
