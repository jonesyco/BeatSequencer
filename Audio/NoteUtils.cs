using System;
using System.Collections.Generic;

namespace BeatSequencer.Audio
{
    public static class NoteUtils
    {
        // Simple mapping for two octaves (C4–B5). Add more if you like.
        private static readonly Dictionary<string, int> NoteNumberMap = new()
        {
            { "C4", 60 }, { "C#4", 61 }, { "D4", 62 }, { "D#4", 63 }, { "E4", 64 },
            { "F4", 65 }, { "F#4", 66 }, { "G4", 67 }, { "G#4", 68 }, { "A4", 69 },
            { "A#4", 70 }, { "B4", 71 },

            { "C5", 72 }, { "C#5", 73 }, { "D5", 74 }, { "D#5", 75 }, { "E5", 76 },
            { "F5", 77 }, { "F#5", 78 }, { "G5", 79 }, { "G#5", 80 }, { "A5", 81 },
            { "A#5", 82 }, { "B5", 83 }
        };

        public static int NoteNameToMidi(string noteName)
        {
            if (!NoteNumberMap.TryGetValue(noteName, out var midi))
                throw new ArgumentException($"Unknown note name: {noteName}", nameof(noteName));

            return midi;
        }

        public static double MidiToFrequency(int midiNote)
        {
            // A4 (midi 69) = 440Hz
            return 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);
        }

        public static double NoteNameToFrequency(string noteName)
        {
            int midi = NoteNameToMidi(noteName);
            return MidiToFrequency(midi);
        }
    }
}
