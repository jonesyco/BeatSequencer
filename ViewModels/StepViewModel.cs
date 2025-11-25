namespace BeatSequencer.ViewModels;

/// <summary>
/// Represents one step (index 0..15) with on/off, velocity, and current-step highlighting.
/// </summary>
public class StepViewModel : ViewModelBase
{
    private bool _isActive;
    private bool _isCurrent;
    private double _velocity; // 0..1
    private double _timingOffsetMs;

    public int Index { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    /// <summary>Step velocity (0..1). Used for volume scaling.</summary>
    public double Velocity
    {
        get => _velocity;
        set => SetProperty(ref _velocity, Math.Clamp(value, 0.0, 1.0));
    }

    public double TimingOffsetMs
    {
        get => _timingOffsetMs;
        set => SetProperty(ref _timingOffsetMs, value);
    }

    public StepViewModel(int index, bool initialActive = false, double initialVelocity = 1.0)
    {
        Index = index;
        _isActive = initialActive;
        _velocity = initialVelocity;
        _timingOffsetMs = 0;
    }
}
