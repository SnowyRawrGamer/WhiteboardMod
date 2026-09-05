using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Snowy.WhiteboardMod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    private const string PluginGuid = "snowy.bigwalk.whiteboardmod";
    private const string PluginName = "Whiteboard Mod";
    private const string PluginVersion = "0.2.0";
    private const string RuntimeRootName = "WhiteboardModRuntime";
    private const string TutorialSceneName = "Tutorial";

    private readonly List<GameObject> _createdObjects = new();
    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _newStructures = null!;
    private ConfigEntry<bool> _showInGameSettings = null!;
    private ConfigEntry<KeyboardShortcut> _toggleShortcut = null!;
    private GameObject? _runtimeRoot;
    private bool _showSettings;

    private void Awake()
    {
        _log = Logger;
        _newStructures = Config.Bind("Structures", "NewStructures", true, "When true, spawn the tutorial whiteboard test structure.");
        _showInGameSettings = Config.Bind("UI", "ShowInGameSettings", true, "Show the in-game settings window.");
        _toggleShortcut = Config.Bind("UI", "ToggleSettingsKey", new KeyboardShortcut(KeyCode.F8), "Key used to show or hide the settings window.");
        _newStructures.SettingChanged += OnNewStructuresChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        _log.LogInfo($"{PluginName} {PluginVersion} loaded. NewStructures={_newStructures.Value}.");
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void Update()
    {
        if (_showInGameSettings.Value && _toggleShortcut.Value.IsDown())
            _showSettings = !_showSettings;
    }

    private void OnGUI()
    {
        if (_showSettings && _showInGameSettings.Value)
            _showSettings = GUI.Window(PluginGuid.GetHashCode(), new Rect(24f, 24f, 320f, 130f), DrawSettingsWindow, "Whiteboard Mod Settings");
    }

    private void DrawSettingsWindow(int windowId)
    {
        bool enabled = GUI.Toggle(new Rect(16f, 34f, 285f, 24f), _newStructures.Value, "NewStructures (spawn structure)");
        if (enabled != _newStructures.Value)
            _newStructures.Value = enabled;
        GUI.Label(new Rect(16f, 64f, 285f, 22f), "Saved to BepInEx config automatically.");
        if (GUI.Button(new Rect(230f, 94f, 74f, 24f), "Close"))
            _showSettings = false;
        GUI.DragWindow(new Rect(0f, 0f, 320f, 28f));
    }

    private void OnNewStructuresChanged(object sender, EventArgs args)
    {
        _log.LogInfo($"NewStructures changed to {_newStructures.Value}.");
        if (_newStructures.Value && _runtimeRoot == null && IsTutorialScene(SceneManager.GetActiveScene().name))
            CreateWhiteboardTestStructure();
        else if (!_newStructures.Value)
            DestroyRuntime();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _log.LogInfo($"Scene loaded: '{scene.name}'.");
        DestroyRuntime();
        if (_newStructures.Value && IsTutorialScene(scene.name))
            CreateWhiteboardTestStructure();
    }

    private static bool IsTutorialScene(string sceneName) => string.Equals(sceneName, TutorialSceneName, StringComparison.OrdinalIgnoreCase) || sceneName.Contains("Tutorial", StringComparison.OrdinalIgnoreCase);

    private void CreateWhiteboardTestStructure()
    {
        if (_runtimeRoot != null)
            return;
        _runtimeRoot = new GameObject(RuntimeRootName);
        GameObject structure = new GameObject("WhiteboardTestStructure");
        structure.transform.SetParent(_runtimeRoot.transform, false);
        CreateWhitePrimitive("Floor", structure.transform, new Vector3(0f, -0.1f, 0f), new Vector3(8f, 0.2f, 8f));
        CreateWhitePrimitive("BackWall", structure.transform, new Vector3(0f, 2f, 4f), new Vector3(8f, 4f, 0.2f));
        CreateWhitePrimitive("LeftWall", structure.transform, new Vector3(-4f, 2f, 0f), new Vector3(0.2f, 4f, 8f));
        CreateWhitePrimitive("RightWall", structure.transform, new Vector3(4f, 2f, 0f), new Vector3(0.2f, 4f, 8f));
        CreateWhitePrimitive("Roof", structure.transform, new Vector3(0f, 4f, 0f), new Vector3(8f, 0.2f, 8f));
        GameObject? prefab = FindWhiteboardPrefab();
        if (prefab == null)
        {
            _log.LogWarning("No whiteboard prefab found; structure remains enabled without the board.");
            return;
        }
        try
        {
            GameObject board = Instantiate(prefab);
            board.name = "WhiteboardMod_Instance";
            board.transform.SetParent(structure.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.7f, 3.75f);
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = Vector3.one;
            _createdObjects.Add(board);
        }
        catch (Exception exception)
        {
            _log.LogError($"Whiteboard instantiation failed: {exception}");
        }
    }

    private GameObject CreateWhitePrimitive(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localScale = scale;
        Renderer? renderer = primitive.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard")) { color = Color.white };
            renderer.material = material;
        }
        _createdObjects.Add(primitive);
        return primitive;
    }

    private GameObject? FindWhiteboardPrefab()
    {
        GameObject[] candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject? match = candidates.FirstOrDefault(candidate => candidate.name.Contains("whiteboard", StringComparison.OrdinalIgnoreCase) || candidate.name.Contains("white board", StringComparison.OrdinalIgnoreCase));
        _log.LogInfo(match == null ? $"Searched {candidates.Length} loaded GameObjects; no whiteboard candidate found." : $"Found whiteboard candidate '{match.name}'.");
        return match;
    }

    private void DestroyRuntime()
    {
        if (_runtimeRoot == null)
            return;
        Destroy(_runtimeRoot);
        _runtimeRoot = null;
        _createdObjects.Clear();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_newStructures != null)
            _newStructures.SettingChanged -= OnNewStructuresChanged;
        DestroyRuntime();
    }
}
