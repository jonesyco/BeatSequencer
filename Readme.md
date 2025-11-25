# BeatSequencer

BeatSequencer is a lightweight multi-step drum machine built with WPF and MVVM. It provides an interactive step-grid per track, per-step velocity/pattern controls, waveform preview, pattern banks, and basic pattern file import/export and WAV export.

---

## Highlights
- Modern dark WPF UI with per-track waveform preview and playhead indicator.
- Per-track sample selection, volume and pan controls.
- Configurable step-length (Steps), BPM, global Volume and Swing.
- Pattern tools: Randomize, Humanize, Clear.
- Pattern banks (A–D) with Store / Recall / Clear.
- Save / Load pattern files and Export WAV from the UI.
- Built with .NET 8 (WPF) and MVVM (MainViewModel, TrackViewModel, StepViewModel).

---

## Requirements
- .NET 8 SDK
- Windows (WPF)
- Visual Studio 2022 or later (or use the `dotnet` CLI)

---

## Build & Run

Using Visual Studio 2022
1. Open the solution in Visual Studio 2022.
2. Restore NuGet packages if required: __Tools > NuGet Package Manager > Restore NuGet Packages__.
3. Build the solution: __Build > Rebuild Solution__.
4. Start the app: __Debug > Start Debugging__ (or __Start Without Debugging__).

Using the CLI
1. From repository root:
   - dotnet restore
   - dotnet build
   - dotnet run --project ./BeatSequencer/BeatSequencer.csproj

---

## Quick Usage
- Play / Stop: Use the Play and Stop buttons in the top bar.
- Steps: Change step-length using the Steps combo box (bound to `StepCount`).
- BPM / Volume / Swing: Adjust sliders in the top bar (bound to `Bpm`, `Volume`, `Swing`).
- Per-track:
  - Select a sample from the track's `AvailableSamples` combo box.
  - Use the step ToggleButtons to toggle steps (bound to each `StepViewModel.IsActive`).
  - Adjust track Volume and Pan using the sliders.
  - Waveform preview shows activity and a playhead dot.
- Pattern tools: Randomize, Humanize, Clear (bound to `RandomizeCommand`, `HumanizeCommand`, `ClearCommand`).
- Banks A–D: Main store button (store current pattern), Recall (R) and Clear (X). Buttons are wired to code-behind handlers (e.g., `BankStoreA_Click`, `BankRecallA_Click`).
- Save / Load pattern: Use Save and Load (commands `SavePatternToFileCommand` and `LoadPatternFromFileCommand`) — the app will prompt for a file location.
- Export WAV: Use the Export WAV button (click handler `ExportWav_Click`) to export the currently active pattern to a WAV file.

---

## Project Structure (important files)
- `MainWindow.xaml` — main UI layout and bindings
- `MainWindow.xaml.cs` — some UI handlers (bank clicks, export)
- `ViewModels\MainViewModel.cs` — central MVVM view model
- `ViewModels\TrackViewModel.cs` — per-track logic and sample selection
- `ViewModels\StepViewModel.cs` — per-step state (IsActive, velocity, IsCurrent, IsRecentlyTriggered)
- `Converters\BoolToBankBrushConverter.cs` — bank fill visual converter
- App targets .NET 8 and WPF.

---

## Development notes
- Follows MVVM; commands exposed on `MainViewModel` (e.g., `PlayCommand`, `StopCommand`, `SavePatternToFileCommand`, `LoadPatternFromFileCommand`).
- UI uses resource brushes and styles defined in `MainWindow.xaml` (global Button style, `StepToggleStyle`, waveform styles).
- If you add new samples, add them to the track's `AvailableSamples` source or your sample loader logic.

---

## Troubleshooting
- No audio: ensure output device is available and not muted; verify app volume slider and per-track volume.
- Missing samples: confirm sample files are present where your sample loader expects them.
- Build errors after upgrading SDK: ensure your environment has the __.NET 8 SDK__ installed and Visual Studio workload for WPF is enabled.

---

## Contributing
- Fork, create a branch, implement features or fixes, and submit a PR.
- Please include unit tests where applicable and update documentation.

---

