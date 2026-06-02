using System;
using System.Threading.Tasks;
using PanelWork.Components;
using PanelWork.Facades;
using PanelWork.Interactions;
using PanelWork.Panels;

namespace PanelWork.Playground;

public static class PanelTest {
    public static void Run() {
        using App app = new();

        Window window = app.CreateWindow();

        ArchetypeComponent hover = ArchetypeBuilder.Create()
            .Add(app.PanelManager.Archetypes.Focus)
            .AddAction<ClickedEvent>()
            .AddEvent<HoverHandler, FocusEvent>()
            .Build(app.PanelManager);

        Panel panel = app.PanelManager.CreatePanel(app.PanelManager.Archetypes.Rect);

        Panel bar = panel.ForkRect()
            .MinHeight(20)
            .GrowWidth(0)
            .RectColor(0xFFFFFF00);

        Panel abar = panel.ForkRect()
            .MinHeight(20)
            .GrowWidth()
            .RectColor(0xFF00FF00);

        panel
            .Layout(LayoutType.Vertical)
            .MinWidth(500)
            .Max(0, 0)
            .Padding(10)
            .Gap(20)
            .RectColor(0xFF3F3F7F)
            .Panels(
                panel.ForkEmpty()
                    .GrowWidth()
                    .Gap(20)
                    .Panels(
                        panel.ForkRect()
                            .Min(100, 100)
                            .GrowWidth()
                            .RectColor(0xFFFF00FF),
                        panel.ForkRect()
                            .Min(150, 150)
                            .GrowWidth()
                            .RectColor(0xFFFFFF00)
                            .AddPanel(panel.Fork(hover)
                                .Min(20, 20)
                                .RectColor(0xFF3FAF3F)
                                .Action<ClickedEvent>(async _ => {
                                    for(int i = 0; i <= 100; i++) {
                                        bar.StarWidth(i / 100f);
                                        abar.StarWidth(1 - i / 100f);

                                        await Task.Delay(10);

                                        //if(i == 50)
                                        //    throw new Exception();
                                    }
                                }))),
                panel.ForkEmpty()
                    .GrowWidth()
                    .Panels(bar, abar));

        Panel panel2 = app.PanelManager.CreatePanel(hover);
        Panel panel3 = app.PanelManager.CreatePanel(hover);
        Panel panel4 = app.PanelManager.CreatePanel(hover);

        window.Panel = panel.Entity;

        Console.WriteLine(SDL.SDL3.SDL_GetError());

        try {
            app.Run();
        } catch(Exception) {
            Console.WriteLine("Err");

            throw;
        }

        Console.WriteLine("Done");
    }

    class HoverHandler : IEventHandler<FocusEvent> {
        public void Handle(Panel panel, ref FocusEvent e) {
            FocusComponent focus = panel.Get<FocusComponent>();

            if(focus.Pressed)
                panel.RectColor(0xFF00007F);
            else if(focus.Hovered)
                panel.RectColor(0xFF0000FF);
            else
                panel.RectColor(0xFFFF0000);
        }
    }
}
