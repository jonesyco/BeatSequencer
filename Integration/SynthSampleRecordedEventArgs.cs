using System;

namespace BeatSequencer.Integration
{
    public class SynthSampleRecordedEventArgs : EventArgs
    {
        public SynthSampleMetadata Metadata { get; }

        public SynthSampleRecordedEventArgs(SynthSampleMetadata metadata)
        {
            Metadata = metadata;
        }
    }
}
