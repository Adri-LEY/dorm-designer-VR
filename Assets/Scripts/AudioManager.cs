using System.Collections.Generic;
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    private const string ResourcePrefabPath = "Audio/AudioManager";

    private enum FeedbackEvent
    {
        Selection,
        Deselection,
        MovementStart,
        MovementStop,
        Deletion,
        WallImpact
    }

    private static AudioManager _instance;

    [Header("Clips")]
    [SerializeField] private AudioClip selectionClip;
    [SerializeField] private AudioClip deselectionClip;
    [SerializeField] private AudioClip movementStartClip;
    [SerializeField] private AudioClip movementStopClip;
    [SerializeField] private AudioClip deletionClip;
    [SerializeField] private AudioClip wallImpactClip;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float selectionVolume = 0.45f;
    [SerializeField, Range(0f, 1f)] private float deselectionVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float movementStartVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float movementStopVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float deletionVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float wallImpactVolume = 0.3f;
    [SerializeField] private bool playMovementStopSound;

    [Header("Playback")]
    [SerializeField, Min(0f)] private float minimumRepeatInterval = 0.12f;
    [SerializeField, Min(0f)] private float wallImpactCooldown = 0.75f;

    private readonly Dictionary<FeedbackEvent, float> _lastPlayedTimes =
        new Dictionary<FeedbackEvent, float>();

    private AudioSource _uiSource;
    private AudioSource _spatialSource;

    public static AudioManager Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateForScene()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(ResourcePrefabPath);
        if (prefab == null)
        {
            Debug.LogError(
                $"AudioManager prefab was not found at Resources/{ResourcePrefabPath}.prefab.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        _uiSource = gameObject.AddComponent<AudioSource>();
        ConfigureSource(_uiSource, 0f);

        GameObject spatialSourceObject = new GameObject("Spatial Audio Source");
        spatialSourceObject.transform.SetParent(transform, false);
        _spatialSource = spatialSourceObject.AddComponent<AudioSource>();
        ConfigureSource(_spatialSource, 1f);
        _spatialSource.rolloffMode = AudioRolloffMode.Linear;
        _spatialSource.minDistance = 0.5f;
        _spatialSource.maxDistance = 8f;

        ValidateClips();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public void PlaySelection()
    {
        Play(FeedbackEvent.Selection, selectionClip, _uiSource, selectionVolume,
            minimumRepeatInterval);
    }

    public void PlayDeselection()
    {
        Play(FeedbackEvent.Deselection, deselectionClip, _uiSource, deselectionVolume,
            minimumRepeatInterval);
    }

    public void PlayMovementStart()
    {
        Play(FeedbackEvent.MovementStart, movementStartClip, _uiSource, movementStartVolume,
            minimumRepeatInterval);
    }

    public void PlayMovementStop()
    {
        if (!playMovementStopSound)
        {
            return;
        }

        Play(FeedbackEvent.MovementStop, movementStopClip, _uiSource, movementStopVolume,
            minimumRepeatInterval);
    }

    public void PlayDeletion()
    {
        Play(FeedbackEvent.Deletion, deletionClip, _uiSource, deletionVolume,
            minimumRepeatInterval);
    }

    public void PlayWallImpact(Vector3 worldPosition)
    {
        Play(FeedbackEvent.WallImpact, wallImpactClip, _spatialSource, wallImpactVolume,
            wallImpactCooldown, worldPosition);
    }

    private void ConfigureSource(AudioSource source, float spatialBlend)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.volume = masterVolume;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
    }

    private void Play(
        FeedbackEvent feedbackEvent,
        AudioClip clip,
        AudioSource source,
        float volume,
        float minimumInterval,
        Vector3? worldPosition = null)
    {
        if (clip == null || source == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        float repeatInterval = Mathf.Max(minimumInterval, clip.length);
        if (_lastPlayedTimes.TryGetValue(feedbackEvent, out float lastPlayed) &&
            now - lastPlayed < repeatInterval)
        {
            return;
        }

        _lastPlayedTimes[feedbackEvent] = now;
        if (worldPosition.HasValue)
        {
            source.transform.position = worldPosition.Value;
        }

        source.PlayOneShot(clip, volume);
    }

    private void ValidateClips()
    {
        ValidateClip(selectionClip, nameof(selectionClip));
        ValidateClip(deselectionClip, nameof(deselectionClip));
        ValidateClip(movementStartClip, nameof(movementStartClip));
        ValidateClip(movementStopClip, nameof(movementStopClip));
        ValidateClip(deletionClip, nameof(deletionClip));
        ValidateClip(wallImpactClip, nameof(wallImpactClip));
    }

    private void ValidateClip(AudioClip clip, string clipField)
    {
        if (clip == null)
        {
            Debug.LogError($"AudioManager has no clip assigned to {clipField}.", this);
        }
    }
}
