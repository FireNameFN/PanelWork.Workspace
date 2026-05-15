using System;
using PanelWork.Entities;
using PanelWork.Primitives;
using SDL3;

namespace PanelWork.Playground;

public static class PanelTest {
    public static void Run() {
        using App app = new();

        AppWindow window = new(app);

        Entity entity = app.entityManager.CreateEntity();

        Entity entity1 = app.entityManager.CreateEntity();

        Entity entity2 = app.entityManager.CreateEntity();

        FacadeComponent facade1 = app.entityManager.AddComponent<FacadeComponent>(entity1);

        facade1.Facade = new RectFacade() { Color = new(1, 0, 1, 1) };

        LayoutComponent layout1 = app.entityManager.AddComponent<LayoutComponent>(entity1);

        layout1.MinWidth = 100;
        layout1.MinHeight = 100;

        FacadeComponent facade2 = app.entityManager.AddComponent<FacadeComponent>(entity2);

        facade2.Facade = new RectFacade() { Color = new(1, 1, 0, 1) };

        LayoutComponent layout2 = app.entityManager.AddComponent<LayoutComponent>(entity2);

        layout2.MinWidth = 150;
        layout2.MinHeight = 150;

        LayoutComponent layout = app.entityManager.AddComponent<LayoutComponent>(entity);

        layout.Children = [entity1, entity2];
        layout.Padding = 10;
        layout.Gap = 20;

        FacadeComponent facade = app.entityManager.AddComponent<FacadeComponent>(entity);

        facade.Facade = new RectFacade() { Color = Color.FromRgba(0.25f, 0.25f, 0.5f, 1) };

        window.Content = entity;

        Console.WriteLine(SDL.GetError());

        app.Run();

        Console.WriteLine("Done");
    }
}
