using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Routing")]
    public AudioMixer mainMixer;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [SerializeField] private AudioClip gameOverMusic;
    [SerializeField] private AudioClip gamePlayBgMusic;
    [SerializeField] private AudioClip menuBgMusic;
    [SerializeField] private AudioClip fireBallSFX;
    [SerializeField] private AudioClip hurtSFX;
    [SerializeField] private AudioClip orbSFX;

    private void Awake()
    {
        // Enforce Singleton pattern and persistence
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        PlayMenuBgMusic();
    }

    public void PlayGameBgMusic()
    {
        if (musicSource.clip == gamePlayBgMusic) return; // Prevent restarting the same track

        musicSource.clip = gamePlayBgMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayGameOverBgMusic()
    {
        if (musicSource.clip == gameOverMusic) return; // Prevent restarting the same track

        musicSource.clip = gameOverMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayMenuBgMusic() {
        if (musicSource.clip == menuBgMusic) return; // Prevent restarting the same track

        musicSource.clip = menuBgMusic;
        musicSource.loop = true;
        musicSource.Play();
    }

    public void PlayOrbSFX() {
        sfxSource.PlayOneShot(orbSFX);
    }

    public void PlayHurtSFX()
    {
        sfxSource.PlayOneShot(hurtSFX);
    }

    public void PlayFireBallSFX()
    {
        sfxSource.PlayOneShot(fireBallSFX);
    }
}
