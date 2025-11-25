using System;
using System.Windows.Threading;

namespace BeatSequencer.Models;

/// <summary>
/// Simple 16-step sequencer using DispatcherTimer.
/// Supports BPM and swing (shuffle).
/// </summary>
public class Sequencer
{
    private readonly DispatcherTimer _timer;
    private int _currentStepIndex = -1;
    private int _steps;
    private double _bpm;
    private double _swing; // 0..1

    public event EventHandler<int>? StepChanged;
    public const int DefaultStepCount = 32;
    public Sequencer(int steps = DefaultStepCount, double initialBpm = 120)
    {
        _steps = steps;
        _timer = new DispatcherTimer();
        _timer.Tick += OnTimerTick;

        Bpm = initialBpm;
        SwingAmount = 0.0;
        UpdateInterval(0); // initialize
    }

    public int Steps
    {
        get => _steps;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _steps = value;
            _currentStepIndex = -1;
        }
    }

    public double Bpm
    {
        get => _bpm;
        set
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _bpm = value;
        }
    }

    /// <summary>
    /// Swing amount 0..1. Swing delays every 2nd 16th-note.
    /// </summary>
    public double SwingAmount
    {
        get => _swing;
        set => _swing = Math.Clamp(value, 0.0, 1.0);
    }

    public bool IsPlaying => _timer.IsEnabled;

    private void UpdateInterval(int nextStepIndex)
    {
        // Quarter-note duration in ms: 60000 / BPM.
        // 16th note = quarter / 4.
        var baseStepMs = 60000.0 / (_bpm * 4.0);

        // Apply swing: delay even steps a bit, advance odd steps slightly.
        // stepIndex 0-based; we consider 1,3,5,... as "swung" (the offbeats).
        bool isSwungStep = (nextStepIndex % 2 == 1);
        double swingFactor = isSwungStep ? (1.0 + _swing * 0.5) : (1.0 - _swing * 0.25);

        var ms = baseStepMs * swingFactor;
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_steps <= 0) return;

        var nextStep = (_currentStepIndex + 1) % _steps;
        _currentStepIndex = nextStep;
        UpdateInterval(nextStep);

        StepChanged?.Invoke(this, _currentStepIndex);
    }

    public void Start()
    {
        if (IsPlaying) return;
        _currentStepIndex = -1;
        UpdateInterval(0);
        _timer.Start();
    }

    public void Stop()
    {
        if (!IsPlaying) return;
        _timer.Stop();
        _currentStepIndex = -1;
        StepChanged?.Invoke(this, _currentStepIndex);
    }
}
