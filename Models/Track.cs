namespace BeatSequencer.Models;

/// <summary>
/// Represents a single track (a drum voice) and its 16-step pattern.
/// Includes per-step velocity and per-track volume/pan.
/// </summary>
public class Track
{
    public string Name { get; set; }
    public string SamplePath { get; set; }

    public int StepCount => Steps.Length;

    public float Volume { get; set; } = 1f; // per-track
    public float Pan { get; set; } = 0f;    // -1..1, left..right

    public bool[] Steps { get; private set; }
    public float[] Velocities { get; private set; }

    public void ResizeSteps(int newCount)
    {
        if (newCount <= 0) throw new ArgumentOutOfRangeException(nameof(newCount));
        if (newCount == Steps.Length) return;

        var newSteps = new bool[newCount];
        var newVels = new float[newCount];

        int copyLen = Math.Min(Steps.Length, newCount);
        Array.Copy(Steps, newSteps, copyLen);
        Array.Copy(Velocities, newVels, copyLen);

        // Default velocity for new steps
        for (int i = copyLen; i < newCount; i++)
            newVels[i] = 1f;

        Steps = newSteps;
        Velocities = newVels;
    }

    public Track(string name, string samplePath, int steps = 16)
    {
        Name = name;
        SamplePath = samplePath;
        Steps = new bool[steps];
        Velocities = Enumerable.Repeat(1f, steps).ToArray();
    }


    public Track Clone()
    {
        var t = new Track(Name, SamplePath, Steps.Length)
        {
            Volume = Volume,
            Pan = Pan
        };
        Steps.CopyTo(t.Steps, 0);
        Velocities.CopyTo(t.Velocities, 0);
        return t;
    }
}
