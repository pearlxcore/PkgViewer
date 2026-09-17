using System.Diagnostics;
using Microsoft.Win32;

namespace PkgViewer.Shell;

/// <summary>
/// Per-user (HKCU) default-app candidate registration for the package extensions. Windows
/// protects the actual user choice, so "set as default" opens the Default apps settings page;
/// this type only makes PkgViewer a registered, selectable handler.
/// </summary>
internal static class PackageShellIntegration
{
    public const string ProgId = "PkgViewer.Package";
    public const string DisplayName = "PS4/PS5 Package";
    public const string ApplicationName = "PkgViewer";

    public static readonly string[] Extensions = [".pkg", ".ffpfsc", ".ffpkg", ".exfat"];

    // ProgIds and executables written by the standalone/older PS4 and PS5 tools. These are what
    // leave a stale icon on .pkg once the old handler is gone.
    private static readonly string[] LegacyProgIds =
    [
        "PS4PKGTool.Package", "PS5PKGTool.Package", "pkg_auto_file"
    ];

    private static readonly string[] LegacyExecutables =
    [
        "PS4 PKG Tool.exe", "PS4PKGViewer.exe", "PS5 PKG Tool.exe", "PS5PKGViewer.exe"
    ];

    public static bool IsInstalled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + ProgId);
        return key is not null;
    }

    public static void Install()
    {
        RemoveLegacyAssociations();

        string executable = ResolveExecutable();
        using RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes");

        using (RegistryKey progId = classes.CreateSubKey(ProgId))
        {
            progId.SetValue(null, DisplayName);
            progId.SetValue("FriendlyTypeName", DisplayName);
            using RegistryKey icon = progId.CreateSubKey("DefaultIcon");
            icon.SetValue(null, $"\"{executable}\",0");
            using RegistryKey command = progId.CreateSubKey(@"shell\open\command");
            command.SetValue(null, $"\"{executable}\" \"%1\"");
        }

        foreach (string extension in Extensions)
        {
            using RegistryKey openWith = classes.CreateSubKey(extension + @"\OpenWithProgids");
            openWith.SetValue(ProgId, string.Empty);
        }

        using (RegistryKey application = classes.CreateSubKey(@"Applications\PkgViewer.exe"))
        {
            application.SetValue("ApplicationName", ApplicationName);
            application.SetValue("FriendlyAppName", "PS4/PS5 Package Viewer");
            using RegistryKey supported = application.CreateSubKey("SupportedTypes");
            foreach (string extension in Extensions)
                supported.SetValue(extension, string.Empty);
            using RegistryKey command = application.CreateSubKey(@"shell\open\command");
            command.SetValue(null, $"\"{executable}\" \"%1\"");
        }

        using (RegistryKey capabilities = Registry.CurrentUser.CreateSubKey(@"Software\PkgViewer\Capabilities"))
        {
            capabilities.SetValue("ApplicationName", ApplicationName);
            capabilities.SetValue("ApplicationDescription", "Quick viewer for PS4 and PS5 packages.");
            using RegistryKey associations = capabilities.CreateSubKey("FileAssociations");
            foreach (string extension in Extensions)
                associations.SetValue(extension, ProgId);
        }

        using RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registered.SetValue(ApplicationName, @"Software\PkgViewer\Capabilities");

        AdoptUnhandledExtensions();
        RefreshShellIcons();
    }

    public static void Uninstall()
    {
        using (RegistryKey classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
        {
            classes.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
            classes.DeleteSubKeyTree(@"Applications\PkgViewer.exe", throwOnMissingSubKey: false);
            foreach (string extension in Extensions)
            {
                using RegistryKey? openWith = classes.OpenSubKey(extension + @"\OpenWithProgids", writable: true);
                openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
            }
        }

        using (RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registered.DeleteValue(ApplicationName, throwOnMissingValue: false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\PkgViewer", throwOnMissingSubKey: false);

        ResetExtensionIcons();
    }

    /// <summary>
    /// Removes the file-type ownership of the old PS4/PS5 PKG tools and standalone viewers so they
    /// no longer supply the .pkg icon or appear as a competing handler.
    /// </summary>
    public static void RemoveLegacyAssociations()
    {
        using RegistryKey? classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
        if (classes is null) return;
        using RegistryKey? fileExts = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts", writable: true);

        foreach (string extension in Extensions)
        {
            ResetLegacyChoice(fileExts, extension);
            RemoveLegacyOpenWithValues(classes, extension);
            RemoveLegacyOpenWithValues(fileExts, extension);
            ClearLegacyExtensionDefault(classes, extension);
        }

        foreach (string progId in LegacyProgIds)
            classes.DeleteSubKeyTree(progId, throwOnMissingSubKey: false);

        using RegistryKey? applications = classes.OpenSubKey("Applications", writable: true);
        if (applications is not null)
        {
            foreach (string executable in LegacyExecutables)
                applications.DeleteSubKeyTree(executable, throwOnMissingSubKey: false);
        }

        ResetExtensionIcons();
    }

    /// <summary>
    /// Clears any leftover per-extension default handler/icon that no longer resolves and refreshes
    /// the Explorer icon cache, so a removed handler's stale icon disappears.
    /// </summary>
    public static void ResetExtensionIcons()
    {
        try
        {
            using RegistryKey? classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            using RegistryKey? fileExts = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts", writable: true);

            if (classes is not null)
            {
                foreach (string extension in Extensions)
                {
                    using RegistryKey? key = classes.OpenSubKey(extension, writable: true);
                    if (key is null) continue;

                    // A default ProgId whose handler key is gone must be cleared, otherwise Explorer
                    // keeps resolving the (now dangling) icon.
                    if (key.GetValue(null) is string progId && progId.Length > 0 && !ProgIdExists(progId))
                        key.SetValue(null, string.Empty);

                    key.DeleteSubKeyTree("DefaultIcon", throwOnMissingSubKey: false);
                }
            }

            ResetDanglingChoice(fileExts);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // The hash-protected choice keys can be guarded; the icon cache is still refreshed below.
        }

        RefreshShellIcons();
    }

    private static void ResetDanglingChoice(RegistryKey? fileExts)
    {
        if (fileExts is null) return;
        foreach (string extension in Extensions)
        {
            foreach (string choiceName in new[] { "UserChoice", "UserChoiceLatest" })
            {
                string subKey = extension + @"\" + choiceName;
                using RegistryKey? choice = fileExts.OpenSubKey(subKey, writable: true);
                if (choice is null) continue;
                string? progId = ReadChoiceProgId(choice, choiceName);
                if (string.IsNullOrWhiteSpace(progId) || ProgIdExists(progId)) continue;
                try
                {
                    fileExts.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
                }
                catch (UnauthorizedAccessException)
                {
                    // Windows may guard the hash-protected choice key.
                }
            }
        }
    }

    private static bool ProgIdExists(string progId)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + progId);
        return key is not null;
    }

    /// <summary>Opens the Windows Default apps page; the user picks PkgViewer there.</summary>
    public static void OpenDefaultAppsSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
        }
        catch (Exception)
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=" + ApplicationName)
            {
                UseShellExecute = true
            });
        }
    }

    /// <summary>Asks Explorer to rebuild its association icon cache (best effort).</summary>
    public static void RefreshShellIcons()
    {
        try
        {
            string ie4uinit = Path.Combine(Environment.SystemDirectory, "ie4uinit.exe");
            if (!File.Exists(ie4uinit)) return;
            using Process? process = Process.Start(new ProcessStartInfo(ie4uinit, "-show")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(5000);
        }
        catch (Exception)
        {
            // Icon cache refresh is cosmetic; never fail registration because of it.
        }
    }

    private static void ResetLegacyChoice(RegistryKey? fileExts, string extension)
    {
        if (fileExts is null) return;
        foreach (string choiceName in new[] { "UserChoice", "UserChoiceLatest" })
        {
            string subKey = extension + @"\" + choiceName;
            using RegistryKey? choice = fileExts.OpenSubKey(subKey, writable: true);
            if (choice is null) continue;
            if (!IsLegacyProgId(ReadChoiceProgId(choice, choiceName))) continue;
            try
            {
                fileExts.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
            }
            catch (UnauthorizedAccessException)
            {
                // Windows may guard the hash-protected choice key; the user can still change it
                // from Settings.
            }
        }
    }

    private static string? ReadChoiceProgId(RegistryKey choice, string choiceName)
    {
        if (choiceName == "UserChoiceLatest")
        {
            using RegistryKey? progIdKey = choice.OpenSubKey("ProgId");
            return progIdKey?.GetValue("ProgId") as string ?? progIdKey?.GetValue(null) as string;
        }
        return choice.GetValue("ProgId") as string;
    }

    private static void RemoveLegacyOpenWithValues(RegistryKey? root, string extension)
    {
        if (root is null) return;
        using RegistryKey? openWith = root.OpenSubKey(extension + @"\OpenWithProgids", writable: true);
        if (openWith is null) return;
        foreach (string progId in LegacyProgIds)
            openWith.DeleteValue(progId, throwOnMissingValue: false);
    }

    private static void ClearLegacyExtensionDefault(RegistryKey classes, string extension)
    {
        using RegistryKey? key = classes.OpenSubKey(extension, writable: true);
        if (key is null) return;
        if (IsLegacyProgId(key.GetValue(null) as string))
            key.SetValue(null, string.Empty);
    }

    // Only claims an extension when nothing valid (or a now-removed legacy handler) owns it, so a
    // different app the user chose is never overridden.
    private static void AdoptUnhandledExtensions()
    {
        using RegistryKey? classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
        if (classes is null) return;
        foreach (string extension in Extensions)
        {
            using RegistryKey key = classes.CreateSubKey(extension);
            string? current = key.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(current) || IsLegacyProgId(current) ||
                string.Equals(current, ProgId, StringComparison.OrdinalIgnoreCase))
                key.SetValue(null, ProgId);
        }
    }

    private static bool IsLegacyProgId(string? progId)
    {
        if (string.IsNullOrWhiteSpace(progId)) return false;
        if (LegacyProgIds.Contains(progId, StringComparer.OrdinalIgnoreCase)) return true;
        using RegistryKey? command = Registry.CurrentUser.OpenSubKey(
            $@"Software\Classes\{progId}\shell\open\command");
        string? commandLine = command?.GetValue(null) as string;
        return commandLine is not null &&
               LegacyExecutables.Any(executable => commandLine.Contains(executable, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveExecutable()
    {
        string? path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            throw new InvalidOperationException("Could not resolve the PkgViewer executable path.");
        return path;
    }
}
