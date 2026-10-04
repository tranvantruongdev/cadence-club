using PrimeTween;
using UnityEngine;

namespace Template.Infra.Audio
{
    /// <summary>Music with crossfades plus a small pool of SFX voices. Lives for the whole session.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int SfxVoices = 8;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource[] _sfx;
        private int _nextSfx;
        private bool _usingA = true;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1f;

        private AudioSource CurrentMusic => _usingA ? _musicA : _musicB;
        private AudioSource OtherMusic => _usingA ? _musicB : _musicA;

        public static AudioService Create()
        {
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioService>();
        }

        private void Awake()
        {
            _musicA = CreateSource("Music A", true);
            _musicB = CreateSource("Music B", true);
            _sfx = new AudioSource[SfxVoices];
            for (int i = 0; i < _sfx.Length; i++)
            {
                _sfx[i] = CreateSource($"SFX {i}", false);
            }
        }

        public void SetVolumes(float music, float sfx)
        {
            _musicVolume = music;
            _sfxVolume = sfx;
            if (CurrentMusic != null)
            {
                CurrentMusic.volume = music;
            }
        }

        public void PlayMusic(AudioClip clip, float fadeSeconds = 0.6f)
        {
            if (clip == null || (CurrentMusic.clip == clip && CurrentMusic.isPlaying))
            {
                return;
            }

            var from = CurrentMusic;
            var to = OtherMusic;
            _usingA = !_usingA;

            to.clip = clip;
            to.volume = 0f;
            to.Play();
            Tween.Custom(to, 0f, _musicVolume, fadeSeconds, (source, v) => source.volume = v, useUnscaledTime: true);

            if (from.isPlaying)
            {
                Tween.Custom(from, from.volume, 0f, fadeSeconds, (source, v) => source.volume = v, useUnscaledTime: true)
                    .OnComplete(from, source => source.Stop());
            }
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null || _sfxVolume <= 0f)
            {
                return;
            }

            var voice = _sfx[_nextSfx];
            _nextSfx = (_nextSfx + 1) % _sfx.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, _sfxVolume * volumeScale);
        }

        private AudioSource CreateSource(string label, bool loop)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }
    }
}
