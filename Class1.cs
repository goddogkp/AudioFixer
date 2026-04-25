using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;

public class AudioFixerMod : IModLoader
{
    private static Harmony _harmony;
    private static bool _managerCreated = false;

    public void OnCreated()
    {
        if (_harmony != null) return;
        string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        PitchHelper.LoadConfig(modFolder);
        _harmony = new Harmony("com.yourname.ghostlore.audiofixer");
        _harmony.PatchAll();
        TryCreateManager();
        Debug.Log("=== AudioFixer mod loaded ===");
    }

    private void TryCreateManager()
    {
        if (_managerCreated) return;
        if (GameObject.Find("AudioFixerManager") != null)
        {
            _managerCreated = true;
            return;
        }
        var go = new GameObject("AudioFixerManager");
        GameObject.DontDestroyOnLoad(go);
        go.AddComponent<AudioFixerManager>();
        _managerCreated = true;
        Debug.Log("=== AudioFixer manager started ===");
    }

    public void OnReleased()
    {
        _harmony?.UnpatchAll("com.yourname.ghostlore.audiofixer");
        _harmony = null;
        _managerCreated = false;
        var manager = GameObject.Find("AudioFixerManager");
        if (manager != null)
            GameObject.Destroy(manager);
        Debug.Log("=== AudioFixer mod unloaded ===");
    }

    public void OnGameLoaded(LoadMode mode)
    {
        string modFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        PitchHelper.LoadConfig(modFolder);
        TryCreateManager();
    }

    public void OnGameUnloaded() { }
}

public static class PitchHelper
{
    public static string ModFolder { get; private set; }

    public static bool ENABLED = true;
    public static float PITCH_VARIANCE = 0.15f;
    public static float PITCH_MIN = 0.85f;
    public static float PITCH_MAX = 1.15f;
    public static int MAX_VOICES = 4;
    public static float VOICE_WINDOW = 0.1f;
    public static HashSet<string> MutedPaths = new HashSet<string>();

    static readonly Dictionary<string, Queue<float>> _recentPlays =
        new Dictionary<string, Queue<float>>();

    public static void PlayWithPitch(EventReference soundRef, Vector3 position)
    {
        if (soundRef.IsNull) return;

        if (!ENABLED)
        {
            var fallback = RuntimeManager.CreateInstance(soundRef);
            fallback.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            fallback.start();
            fallback.release();
            return;
        }

        string path = AudioManager.GetPath(soundRef);
        if (MutedPaths.Contains(path)) return;

        string key = soundRef.Guid.ToString();

        if (!_recentPlays.ContainsKey(key))
            _recentPlays[key] = new Queue<float>();

        var queue = _recentPlays[key];
        float now = Time.realtimeSinceStartup;

        while (queue.Count > 0 && now - queue.Peek() > VOICE_WINDOW)
            queue.Dequeue();

        if (queue.Count >= MAX_VOICES)
            return;

        queue.Enqueue(now);

        float pitch = UnityEngine.Random.Range(1f - PITCH_VARIANCE, 1f + PITCH_VARIANCE);
        pitch = Mathf.Clamp(pitch, PITCH_MIN, PITCH_MAX);

        EventInstance ev = RuntimeManager.CreateInstance(soundRef);
        ev.setPitch(pitch);
        ev.set3DAttributes(RuntimeUtils.To3DAttributes(position));
        ev.start();
        ev.release();
    }

    public static void LoadConfig(string modFolder)
    {
        ModFolder = modFolder;
        MutedPaths.Clear();
        string path = Path.Combine(modFolder, "config.txt");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(modFolder);
            File.WriteAllText(path, BuildConfigText());
            Debug.Log("=== AudioFixer: config.txt not found, wrote defaults ===");
            return;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith("#")) continue;
            var parts = line.Split('=');
            if (parts.Length != 2) continue;
            var key = parts[0].Trim();
            var val = parts[1].Split('#')[0].Trim();

            try
            {
                switch (key)
                {
                    case "ENABLED":        ENABLED        = bool.Parse(val);  break;
                    case "PITCH_VARIANCE": PITCH_VARIANCE = float.Parse(val); break;
                    case "PITCH_MIN":      PITCH_MIN      = float.Parse(val); break;
                    case "PITCH_MAX":      PITCH_MAX      = float.Parse(val); break;
                    case "MAX_VOICES":     MAX_VOICES     = int.Parse(val);   break;
                    case "VOICE_WINDOW":   VOICE_WINDOW   = float.Parse(val); break;
                    case "MUTE":           MutedPaths.Add(val);               break;
                }
            }
            catch
            {
                Debug.Log($"=== AudioFixer: could not parse config line: {line} ===");
            }
        }

        Debug.Log($"=== AudioFixer: config loaded — ENABLED={ENABLED}, PITCH_VARIANCE={PITCH_VARIANCE}, MAX_VOICES={MAX_VOICES} ===");
    }

    public static void SaveConfig()
    {
        string path = Path.Combine(ModFolder, "config.txt");
        Directory.CreateDirectory(ModFolder);
        File.WriteAllText(path, BuildConfigText());
        Debug.Log("=== AudioFixer: config saved ===");
    }

    private static string BuildConfigText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AudioFixer Configuration");
        sb.AppendLine("# ENABLED: master on/off for pitch variance and voice limiting");
        sb.AppendLine("# PITCH_VARIANCE: random pitch shift amount (0 = none, 0.5 = large)");
        sb.AppendLine("# PITCH_MIN / PITCH_MAX: hard pitch clamp (1.0 = normal speed)");
        sb.AppendLine("# MAX_VOICES: max simultaneous instances of the same sound within VOICE_WINDOW seconds");
        sb.AppendLine("# VOICE_WINDOW: time window in seconds for voice limiting");
        sb.AppendLine("# MUTE = event:/path/to/sound   (one line per sound to mute, config-file only)");
        sb.AppendLine();
        sb.AppendLine($"ENABLED = {ENABLED}");
        sb.AppendLine($"PITCH_VARIANCE = {PITCH_VARIANCE}");
        sb.AppendLine($"PITCH_MIN = {PITCH_MIN}");
        sb.AppendLine($"PITCH_MAX = {PITCH_MAX}");
        sb.AppendLine($"MAX_VOICES = {MAX_VOICES}");
        sb.AppendLine($"VOICE_WINDOW = {VOICE_WINDOW}");
        if (MutedPaths.Count > 0)
        {
            sb.AppendLine();
            foreach (var p in MutedPaths)
                sb.AppendLine($"MUTE = {p}");
        }
        return sb.ToString();
    }
}

public class AudioFixerManager : MonoBehaviour
{
    private bool _showEditor;
    private Rect _editorWindowRect;
    private Vector2 _scrollPos;
    private GameObject _blockerGO;
    private RectTransform _blockerRect;

    private void Awake()
    {
        _showEditor = PlayerPrefs.GetInt("AudioFixer_EditorVisible", 0) == 1;
        if (_showEditor)
            CenterWindow();
        CreateInputBlocker();
    }

    private void OnDestroy()
    {
        if (_blockerGO != null)
            Destroy(_blockerGO);
    }

    private void CreateInputBlocker()
    {
        _blockerGO = new GameObject("AudioFixer_InputBlocker");
        DontDestroyOnLoad(_blockerGO);

        var canvas = _blockerGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        _blockerGO.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(_blockerGO.transform, false);
        panel.AddComponent<Image>().color = Color.clear;

        _blockerRect = panel.GetComponent<RectTransform>();
        _blockerRect.anchorMin = Vector2.zero;
        _blockerRect.anchorMax = Vector2.zero;
        _blockerRect.pivot     = Vector2.zero;

        _blockerGO.SetActive(false);
    }

    private bool InGame()
    {
        return Singleton<PlayerManager>.instance != null
            && Singleton<PlayerManager>.instance.GetPlayerUI(0) != null;
    }

    private void CenterWindow()
    {
        _editorWindowRect = new Rect(
            (Screen.width  - 380f) / 2f,
            (Screen.height - 390f) / 2f,
            380f, 390f);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.digit9Key.wasPressedThisFrame && InGame())
            ToggleEditor();

        if (_blockerGO != null)
        {
            _blockerGO.SetActive(_showEditor);
            if (_showEditor)
            {
                _blockerRect.anchoredPosition = new Vector2(
                    _editorWindowRect.x,
                    Screen.height - _editorWindowRect.y - _editorWindowRect.height);
                _blockerRect.sizeDelta = new Vector2(_editorWindowRect.width, _editorWindowRect.height);
            }
        }
    }

    private void ToggleEditor()
    {
        _showEditor = !_showEditor;
        if (_showEditor)
            CenterWindow();
        PlayerPrefs.SetInt("AudioFixer_EditorVisible", _showEditor ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnGUI()
    {
        if (!InGame()) return;
        if (_showEditor)
            _editorWindowRect = GUILayout.Window(9002, _editorWindowRect, DrawEditorWindow, "AudioFixer");
    }

    private void DrawEditorWindow(int id)
    {
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(310));

        // Master enable/disable
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("AudioFixer", GUILayout.Width(120));
        bool newEnabled = GUILayout.Toggle(PitchHelper.ENABLED, "Enabled");
        if (newEnabled != PitchHelper.ENABLED)
            PitchHelper.ENABLED = newEnabled;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(2);

        // Pitch and voice settings (grayed out when disabled)
        GUILayout.BeginVertical(GUI.skin.box);
        GUI.enabled = PitchHelper.ENABLED;

        PitchHelper.PITCH_VARIANCE = FloatSlider("Pitch Variance", PitchHelper.PITCH_VARIANCE, 0f,    0.5f);
        PitchHelper.PITCH_MIN      = FloatSlider("Pitch Min",      PitchHelper.PITCH_MIN,      0.5f,  1f);
        PitchHelper.PITCH_MAX      = FloatSlider("Pitch Max",      PitchHelper.PITCH_MAX,      1f,    2f);
        PitchHelper.MAX_VOICES     = IntSlider(  "Max Voices",     PitchHelper.MAX_VOICES,     1,     16);
        PitchHelper.VOICE_WINDOW   = FloatSlider("Voice Window",   PitchHelper.VOICE_WINDOW,   0.01f, 1f);

        GUI.enabled = true;
        GUILayout.EndVertical();

        GUILayout.EndScrollView();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save to config", GUILayout.Height(28)))
            PitchHelper.SaveConfig();
        if (GUILayout.Button("Close", GUILayout.Width(70), GUILayout.Height(28)))
        {
            _showEditor = false;
            PlayerPrefs.SetInt("AudioFixer_EditorVisible", 0);
            PlayerPrefs.Save();
        }
        GUILayout.EndHorizontal();

        var hintStyle = new GUIStyle(GUI.skin.label);
        hintStyle.fontSize = 10;
        hintStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
        hintStyle.alignment = TextAnchor.MiddleCenter;
        GUILayout.Label("Press 9 to close this menu.", hintStyle);
        GUILayout.Space(2);

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private float FloatSlider(string label, float value, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));
        float next = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.Label(next.ToString("F2"), GUILayout.Width(36));
        GUILayout.EndHorizontal();
        return next;
    }

    private int IntSlider(string label, int value, int min, int max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(100));
        float next = GUILayout.HorizontalSlider((float)value, (float)min, (float)max);
        int rounded = Mathf.RoundToInt(next);
        GUILayout.Label(rounded.ToString(), GUILayout.Width(36));
        GUILayout.EndHorizontal();
        return rounded;
    }
}

static class AudioTranspilerHelper
{
    static readonly MethodInfo _playOneShot =
        typeof(AudioManager).GetMethod(
            "PlayOneShot",
            new[] { typeof(EventReference), typeof(Vector2) });

    static readonly MethodInfo _opImplicit =
        typeof(Vector2).GetMethod(
            "op_Implicit",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(Vector3) }, null);

    static readonly FieldInfo _singletonField =
        typeof(Singleton<AudioManager>).GetField(
            "instance",
            BindingFlags.Public | BindingFlags.Static);

    static readonly MethodInfo _replacement =
        typeof(PitchHelper).GetMethod("PlayWithPitch");

    internal static IEnumerable<CodeInstruction> PatchPlayOneShot(
        IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);

        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].Calls(_playOneShot))
                continue;

            list[i].opcode  = OpCodes.Call;
            list[i].operand = _replacement;

            for (int j = i - 1; j >= 0; j--)
            {
                if (list[j].Calls(_opImplicit))
                {
                    list[j].opcode  = OpCodes.Nop;
                    list[j].operand = null;
                    break;
                }
            }

            for (int j = i - 1; j >= 0; j--)
            {
                if (list[j].opcode == OpCodes.Ldsfld
                    && list[j].operand is FieldInfo fi
                    && fi == _singletonField)
                {
                    list[j].opcode  = OpCodes.Nop;
                    list[j].operand = null;
                    break;
                }
            }

            break;
        }

        return list;
    }
}

// Enemy death sounds
[HarmonyPatch(typeof(DeathEffect), nameof(DeathEffect.TriggerDeath))]
static class Patch_DeathEffect_TriggerDeath
{
    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
        => AudioTranspilerHelper.PatchPlayOneShot(instructions);
}

// Hit sounds
[HarmonyPatch(typeof(CauseDamageInstance), "PerformDamage")]
static class Patch_CauseDamageInstance_PerformDamage
{
    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
        => AudioTranspilerHelper.PatchPlayOneShot(instructions);
}

// Projectile/skill sounds (combat only)
[HarmonyPatch(typeof(ProjectileInstance), "PlayAudio")]
static class Patch_ProjectileInstance_PlayAudio
{
    static bool Prefix(ProjectileInstance __instance)
    {
        if (!PitchHelper.ENABLED) return true; // disabled — let original run

        if (__instance.Power == null) return true; // pickup/non-combat — play normally

        var projectileField = typeof(ProjectileInstance)
            .GetField("projectile", BindingFlags.NonPublic | BindingFlags.Instance);
        var projectile = projectileField?.GetValue(__instance) as Projectile;

        if (projectile?.SoundFX == null) return true;

        foreach (var sound in projectile.SoundFX)
            PitchHelper.PlayWithPitch(sound, __instance.transform.position);

        return false; // handled all sounds, skip original
    }
}
