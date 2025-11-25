using System;

namespace BeatSequencer.Audio
{
    internal enum EnvelopeState
    {
        Attack,
        Decay,
        Sustain,
        Release,
        Off
    }

    internal class SynthVoice
    {
        private readonly double _sampleRate;
        private readonly double _frequency;
        private readonly OscillatorType _oscType;
        private readonly AdsrSettings _adsr;
        private readonly float _velocity;

        private double _phase;
        private int _samplePosition;
        private bool _isReleased;
        private int _releaseStartSample;

        private int AttackSamples => Math.Max(1, (int)(_adsr.AttackSeconds * _sampleRate));
        private int DecaySamples => Math.Max(1, (int)(_adsr.DecaySeconds * _sampleRate));
        private int ReleaseSamples => Math.Max(1, (int)(_adsr.ReleaseSeconds * _sampleRate));

        public bool IsFinished { get; private set; }

        public SynthVoice(double sampleRate, double frequency, OscillatorType oscType, AdsrSettings adsr, float velocity)
        {
            _sampleRate = sampleRate;
            _frequency = frequency;
            _oscType = oscType;
            _adsr = adsr.Clone();
            _velocity = velocity;
        }

        public void NoteOff()
        {
            if (_isReleased) return;
            _isReleased = true;
            _releaseStartSample = _samplePosition;
        }

        public float NextSample()
        {
            if (IsFinished) return 0f;

            double amplitude = CalculateEnvelopeAmplitude();
            if (amplitude <= 0 && _isReleased)
            {
                IsFinished = true;
                return 0f;
            }

            double oscValue = GenerateOscillatorSample();
            _phase += 2.0 * Math.PI * _frequency / _sampleRate;
            if (_phase > 2.0 * Math.PI)
                _phase -= 2.0 * Math.PI;

            _samplePosition++;
            return (float)(oscValue * amplitude * _velocity);
        }

        private double GenerateOscillatorSample()
        {
            double value = 0;

            switch (_oscType)
            {
                case OscillatorType.Sine:
                    value = Math.Sin(_phase);
                    break;

                case OscillatorType.Square:
                    value = Math.Sign(Math.Sin(_phase));
                    break;

                case OscillatorType.Saw:
                    // -1..1 saw
                    value = 2.0 * (_phase / (2.0 * Math.PI)) - 1.0;
                    break;

                case OscillatorType.Triangle:
                    value = 2.0 * Math.Abs(2.0 * (_phase / (2.0 * Math.PI)) - 1.0) - 1.0;
                    break;
            }

            return value;
        }

        private double CalculateEnvelopeAmplitude()
        {
            if (!_isReleased)
            {
                if (_samplePosition < AttackSamples)
                {
                    // Attack: ramp 0 -> 1
                    return (double)_samplePosition / AttackSamples;
                }

                int decayStart = AttackSamples;
                int decayEnd = AttackSamples + DecaySamples;

                if (_samplePosition < decayEnd)
                {
                    // Decay: 1 -> SustainLevel
                    double decayPos = (double)(_samplePosition - decayStart) / DecaySamples;
                    return 1.0 + decayPos * (_adsr.SustainLevel - 1.0);
                }

                // Sustain
                return _adsr.SustainLevel;
            }
            else
            {
                int releasePos = _samplePosition - _releaseStartSample;
                if (releasePos >= ReleaseSamples)
                {
                    return 0.0;
                }

                double releaseFactor = 1.0 - (double)releasePos / ReleaseSamples;
                return _adsr.SustainLevel * releaseFactor;
            }
        }
    }
}
