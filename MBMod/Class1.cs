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
private APUIBridge _uiBridge;

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


    _uiBridge = new APUIBridge();
    _uiBridge.Start();

    // Start with level 1 unlocked.
    UnlockedLevels.Add(1);

    Log.LogInfo("Mathbreakers Save Test loaded!");
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
