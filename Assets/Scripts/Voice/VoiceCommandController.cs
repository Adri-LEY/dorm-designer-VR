using System;
using System.Collections;
using System.Linq;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Lib;
using Meta.WitAi.Requests;
using Oculus.Voice;
using UnityEngine;

/// <summary>
/// Push-to-talk voice commands combined with pointing ("put that there").
///
/// Hold the left X button, point the right controller at the floor and say "place a chair here".
/// While held, a marker shows the pointed spot and Wit.ai (Meta Voice SDK, AppVoiceExperience) listens.
/// On release, the spot is frozen and the final transcript is parsed by <see cref="VoiceCommandParser"/>;
/// the furniture is then placed at the closest valid spot with <see cref="ObjectSpawner.SpawnNear"/>.
///
/// Without a configured Wit.ai app you can still test with "Run Test Command" (component context menu)
/// or <see cref="RunTextCommand"/>.
/// </summary>
public sealed class VoiceCommandController : MonoBehaviour
{
    private enum State { Idle, Listening, WaitingForTranscript }

    [Header("Speech (Meta Voice SDK)")]
    [Tooltip("AppVoiceExperience with a Wit.ai configuration (Meta > Voice SDK > Get Started).")]
    public AppVoiceExperience voice;

    [Header("Push to talk")]
    [Tooltip("Hold to listen, release to send. Button.One on the left controller is X.")]
    public OVRInput.Button pushToTalkButton = OVRInput.Button.One;
    public OVRInput.Controller pushToTalkController = OVRInput.Controller.LTouch;
    [Tooltip("Seconds to wait for Wit.ai's answer after releasing the button.")]
    [Min(1f)] public float transcriptTimeout = 8f;
    [Tooltip("Log every step of the voice pipeline (mic start/stop, levels, transcripts) to the Console.")]
    public bool verboseLogging = true;

    public enum MicrophoneChoice
    {
        [Tooltip("On PC / in the Editor: the PC's own mic (the Quest mic over Link is often silent). " +
                 "On a Quest build: the headset mic (the only one).")]
        Auto,
        [Tooltip("Always the headset mic (\"Headset Microphone (Oculus Virtual Audio Device)\" on PC).")]
        Headset,
        [Tooltip("Always the PC's own mic (the first one that is not the Oculus/Quest one).")]
        Computer,
        [Tooltip("The mic whose name contains Custom Microphone Name.")]
        Custom,
    }

    [Header("Microphone")]
    [Tooltip("Which mic to record from. It is chosen once when listening starts and never switched automatically.")]
    public MicrophoneChoice microphone = MicrophoneChoice.Auto;
    [Tooltip("Used when Microphone = Custom: part of the device name, e.g. \"Realtek\".")]
    public string customMicrophoneName = "";
    [Tooltip("If no audio at all has arrived after this many seconds, the attempt is cancelled with a message. " +
             "As soon as any audio arrives, you can talk as long as you hold the button.")]
    [Min(0.5f)] public float micStartTimeout = 4f;
    [Tooltip("Loudest level (0-1) the recording must reach to count as speech. Below it, the result is ignored " +
             "(Wit.ai tends to invent words from noise). Check \"max mic level\" in the [Voice] logs to tune it.")]
    [Range(0f, 0.5f)] public float minSpeechLevel = 0.02f;

    [Tooltip("Keep recording while the button is held, even through pauses. Otherwise the SDK stops after ~1 s " +
             "without new words and cuts the sentence (Wit Runtime Configuration keep-alive settings).")]
    public bool listenUntilRelease = true;

    [Header("Testing without controllers")]
    [Tooltip("While ticked in Play mode, acts as if the push-to-talk button were held (untick = release).")]
    public bool simulatePushToTalk;

    [Header("References")]
    public FurnitureMenuManager catalog;
    public ObjectSpawner spawner;
    public PointingTarget pointing;
    public VoiceFeedbackDisplay feedback;

    [Header("Testing without a microphone")]
    [Tooltip("Run from the component's context menu (three-dot menu > Run Test Command) in Play mode. Uses the current pointed spot.")]
    public string testCommand = "place a chair here";

    /// <summary>Raised after each command: the parsed command and whether it succeeded.</summary>
    public event Action<VoiceCommand, bool> CommandExecuted;

    private State _state = State.Idle;
    private Vector3? _liveTarget;
    private Vector3? _frozenTarget;
    private float _timeoutAt;
    private VoiceServiceRequest _request;
    private float _maxMicLevel;
    private bool _wasHeld;
    private bool _micStarted;
    private float _listenStartedAt;
    private float _nextMicSelectionAt;
    // Old versions remembered the last mic in PlayerPrefs; that caused mic flip-flopping, so the key is cleared.
    private const string LegacyMicPrefsKey = "DormDesigner.VoiceMic";

    private bool VoiceConfigured => voice != null && voice.RuntimeConfiguration != null &&
                                    voice.RuntimeConfiguration.witConfiguration != null;

    private void Start()
    {
        if (catalog == null) catalog = FindFirstObjectByType<FurnitureMenuManager>();
        if (spawner == null) spawner = FindFirstObjectByType<ObjectSpawner>();

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif

        if (!VoiceConfigured)
        {
            Debug.LogWarning("VoiceCommandController: no Wit.ai configuration on the AppVoiceExperience; " +
                             "voice input is off (typed test commands still work).", this);
        }

        string devices = Microphone.devices.Length > 0 ? string.Join(", ", Microphone.devices) : "(none)";
        Log($"Microphones: {devices}");
        PlayerPrefs.DeleteKey(LegacyMicPrefsKey);
        StartCoroutine(SelectMicrophoneWhenReady());

        if (listenUntilRelease && VoiceConfigured)
        {
            WitRuntimeConfiguration config = voice.RuntimeConfiguration;
            float max = Mathf.Max(5f, config.maxRecordingTime);
            config.minKeepAliveTimeInSeconds = max;
            config.minTranscriptionKeepAliveTimeInSeconds = max;
            Log($"Push-to-talk: recording continues until X is released (max {max:0} s).");
        }
    }

    // ------------------------------------------------------------------ Microphone choice

    private IEnumerator SelectMicrophoneWhenReady()
    {
        // The SDK creates and opens its mic lazily. Wait until it is open and idle, then select ours,
        // so the first press doesn't pay the switch delay.
        for (int i = 0; i < 40; i++)
        {
#if !UNITY_WEBGL
            Mic mic = SdkMic;
            if (mic != null && mic.Devices.Count > 0 && mic.ActivationState == VoiceAudioInputState.On && !mic.IsRecording) break;
#endif
            yield return new WaitForSecondsRealtime(0.25f);
        }

        SelectPreferredMicrophone();
    }

#if !UNITY_WEBGL
    private static Mic SdkMic => AudioBuffer.Instance != null ? AudioBuffer.Instance.MicInput as Mic : null;
#endif

    /// <summary>
    /// Makes the SDK record from the mic chosen by <see cref="microphone"/>. The Voice SDK alone always takes the
    /// first device, which on a PC is the Quest Link mic even when the player is not wearing the headset.
    /// Only changes device when the wanted one differs from the current one (changing restarts the mic, ~1 s).
    /// </summary>
    private void SelectPreferredMicrophone(bool quiet = false)
    {
#if !UNITY_WEBGL
        // On Quest (Android) there is a single mic: nothing to choose. (UNITY_ANDROID is also defined in the
        // Editor when the build target is Android, so test the actual platform.)
        if (Application.platform == RuntimePlatform.Android) return;

        Mic mic = SdkMic;
        if (mic == null || mic.Devices.Count == 0) return;

        int wanted = FindWantedMicrophone(mic.Devices);
        if (wanted < 0 || wanted == mic.CurrentDeviceIndex) return;

        // Never switch while the SDK is reading the mic: ChangeMicDevice destroys the AudioClip its read loop holds,
        // the loop then stops silently while the SDK still believes it is recording, and no audio ever arrives again.
        if (mic.IsRecording || mic.ActivationState != VoiceAudioInputState.On)
        {
            if (!quiet) Log($"Microphone busy ({mic.ActivationState}, recording: {mic.IsRecording}); keeping \"{mic.CurrentDeviceName}\" for now.");
            return;
        }

        Log($"Using microphone \"{mic.Devices[wanted]}\" ({microphone}).");
        mic.ChangeMicDevice(wanted);
#endif
    }

    private int FindWantedMicrophone(System.Collections.Generic.List<string> devices)
    {
        int headset = devices.FindIndex(IsHeadsetMic);
        int computer = devices.FindIndex(d => !IsHeadsetMic(d));

        switch (microphone)
        {
            case MicrophoneChoice.Headset:
                return headset;
            case MicrophoneChoice.Computer:
                return computer;
            case MicrophoneChoice.Custom:
                return string.IsNullOrWhiteSpace(customMicrophoneName)
                    ? -1
                    : devices.FindIndex(d => d.IndexOf(customMicrophoneName, StringComparison.OrdinalIgnoreCase) >= 0);
            default:
                // The Quest mic seen by Windows ("Oculus Virtual Audio Device") only carries audio during an active
                // Link session with the right Link audio settings; otherwise it records silence. The PC mic is reliable.
                return computer >= 0 ? computer : headset;
        }
    }

    private static bool IsHeadsetMic(string device) =>
        device.IndexOf("Oculus", StringComparison.OrdinalIgnoreCase) >= 0 ||
        device.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0 ||
        device.IndexOf("Meta", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>Direct check (no events): the SDK's audio buffer is on and its mic is reading audio.</summary>
    private static bool IsSdkAudioRunning()
    {
        if (!AudioBuffer.HasInstance) return false;
        AudioBuffer buffer = AudioBuffer.Instance;
        return buffer.AudioState == VoiceAudioInputState.On && buffer.MicInput != null && buffer.MicInput.IsRecording;
    }

    private string MicStateForLogs()
    {
#if !UNITY_WEBGL
        Mic mic = SdkMic;
        return mic != null ? $"mic state {mic.ActivationState}, recording {mic.IsRecording}" : "no SDK mic";
#else
        return "WebGL mic";
#endif
    }

    private string CurrentMicName()
    {
#if !UNITY_WEBGL
        Mic mic = SdkMic;
        return mic != null && !string.IsNullOrEmpty(mic.CurrentDeviceName) ? mic.CurrentDeviceName : "default mic";
#else
        return "default mic";
#endif
    }

    private void OnEnable()
    {
        // The real mic level comes from the AudioBuffer (VoiceEvents only forwards it for custom providers).
        if (AudioBuffer.Instance != null) AudioBuffer.Instance.Events.OnMicLevelChanged.AddListener(HandleMicLevel);

        if (voice == null) return;
        voice.VoiceEvents.OnPartialTranscription.AddListener(HandlePartialTranscription);
        voice.VoiceEvents.OnFullTranscription.AddListener(HandleFullTranscription);
        voice.VoiceEvents.OnError.AddListener(HandleError);
        voice.VoiceEvents.OnStartListening.AddListener(HandleStartListening);
        voice.VoiceEvents.OnStoppedListening.AddListener(HandleStoppedListening);
        voice.VoiceEvents.OnAborted.AddListener(HandleAborted);
        voice.VoiceEvents.OnRequestCompleted.AddListener(HandleRequestCompleted);
    }

    private void OnDisable()
    {
        if (AudioBuffer.HasInstance) AudioBuffer.Instance.Events.OnMicLevelChanged.RemoveListener(HandleMicLevel);

        if (voice == null) return;
        voice.VoiceEvents.OnPartialTranscription.RemoveListener(HandlePartialTranscription);
        voice.VoiceEvents.OnFullTranscription.RemoveListener(HandleFullTranscription);
        voice.VoiceEvents.OnError.RemoveListener(HandleError);
        voice.VoiceEvents.OnStartListening.RemoveListener(HandleStartListening);
        voice.VoiceEvents.OnStoppedListening.RemoveListener(HandleStoppedListening);
        voice.VoiceEvents.OnAborted.RemoveListener(HandleAborted);
        voice.VoiceEvents.OnRequestCompleted.RemoveListener(HandleRequestCompleted);
    }

    private void Update()
    {
        UpdateLiveTarget();

        // If the preferred mic could not be selected yet (SDK mic busy), retry quietly between commands.
        if (_state == State.Idle && Time.unscaledTime >= _nextMicSelectionAt)
        {
            _nextMicSelectionAt = Time.unscaledTime + 2f;
            SelectPreferredMicrophone(quiet: true);
        }

        bool held = OVRInput.Get(pushToTalkButton, pushToTalkController) || simulatePushToTalk;
        if (held && !_wasHeld) BeginListening();
        if (!held && _wasHeld && _state == State.Listening) EndListening();
        _wasHeld = held;

        // Mic watchdog: never switch device on our own (that caused mic flip-flopping); just report and reset.
        if (_state == State.Listening && !_micStarted && IsSdkAudioRunning()) MarkMicWorking("audio buffer on");
        if (_state == State.Listening && !_micStarted && Time.unscaledTime - _listenStartedAt > micStartTimeout)
        {
            Log($"Microphone \"{CurrentMicName()}\" did not start within {micStartTimeout}s ({MicStateForLogs()}).");
            AbortRequest();
            _state = State.Idle;
            feedback?.ShowMessage($"The microphone ({CurrentMicName()}) doesn't respond.\nCheck it, or pick another one in Voice Command Controller > Microphone.",
                VoiceFeedbackDisplay.Tone.Error, 5f);
            Pulse(0.6f);
        }

        if (_state == State.WaitingForTranscript && Time.unscaledTime >= _timeoutAt)
        {
            Log($"No transcript after {transcriptTimeout}s (max mic level heard: {_maxMicLevel:0.000}). Aborting the request.");
            AbortRequest();
            _state = State.Idle;
            if (feedback != null)
            {
                feedback.HideMarker();
                feedback.ShowMessage("I didn't catch that. Hold X, point, and say \"Place a chair here\".",
                    VoiceFeedbackDisplay.Tone.Error, 4f);
            }
        }
    }

    // ------------------------------------------------------------------ Push to talk

    private void BeginListening()
    {
        if (!VoiceConfigured)
        {
            feedback?.ShowMessage("Voice commands are not set up yet (Wit.ai configuration missing).",
                VoiceFeedbackDisplay.Tone.Error, 4f);
            return;
        }

        // A previous request that never finished (no answer, mic problem...) would block every new one
        // with "Audio input is already being performed": always start from a clean state.
        if (_request != null || voice.IsAudioInputActive || voice.IsRequestActive)
        {
            Log("Aborting the previous voice request before listening again.");
            AbortRequest();
        }

        SelectPreferredMicrophone();

        // Reset BEFORE activating: when the SDK's audio buffer is already running, it raises "mic started"
        // synchronously inside ActivateImmediately, and resetting afterwards would erase that signal.
        _state = State.Listening;
        _frozenTarget = null;
        _micStarted = false;
        _maxMicLevel = 0f;
        _listenStartedAt = Time.unscaledTime;

        // Push-to-talk: record immediately. Activate() would wait for the volume to pass a wake threshold first.
        _request = voice.ActivateImmediately(new WitRequestOptions(), new VoiceServiceRequestEvents());
        if (_request == null)
        {
            _state = State.Idle;
            string reason = voice.GetActivateAudioError();
            Log("Could not start listening: " + reason);
            feedback?.ShowMessage("Can't listen right now: " + (string.IsNullOrEmpty(reason) ? "voice service busy" : reason),
                VoiceFeedbackDisplay.Tone.Error, 4f);
            Pulse(0.6f);
            return;
        }

        Pulse();
        Log("Listening (push-to-talk pressed).");
        feedback?.ShowMessage("Listening...  point and speak", VoiceFeedbackDisplay.Tone.Info, 0f, _liveTarget);
    }

    private void AbortRequest()
    {
        if (voice != null) voice.DeactivateAndAbortRequest();
        _request = null;
    }

    private void EndListening()
    {
        // "Here" = where the player points when they finish speaking.
        _frozenTarget = _liveTarget;
        _state = State.WaitingForTranscript;
        _timeoutAt = Time.unscaledTime + transcriptTimeout;
        Pulse();
        if (voice != null) voice.Deactivate();
        Log($"Push-to-talk released, waiting for Wit.ai (max mic level heard: {_maxMicLevel:0.000}).");
        feedback?.ShowMessage("...", VoiceFeedbackDisplay.Tone.Info, 0f, _frozenTarget);
    }

    private void UpdateLiveTarget()
    {
        _liveTarget = null;
        if (pointing != null && pointing.TryGetTarget(out Vector3 point, out _)) _liveTarget = point;

        if (feedback == null) return;

        Vector3? shown = _state == State.Listening ? _liveTarget
            : _state == State.WaitingForTranscript ? _frozenTarget
            : null;

        if (shown.HasValue)
        {
            feedback.ShowMarker(shown.Value);
            if (_state == State.Listening) feedback.SetLabelAnchor(shown);
        }
        else
        {
            feedback.HideMarker();
        }
    }

    // ------------------------------------------------------------------ Wit.ai events

    private void HandlePartialTranscription(string text)
    {
        if (_state == State.Idle || string.IsNullOrWhiteSpace(text)) return;
        MarkMicWorking("partial transcript");
        feedback?.ShowMessage("\"" + text + "\"", VoiceFeedbackDisplay.Tone.Info, 0f);
    }

    private void HandleFullTranscription(string text)
    {
        Log($"Full transcription: \"{text}\"");
        if (_state == State.Idle) return;
        _request = null;

        // Wit.ai may stop on silence before the button is released: use the spot pointed right now.
        Vector3? target = _state == State.Listening ? _liveTarget : _frozenTarget;
        _state = State.Idle;

        // Near-silent recordings make Wit.ai guess random words: ignore them instead of acting on them.
        if (_maxMicLevel < minSpeechLevel)
        {
            Log($"Ignored \"{text}\": recording too quiet (max level {_maxMicLevel:0.000} < {minSpeechLevel:0.000}).");
            feedback?.HideMarker();
            feedback?.ShowMessage($"I couldn't hear you well ({CurrentMicName()}).\nSpeak closer to the mic while holding X.",
                VoiceFeedbackDisplay.Tone.Error, 4f);
            return;
        }

        Execute(text, target);
    }

    private void HandleStartListening() => MarkMicWorking("start event");

    private void HandleStoppedListening() => Log("Microphone stopped.");

    private void HandleMicLevel(float level)
    {
        // Only count the loudness while we are recording a command.
        if (_state != State.Listening) return;
        _maxMicLevel = Mathf.Max(_maxMicLevel, level);
        if (level > 0.0001f) MarkMicWorking("audio level"); // a dead/silent device reports exact zeros
    }

    /// <summary>
    /// Any sign of audio means the mic works. The SDK's "start listening" event alone is not reliable
    /// (it can arrive late or not at all), and relying on it cancelled long commands after a few seconds.
    /// </summary>
    private void MarkMicWorking(string evidence)
    {
        if (_micStarted) return;
        _micStarted = true;
        Log($"Microphone working ({CurrentMicName()}, detected by {evidence}).");
    }

    private void HandleAborted() => Log("Request aborted.");

    private void HandleRequestCompleted()
    {
        Log("Request completed.");
        _request = null;
    }

    private void Log(string message)
    {
        if (verboseLogging) Debug.Log("[Voice] " + message, this);
    }

    private void HandleError(string error, string message)
    {
        Log($"Error: {error} {message}");
        _request = null;
        if (_state == State.Idle) return;
        _state = State.Idle;
        feedback?.HideMarker();
        feedback?.ShowMessage("Voice error: " + (string.IsNullOrEmpty(message) ? error : message),
            VoiceFeedbackDisplay.Tone.Error, 5f);
        Debug.LogWarning($"VoiceCommandController: {error} {message}", this);
    }

    // ------------------------------------------------------------------ Commands

    /// <summary>Runs a command as if it had been spoken, at the spot currently pointed at.</summary>
    public void RunTextCommand(string text)
    {
        UpdateLiveTarget();
        Execute(text, _liveTarget);
    }

    [ContextMenu("Debug: Start Listening (like pressing X)")]
    private void DebugStartListening()
    {
        if (Application.isPlaying) BeginListening();
    }

    [ContextMenu("Debug: Stop Listening (like releasing X)")]
    private void DebugStopListening()
    {
        if (Application.isPlaying && _state == State.Listening) EndListening();
    }

    [ContextMenu("Run Test Command")]
    private void RunTestCommand()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play mode first: the command uses the spot the right controller points at.", this);
            return;
        }

        RunTextCommand(testCommand);
    }

    private void Execute(string transcript, Vector3? target)
    {
        StartCoroutine(HideMarkerSoon());
        VoiceCommand command = VoiceCommandParser.Parse(transcript, catalog != null ? catalog.allFurniture : null);
        Debug.Log($"[Voice] \"{transcript}\" -> {command.Action} {command.Item?.itemName ?? "(no item)"} at {target?.ToString() ?? "(no target)"}");

        if (string.IsNullOrWhiteSpace(transcript))
        {
            Report(command, false, "I didn't hear anything. Hold X while you speak.", target);
            return;
        }

        if (command.Action != VoiceAction.Place)
        {
            Report(command, false, $"Heard \"{transcript}\".\nFor now I can place furniture: try \"Place a chair here\".", target);
            return;
        }

        if (command.Item == null)
        {
            string known = catalog != null ? string.Join(", ", VoiceCommandParser.KnownCategories(catalog.allFurniture)) : "chair, bed";
            string unknown = string.IsNullOrEmpty(command.SpokenObject) ? "which furniture you want" : $"\"{command.SpokenObject}\"";
            Report(command, false, $"I don't know {unknown}.\nTry: {known}.", target);
            return;
        }

        if (!target.HasValue)
        {
            Report(command, false, "Point at the floor where you want it, then try again.", null);
            return;
        }

        GameObject spawned = spawner != null ? spawner.SpawnNear(command.Item, command.Size, command.Color, target.Value) : null;
        if (spawned == null)
        {
            Report(command, false, spawner != null ? spawner.LastFailureMessage : "No spawner in the scene.", target);
            return;
        }

        string details = string.Join(", ", new[] { command.Size?.label, command.Color?.label }
            .Where(s => !string.IsNullOrEmpty(s) && s != "Original" && s != "Standard"));
        string itemName = FurnitureCatalogUtility.PrettyName(command.Item.itemName);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMovementStop();
        Report(command, true, string.IsNullOrEmpty(details) ? $"Placed {itemName}" : $"Placed {itemName} ({details})", spawned.transform.position);
    }

    private void Report(VoiceCommand command, bool success, string message, Vector3? near)
    {
        feedback?.ShowMessage(message, success ? VoiceFeedbackDisplay.Tone.Success : VoiceFeedbackDisplay.Tone.Error,
            success ? 2.5f : 4.5f, near);
        if (!success) Pulse(0.6f);
        CommandExecuted?.Invoke(command, success);
    }

    private IEnumerator HideMarkerSoon()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        if (_state == State.Idle) feedback?.HideMarker();
    }

    // ------------------------------------------------------------------ Haptics

    private void Pulse(float amplitude = 0.35f) => StartCoroutine(PulseRoutine(amplitude));

    private IEnumerator PulseRoutine(float amplitude)
    {
        OVRInput.SetControllerVibration(0.6f, amplitude, pushToTalkController);
        yield return new WaitForSecondsRealtime(0.07f);
        OVRInput.SetControllerVibration(0f, 0f, pushToTalkController);
    }
}
