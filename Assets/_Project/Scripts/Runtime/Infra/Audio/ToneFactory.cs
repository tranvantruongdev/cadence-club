using UnityEngine;

namespace Template.Infra.Audio
{
    /// <summary>Generates simple blips in code, so the template has sound without any audio assets.</summary>
    public static class ToneFactory
    {
        private const int SampleRate = 44100;

        public static AudioClip Blip(string name, float frequency, float seconds = 0.08f, float volume = 0.5f)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * seconds));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = 1f - i / (float)samples; // linear decay, no click at the end
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }

            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
