using UnityEngine;
using UnityEngine.Audio;

namespace Evo.UI
{
    /// <summary>
    /// Manages audio-related methods for runtime usage.
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Constants.HELP_URL)]
    [AddComponentMenu("Evo/UI/Audio/Audio Manager")]
    public class AudioManager : MonoBehaviour
    {
        // Static Instances
        public static AudioManager instance;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioMixerGroup audioMixerGroup;

        [Header("Settings")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        void Awake()
        {
            if (instance == null) 
            { 
                instance = this;

                if (dontDestroyOnLoad) 
                {
                    if (instance.transform.parent != null) { instance.transform.parent = null; }
                    DontDestroyOnLoad(instance);
                }
            }
        }

        static void CreateInstance()
        {
            if (instance != null)
                return;

            var go = new GameObject { name = "[Evo UI - Audio]" };
            go.AddComponent<AudioManager>();
        }

        /// <summary>
        /// Plays an audio clip via AudioSource.
        /// </summary>
        public static void PlayClip(AudioClip clip, float volume = 1, bool bypassEffects = true, AudioMixerGroup mixerGroup = null)
        {
            if (clip == null) { return; }
            if (instance == null) { CreateInstance(); }
            if (instance.audioSource == null) { instance.audioSource = instance.gameObject.AddComponent<AudioSource>(); }

            instance.audioSource.outputAudioMixerGroup = mixerGroup == null ? instance.audioMixerGroup : mixerGroup;
            instance.audioSource.bypassEffects = !bypassEffects;
            instance.audioSource.bypassReverbZones = !bypassEffects;
            instance.audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}