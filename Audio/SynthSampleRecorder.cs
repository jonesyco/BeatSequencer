using System;
using System.IO;
using NAudio.Wave;
using BeatSequencer.Integration;

namespace BeatSequencer.Audio
{
    public class SynthSampleRecorder : IDisposable
    {
        private readonly WaveFormat _waveFormat;
        private readonly double _maxDurationSeconds;
        private readonly string _samplesDirectory;

        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private bool _isRecording;
        private long _totalSamplesRecorded;

        public bool IsRecording => _isRecording;

        public SynthSampleRecorder(WaveFormat waveFormat, double maxDurationSeconds, string samplesDirectory)
        {
            _waveFormat = waveFormat;
            _maxDurationSeconds = maxDurationSeconds;
            _samplesDirectory = samplesDirectory;

            Directory.CreateDirectory(_samplesDirectory);
        }

        public void Start()
        {
            if (_isRecording) return;

            _totalSamplesRecorded = 0;
            _currentFilePath = GenerateFilePath();

            _writer = new WaveFileWriter(_currentFilePath, _waveFormat);
            _isRecording = true;
        }

        private string GenerateFilePath()
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"SynthSample_{stamp}.wav";
            return Path.Combine(_samplesDirectory, fileName);
        }

        public void AddSamples(float[] buffer, int offset, int count)
        {
            if (!_isRecording || _writer == null) return;
            if (count <= 0) return;

            _writer.WriteSamples(buffer, offset, count);
            _totalSamplesRecorded += count;

            double seconds = (double)_totalSamplesRecorded / (_waveFormat.SampleRate * _waveFormat.Channels);
            if (seconds >= _maxDurationSeconds)
            {
                // Auto-stop when max is reached
                Stop();
            }
        }

        public SynthSampleMetadata? Stop()
        {
            if (!_isRecording) return null;

            _isRecording = false;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            if (_currentFilePath == null) return null;

            double durationSeconds = (double)_totalSamplesRecorded / (_waveFormat.SampleRate * _waveFormat.Channels);

            var metadata = new SynthSampleMetadata
            {
                Name = Path.GetFileName(_currentFilePath),
                FilePath = _currentFilePath,
                Duration = TimeSpan.FromSeconds(durationSeconds),
                SampleRate = _waveFormat.SampleRate,
                BitDepth = 32, // 32-bit float
                Channels = _waveFormat.Channels
            };

            _currentFilePath = null;
            return metadata;
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
