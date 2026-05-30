using System;
using PanelWork.Components;
using PanelWork.Facades;

namespace PanelWork.Playground;

public static class PanelTest {
    public static void Run() {
        using App app = new();

        Window window = app.CreateWindow();

        Panel panel = app.PanelManager.CreatePanel(app.PanelManager.Archetypes.Rect);

        panel
            .MinWidth(500)
            .Max(0, 0)
            .Padding(10)
            .Gap(20)
            .RectColor(0xFF3F3F7F)
            .Panels(
                panel.Fork(app.PanelManager.Archetypes.Rect)
                    .Min(100, 100)
                    .GrowWidth()
                    .RectColor(0xFFFF00FF),
                panel.Fork(app.PanelManager.Archetypes.Rect)
                    .Min(150, 150)
                    .GrowWidth()
                    .RectColor(0xFFFFFF00)
                    .AddPanel(panel.Fork(app.PanelManager.Archetypes.Rect)
                        .Min(20, 20)
                        .RectColor(0xFF3FAF3F)));

        Panel panel2 = app.PanelManager.CreatePanel(app.PanelManager.Archetypes.Rect);

        window.Panel = panel.Entity;

        Console.WriteLine(SDL.SDL3.SDL_GetError());

        app.Run();

        Console.WriteLine("Done");
    }
}
