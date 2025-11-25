using System;

namespace BeatSequencer.Integration
{
    public class SynthSampleMetadata
    {
        public string Name { get; set; } = "";
        public string FilePath { get; set; } = "";
        public TimeSpan Duration { get; set; }

        public int SampleRate { get; set; }
        public int BitDepth { get; set; }
        public int Channels { get; set; }
    }
}
