using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BeatSequencer.Audio;

namespace BeatSequencer.Synth
{
    public class SynthesizerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private double _attackSeconds = 0.01;
        private double _decaySeconds = 0.2;
        private double _sustainLevel = 0.7;
        private double _releaseSeconds = 0.3;
        private double _masterVolume = 0.5;
        private OscillatorType _selectedOscillator = OscillatorType.Sine;
        private bool _isRecording;
        private string _statusText = "Idle";

        public ObservableCollection<OscillatorType> OscillatorTypes { get; } =
            new ObservableCollection<OscillatorType>
            {
                OscillatorType.Sine,
                OscillatorType.Square,
                OscillatorType.Saw,
                OscillatorType.Triangle
            };

        public OscillatorType SelectedOscillator
        {
            get => _selectedOscillator;
            set => SetField(ref _selectedOscillator, value);
        }

        public double AttackSeconds
        {
            get => _attackSeconds;
            set => SetField(ref _attackSeconds, value);
        }

        public double DecaySeconds
        {
            get => _decaySeconds;
            set => SetField(ref _decaySeconds, value);
        }

        public double SustainLevel
        {
            get => _sustainLevel;
            set => SetField(ref _sustainLevel, value);
        }

        public double ReleaseSeconds
        {
            get => _releaseSeconds;
            set => SetField(ref _releaseSeconds, value);
        }

        public double MasterVolume
        {
            get => _masterVolume;
            set => SetField(ref _masterVolume, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set => SetField(ref _isRecording, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
