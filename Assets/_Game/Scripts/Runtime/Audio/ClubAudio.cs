using System;
using Template.Infra;
using Template.Infra.Audio;
using UnityEngine;

namespace CadenceClub.Audio
{
    /// <summary>
    /// Every sound in the game, synthesised in code the first time it's asked for: short effects for the board, the
    /// club and the gacha, and two music loops (Home, in a level). The loops wrap notes that ring past their end back
    /// to the start, so they repeat without a seam.
    /// </summary>
    public static class ClubAudio
    {
        private const int SfxRate = 44100;
        private const int MusicRate = 22050;

        private static AudioClip _special, _blast, _power, _win, _lose, _chime, _bell, _flip, _fanfare, _coin, _home, _level;

        public static AudioClip Special => _special != null ? _special : (_special = Clip("special", Sweep(0.14f, 600f, 1300f, 0.5f)));
        public static AudioClip Blast => _blast != null ? _blast : (_blast = Clip("blast", Blasted()));
        public static AudioClip Power => _power != null ? _power : (_power = Clip("power", Mix(Sweep(0.32f, 250f, 900f, 0.45f), Noise(0.32f, 0.2f, true))));
        public static AudioClip Win => _win != null ? _win : (_win = Clip("win", Arpeggio(new[] { 72, 76, 79, 84 }, 0.1f, 0.42f, Wave.Triangle, 0.4f)));
        public static AudioClip Lose => _lose != null ? _lose : (_lose = Clip("lose", Arpeggio(new[] { 67, 64, 60 }, 0.18f, 0.4f, Wave.Sine, 0.35f)));
        public static AudioClip Chime => _chime != null ? _chime : (_chime = Clip("chime", Arpeggio(new[] { 88, 95 }, 0.07f, 0.6f, Wave.Sine, 0.3f, 4f)));
        public static AudioClip Bell => _bell != null ? _bell : (_bell = Clip("bell", BikeBell()));
        public static AudioClip Flip => _flip != null ? _flip : (_flip = Clip("flip", Sweep(0.06f, 500f, 1000f, 0.3f)));
        public static AudioClip Fanfare => _fanfare != null ? _fanfare : (_fanfare = Clip("fanfare", Fanfared()));
        public static AudioClip Coin => _coin != null ? _coin : (_coin = Clip("coin", Arpeggio(new[] { 83, 88 }, 0.06f, 0.22f, Wave.Triangle, 0.35f)));

        public static AudioClip HomeMusic => _home != null ? _home : (_home = Clip("home-loop", HomeLoopSamples(), MusicRate));
        public static AudioClip LevelMusic => _level != null ? _level : (_level = Clip("level-loop", LevelLoopSamples(), MusicRate));

        /// <summary>Plays an effect through the template's audio service (nothing before boot).</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (Services.TryGet<AudioService>(out var audio))
            {
                audio.PlaySfx(clip, volume, pitch);
            }
        }

        /// <summary>Crossfades to a music loop (nothing before boot; the same loop keeps playing).</summary>
        public static void Music(AudioClip clip)
        {
            if (Services.TryGet<AudioService>(out var audio))
            {
                audio.PlayMusic(clip);
            }
        }

        // ---- Music

        /// <summary>Home: 90 BPM, eight bars of C–Am–F–G–C–Am–Dm–G; soft pads, a walking bass, an eighth-note arpeggio.</summary>
        public static float[] HomeLoopSamples()
        {
            int[] roots = { 60, 57, 53, 55, 60, 57, 62, 55 };
            bool[] minor = { false, true, false, false, false, true, true, false };
            float beat = 60f / 90f;
            var buffer = new float[Mathf.RoundToInt(roots.Length * 4 * beat * MusicRate)];
            for (int bar = 0; bar < roots.Length; bar++)
            {
                float t0 = bar * 4 * beat;
                int[] chord = Triad(roots[bar], minor[bar]);
                foreach (int note in chord)
                {
                    Note(buffer, MusicRate, t0, Hz(note), 4 * beat, 0.06f, Wave.Sine, 0.3f, 0.6f);
                }

                Note(buffer, MusicRate, t0, Hz(roots[bar] - 24), 1.5f * beat, 0.13f, Wave.Triangle, 0.01f, 0.2f);
                Note(buffer, MusicRate, t0 + 2 * beat, Hz(roots[bar] - 17), 1.5f * beat, 0.11f, Wave.Triangle, 0.01f, 0.2f);
                int[] arp = { chord[0] + 12, chord[1] + 12, chord[2] + 12, chord[0] + 24 };
                for (int i = 0; i < 8; i++)
                {
                    Note(buffer, MusicRate, t0 + i * beat * 0.5f, Hz(arp[i % 4]), beat * 0.4f, 0.045f, Wave.Triangle, 0.01f, 0.15f);
                }
            }

            return Normalized(buffer, 0.7f);
        }

        /// <summary>In a level: 118 BPM, eight bars of Am–F–C–G–Am–F–C–E; eighth-note bass, an arpeggio, kick and hats.</summary>
        public static float[] LevelLoopSamples()
        {
            int[] roots = { 57, 53, 60, 55, 57, 53, 60, 52 };
            bool[] minor = { true, false, false, false, true, false, false, false };
            float beat = 60f / 118f;
            var buffer = new float[Mathf.RoundToInt(roots.Length * 4 * beat * MusicRate)];
            var hats = new System.Random(11);
            for (int bar = 0; bar < roots.Length; bar++)
            {
                float t0 = bar * 4 * beat;
                int[] chord = Triad(roots[bar], minor[bar]);
                foreach (int note in chord)
                {
                    Note(buffer, MusicRate, t0, Hz(note), 4 * beat, 0.04f, Wave.Sine, 0.2f, 0.4f);
                }

                int[] arp = { chord[0] + 12, chord[2] + 12, chord[1] + 12, chord[2] + 12 };
                for (int i = 0; i < 8; i++)
                {
                    float t = t0 + i * beat * 0.5f;
                    Note(buffer, MusicRate, t, Hz(roots[bar] - 24 + (i % 4 == 3 ? 12 : 0)), beat * 0.4f, 0.12f, Wave.Triangle, 0.005f, 0.06f);
                    Note(buffer, MusicRate, t, Hz(arp[i % 4]), beat * 0.3f, 0.045f, Wave.Triangle, 0.005f, 0.1f);
                    if (i % 2 == 1)
                    {
                        Hat(buffer, t, 0.025f, hats);
                    }
                }

                Kick(buffer, t0);
                Kick(buffer, t0 + 2 * beat);
            }

            return Normalized(buffer, 0.7f);
        }

        private static int[] Triad(int root, bool minor) => new[] { root, root + (minor ? 3 : 4), root + 7 };

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static void Kick(float[] buffer, float at)
        {
            int start = Mathf.RoundToInt(at * MusicRate);
            int n = Mathf.RoundToInt(0.14f * MusicRate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2.0 * Math.PI * Mathf.Lerp(120f, 48f, t) / MusicRate;
                buffer[(start + i) % buffer.Length] += (float)Math.Sin(phase) * (1f - t) * 0.22f;
            }
        }

        private static void Hat(float[] buffer, float at, float amp, System.Random random)
        {
            int start = Mathf.RoundToInt(at * MusicRate);
            int n = Mathf.RoundToInt(0.03f * MusicRate);
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                float high = white - last; // a crude high-pass, so it ticks instead of hissing
                last = white;
                buffer[(start + i) % buffer.Length] += high * (1f - i / (float)n) * amp;
            }
        }

        // ---- Effects

        private static float[] Blasted()
        {
            var thump = new float[Mathf.RoundToInt(0.3f * SfxRate)];
            Note(thump, SfxRate, 0f, 90f, 0.05f, 0.5f, Wave.Sine, 0.002f, 0.2f, 0f, false);
            return Mix(thump, Noise(0.3f, 0.35f, false));
        }

        private static float[] BikeBell()
        {
            var buffer = new float[Mathf.RoundToInt(0.6f * SfxRate)];
            foreach (float strike in new[] { 0f, 0.13f })
            {
                Note(buffer, SfxRate, strike, 2100f, 0.4f, 0.22f, Wave.Sine, 0.001f, 0.05f, 9f, false);
                Note(buffer, SfxRate, strike, 2870f, 0.4f, 0.12f, Wave.Sine, 0.001f, 0.05f, 12f, false);
            }

            return buffer;
        }

        private static float[] Fanfared()
        {
            var buffer = new float[Mathf.RoundToInt(1.1f * SfxRate)];
            int[] notes = { 72, 76, 79, 84 };
            for (int i = 0; i < notes.Length; i++)
            {
                Note(buffer, SfxRate, i * 0.07f, Hz(notes[i]), 0.8f - i * 0.07f, 0.16f, Wave.Triangle, 0.01f, 0.2f, 1.5f, false);
            }

            return buffer;
        }

        private static float[] Arpeggio(int[] notes, float step, float last, Wave wave, float amp, float decay = 0f)
        {
            var buffer = new float[Mathf.RoundToInt((step * (notes.Length - 1) + last + 0.1f) * SfxRate)];
            for (int i = 0; i < notes.Length; i++)
            {
                bool final = i == notes.Length - 1;
                Note(buffer, SfxRate, i * step, Hz(notes[i]), final ? last : step, amp, wave, 0.004f, final ? 0.08f : 0.03f, decay, false);
            }

            return buffer;
        }

        private static float[] Sweep(float seconds, float fromHz, float toHz, float amp)
        {
            var buffer = new float[Mathf.RoundToInt(seconds * SfxRate)];
            double phase = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                phase += 2.0 * Math.PI * Mathf.Lerp(fromHz, toHz, t) / SfxRate;
                buffer[i] = (float)Math.Sin(phase) * (1f - t) * Mathf.Min(1f, i / 80f) * amp;
            }

            return buffer;
        }

        private static float[] Noise(float seconds, float amp, bool swell)
        {
            var buffer = new float[Mathf.RoundToInt(seconds * SfxRate)];
            var random = new System.Random(7);
            float smoothed = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                smoothed = Mathf.Lerp(smoothed, (float)(random.NextDouble() * 2.0 - 1.0), swell ? 0.1f : 0.3f); // crude low-pass
                buffer[i] = smoothed * (swell ? Mathf.Sin(t * Mathf.PI) : (1f - t) * (1f - t)) * amp;
            }

            return buffer;
        }

        // ---- Synthesis

        private enum Wave
        {
            Sine,
            Triangle,
        }

        /// <summary>
        /// Adds one note: a linear attack, a hold for <paramref name="seconds"/>, a linear release, and an optional
        /// exponential <paramref name="decay"/> (per second) for struck sounds. With <paramref name="wrap"/>, samples
        /// past the end continue at the start.
        /// </summary>
        private static void Note(float[] buffer, int rate, float at, float hz, float seconds, float amp, Wave wave, float attack, float release,
            float decay = 0f, bool wrap = true)
        {
            int start = Mathf.RoundToInt(at * rate);
            int n = Mathf.RoundToInt((seconds + release) * rate);
            for (int i = 0; i < n; i++)
            {
                int index = start + i;
                if (index >= buffer.Length)
                {
                    if (!wrap)
                    {
                        break;
                    }

                    index %= buffer.Length;
                }

                float t = i / (float)rate;
                float envelope = t < attack ? t / attack : t < seconds ? 1f : Mathf.Max(0f, 1f - (t - seconds) / release);
                if (decay > 0f)
                {
                    envelope *= Mathf.Exp(-t * decay);
                }

                float phase = 2f * Mathf.PI * hz * t;
                float sample = wave == Wave.Sine ? Mathf.Sin(phase) : 2f / Mathf.PI * Mathf.Asin(Mathf.Sin(phase));
                buffer[index] += sample * envelope * amp;
            }
        }

        private static float[] Mix(float[] a, float[] b)
        {
            var mixed = new float[Math.Max(a.Length, b.Length)];
            for (int i = 0; i < mixed.Length; i++)
            {
                mixed[i] = (i < a.Length ? a[i] : 0f) + (i < b.Length ? b[i] : 0f);
            }

            return mixed;
        }

        private static float[] Normalized(float[] buffer, float peak)
        {
            float max = 0f;
            foreach (float s in buffer)
            {
                max = Mathf.Max(max, Mathf.Abs(s));
            }

            if (max > 0f)
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] *= peak / max;
                }
            }

            return buffer;
        }

        private static AudioClip Clip(string name, float[] samples, int rate = SfxRate)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
