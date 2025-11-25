namespace BeatSequencer.Audio
{
    public class AdsrSettings
    {
        public double AttackSeconds { get; set; } = 0.01;
        public double DecaySeconds { get; set; } = 0.2;
        public double SustainLevel { get; set; } = 0.7; // 0–1
        public double ReleaseSeconds { get; set; } = 0.3;

        public AdsrSettings Clone()
        {
            return new AdsrSettings
            {
                AttackSeconds = AttackSeconds,
                DecaySeconds = DecaySeconds,
                SustainLevel = SustainLevel,
                ReleaseSeconds = ReleaseSeconds
            };
        }
    }
}
