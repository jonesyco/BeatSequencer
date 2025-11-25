using BeatSequencer.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BeatSequencer.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly Sequencer _sequencer;
    private readonly AudioEngine _audioEngine;
    private FileSystemWatcher? _sampleWatcher;
    private readonly object _sampleLock = new();
    private readonly string _samplesDir;
    private double _bpm;
    private double _volume;
    private double _swing;
    private int _currentStepIndex;
    private bool _isPlaying;
    private int _stepCount = 16;

    public int[] StepCountOptions { get; } = new[] { 8, 16, 32, 64 };

    public int StepCount
    {
        get => _stepCount;
        set
        {
            if (SetProperty(ref _stepCount, value))
            {
                ApplyStepCount(value);
            }
        }
    }

    private void ApplyStepCount(int newCount)
    {
        if (newCount <= 0) return;

        // Tell the sequencer how many steps per pattern
        _sequencer.Steps = newCount;

        // Resize each track’s pattern & VM
        foreach (var trackVm in Tracks)
        {
            trackVm.ResizeSteps(newCount);
        }
    }


    // pattern banks A-D
    private readonly Dictionary<char, PatternSnapshot> _patternBanks = new();

    public ObservableCollection<TrackViewModel> Tracks { get; } = new();
    public ObservableCollection<string> AllSamplePaths { get; } = new();

    public RelayCommand PlayCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RandomizeCommand { get; }
    public RelayCommand HumanizeCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand SavePatternToFileCommand { get; }
    public RelayCommand LoadPatternFromFileCommand { get; }

    private string[] ScanSampleFiles()
    {
        if (!Directory.Exists(_samplesDir))
        {
            Directory.CreateDirectory(_samplesDir);
        }

        var wavs = Directory.GetFiles(_samplesDir, "*.wav", SearchOption.TopDirectoryOnly);
        var mp3s = Directory.GetFiles(_samplesDir, "*.mp3", SearchOption.TopDirectoryOnly);

        return wavs.Concat(mp3s)
                   .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                   .ToArray();
    }

    private void SetupSampleWatcher()
    {
        if (!Directory.Exists(_samplesDir))
        {
            Directory.CreateDirectory(_samplesDir);
        }

        _sampleWatcher = new FileSystemWatcher(_samplesDir)
        {
            Filter = "*.*",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _sampleWatcher.Created += OnSamplesChanged;
        _sampleWatcher.Deleted += OnSamplesChanged;
        _sampleWatcher.Renamed += OnSamplesRenamed;
    }

    private void OnSamplesChanged(object sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.FullPath)?.ToLowerInvariant();
        if (ext != ".wav" && ext != ".mp3") return;

        RefreshSamplesOnUiThread();
    }

    private void OnSamplesRenamed(object sender, RenamedEventArgs e)
    {
        var newExt = Path.GetExtension(e.FullPath)?.ToLowerInvariant();
        var oldExt = Path.GetExtension(e.OldFullPath)?.ToLowerInvariant();

        if ((newExt == ".wav" || newExt == ".mp3") ||
            (oldExt == ".wav" || oldExt == ".mp3"))
        {
            RefreshSamplesOnUiThread();
        }
    }

    /// <summary>
    /// Marshal refresh onto UI thread and debounce via a lock.
    /// </summary>
    private void RefreshSamplesOnUiThread()
    {
        lock (_sampleLock)
        {
            Application.Current?.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(RefreshSamples));
        }
    }

    private void RefreshSamples()
    {
        // 1) Re-scan current files from disk
        var currentFiles = ScanSampleFiles();
        var currentSet = new HashSet<string>(currentFiles, StringComparer.OrdinalIgnoreCase);

        // 2) Remove tracks with missing sample files
        for (int i = Tracks.Count - 1; i >= 0; i--)
        {
            var tvm = Tracks[i];
            var path = tvm.Model.SamplePath;
            if (!currentSet.Contains(path))
            {
                Tracks.RemoveAt(i);
            }
        }

        // 3) Update AllSamplePaths
        AllSamplePaths.Clear();
        foreach (var p in currentFiles)
            AllSamplePaths.Add(p);

        // 4) Add tracks for new files not yet represented
        var existingPaths = new HashSet<string>(
            Tracks.Select(t => t.Model.SamplePath),
            StringComparer.OrdinalIgnoreCase);

        var newFiles = currentFiles.Where(p => !existingPaths.Contains(p)).ToArray();
        if (newFiles.Length > 0)
        {
            _audioEngine.PreloadSamples(newFiles);
        }

        foreach (var path in newFiles)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var track = new Track(name, path);

            var trackVm = new TrackViewModel(
                track,
                currentFiles,
                SampleSelector,
                GenerateWaveformPoints);

            Tracks.Add(trackVm);
        }

        // 5) Refresh per-track AvailableSamples and SelectedSampleName
        foreach (var tvm in Tracks)
        {
            tvm.AvailableSamples.Clear();
            foreach (var p in AllSamplePaths)
            {
                tvm.AvailableSamples.Add(Path.GetFileNameWithoutExtension(p));
            }

            if (!string.IsNullOrEmpty(tvm.SelectedSampleName))
            {
                var match = AllSamplePaths.FirstOrDefault(p =>
                    string.Equals(Path.GetFileNameWithoutExtension(p),
                                  tvm.SelectedSampleName,
                                  StringComparison.OrdinalIgnoreCase));

                if (match == null && AllSamplePaths.Any())
                {
                    // Old sample disappeared; pick the first available
                    var firstName = Path.GetFileNameWithoutExtension(AllSamplePaths[0]);
                    tvm.SelectedSampleName = firstName;
                }
            }
        }
    }


    public MainViewModel()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _samplesDir = Path.Combine(baseDir, "Samples");

        _audioEngine = new AudioEngine();
        _sequencer = new Sequencer(steps: 16, initialBpm: 120);
        _sequencer.StepChanged += OnStepChanged;

        _bpm = _sequencer.Bpm;
        _volume = 0.8;
        _audioEngine.MasterVolume = (float)_volume;
        _swing = 0.0;

        InitializeSamplesAndTracks();
        SetupSampleWatcher();

        PlayCommand = new RelayCommand(_ => Start(), _ => !IsPlaying && Tracks.Any());
        StopCommand = new RelayCommand(_ => Stop(), _ => IsPlaying);
        RandomizeCommand = new RelayCommand(_ => RandomizePattern());
        HumanizeCommand = new RelayCommand(_ => HumanizePattern());
        ClearCommand = new RelayCommand(_ => ClearPattern());
        SavePatternToFileCommand = new RelayCommand(_ => SavePatternToFile());
        LoadPatternFromFileCommand = new RelayCommand(_ => LoadPatternFromFile());
    }

    public bool BankA_Filled => _patternBanks.ContainsKey('A');
    public bool BankB_Filled => _patternBanks.ContainsKey('B');
    public bool BankC_Filled => _patternBanks.ContainsKey('C');
    public bool BankD_Filled => _patternBanks.ContainsKey('D');

    private void NotifyBankStateChanged()
    {
        OnPropertyChanged(nameof(BankA_Filled));
        OnPropertyChanged(nameof(BankB_Filled));
        OnPropertyChanged(nameof(BankC_Filled));
        OnPropertyChanged(nameof(BankD_Filled));
    }




    public double Bpm
    {
        get => _bpm;
        set
        {
            if (SetProperty(ref _bpm, value))
            {
                _sequencer.Bpm = _bpm;
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _audioEngine.MasterVolume = (float)_volume;
            }
        }
    }

    /// <summary>Swing 0..100 (mapped to 0..1 in Sequencer).</summary>
    public double Swing
    {
        get => _swing;
        set
        {
            if (SetProperty(ref _swing, Math.Clamp(value, 0, 100)))
            {
                _sequencer.SwingAmount = _swing / 100.0;
            }
        }
    }

    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set => SetProperty(ref _currentStepIndex, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }



private PointCollection GenerateWaveformPoints(string samplePath)
{
    const double width = 180;
    const double height = 30;
    const int pointCount = 120;

    var data = _audioEngine.GetWaveformData(samplePath, pointCount);
    var points = new PointCollection();

    if (data == null || data.Length == 0)
        return points;

    double midY = height / 2.0;
    int len = data.Length;

    for (int i = 0; i < len; i++)
    {
        double x = (len == 1) ? width / 2.0 : (i / (double)(len - 1)) * width;

        double amp = Math.Clamp(data[i], 0.0, 1.0);
        double y = midY - amp * (height / 2.0);

        points.Add(new Point(x, y));
    }

    return points;
}

private void InitializeSamplesAndTracks()
    {
        var all = ScanSampleFiles();

        //var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        //var samplesDir = Path.Combine(baseDir, "Samples");

        //if (!Directory.Exists(samplesDir))
        //{
        //    Directory.CreateDirectory(samplesDir);
        //}

        //var wavs = Directory.GetFiles(samplesDir, "*.wav");
        //var mp3s = Directory.GetFiles(samplesDir, "*.mp3");
        //var all = wavs.Concat(mp3s).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        AllSamplePaths.Clear();
        foreach (var p in all)
            AllSamplePaths.Add(p);

        _audioEngine.PreloadSamples(all);

        Tracks.Clear();

        int index = 0;
        foreach (var path in all)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var track = new Track(name, path);

            // default pattern: simple kick on 1 for first track
            if (index == 0 && track.Steps.Length > 0)
            {
                track.Steps[0] = true;
                track.Velocities[0] = 1f;
            }

            var trackVm = new TrackViewModel(
                track,
                all,
                SampleSelector,
                GenerateWaveformPoints);

            Tracks.Add(trackVm);
            index++;
        }
    }

    private string SampleSelector(string[] allNames, string[] selected)
    {
        // selected[0] is sample name (without extension). Map back to full path.
        if (selected.Length == 0) return string.Empty;
        var name = selected[0];
        var match = AllSamplePaths.FirstOrDefault(
            p => string.Equals(Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase));
        return match ?? string.Empty;
    }

    private void OnStepChanged(object? sender, int stepIndex)
    {
        CurrentStepIndex = stepIndex;

        // Highlight current step
        foreach (var trackVm in Tracks)
        {
            foreach (var stepVm in trackVm.Steps)
            {
                stepVm.IsCurrent = (stepIndex >= 0 && stepVm.Index == stepIndex);
            }
        }

        if (stepIndex < 0) return;

        // Trigger audio (with humanized timing + waveform pulse)
        foreach (var tvm in Tracks)
        {
            var model = tvm.Model;

            if (stepIndex >= 0 && stepIndex < model.Steps.Length && model.Steps[stepIndex])
            {
                var vel = model.Velocities[stepIndex];
                var stepVm = tvm.Steps[stepIndex];
                double jitterMs = stepVm.TimingOffsetMs;

                // Fire-and-forget, don’t block UI
                _ = TriggerHitAsync(tvm, jitterMs, vel);
            }
        }
    }


    private void Start()
    {
        _sequencer.Start();
        IsPlaying = true;
    }

    private void Stop()
    {
        _sequencer.Stop();
        IsPlaying = false;
        _audioEngine.StopRecording();
    }

    public void StartExport(string filePath)
    {
        _audioEngine.StartRecording(filePath);
        if (!IsPlaying)
        {
            Start();
        }
    }

    public void RandomizePattern()
    {
        var rng = new Random();
        foreach (var tvm in Tracks)
        {
            foreach (var step in tvm.Steps)
            {
                step.IsActive = rng.NextDouble() < 0.25;          // 25% chance
                step.Velocity = 0.6 + rng.NextDouble() * 0.4;     // 0.6..1.0
            }
        }
    }

    private async Task TriggerHitAsync(TrackViewModel trackVm, double timingOffsetMs, float velocity)
    {
        var model = trackVm.Model;

        if (timingOffsetMs > 0)
        {
            await Task.Delay((int)Math.Round(timingOffsetMs));
        }

        trackVm.IsRecentlyTriggered = true;

        // start audio
        _audioEngine.PlaySample(model.SamplePath, model.Volume, model.Pan, velocity);

        // animate playhead along waveform
        await AnimateWaveformPlayheadAsync(trackVm, 200); // 200ms feels snappy; adjust if you like

        trackVm.IsRecentlyTriggered = false;
        trackVm.UpdatePlayhead(0.0);
    }

    private async Task AnimateWaveformPlayheadAsync(TrackViewModel trackVm, int durationMs)
    {
        if (durationMs <= 0)
        {
            trackVm.UpdatePlayhead(1.0);
            return;
        }

        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < durationMs)
        {
            double progress = sw.ElapsedMilliseconds / (double)durationMs;
            trackVm.UpdatePlayhead(progress);
            await Task.Delay(16); // ~60 FPS
        }

        trackVm.UpdatePlayhead(1.0);
    }




    public void HumanizePattern()
    {
        // here we'll just randomize velocities slightly; timing is already "swung".
        var rng = new Random();

        const double maxVelocityJitter = 0.2;
        const double maxTimingJitterMs = 12;

        foreach (var tvm in Tracks)
        {
            foreach (var step in tvm.Steps)
            {
                if (!step.IsActive)
                {
                    step.TimingOffsetMs = 0;
                    continue;
                }

                var delta = (rng.NextDouble() - 0.5) *maxVelocityJitter; // +/-0.1
                step.Velocity = Math.Clamp(step.Velocity + delta, 0.4, 1.0);

                step.TimingOffsetMs = rng.NextDouble() * maxTimingJitterMs;
            }
        }
    }



    public void ClearPattern()
    {
        foreach (var tvm in Tracks)
        {
            foreach (var s in tvm.Steps)
            {
                s.IsActive = false;
                s.Velocity = 1.0;
                s.TimingOffsetMs = 0;
            }
        }
    }

    // --- Pattern snapshot for banks / save/load ---

    private PatternSnapshot CapturePatternSnapshot()
    {
        var snap = new PatternSnapshot
        {
            Bpm = Bpm,
            Swing = Swing,
            Tracks = Tracks.Select(t => new TrackSnapshot
            {
                Name = t.Name,
                SamplePath = t.Model.SamplePath,
                Volume = (float)t.Volume,
                Pan = (float)t.Pan,
                Steps = t.Model.Steps.ToArray(),
                Velocities = t.Model.Velocities.ToArray()
            }).ToList()
        };
        return snap;
    }

    private void ApplyPatternSnapshot(PatternSnapshot snap)
    {
        Bpm = snap.Bpm;
        Swing = snap.Swing;

        // naive: rebuild tracks from scratch
        Tracks.Clear();
        AllSamplePaths.Clear();
        foreach (var ts in snap.Tracks)
        {
            if (!string.IsNullOrWhiteSpace(ts.SamplePath) && File.Exists(ts.SamplePath))
            {
                if (!AllSamplePaths.Contains(ts.SamplePath))
                    AllSamplePaths.Add(ts.SamplePath);
            }
        }

        _audioEngine.PreloadSamples(AllSamplePaths);

        foreach (var ts in snap.Tracks)
        {
            var track = new Track(ts.Name, ts.SamplePath, ts.Steps.Length)
            {
                Volume = ts.Volume,
                Pan = ts.Pan
            };
            ts.Steps.CopyTo(track.Steps, 0);
            ts.Velocities.CopyTo(track.Velocities, 0);

            var tvm = new TrackViewModel(track, AllSamplePaths, SampleSelector, GenerateWaveformPoints);
            Tracks.Add(tvm);
        }
    }

    public void ClearBank(char bank)
    {
        bank = char.ToUpperInvariant(bank);
        if (bank is < 'A' or > 'D') return;

        if (_patternBanks.ContainsKey(bank))
        {
            _patternBanks.Remove(bank);
            NotifyBankStateChanged();
        }
    }

    public void ClearAllBanks()
    {
        _patternBanks.Clear();
        NotifyBankStateChanged();
    }



    public void StorePatternToBank(char bank)
    {
        bank = char.ToUpperInvariant(bank);
        if (bank is < 'A' or > 'D') return;
        _patternBanks[bank] = CapturePatternSnapshot();
        NotifyBankStateChanged();
    }

    public void RecallPatternFromBank(char bank)
    {
        bank = char.ToUpperInvariant(bank);
        if (_patternBanks.TryGetValue(bank, out var snap))
        {
            ApplyPatternSnapshot(snap);
        }
    }

    public void SavePatternToFile()
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Pattern",
                Filter = "Beat Pattern (*.beat)|*.beat|JSON (*.json)|*.json",
                DefaultExt = ".beat",
                FileName = "pattern.beat"
            };
            if (dlg.ShowDialog() == true)
            {
                var snap = CapturePatternSnapshot();
                var json = JsonSerializer.Serialize(snap, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(dlg.FileName, json);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save pattern: {ex.Message}", "Error");
        }
    }

    public void LoadPatternFromFile()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Pattern",
                Filter = "Beat Pattern (*.beat;*.json)|*.beat;*.json"
            };
            if (dlg.ShowDialog() == true)
            {
                var json = File.ReadAllText(dlg.FileName);
                var snap = JsonSerializer.Deserialize<PatternSnapshot>(json);
                if (snap != null)
                {
                    ApplyPatternSnapshot(snap);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load pattern: {ex.Message}", "Error");
        }
    }

    public void Dispose()
    {
        _sequencer.StepChanged -= OnStepChanged;
        _audioEngine.Dispose();

        if (_sampleWatcher != null)
        {
            _sampleWatcher.EnableRaisingEvents = false;
            _sampleWatcher.Created -= OnSamplesChanged;
            _sampleWatcher.Deleted -= OnSamplesChanged;
            _sampleWatcher.Renamed -= OnSamplesRenamed;
            _sampleWatcher.Dispose();
            _sampleWatcher = null;
        }
    }

}

// DTOs used for pattern save/load / banks.
public class PatternSnapshot
{
    public double Bpm { get; set; }
    public double Swing { get; set; }
    public List<TrackSnapshot> Tracks { get; set; } = new();
}

public class TrackSnapshot
{
    public string Name { get; set; } = string.Empty;
    public string SamplePath { get; set; } = string.Empty;
    public float Volume { get; set; }
    public float Pan { get; set; }
    public bool[] Steps { get; set; } = Array.Empty<bool>();
    public float[] Velocities { get; set; } = Array.Empty<float>();
}

