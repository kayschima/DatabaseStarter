using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using DatabaseStarter.Models;
using DatabaseStarter.Resources;

namespace DatabaseStarter;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplyLanguage(GetStartupLanguagePreference());

        try
        {
            DatabaseDefaults.EnsureAvailableVersionsLoaded();

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex) when (IsStartupConfigurationException(ex))
        {
            MessageBox.Show(
                BuildStartupConfigurationErrorMessage(ex),
                Strings.TitleError,
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
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

    private static string GetStartupLanguagePreference()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (!File.Exists(settingsPath))
        {
            return "en";
        }

        try
        {
            using var stream = File.OpenRead(settingsPath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.TryGetProperty("Language", out var languageProperty))
            {
                var language = languageProperty.GetString();
                return string.Equals(language, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            }
        }
        catch
        {
            // Ignore malformed settings and fall back to English for the startup dialog.
        }

        return "en";
    }

    private static bool IsStartupConfigurationException(Exception ex) =>
        ex is FileNotFoundException or InvalidOperationException;

    private static string BuildStartupConfigurationErrorMessage(Exception ex)
    {
        if (string.Equals(Strings.Culture?.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase))
        {
            return
                $"Die Versionskonfiguration konnte beim Start nicht geladen werden.{Environment.NewLine}{Environment.NewLine}" +
                $"Bitte prüfen Sie die Datei 'Data\\database-versions.json'.{Environment.NewLine}{Environment.NewLine}" +
                $"Details:{Environment.NewLine}{ex.Message}";
        }

        return
            $"The version configuration could not be loaded during startup.{Environment.NewLine}{Environment.NewLine}" +
            $"Please check the file 'Data\\database-versions.json'.{Environment.NewLine}{Environment.NewLine}" +
            $"Details:{Environment.NewLine}{ex.Message}";
    }
}