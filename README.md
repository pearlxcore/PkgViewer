# Pkg Viewer

A quick little viewer for PS4 and PS5 packages. Open a package or an image, poke around inside, preview files, and grab what you need - without opening the full PS4/PS5 PKG Tool.

# Support My Work

[![ko-fi](https://github.com/user-attachments/assets/be9cc4be-3352-4bd5-8086-05c30930f81d)](https://ko-fi.com/R6R524N7X)

[![paypal](https://user-images.githubusercontent.com/36906814/102657760-39d1ce00-41b1-11eb-96fe-c10e2d9b3f39.png)](https://www.paypal.com/paypalme/pearlxcoree)

# Requirement

- Windows 10 or 11 (64-bit).

The release is a single `PkgViewer.exe` with everything it needs bundled in - no .NET install.

# Features

- Open PS4 `.pkg`, PS5 `.pkg`, and `.ffpfsc`, `.ffpkg`, `.exfat` images.
- A clean overview: title, IDs, version, size, region and more, plus the raw PARAM.SFO / param.json.
- Container internals for packages, and Activities / Executable tabs for PS5.
- Trophies with icons and details, and an Artwork tab for the icon and backgrounds.
- File Browser with a folder tree and list, a search box, and a built-in preview pane (images, text and hex).
- Extract a single file, a folder, or the whole package, with a simple "what if the file already exists" choice.
- Save artwork, or export a metadata report.
- Recent files, a remembered window/column layout, and an optional file-type association.
- Dark, tidy interface (DarkUI).

# How To Use

1. Run `PkgViewer.exe` and pick a package or image - or just drag one onto the window.
2. Walk the tabs: **Overview**, **Package**, **Activities**/**Executable** (PS5), **Trophies**, **File Browser**, **Artwork**.
3. In **File Browser**, double-click a file to preview it, or right-click to copy, reveal or extract.
4. Use **File > Extract All...** to pull out everything.

# Download

Grab the latest `PkgViewer-v1.0.0.zip` from the [releases page](https://github.com/pearlxcore/PkgViewer/releases/latest), unzip it, and run `PkgViewer.exe`.

# Build

Pkg Viewer shares the DarkUI and PS4/PS5 PKG Tool sources, so build it from the same workspace:

```
dotnet build .\PkgViewer.slnx -c Release
```

# Screenshots

<img width="1102" height="712" alt="image" src="https://github.com/user-attachments/assets/336b1389-f716-4d50-8a50-b7c83fb28b1f" />
<img width="1102" height="712" alt="image" src="https://github.com/user-attachments/assets/60947ca2-80d9-46b2-8db7-c00160de3ad5" />
<img width="1102" height="712" alt="image" src="https://github.com/user-attachments/assets/ec03f0d4-e2ee-4457-b13a-2af70dc63f68" />
<img width="1102" height="712" alt="image" src="https://github.com/user-attachments/assets/299716c9-e6bf-46fa-8b20-c6b13e05e35e" />


# License

GPL-3.0.

# Credit

- [Robin Perris](https://github.com/RobinPerris/DarkUI)
- [SvenGDK](https://github.com/SvenGDK/LibProsperoPKG)
- PSBrew / Renan Barreto
- kerrdec97
- strongt1me
- Sony
