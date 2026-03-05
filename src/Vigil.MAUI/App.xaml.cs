using MauiApplication = Microsoft.Maui.Controls.Application;

namespace Vigil.MAUI;

public partial class App : MauiApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "Vigil" };
    }
}
