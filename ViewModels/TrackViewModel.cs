using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using BeatSequencer.Models;

namespace BeatSequencer.ViewModels;

public class TrackViewModel : ViewModelBase
{
    private readonly Track _track;
    private readonly Func<string[], string[], string> _sampleSelector; // provided by MainVM
    private readonly Func<string, PointCollection> _waveformGenerator;
    private double _volume = 1.0;
    private double _pan;
    private string _selectedSampleName;
    private PointCollection _waveformPoints = new();

    public string Name
    {
        get => _track.Name;
        set
        {
            _track.Name = value;
            OnPropertyChanged();
        }
    }

    public void ResizeSteps(int newCount)
    {
        // First resize the model
        _track.ResizeSteps(newCount);

        // Then rebuild the StepViewModels to reflect the new size
        Steps.Clear();
        for (int i = 0; i < _track.Steps.Length; i++)
        {
            var vm = new StepViewModel(i, _track.Steps[i], _track.Velocities[i]);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StepViewModel.IsActive))
                    _track.Steps[vm.Index] = vm.IsActive;
                else if (e.PropertyName == nameof(StepViewModel.Velocity))
                    _track.Velocities[vm.Index] = (float)vm.Velocity;
            };
            Steps.Add(vm);
        }
    }


    public ObservableCollection<StepViewModel> Steps { get; } = new();

    /// <summary>Per-track volume 0..1.</summary>
    public double Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, Math.Clamp(value, 0.0, 1.0)))
            {
                _track.Volume = (float)_volume;
            }
        }
    }

    /// <summary>Per-track pan -1..1.</summary>
    public double Pan
    {
        get => _pan;
        set
        {
            if (SetProperty(ref _pan, Math.Clamp(value, -1.0, 1.0)))
            {
                _track.Pan = (float)_pan;
            }
        }
    }

    /// <summary>Available sample names (provided by MainViewModel).</summary>
    public ObservableCollection<string> AvailableSamples { get; } = new();

    /// <summary>Current sample name selected in UI.</summary>
    public string SelectedSampleName
    {
        get => _selectedSampleName;
        set
        {
            if (SetProperty(ref _selectedSampleName, value))
            {
                // Map name to file path (simple: same name, different extension or same path)
                var fullPath = _sampleSelector(AvailableSamples.ToArray(), new[] { value });
                if (!string.IsNullOrEmpty(fullPath))
                {
                    _track.SamplePath = fullPath;
                    UpdateWaveformPreview(fullPath);
                }
            }
        }
    }

    /// <summary>Sample path displayed for debugging.</summary>
    public string SamplePath => _track.SamplePath;

    /// <summary>Waveform preview points for WPF Polyline.</summary>
    public PointCollection WaveformPoints
    {
        get => _waveformPoints;
        private set => SetProperty(ref _waveformPoints, value);
    }

    public Track Model => _track;

    public TrackViewModel(Track track,
                          IEnumerable<string> allSamples,
                          Func<string[], string[], string> sampleSelector,
                          Func<string, PointCollection> waveformGenerator)
    {
        _track = track;
        _sampleSelector = sampleSelector;
        _waveformGenerator = waveformGenerator;

        foreach (var s in allSamples)
        {
            AvailableSamples.Add(Path.GetFileNameWithoutExtension(s));
        }

        // default selected
        _selectedSampleName = Path.GetFileNameWithoutExtension(track.SamplePath);
        UpdateWaveformPreview(track.SamplePath);

        for (int i = 0; i < _track.Steps.Length; i++)
        {
            var vm = new StepViewModel(i, _track.Steps[i], _track.Velocities[i]);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(StepViewModel.IsActive))
                {
                    _track.Steps[vm.Index] = vm.IsActive;
                }
                else if (e.PropertyName == nameof(StepViewModel.Velocity))
                {
                    _track.Velocities[vm.Index] = (float)vm.Velocity;
                }
            };
            Steps.Add(vm);
        }

        _volume = _track.Volume;
        _pan = _track.Pan;
    }

    private void UpdateWaveformPreview(string path)
    {
        if (_waveformGenerator != null && !string.IsNullOrWhiteSpace(path))
        {
            WaveformPoints = _waveformGenerator(path);
        }
        else
        {
            WaveformPoints = new System.Windows.Media.PointCollection();
        }
    }

}
