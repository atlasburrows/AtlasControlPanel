using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Atlas.MAUI;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Set status bar color to match header (#161b22)
        if (Window is not null)
        {
            Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#161b22"));
            // Light status bar icons (white) on dark background
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window.InsetsController?.SetSystemBarsAppearance(0,
                    (int)WindowInsetsControllerAppearance.LightStatusBars);
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
#pragma warning disable CA1422
                Window.DecorView.SystemUiVisibility &= ~(StatusBarVisibility)SystemUiFlags.LightStatusBar;
#pragma warning restore CA1422
            }
        }
    }
}
