using DarkUI.Forms;
using PkgViewer.Forms;
using PkgViewer.Infrastructure;
using PkgViewer.Shell;

namespace PkgViewer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.ThreadException += (_, e) => HandleFatal("UI thread", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleFatal("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Exception("Unobserved task", e.Exception);
            e.SetObserved();
        };

        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            HandleFatal("Startup", ex);
        }
    }

    private static void Run(string[] args)
    {
        if (args.Length > 0 && args[0] is "--register" or "--unregister" or "--uninstall")
        {
            try
            {
                if (args[0] == "--register")
                {
                    PackageShellIntegration.Install();
                    Logger.Info("File-type integration installed.");
                }
                else
                {
                    PackageShellIntegration.Uninstall();
                    Logger.Info("File-type integration removed.");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception("Shell registration", ex);
                DarkMessageBox.ShowError(ex.Message, "PkgViewer");
            }
            return;
        }

        if (args.Length > 0 && args[0] is "--help" or "-h" or "/?")
        {
            DarkMessageBox.ShowInformation(
                "PkgViewer - quick viewer for PS4 and PS5 packages\n\n" +
                "Usage:\n  PkgViewer.exe <package file>\n  PkgViewer.exe --open <package file>\n" +
                "  PkgViewer.exe --register\n  PkgViewer.exe --unregister (or --uninstall)\n\n" +
                "Supported: .pkg (PS4 and PS5), .ffpfsc, .ffpkg, .exfat",
                "PkgViewer");
            return;
        }

        if (args.Length > 0 && args[0] == "--smoke")
        {
            // Headless construction check for the viewer form (no package is opened).
            try
            {
                using var form = new PackageViewerForm(Path.Combine(Path.GetTempPath(), "pkgviewer-smoke.pkg"));
                form.CreateControl();
                using var about = new AboutForm("1.2.0");
                about.CreateControl();
                using var update = new UpdateAvailableForm(
                    new UpdateCheck("9.9.9", "https://example.com", "### Fixes\n\n- Smoke test note."), "1.2.0");
                update.CreateControl();
                Logger.Info("Smoke: OK");
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Logger.Exception("Smoke", ex);
                Environment.ExitCode = 1;
            }
            return;
        }

        string? path = ResolvePackagePath(args);
        AppSettings settings = AppSettings.Load();

        if (args.Length == 0 && !settings.IntegrationPromptShown && !settings.IntegrationPromptDeclined &&
            !PackageShellIntegration.IsInstalled())
        {
            // First-run (bare launch only): offer to set the viewer up once. Either choice is
            // remembered, and the app continues to the file picker either way.
            using var prompt = new IntegrationPromptForm();
            prompt.ShowDialog();
            if (prompt.Integrated) settings.IntegrationPromptShown = true;
            if (prompt.Declined) settings.IntegrationPromptDeclined = true;
            if (prompt.Integrated || prompt.Declined) settings.Save();
        }
        else
        {
            OfferInstalledUpdate(settings);
        }

        if (path is null)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Open PS4/PS5 package",
                Filter = "Package files|*.pkg;*.ffpfsc;*.ffpkg;*.exfat|All files|*.*",
                CheckFileExists = true
            };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            path = dialog.FileName;
        }

        if (!File.Exists(path))
        {
            DarkMessageBox.ShowError($"The file does not exist:\n\n{path}", "PkgViewer");
            return;
        }

        Logger.Info($"Opening '{path}'.");
        Application.Run(new PackageViewerForm(path));
    }

    /// <summary>
    /// When a newer portable copy runs while an older managed copy is installed, offer to update the
    /// managed copy (and its registration) so the file association keeps working. Asked once per
    /// version.
    /// </summary>
    private static void OfferInstalledUpdate(AppSettings settings)
    {
        if (!PackageShellIntegration.IsInstalled() || PackageShellIntegration.IsRunningInstalledCopy) return;

        Version? running = PackageShellIntegration.RunningVersion;
        Version? installed = PackageShellIntegration.InstalledVersion;
        if (running is null || installed is null || running <= installed) return;

        string version = running.ToString(3);
        if (string.Equals(settings.UpdatePromptedVersion, version, StringComparison.OrdinalIgnoreCase)) return;

        settings.UpdatePromptedVersion = version;
        settings.Save();

        DialogResult answer = DarkMessageBox.DialogYesNo(
            $"PkgViewer {version} is newer than the installed copy ({installed.ToString(3)}).\n\n" +
            "Update the installed copy that package files open with?",
            "PkgViewer");
        if (answer != DialogResult.Yes) return;

        try
        {
            PackageShellIntegration.Install();
            Logger.Info($"Installed copy updated to {version}.");
        }
        catch (Exception ex)
        {
            Logger.Exception("Installed copy update", ex);
            DarkMessageBox.ShowError(ex.Message, "PkgViewer");
        }
    }

    private static string? ResolvePackagePath(string[] args)
    {
        if (args.Length >= 2 && args[0] is "--open" or "-o") return args[1];
        if (args.Length >= 1 && !args[0].StartsWith('-')) return args[0];
        return null;
    }

    private static void HandleFatal(string context, Exception? exception)
    {
        Logger.Exception(context, exception ?? new Exception("Unknown error"));
        try
        {
            DarkMessageBox.ShowError(
                (exception?.Message ?? "An unknown error occurred.") +
                $"\n\nA log was written to:\n{Logger.LogPath}",
                "PkgViewer error");
        }
        catch (Exception)
        {
            // Nothing more can be done while handling a fatal error.
        }
    }
}
