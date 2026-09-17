# PkgViewer audit — status

The prioritised findings from the 2026-09-18 audit have been implemented. PkgViewer stays a viewer:
no library management or builder UI was added.

## Extraction

- **Conflict policy** (`PackageConflictPolicy`: Fail/Skip/Replace/KeepBoth) with a prompt and an
  "apply to all" option; full extraction asks once up front.
- **No truncated replacements** — entries are written to a temp file and moved into place, so a
  cancelled or failed write never leaves a half-written replacement.
- **Containment** — `PackagePath` normalizes package-controlled paths (rejecting traversal, drive
  letters, invalid and reserved names) and resolves every destination beneath the chosen root; the
  same rule now drops unsafe entries from the file tree.
- **Partial-failure reporting** — extraction returns counts plus per-file errors and surfaces them
  distinctly ("completed with errors") instead of a false success.
- **Visible Extract All** — File > Extract All... and a Files-tab button.
- **Multi-select** — the file list allows Ctrl/Shift selection; the selection extracts together.
- **Progress/cancel** — selected extraction uses an indeterminate bar with a file counter; Stop
  explains that the current file completes first.

## Navigation

- A synthetic **package root** shows top-level files and folders together; a file's folder contents
  are shown when a leaf is selected.
- **Breadcrumb + Up** in the Files action row.
- **Folder activation clears the global filter**, so opening a search result navigates into it.
- The match limit is a **single shared stop** with one "first N matches" notice.
- File context menu adds Preview / Open containing folder, and supports the Shift+F10 / Menu key.

## Reliability

- **Latest preview wins**: superseded previews are cancelled and stale images disposed.
- Lazy loads (files, trophies, detail tabs) take a **lifetime cancellation token** and a faulted
  load is retried rather than replayed.
- Opening **another package** (Ctrl+O) reuses the window; the first-run prompt is remembered either
  way and no longer blocks the picker.
- **Passcode recovery**: Tools > Retry Content Access..., and a clearer prompt that distinguishes a
  32-character debug passcode from metadata-only access, with masked input and a Show toggle.
- Persistent **warning indicator** with a full, copyable warning report; warnings present at open
  are surfaced instead of only counted.

## Diagnostics and UI

- **Save Artwork** exports PIC2 as well and reports saved/skipped/failed counts and destination.
- **Export Metadata...** writes a text report of the overview, header, build info, PARAM.SFO,
  entries and warnings.
- **File Associations...** is a dedicated dialog (register/remove only PkgViewer, open Default apps,
  and an explicit advanced action to remove legacy handlers).
- **Shell integration** no longer removes other tools' registrations on install; icon resets and
  ProgId checks are ownership-aware and resolve the merged per-user/machine classes view.
- **Window/view state** persisted: size/maximized, file splitter sizes, column widths, preview pane
  visibility and last extraction folder; the window is resizable.
- Menus reworked to File / Edit / View / Tools / Help; the nonfunctional "Check for update" command
  was removed.

## Deliberately not done

- Drag-and-drop opening, a preview-on-selection delay, and text-encoding detection/choice.
- Decoded-image dimension/memory caps beyond the existing size ceiling.
- Backend capability-driven command enabling.
- No user-facing Settings dialog (view state is remembered automatically).

## Verification

- `dotnet build PkgViewer.slnx -c Release`: succeeded, zero errors.
- `dotnet test PkgViewer.Tests`: 52 passed, 0 failed (adds path-safety and file-tree tests).
- `PkgViewer.exe --smoke`: constructs the viewer headlessly, exit 0.
- Real-package fixtures were not exercised (the opt-in environment variables were unset).
