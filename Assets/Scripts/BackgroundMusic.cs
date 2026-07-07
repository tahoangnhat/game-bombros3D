using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BackgroundMusic : MonoBehaviour
{
    private const string LoggedOutMusicPath = "Music/AdhesiveWombat - Night Shade  NO COPYRIGHT 8-bit Music - YouTube";
    private const string LoggedInMusicPath = "Music/Kevin MacLeod - Itty Bitty 8 Bit  NO COPYRIGHT 8-bit Music - YouTube";

    private static BackgroundMusic instance;

    [SerializeField] private AudioClip loggedOutMusic;
    [SerializeField] private AudioClip loggedInMusic;

    private AudioSource audioSource;
    private bool wasSignedIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject musicObject = new GameObject(nameof(BackgroundMusic));
        musicObject.AddComponent<AudioSource>();
        instance = musicObject.AddComponent<BackgroundMusic>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;

        if (loggedOutMusic == null)
        {
            loggedOutMusic = Resources.Load<AudioClip>(LoggedOutMusicPath);
        }

        if (loggedInMusic == null)
        {
            loggedInMusic = Resources.Load<AudioClip>(LoggedInMusicPath);
        }

        wasSignedIn = SpringAuthSession.IsSignedIn;
        PlayMusicForAuthState(wasSignedIn);
    }

    private void Update()
    {
        bool isSignedIn = SpringAuthSession.IsSignedIn;
        if (isSignedIn == wasSignedIn)
        {
            return;
        }

        wasSignedIn = isSignedIn;
        PlayMusicForAuthState(isSignedIn);
    }

    private void PlayMusicForAuthState(bool isSignedIn)
    {
        AudioClip targetClip = isSignedIn ? loggedInMusic : loggedOutMusic;
        if (targetClip == null || audioSource.clip == targetClip)
        {
            return;
        }

        audioSource.clip = targetClip;
        audioSource.Play();
    }
}
