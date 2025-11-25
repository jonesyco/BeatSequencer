namespace BeatSequencer.Integration
{
    public interface ISynthSampleListener
    {
        // Implement this in your sequencer to auto-import samples
        void OnSynthSampleRecorded(SynthSampleMetadata metadata);
    }
}
