using System;
using BepInEx.Logging;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ArchipelagoNet;

[BepInPlugin("nyix.mathbreakers.saves", "Mathbreakers Save Test", "1.0.0")]
public class MBMod : BaseUnityPlugin
{
    // Levels currently unlocked by our mod.
    private static readonly HashSet<int> UnlockedLevels = new HashSet<int>();

    private static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        Log.LogInfo("================================");
        Log.LogInfo("Mathbreakers Save Test loading...");
        Log.LogInfo("================================");

        var harmony = new Harmony("nyix.mathbreakers.saves");

        // Find UnityEngine.PlayerPrefs methods.
        MethodInfo setInt = AccessTools.Method(
            typeof(PlayerPrefs),
            "SetInt",
            new Type[] { typeof(string), typeof(int) }
        );

        MethodInfo hasKey = AccessTools.Method(
            typeof(PlayerPrefs),
            "HasKey",
            new Type[] { typeof(string) }
        );

        if (setInt == null)
        {
            Log.LogError("Could not find PlayerPrefs.SetInt!");
        }
        else
        {
            harmony.Patch(
                setInt,
                prefix: new HarmonyMethod(
                    typeof(MBMod),
                    nameof(PlayerPrefsSetIntPrefix)
                )
            );

            Log.LogInfo("Patched PlayerPrefs.SetInt");
        }

        if (hasKey == null)
        {
            Log.LogError("Could not find PlayerPrefs.HasKey!");
        }
        else
        {
            harmony.Patch(
                hasKey,
                prefix: new HarmonyMethod(
                    typeof(MBMod),
                    nameof(PlayerPrefsHasKeyPrefix)
                )
            );

            Log.LogInfo("Patched PlayerPrefs.HasKey");
        }
MethodInfo createNewWall = AccessTools.Method(
    AccessTools.TypeByName("NumberWallCreator"),
    "CreateNewWall"
);

if (createNewWall != null)
{
    harmony.Patch(
        createNewWall,
        prefix: new HarmonyMethod(typeof(MBMod), nameof(NumberWallCreatorPrefix))
    );
    Log.LogInfo("Patched NumberWallCreator.CreateNewWall");
}
        var uiObj = new GameObject("APConnectUI");
        UnityEngine.Object.DontDestroyOnLoad(uiObj);
        var ui = uiObj.AddComponent<APConnectUI>();
        ui.OnConnectRequested += (hostname, port, game, playerName, password) =>
        {
            var client = new ArchipelagoClient(hostname, port, game, playerName, password);
            client.OnLog += msg => Log.LogInfo(msg);
            client.OnError += msg =>
            {
                Log.LogError(msg);
                ui.SetStatus("Error: " + msg);
            };
            client.OnConnectedEvent += () =>
            {
                ui.SetStatus("Connected!");
            };
            client.Connect();
        };

        // Start with level 1 unlocked, just like the game's
        // DefaultPlayerPrefs() did.
        UnlockedLevels.Add(1);

        Log.LogInfo("Mathbreakers Save Test loaded!");

        // Deferred (Invoke-delayed) self-test: runs a couple seconds after
        // startup, well after AddComponent/Awake have already succeeded, so
        // whichever of these throws can't take the whole plugin down with
        // it. This isolates which dependency assembly is actually failing
        // to load on this very old Unity/Mono runtime.
        Invoke("SelfTestWebSocketSharp", 2.1f);
    }
    private static bool NumberWallCreatorPrefix(object __instance)
{
    var mb = __instance as UnityEngine.MonoBehaviour;
    if (mb == null) return true;

    string wallName = mb.gameObject.name;
    Vector3 pos = mb.transform.position;
    int currentLevel = Application.loadedLevel;

    // Unique ID scoped to the level and specific wall instance
    string wallUniqueId = string.Format("Level_{0}_{1}_pos_{2:F1}_{3:F1}_{4:F1}",
        currentLevel, wallName, pos.x, pos.y, pos.z);

    Debug.Log("[MBMod] Unique Wall ID: " + wallUniqueId);

    // true = allow the original CreateNewWall method to run normally
    return true;
}
    private void SelfTestWebSocketSharp()
    {
        try
        {
            var ws = new WebSocketSharp.WebSocket("ws://127.0.0.1:1");
            Log.LogInfo("[SelfTest] WebSocketSharp OK: " + ws.GetType().AssemblyQualifiedName);
        }
        catch (Exception ex)
        {
            Log.LogError("[SelfTest] WebSocketSharp FAILED: " + ex);
        }
    }

    /*
     * Intercepts:
     *
     * PlayerPrefs.SetInt("unlockedLevel7", 1);
     *
     * We prevent the game from writing that key to PlayerPrefs
     * and put the unlock into our own in-memory save instead.
     */
    private static bool PlayerPrefsSetIntPrefix(string key, int value)
    {
        int level;

        if (!TryGetLevelKey(key, out level))
        {
            // Not one of our keys.
            // Let Unity handle it normally.
            return true;
        }

        Log.LogInfo(
            "[SAVE] Game requested unlock: " +
            key +
            " = " +
            value
        );

        if (value != 0)
        {
            UnlockedLevels.Add(level);

            Log.LogInfo(
                "[SAVE] Level " +
                level +
                " unlocked in mod save."
            );
        }
        else
        {
            UnlockedLevels.Remove(level);

            Log.LogInfo(
                "[SAVE] Level " +
                level +
                " locked in mod save."
            );
        }

        // false = don't execute the original PlayerPrefs.SetInt().
        return false;
    }

    /*
     * Intercepts:
     *
     * PlayerPrefs.HasKey("unlockedLevel7");
     *
     * The game's MenuButton_LevelSelect.CheckUnlocks()
     * will receive the result from our save instead.
     */
    private static bool PlayerPrefsHasKeyPrefix(
        string key,
        ref bool __result)
    {
        int level;

        if (!TryGetLevelKey(key, out level))
        {
            // Not one of our keys.
            return true;
        }

        bool unlocked = UnlockedLevels.Contains(level);

        Log.LogInfo(
            "[SAVE] Game asked whether level " +
            level +
            " is unlocked -> " +
            unlocked
        );

        __result = unlocked;

        // false = don't execute the original PlayerPrefs.HasKey().
        return false;
    }

    /*
     * Turns:
     *
     * "unlockedLevel1"
     * "unlockedLevel2"
     * "unlockedLevel42"
     *
     * into the corresponding integer level.
     */
    private static bool TryGetLevelKey(
        string key,
        out int level)
    {
        const string prefix = "unlockedLevel";

        level = 0;

        if (key == null)
            return false;

        if (!key.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        string number = key.Substring(prefix.Length);

        return int.TryParse(number, out level);
    }
}
