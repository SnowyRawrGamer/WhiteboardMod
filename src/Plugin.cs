using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Snowy.WhiteboardMod;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    private const string PluginGuid = "snowy.bigwalk.whiteboardmod";
    private const string PluginName = "Whiteboard Mod";
    private const string PluginVersion = "0.4.1";
    private ConfigEntry<bool> _newStructures;
    private WhiteboardRuntime _runtime;

    public override void Load()
    {
        _newStructures = Config.Bind("Structures", "NewStructures", true, "When enabled, spawn the tutorial whiteboard test structure.");
        _newStructures.SettingChanged += OnNewStructuresChanged;
        GameObject host = new GameObject("WhiteboardModHost");
        UnityEngine.Object.DontDestroyOnLoad(host);
        _runtime = host.AddComponent<WhiteboardRuntime>();
        _runtime.Initialize(_newStructures, Log);
        Log.LogInfo($"{PluginName} {PluginVersion} loaded. NewStructures={_newStructures.Value}.");
    }

    private void OnNewStructuresChanged(object sender, EventArgs args) => _runtime?.Apply(_newStructures.Value);
}

internal sealed class WhiteboardRuntime : MonoBehaviour
{
    private ConfigEntry<bool> _setting;
    private ManualLogSource _log;
    private GameObject _root;
    private UnityAction<Scene, LoadSceneMode> _sceneLoadedHandler;

    public void Initialize(ConfigEntry<bool> setting, ManualLogSource log)
    {
        _setting = setting;
        _log = log;
        _sceneLoadedHandler = new UnityAction<Scene, LoadSceneMode>(OnSceneLoaded);
        SceneManager.sceneLoaded += _sceneLoadedHandler;
        Apply(_setting.Value);
    }

    public void Apply(bool enabled)
    {
        if (!enabled)
        {
            if (_root != null) { Destroy(_root); _root = null; }
            return;
        }
        if (_root == null && IsTutorial(SceneManager.GetActiveScene().name))
            _root = new GameObject("WhiteboardModRuntime");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_root != null) { Destroy(_root); _root = null; }
        Apply(_setting.Value);
    }

    private static bool IsTutorial(string name) => string.Equals(name, "Tutorial", StringComparison.OrdinalIgnoreCase) || name.Contains("Tutorial", StringComparison.OrdinalIgnoreCase);

    private void OnDestroy()
    {
        if (_sceneLoadedHandler != null)
            SceneManager.sceneLoaded -= _sceneLoadedHandler;
    }
}
