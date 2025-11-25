using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace BeatSequencer.Audio
{
    public class SynthesizerEngine : ISampleProvider
    {
        private readonly object _lock = new();
        private readonly List<SynthVoice> _voices = new();

        private readonly WaveFormat _waveFormat;
        private readonly double _sampleRate;

        private readonly Dictionary<int, SynthVoice> _activeVoicesByMidiNote = new();

        public WaveFormat WaveFormat => _waveFormat;

        public OscillatorType OscillatorType { get; set; } = OscillatorType.Sine;
        public AdsrSettings Adsr { get; } = new AdsrSettings();
        public float MasterVolume { get; set; } = 0.5f;

        public SynthesizerEngine(int sampleRate = 44100, int channels = 1)
        {
            _sampleRate = sampleRate;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public void NoteOn(int midiNote, float velocity = 1.0f)
        {
            double freq = NoteUtils.MidiToFrequency(midiNote);
            var voice = new SynthVoice(_sampleRate, freq, OscillatorType, Adsr, velocity);

            lock (_lock)
            {
                _voices.Add(voice);
                _activeVoicesByMidiNote[midiNote] = voice;
            }
        }

        public void NoteOff(int midiNote)
        {
            lock (_lock)
            {
                if (_activeVoicesByMidiNote.TryGetValue(midiNote, out var voice))
                {
                    voice.NoteOff();
                    _activeVoicesByMidiNote.Remove(midiNote);
                }
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);

            lock (_lock)
            {
                if (_voices.Count == 0)
                    return count;

                var finishedVoices = new List<SynthVoice>();

                for (int n = 0; n < count; n++)
                {
                    double sampleValue = 0.0;

                    foreach (var voice in _voices)
                    {
                        var s = voice.NextSample();
                        sampleValue += s;

                        if (voice.IsFinished && !finishedVoices.Contains(voice))
                        {
                            finishedVoices.Add(voice);
                        }
                    }

                    // Simple soft clipping
                    sampleValue *= MasterVolume;
                    if (sampleValue > 1.0) sampleValue = 1.0;
                    if (sampleValue < -1.0) sampleValue = -1.0;

                    buffer[offset + n] = (float)sampleValue;
                }

                foreach (var v in finishedVoices)
                {
                    _voices.Remove(v);
                }
            }

            return count;
        }
    }
}
