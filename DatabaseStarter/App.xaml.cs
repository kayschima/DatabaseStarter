using System.Globalization;
using System.Windows;
using DatabaseStarter.Resources;
using DatabaseStarter.Services;

namespace DatabaseStarter;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load language preference before any UI is created
        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        ApplyLanguage(settings.Language);
    }

    /// <summary>
    /// Sets the UI culture for the entire application so that
    /// resource lookups (Strings.resx / Strings.de.resx) use the correct language.
    /// </summary>
    public static void ApplyLanguage(string languageCode)
    {
        var culture = new CultureInfo(languageCode);
        Thread.CurrentThread.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        // Update the generated resource class so it picks up the new culture
        Strings.Culture = culture;
    }
}