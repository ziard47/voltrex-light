# DiskScope — Native Windows Disk Space Analyzer

**DiskScope** is a high-performance native Windows disk-space analyzer inspired by **TreeSize** and **KDE Filelight**. Built using **C#**, **.NET 8**, **WinUI 3**, **XAML**, and the **Windows App SDK**, it adheres strictly to MVVM architectural principles and Windows 11 Fluent Design standards.

---

## Key Capabilities Across Phases

### Phase 1: Native WinUI 3 Foundation & Shell
- Modern Windows 11 desktop experience with **Mica backdrop** and **NavigationView**.
- Real-time Windows drive monitoring via `DriveInfo` with visual capacity gauges and free-space indicators.
- Native `FolderPicker` integration with WinRT interop (`InitializeWithWindow`).
- System-integrated Light, Dark, and High-Contrast theme support with persistent preferences.

### Phase 2: High-Performance Disk Scanning Engine
- Asynchronous, non-blocking recursive directory scanning on background worker threads.
- Reparse point and junction cycle detection preventing infinite traversal loops and storage double-counting.
- Full 64-bit integer arithmetic ensuring exact byte precision for multi-terabyte drives.
- Cancellation support (`CancellationTokenSource`) and UI-throttled progress updates (~60 ms).
- Graceful permission handling: inaccessible paths logged as warnings with partial scan badges.
- Multi-column interactive table sorting by Name, Size, % of Parent, Files, and Date Modified.

### Phase 3: Interactive Visualizations (Sunburst & Treemap)
- **Filelight Radial Sunburst**:
  - Center disc representing root directory; concentric outer rings representing successive directory depths.
  - Polar coordinates and angular span proportional to logical size ($2\pi \times \frac{\text{size}}{\text{total}}$).
  - Strict parent-child angular interval containment.
  - Double-click drill-down and center-disc navigate-up interactions.
- **Squarified Treemap**:
  - Bruls, Huizing, and van Wijk squarified layout algorithm optimizing rectangle aspect ratios.
  - Proportional space-filling representation with smart label placement.
- **Unified Visualizer Host**:
  - Instant switcher between Radial Sunburst and Squarified Treemap modes.
  - Interactive breadcrumbs trail with ancestry navigation.
  - Two-way selection synchronization across visualizer, folder table, and details panel.
  - Floating hover tooltip displaying path, size, percentage, and type.

### Phase 4: Final Features, Quality Assurance & Release
- **Largest Files Workspace**:
  - Instant identification of top disk consumers across scanned trees.
  - **Scope-Aware Context**: Explicitly distinguishes full-drive scans from folder sub-scans ("not entire drive").
  - **Partial Scan Badging**: Displays clear notice when results reflect accessible files within a partial scan.
  - **Category Filtering**: Documents, Videos, Audio, Images, Archives, Executables, Code, and Other.
  - **Size Thresholds**: Filter by `> 1 MB`, `> 10 MB`, `> 50 MB`, `> 100 MB`, `> 500 MB`, `> 1 GB`.
  - **Multi-Factor Sorting**: Sort by Size (Largest/Smallest), Name (A-Z/Z-A), Modified date, or Extension.
  - **Safe Shell Integration**:
    - "Open File" via Windows shell file association (`UseShellExecute = true`).
    - "Open in Explorer" with `/select,"path"` highlighting.
    - "Go to Folder" navigating to Folder Analysis with the parent directory selected.
- **Report Export**:
  - **RFC 4180 CSV Export**: Complete directory hierarchy with metadata header, 64-bit size precision, and proper escaping for quotes, commas, and newlines.
  - **Hierarchical JSON Export**: Documented schema (`schemaVersion: "1.0"`) containing scan metadata, warning logs, and full recursive tree nodes.
  - Native Windows `FileSavePicker` integration.
- **Scan History & Privacy**:
  - Versioned local storage in `%LOCALAPPDATA%\DiskScope\history.json` (retaining up to 50 recent scans).
  - Clear history action.
  - **100% Offline & Zero-Telemetry**: No paths, metrics, or telemetry are ever transmitted off-device.
- **Size Unit Convention**:
  - Instant toggle between Standard decimal-like units (`KB`, `MB`, `GB`, `TB`) and IEC binary notation (`KiB`, `MiB`, `GiB`, `TiB`).

---

## Architecture Overview

```
voltrex-light/
├── Helpers/                  # MVVM primitives & formatters
│   ├── ObservableObject.cs
│   ├── RelayCommand.cs
│   ├── AsyncRelayCommand.cs
│   └── FileSystemFormatter.cs # Standard (KB) vs Binary (KiB) formatting
├── Models/                   # Domain entities
│   ├── ScanStatus.cs
│   ├── ScanWarning.cs
│   ├── ScanOptions.cs
│   ├── ScanResult.cs
│   ├── ScanProgressInfo.cs
│   ├── FileSystemNode.cs
│   ├── DriveItem.cs
│   ├── AppSettings.cs
│   └── ScanHistoryItem.cs
├── Services/                 # Business logic and system services
│   ├── IScanService.cs / ScanService.cs
│   ├── IDriveService.cs / DriveService.cs
│   ├── IFolderPickerService.cs / FolderPickerService.cs
│   ├── ISettingsService.cs / SettingsService.cs
│   ├── INavigationService.cs / NavigationService.cs
│   ├── IExportService.cs / ExportService.cs         # CSV and JSON generation
│   ├── IScanHistoryService.cs / ScanHistoryService.cs # Versioned history
│   └── AppServices.cs                              # Zero-dependency IoC provider
├── Visualizations/           # Interactive Visualization Subsystem
│   ├── Models/
│   │   ├── TreemapItem.cs               # Rectangle geometry & hit testing
│   │   ├── SunburstSlice.cs             # Polar sector geometry & hit testing
│   │   └── VisualizationBreadcrumbItem.cs # Breadcrumb trail model
│   ├── Engines/
│   │   ├── ITreemapLayoutEngine.cs
│   │   ├── SquarifiedTreemapEngine.cs   # Bruls squarified treemap algorithm
│   │   ├── ISunburstLayoutEngine.cs
│   │   ├── SunburstLayoutEngine.cs      # Filelight radial annular engine
│   │   └── PaletteProvider.cs           # Accessible, theme-aware palette generator
│   └── Controls/
│       ├── TreemapControl.xaml (.cs)     # Canvas-based vector treemap renderer
│       ├── SunburstControl.xaml (.cs)    # Canvas-based vector sunburst renderer
│       └── DiskSpaceVisualizerControl.xaml (.cs) # Toolbar, breadcrumbs & host
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── OverviewViewModel.cs
│   ├── DriveAnalysisViewModel.cs        # Drive scanning, visualization & export
│   ├── FolderAnalysisViewModel.cs       # Folder scanning, visualization & export
│   ├── LargestFilesViewModel.cs         # Filtering, sorting, shell actions
│   └── SettingsViewModel.cs             # Theme, units, visualization default, history
├── Views/
│   ├── MainWindow.xaml (.cs)            # Root shell with NavigationView
│   ├── OverviewPage.xaml (.cs)          # Drive grid and dashboard
│   ├── DriveAnalysisPage.xaml (.cs)     # Tree, visualizer, export toolbar
│   ├── FolderAnalysisPage.xaml (.cs)    # Tree, visualizer, export toolbar
│   ├── LargestFilesPage.xaml (.cs)      # Top space consumers workspace
│   └── SettingsPage.xaml (.cs)          # Settings & history management
├── Converters/                          # XAML Value Converters
└── tests/voltrex-light.Tests/           # Comprehensive Unit Tests (54 passing tests)
    ├── FileSystemFormatterTests.cs
    ├── DriveItemTests.cs
    ├── RelayCommandTests.cs
    ├── SettingsServiceTests.cs
    ├── DriveServiceTests.cs
    ├── ScanServiceTests.cs
    ├── ScanSortingTests.cs
    ├── VisualizationEngineTests.cs
    ├── OverviewViewModelTests.cs
    ├── ExportServiceTests.cs            # RFC 4180 CSV escaping & JSON schema validation
    ├── ScanHistoryServiceTests.cs       # History persistence, FIFO capping & clear
    └── LargestFilesViewModelTests.cs    # Scope, categories, thresholds, and navigation
```

---

## Building and Running

### Prerequisites
- **IDE**: Visual Studio 2026 (or Visual Studio 2022 v17.10+ with .NET desktop and Windows App SDK workloads).
- **Target Framework**: .NET 8 (`net8.0-windows10.0.19041.0`).
- **Platform**: `x64` (or `ARM64`).
- **OS Minimum Requirement**: Windows 10, version 1809 (Build 17763) or Windows 11 (Recommended: Windows 11 22H2+).

### Release Build
```powershell
# Build application in Release x64 configuration
dotnet build "voltrex-light.csproj" -c Release -p:Platform=x64 --no-restore

# Output binary located at:
# bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\voltrex-light.exe
```

### Running Unit Tests
```powershell
# Execute the full unit test suite (54 automated tests)
dotnet test "tests/voltrex-light.Tests/voltrex-light.Tests.csproj" --no-restore
```

---

## Quality Assurance & Verification Results

All 54 unit tests pass without failures:
- **Export Verification**:
  - RFC 4180 CSV formatting handles double quotes (`""`), commas, and embedded newlines correctly.
  - 64-bit integer values retain complete byte precision without scientific notation truncation.
  - JSON output passes `JsonDocument.Parse` validation with explicit `schemaVersion: "1.0"`.
- **Scan History**:
  - Validates persistent serialization to `%LOCALAPPDATA%\DiskScope\history.json`.
  - Enforces FIFO capping to 50 items.
  - Validates complete history purge upon user request.
- **Visualizer Layout Engines**:
  - Treemap squarification aspect ratio preservation and area proportionality.
  - Sunburst radial wedge angle accuracy and child angular interval containment.
  - Polar and Cartesian hit-testing accuracy.
- **Largest Files**:
  - Scope string detection (`Drive` vs `Folder (not entire drive)`).
  - Category extension matching (Documents, Videos, Audio, Images, Archives, Executables, Code).
  - Minimum size thresholding and multi-column sorting.

