namespace PkgViewer.Forms;

/// <summary>Loads the embedded application icon (shared by every window).</summary>
internal static class AppIcon
{
    private static Icon? _icon;
    private static bool _attempted;

    public static Icon? Load()
    {
        if (_attempted) return _icon;
        _attempted = true;
        try
        {
            using Stream? stream = typeof(AppIcon).Assembly
                .GetManifestResourceStream("PkgViewer.PackageIcon.ico");
            if (stream is not null) _icon = new Icon(stream);
        }
        catch (Exception)
        {
            // Missing/corrupt icon must never break startup.
        }
        return _icon;
    }

    public static void Apply(Form form)
    {
        if (Load() is { } icon) form.Icon = icon;
    }
}
