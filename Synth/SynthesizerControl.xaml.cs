using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BeatSequencer.Audio;
using BeatSequencer.Integration;
using NAudio.Wave;

namespace BeatSequencer.Synth
{
    public partial class SynthesizerControl : UserControl
    {
        private readonly SynthesizerViewModel _viewModel;
        private readonly SynthesizerEngine _engine;
        private readonly RecordingSampleProvider _recordingProvider;
        private readonly SynthSampleRecorder _recorder;
        private readonly WaveOutEvent _outputDevice;

        private readonly ISynthSampleListener? _sampleListener;

        private readonly Dictionary<string, int> _noteNameToMidi = new();
        private readonly Dictionary<Key, string> _keyToNote = new();

        public event EventHandler<SynthSampleRecordedEventArgs>? SampleRecorded;

        public SynthesizerControl()
            : this(null)
        {
        }

        public SynthesizerControl(ISynthSampleListener? sampleListener)
        {
            InitializeComponent();

            _sampleListener = sampleListener;

            _viewModel = new SynthesizerViewModel();
            DataContext = _viewModel;

            _engine = new SynthesizerEngine();
            _recordingProvider = new RecordingSampleProvider(_engine);
            _recorder = new SynthSampleRecorder(
                _engine.WaveFormat,
                maxDurationSeconds: 10.0,
                samplesDirectory: System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Samples")
            );
            _recordingProvider.AttachRecorder(_recorder);

            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_recordingProvider);
            _outputDevice.Play();

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            InitNoteMaps();
            Loaded += SynthesizerControl_Loaded;
        }

        private void SynthesizerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(this);
        }

        private void InitNoteMaps()
        {
            // Mirror the NoteUtils mapping for keys we expose in UI
            string[] notes =
            {
                "C4","D4","E4","F4","G4","A4","B4",
                "C5","D5","E5","F5","G5","A5","B5"
            };

            foreach (var note in notes)
            {
                _noteNameToMidi[note] = BeatSequencer.Audio.NoteUtils.NoteNameToMidi(note);
            }

            // Basic PC keyboard mapping (lower row, like a small piano)
            _keyToNote[Key.Z] = "C4";
            _keyToNote[Key.X] = "D4";
            _keyToNote[Key.C] = "E4";
            _keyToNote[Key.V] = "F4";
            _keyToNote[Key.B] = "G4";
            _keyToNote[Key.N] = "A4";
            _keyToNote[Key.M] = "B4";

            _keyToNote[Key.S] = "C#4";
            _keyToNote[Key.D] = "D#4";
            _keyToNote[Key.G] = "F#4";
            _keyToNote[Key.H] = "G#4";
            _keyToNote[Key.J] = "A#4";
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SynthesizerViewModel.SelectedOscillator):
                    _engine.OscillatorType = _viewModel.SelectedOscillator;
                    break;

                case nameof(SynthesizerViewModel.AttackSeconds):
                case nameof(SynthesizerViewModel.DecaySeconds):
                case nameof(SynthesizerViewModel.SustainLevel):
                case nameof(SynthesizerViewModel.ReleaseSeconds):
                    _engine.Adsr.AttackSeconds = _viewModel.AttackSeconds;
                    _engine.Adsr.DecaySeconds = _viewModel.DecaySeconds;
                    _engine.Adsr.SustainLevel = _viewModel.SustainLevel;
                    _engine.Adsr.ReleaseSeconds = _viewModel.ReleaseSeconds;
                    break;

                case nameof(SynthesizerViewModel.MasterVolume):
                    _engine.MasterVolume = (float)_viewModel.MasterVolume;
                    break;
            }
        }

        private void PianoKey_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is string noteName)
            {
                PlayNote(noteName);
            }
        }

        private void PianoKey_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is string noteName)
            {
                ReleaseNote(noteName);
            }
        }

        private void PlayNote(string noteName)
        {
            if (!_noteNameToMidi.TryGetValue(noteName, out var midi))
            {
                // If not in the simple map, try via NoteUtils
                midi = BeatSequencer.Audio.NoteUtils.NoteNameToMidi(noteName);
                _noteNameToMidi[noteName] = midi;
            }

            _engine.NoteOn(midi);
        }

        private void ReleaseNote(string noteName)
        {
            if (!_noteNameToMidi.TryGetValue(noteName, out var midi))
            {
                midi = BeatSequencer.Audio.NoteUtils.NoteNameToMidi(noteName);
                _noteNameToMidi[noteName] = midi;
            }

            _engine.NoteOff(midi);
        }

        private readonly HashSet<Key> _keysDown = new();

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (_keysDown.Contains(e.Key)) return;
            _keysDown.Add(e.Key);

            if (_keyToNote.TryGetValue(e.Key, out var note))
            {
                PlayNote(note);
            }
        }

        private void UserControl_KeyUp(object sender, KeyEventArgs e)
        {
            _keysDown.Remove(e.Key);

            if (_keyToNote.TryGetValue(e.Key, out var note))
            {
                ReleaseNote(note);
            }
        }

        private void RecordToggle_Click(object sender, RoutedEventArgs e)
        {
            if (RecordToggle.IsChecked == true)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RecordToggle.IsChecked = false;
            StopRecording();
        }

        private void StartRecording()
        {
            if (_recorder.IsRecording) return;
            _recorder.Start();
            _viewModel.StatusText = "Recording...";
            _viewModel.IsRecording = true;
        }

        private void StopRecording()
        {
            if (!_recorder.IsRecording) return;

            var metadata = _recorder.Stop();
            _viewModel.IsRecording = false;
            _viewModel.StatusText = metadata == null ? "Idle" : $"Recorded: {metadata.Name}";

            if (metadata != null)
            {
                // Notify sequencer via interface + event
                _sampleListener?.OnSynthSampleRecorded(metadata);
                SampleRecorded?.Invoke(this, new SynthSampleRecordedEventArgs(metadata));
            }
        }

        private void SynthesizerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _recorder?.Dispose();

            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

    }
}
