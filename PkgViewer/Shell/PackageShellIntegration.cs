using System.Diagnostics;
using Microsoft.Win32;

namespace PkgViewer.Shell;

/// <summary>
/// Per-user (HKCU) default-app candidate registration for the package extensions. Windows
/// protects the actual user choice, so "set as default" opens the Default apps settings page;
/// this type only makes PkgViewer a registered, selectable handler.
///
/// Registration points at a fixed managed copy under %LocalAppData%\PkgViewer, so the command path
/// stays the same across releases and the user's default-app choice keeps working after an update.
/// </summary>
internal static class PackageShellIntegration
{
    public const string ProgId = "PkgViewer.Package";
    public const string DisplayName = "PS4/PS5 Package";
    public const string ApplicationName = "PkgViewer";

    public static readonly string[] Extensions = [".pkg", ".ffpfsc", ".ffpkg", ".exfat"];

    /// <summary>
    /// Fixed per-user folder the viewer installs itself into, so the registered command path is
    /// stable across releases.
    /// </summary>
    public static string InstallDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PkgViewer");

    /// <summary>The managed copy of the viewer; the file associations always point here.</summary>
    public static string InstalledExecutable { get; } = Path.Combine(InstallDirectory, "PkgViewer.exe");

    /// <summary>True when the running executable is the managed install copy.</summary>
    public static bool IsRunningInstalledCopy => PathsEqual(Environment.ProcessPath, InstalledExecutable);

    /// <summary>True when the managed install copy exists on disk.</summary>
    public static bool HasInstalledCopy => File.Exists(InstalledExecutable);

    /// <summary>The managed copy's file version, or null when it is not present.</summary>
    public static Version? InstalledVersion => ReadVersion(InstalledExecutable);

    /// <summary>The running executable's file version, or null when it cannot be read.</summary>
    public static Version? RunningVersion => ReadVersion(Environment.ProcessPath);

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
        // Installs or refreshes the fixed managed copy and registers it. Registration only ever adds
        // PkgViewer; removing other tools' handlers is a separate, explicit action
        // (RemoveLegacyAssociations) so installing this viewer never breaks them.
        string executable = EnsureInstalledCopy();
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

        RemoveInstalledCopy();
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

                    string defaultProgId = key.GetValue(null) as string ?? string.Empty;
                    bool ours = string.Equals(defaultProgId, ProgId, StringComparison.OrdinalIgnoreCase);
                    bool dangling = defaultProgId.Length > 0 && !ProgIdExists(defaultProgId);
                    // Only touch the extension icon when it belongs to us or points at a removed
                    // handler; another app's icon is left alone.
                    if (ours || dangling)
                    {
                        if (dangling) key.SetValue(null, string.Empty);
                        key.DeleteSubKeyTree("DefaultIcon", throwOnMissingSubKey: false);
                    }
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

    /// <summary>
    /// Resolves a ProgId against the effective merged classes view (per-user first, then machine
    /// wide) so a machine-registered handler is not mistaken for a missing one.
    /// </summary>
    private static bool ProgIdExists(string progId)
    {
        using (RegistryKey? perUser = Registry.CurrentUser.OpenSubKey(@"Software\Classes\" + progId))
        {
            if (perUser is not null) return true;
        }
        using RegistryKey? machine = Registry.LocalMachine.OpenSubKey(@"Software\Classes\" + progId);
        return machine is not null;
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

    /// <summary>
    /// Copies the running executable to the fixed install path when it is not already running from
    /// there, and returns that path. This is what keeps the registered command stable across
    /// releases so the user's default-app choice survives an update.
    /// </summary>
    public static string EnsureInstalledCopy()
    {
        string? running = Environment.ProcessPath;
        if (string.IsNullOrEmpty(running) || !File.Exists(running))
            throw new InvalidOperationException("Could not resolve the PkgViewer executable path.");
        if (PathsEqual(running, InstalledExecutable)) return InstalledExecutable;

        Directory.CreateDirectory(InstallDirectory);
        File.Copy(running, InstalledExecutable, overwrite: true);
        return InstalledExecutable;
    }

    /// <summary>Deletes the managed copy, unless it is the currently running executable.</summary>
    private static void RemoveInstalledCopy()
    {
        try
        {
            if (IsRunningInstalledCopy) return; // cannot delete the running executable
            if (File.Exists(InstalledExecutable)) File.Delete(InstalledExecutable);
            if (Directory.Exists(InstallDirectory) &&
                Directory.GetFileSystemEntries(InstallDirectory).Length == 0)
                Directory.Delete(InstallDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort; removing the registration is what matters.
        }
    }

    private static Version? ReadVersion(string? path)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            string? text = FileVersionInfo.GetVersionInfo(path).FileVersion;
            return Version.TryParse(text, out Version? version) ? version : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd('\\'),
                Path.GetFullPath(right).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
