using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BeatSequencer.Models;

/// <summary>
/// Audio engine using NAudio.
/// - WaveOutEvent with mixer
/// - In-memory cached samples (WAV/MP3 via AudioFileReader)
/// - Master recording to WAV
/// - Per-trigger volume & pan
/// </summary>
public class AudioEngine : IDisposable
{
    private readonly IWavePlayer _outputDevice;
    private readonly MixingSampleProvider _mixer;
    private readonly RecordingSampleProvider _recordingProvider;

    private readonly Dictionary<string, CachedSound> _sampleCache = new();

    private float _masterVolume = 0.8f;
    private bool _disposed;

    public AudioEngine()
    {
        // Mixer format: 44.1kHz stereo float
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _mixer = new MixingSampleProvider(waveFormat)
        {
            ReadFully = true
        };

        _recordingProvider = new RecordingSampleProvider(_mixer);

        var waveOut = new WaveOutEvent
        {
            DesiredLatency = 100 // ms
        };
        _outputDevice = waveOut;
        _outputDevice.Init(_recordingProvider);
        _outputDevice.Play();
    }

    public float[]? GetWaveformData(string samplePath, int pointCount)
    {
        if (string.IsNullOrWhiteSpace(samplePath)) return null;

        if (!_sampleCache.TryGetValue(samplePath, out var cached))
        {
            if (!File.Exists(samplePath)) return null;

            try
            {
                cached = new CachedSound(samplePath);
                _sampleCache[samplePath] = cached;
            }
            catch
            {
                return null;
            }
        }

        var samples = cached.AudioData;
        if (samples.Length == 0 || pointCount <= 0)
            return Array.Empty<float>();

        int channels = cached.WaveFormat.Channels;
        int totalFrames = samples.Length / channels;

        if (totalFrames <= 0)
            return Array.Empty<float>();

        pointCount = Math.Min(pointCount, totalFrames);
        var result = new float[pointCount];

        double framesPerPoint = totalFrames / (double)pointCount;

        // For each display point, find the max absolute amplitude in that window
        for (int i = 0; i < pointCount; i++)
        {
            int startFrame = (int)(i * framesPerPoint);
            int endFrame = (int)((i + 1) * framesPerPoint);
            if (endFrame <= startFrame) endFrame = startFrame + 1;
            if (endFrame > totalFrames) endFrame = totalFrames;

            float maxAmp = 0f;

            for (int frame = startFrame; frame < endFrame; frame++)
            {
                int baseIndex = frame * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    float v = Math.Abs(samples[baseIndex + ch]);
                    if (v > maxAmp) maxAmp = v;
                }
            }

            result[i] = maxAmp;
        }

        return result;
    }

    public float MasterVolume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
    }

    public void PreloadSamples(IEnumerable<string> samplePaths)
    {
        foreach (var path in samplePaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!File.Exists(path)) continue;
            if (_sampleCache.ContainsKey(path)) continue;

            try
            {
                _sampleCache[path] = new CachedSound(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioEngine] Skipping '{path}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Trigger a sample with extra volume (0..1) and pan (-1..1).
    /// </summary>
    public void PlaySample(string samplePath, float trackVolume, float pan, float stepVelocity)
    {
        if (string.IsNullOrWhiteSpace(samplePath)) return;
        if (!File.Exists(samplePath)) return;

        if (!_sampleCache.TryGetValue(samplePath, out var cached))
        {
            cached = new CachedSound(samplePath);
            _sampleCache[samplePath] = cached;
        }

        float volume = MasterVolume * trackVolume * stepVelocity;
        volume = Math.Clamp(volume, 0f, 1f);

        var provider = new CachedSoundSampleProvider(cached, volume, pan);

        _mixer.AddMixerInput(provider);
    }

    public void StartRecording(string filePath)
    {
        _recordingProvider.StartRecording(filePath);
    }

    public void StopRecording()
    {
        _recordingProvider.StopRecording();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _outputDevice.Stop();
        _outputDevice.Dispose();
        _recordingProvider.Dispose();
    }
}

/// <summary>
/// Wraps source and optionally writes to WaveFileWriter.
/// </summary>
public class RecordingSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider _source;
    private WaveFileWriter? _writer;

    public RecordingSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public void StartRecording(string filePath)
    {
        StopRecording();
        _writer = new WaveFileWriter(filePath, WaveFormat);
    }

    public void StopRecording()
    {
        _writer?.Dispose();
        _writer = null;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);

        if (_writer != null && read > 0)
        {
            _writer.WriteSamples(buffer, offset, read);
        }

        return read;
    }

    public void Dispose()
    {
        StopRecording();
    }
}

/// <summary>
/// In-memory audio clip, always normalized to 44.1kHz stereo float.
/// Supports WAV/MP3 etc. via AudioFileReader.
/// </summary>
public class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    public CachedSound(string audioFileName)
    {
        using var reader = new AudioFileReader(audioFileName); // handles WAV/MP3/etc.

        ISampleProvider sourceProvider = reader;

        const int targetSampleRate = 44100;
        const int targetChannels = 2;

        // ensure stereo
        if (sourceProvider.WaveFormat.Channels == 1 && targetChannels == 2)
        {
            sourceProvider = new MonoToStereoSampleProvider(sourceProvider);
        }
        else if (sourceProvider.WaveFormat.Channels == 2 && targetChannels == 1)
        {
            sourceProvider = new StereoToMonoSampleProvider(sourceProvider);
        }

        // ensure sample rate
        if (sourceProvider.WaveFormat.SampleRate != targetSampleRate)
        {
            sourceProvider = new WdlResamplingSampleProvider(sourceProvider, targetSampleRate);
        }

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, targetChannels);

        var wholeFile = new List<float>();
        var readBuffer = new float[targetSampleRate * targetChannels];

        int samplesRead;
        while ((samplesRead = sourceProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
            {
                wholeFile.Add(readBuffer[i]);
            }
        }

        AudioData = wholeFile.ToArray();
    }
}

/// <summary>
/// Plays a CachedSound from memory with volume and pan.
/// </summary>
public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _cachedSound;
    private long _position;
    private readonly float _volume;
    private readonly float _pan; // -1..1

    public CachedSoundSampleProvider(CachedSound cachedSound, float volume, float pan)
    {
        _cachedSound = cachedSound;
        _volume = Math.Clamp(volume, 0f, 1f);
        _pan = Math.Clamp(pan, -1f, 1f);
    }

    public WaveFormat WaveFormat => _cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = _cachedSound.AudioData.Length - _position;
        if (availableSamples <= 0)
            return 0;

        var samplesToCopy = (int)Math.Min(availableSamples, count);

        // simple equal-power pan law
        double leftGain = Math.Cos((_pan + 1) * Math.PI / 4);
        double rightGain = Math.Sin((_pan + 1) * Math.PI / 4);

        for (int i = 0; i < samplesToCopy; i += 2)
        {
            var srcIndex = _position + i;
            if (srcIndex + 1 >= _cachedSound.AudioData.Length) break;

            float left = _cachedSound.AudioData[srcIndex] * _volume * (float)leftGain;
            float right = _cachedSound.AudioData[srcIndex + 1] * _volume * (float)rightGain;

            buffer[offset + i] = left;
            if (i + 1 < samplesToCopy)
                buffer[offset + i + 1] = right;
        }

        _position += samplesToCopy;
        return samplesToCopy;
    }
}
