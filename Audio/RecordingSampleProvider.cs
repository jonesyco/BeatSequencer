using NAudio.Wave;

namespace BeatSequencer.Audio
{
    public class RecordingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private SynthSampleRecorder? _recorder;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public RecordingSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public void AttachRecorder(SynthSampleRecorder recorder)
        {
            _recorder = recorder;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);

            _recorder?.AddSamples(buffer, offset, read);

            return read;
        }
    }
}
