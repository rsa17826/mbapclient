using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System;
using BepInEx.Logging;
using System.Collections.Generic;
using BepInEx;
using ArchipelagoNet;

[BepInPlugin("nyix.mathbreakers.saves", "Mathbreakers Save Test", "1.0.0")]
public class MBMod : BaseUnityPlugin
{
    // Levels currently unlocked by our mod.
    private static readonly HashSet<int> UnlockedLevels = new HashSet<int>();

    private static ManualLogSource Log;
    private static ArchipelagoClient ApClient;
public KeyCode DumpKey = KeyCode.F9;
private bool speedBoostApplied = false; // Declare it here
    private void Update()
    {
        if (Input.GetKeyDown(DumpKey))
        {
            Debug.Log("[NodeDumper] === DUMPING ALL LEVEL NODES ===");

            Transform[] allTransforms = FindObjectsOfType(typeof(Transform)) as Transform[];
            if (allTransforms != null)
            {
                foreach (Transform t in allTransforms)
                {
                    if (t == null) continue;

                    Component[] components = t.GetComponents<Component>();
                    string componentList = "";
                    foreach (Component comp in components)
                    {
                        if (comp != null)
                        {
                            componentList += comp.GetType().Name + ", ";
                        }
                    }

                    Debug.Log("[NodeDumper] Node: " + t.name + " | Components: [" + componentList + "]");
                }
            }
            Debug.Log("[NodeDumper] === DUMP COMPLETE ===");
        }
        if (!speedBoostApplied)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            var walker = player.GetComponent("FPSWalkerEnhanced");
            if (walker != null)
            {
                var type = walker.GetType();
string[] speedFields = {
    "speed", "walkSpeed", "runSpeed", "moveSpeed",
    "jumpSpeed", "ySpeed", "gravity"
};
                bool modifiedAny = false;

                foreach (var fieldName in speedFields)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(float))
                    {
                        float val = (float)field.GetValue(walker);
                        if (val > 0f && val < 100f) // Prevent multi-frame stacking
                        {
                            field.SetValue(walker, val * 7f);
                            Debug.Log(string.Format("[MBMod] Quadrupled FPSWalkerEnhanced.{0} to {1}", fieldName, val * 4f));
                            modifiedAny = true;
                        }
                    }
                    if (field != null && field.FieldType == typeof(Vector3))
                    {
                        Vector3 val = (Vector3)field.GetValue(walker);
                                field.SetValue(walker, val * 4f);
                            Debug.Log(string.Format("[MBMod] Quadrupled FPSWalkerEnhanced.{0} to {1}", fieldName, val * 4f));
                            modifiedAny = true;
                    }
                }

                if (modifiedAny)
                {
                    speedBoostApplied = true; // Lock it in so it only runs once per session/spawn
                }
            }
        }
    }
    }
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

        MethodInfo lightAreaStart = AccessTools.Method(
            typeof(PositiveNegativeLightArea),
            "Start"
        );

        if (lightAreaStart == null)
        {
            Log.LogError("Could not find PositiveNegativeLightArea.Start!");
        }
        else
        {
            harmony.Patch(
                lightAreaStart,
                postfix: new HarmonyMethod(
                    typeof(PositiveNegativeLightArea_StartPatch),
                    nameof(PositiveNegativeLightArea_StartPatch.Postfix)
                )
            );

            Log.LogInfo("Patched PositiveNegativeLightArea.Start");
        }

        MethodInfo numberHoopStart = AccessTools.Method(
            typeof(NumberHoop),
            "Start"
        );

        if (numberHoopStart == null)
        {
            Log.LogError("Could not find NumberHoop.Start!");
        }
        else
        {
            harmony.Patch(
                numberHoopStart,
                postfix: new HarmonyMethod(
                    typeof(NumberHoop_StartPatch),
                    nameof(NumberHoop_StartPatch.Postfix)
                )
            );

            Log.LogInfo("Patched NumberHoop.Start");
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
MethodInfo modifyNumber = AccessTools.Method(
    AccessTools.TypeByName("NumberManager"),
    "ModifyNumber",
    new Type[] {
        typeof(UnityEngine.GameObject),
        AccessTools.TypeByName("Fraction"),
        typeof(bool),
        AccessTools.TypeByName("OperationType"),
        typeof(bool),
        typeof(string)
    }
);

if (modifyNumber != null)
{
    harmony.Patch(
        modifyNumber,
        prefix: new HarmonyMethod(typeof(MBMod), nameof(ModifyNumberPrefix))
    );
    Log.LogInfo("Patched NumberManager.ModifyNumber (Strict Signature)");
}
else
{
    Log.LogError("Could not find the specific ModifyNumber overload!");
}
var numberInfoType = AccessTools.TypeByName("NumberInfo");
if (numberInfoType != null)
{
    var onDestroyNumberMethod = AccessTools.Method(numberInfoType, "OnDestroyNumber");
    if (onDestroyNumberMethod != null)
    {
        harmony.Patch(
            onDestroyNumberMethod,
            prefix: new HarmonyMethod(typeof(MBMod), nameof(NumberInfoOnDestroyNumberPrefix))
        );
        Log.LogInfo("Patched NumberInfo.OnDestroyNumber successfully!");
    }
}
        var uiObj = new GameObject("APConnectUI");
        UnityEngine.Object.DontDestroyOnLoad(uiObj);
        var ui = uiObj.AddComponent<APConnectUI>();
        var uiObj2 = new GameObject("PlayerCoordsUI");
        UnityEngine.Object.DontDestroyOnLoad(uiObj2);
        var ui2 = uiObj2.AddComponent<PlayerCoordsUI>();
        ui.OnConnectRequested += (hostname, port, game, playerName, password) =>
        {
            var client = new ArchipelagoClient(hostname, port, game, playerName, password);
            ApClient = client;
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
            client.OnGiveItem += (itemName, item) =>
            {
                // TODO: replace these placeholder names with whatever your
                // actual AP items are named once they're defined server-side.
                if (itemName == "Light Area Unlock")
                {
                    ApState.LightAreaUnlocked = true;
                    foreach (var area in UnityEngine.Object.FindObjectsOfType(typeof(PositiveNegativeLightArea)))
                    {
                        var col = MBMod.FindTriggerCollider((PositiveNegativeLightArea)area);
                        if (col != null) col.enabled = true;
                    }
                    Log.LogInfo("[AP] Light Area Unlock received - enabling all PositiveNegativeLightArea colliders.");
                }
                else if (itemName == "Multiply Hoop Unlock"
                    || itemName == "Add Hoop Unlock"
                    || itemName == "Exponent Hoop Unlock")
                {
                    HoopType unlockedType =
                        itemName == "Multiply Hoop Unlock" ? HoopType.Multiply :
                        itemName == "Add Hoop Unlock" ? HoopType.Add :
                        HoopType.Exponent;

                    if (unlockedType == HoopType.Multiply) ApState.HoopMultiplyUnlocked = true;
                    if (unlockedType == HoopType.Add) ApState.HoopAddUnlocked = true;
                    if (unlockedType == HoopType.Exponent) ApState.HoopExponentUnlocked = true;

                    // Only enable colliders on hoops matching this specific
                    // type - other hoop types stay gated on their own item.
                    foreach (var hoopObj in UnityEngine.Object.FindObjectsOfType(typeof(NumberHoop)))
                    {
                        var hoop = (NumberHoop)hoopObj;
                        if (hoop.ht != unlockedType) continue;

                        var col = MBMod.FindTriggerCollider(hoop);
                        if (col != null) col.enabled = true;
                    }

                    Log.LogInfo("[AP] " + itemName + " received - enabling matching NumberHoop colliders.");
                }
            };
            client.Connect();
        };

        // Start with level 1 unlocked, just like the game's
        // DefaultPlayerPrefs() did.
        // UnlockedLevels.Add(1);

        Log.LogInfo("Mathbreakers Save Test loaded!");

        // Deferred (Invoke-delayed) self-test: runs a couple seconds after
        // startup, well after AddComponent/Awake have already succeeded, so
        // whichever of these throws can't take the whole plugin down with
        // it. This isolates which dependency assembly is actually failing
        // to load on this very old Unity/Mono runtime.
        Invoke("SelfTestWebSocketSharp", 2.1f);
    }
    private static void ModifyNumberPrefix(object[] __args)
{
    // 1. Use the BepInEx logger to guarantee it shows up in the console
    Log.LogInfo("[asdasdasdasd: ModifyNumberPrefix triggered!");

    // 2. Safely check if we have the expected number of arguments
    if (__args == null || __args.Length < 2) return;

    // 3. Extract the arguments using the __args array
    // __args[0] is GameObject numberObject
    // __args[1] is Fraction newFrac
    var numberObject = __args[0] as UnityEngine.GameObject;
    var newFrac = __args[1];

    if (numberObject == null || newFrac == null) return;

    // Check if the numerator field of the Fraction object is 0 via reflection
    var numField = newFrac.GetType().GetField("numerator");
    if (numField != null && (int)numField.GetValue(newFrac) == 0)
    {
        var component = numberObject.GetComponent("NumberInfo");
        if (component != null)
        {
            var destroyField = component.GetType().GetField("destroyIfZero");
            bool destroyIfZero = destroyField != null && (bool)destroyField.GetValue(component);

            if (destroyIfZero)
            {
                var parent = numberObject.transform.parent;
                if (parent != null)
                {
                    var wallCreator = parent.GetComponent("NumberWallCreator");
                    if (wallCreator != null)
                    {
                        string wallId = GetWallUniqueId(wallCreator as UnityEngine.Component);
                        Log.LogInfo("[MBMod] Zeroed-out brick belonged to wall ID: " + wallId);
                    }
                }
            }
        }
    }
}
private static bool NumberWallCreatorPrefix(object __instance)
{
    var mb = __instance as UnityEngine.MonoBehaviour;
    if (mb == null) return true;

    string wallUniqueId = GetWallUniqueId(mb);
    Debug.Log("[MBMod] Unique Wall ID: " + wallUniqueId);

    return true;
}
private static void NumberInfoOnDestroyNumberPrefix(UnityEngine.MonoBehaviour __instance)
{
    if (__instance == null) return;

    var go = __instance.gameObject;
    if (go == null) return;

    string wallId = "UnknownWall";
    var curr = go.transform.parent;
    while (curr != null)
    {
        var wallCreator = curr.GetComponent("NumberWallCreator");
        if (wallCreator != null)
        {
            wallId = GetWallUniqueId(wallCreator as UnityEngine.Component);
            break;
        }
        curr = curr.parent;
    }

    Log.LogInfo("[MBMod] Block destroyed via NumberInfo.OnDestroyNumber! Wall ID: " + wallId);

    SendNewLocationCheck("level"+Application.loadedLevel+" - wall:" + wallId);
}

/// <summary>
/// Port of the JS newItem() location-check guard logic: looks the name up
/// in slot_data.AP_LOCATION_IDS, skips it if already checked or unmapped,
/// and sends the LocationChecks packet if the client is authenticated.
/// </summary>
private static void SendNewLocationCheck(string name)
{
    if (ApClient == null || ApClient.SlotData == null)
    {
        Log.LogError("[AP] SendNewLocationCheck: failed to check " + name);
        return;
    }

    object idsToken;
    if (!ApClient.SlotData.TryGetValue("AP_LOCATION_IDS", out idsToken))
    {
        Log.LogError("[AP] SendNewLocationCheck: failed to check " + name + " (no AP_LOCATION_IDS in slot_data)");
        return;
    }

    var idsMap = idsToken as Dictionary<string, object>;
    object idToken;
    int apLocationId;
    if (idsMap == null || !idsMap.TryGetValue(name, out idToken) || idToken == null)
    {
        Log.LogWarning("[Archipelago] Ignored location check (No ID mapped): \"" + name + "\"");
        return;
    }
    apLocationId = Convert.ToInt32(idToken);

    if (ApClient.CheckedLocations.Contains(apLocationId))
    {
        Log.LogWarning("[Archipelago] Ignored location check (location already checked): \"" + name + "\"");
        return;
    }

    if (ApClient.IsAuthenticated)
    {
        Log.LogInfo("[Archipelago] Check registered: " + name + " (ID: " + apLocationId + ")");
        ApClient.AddToChecksInFlight(apLocationId);
        ApClient.SendLocationChecks(new[] { apLocationId });
    }
    else
    {
        Log.LogWarning("[Archipelago] failed to send - Client not authenticated.");
    }
}
private static string GetWallUniqueId(UnityEngine.Component mb)
{
    if (mb == null) return "UnknownWall";

    string wallName = mb.gameObject.name;
    Vector3 pos = mb.transform.position;
    int currentLevel = Application.loadedLevel;

    string wallUniqueId = string.Format("Level_{0}_{1}_pos_{2:F1}_{3:F1}_{4:F1}",
        currentLevel, wallName, pos.x, pos.y, pos.z);

    var type = mb.GetType();
    if (type.Name.Contains("Round"))
    {
        var radiusField = type.GetField("radius");
        var thicknessField = type.GetField("thickness");
        var degreesField = type.GetField("degreesToComplete");
        var heightField = type.GetField("height");

        int radius = radiusField != null ? (int)radiusField.GetValue(mb) : 0;
        int thickness = thicknessField != null ? (int)thicknessField.GetValue(mb) : 0;
        int degrees = degreesField != null ? (int)degreesField.GetValue(mb) : 360;
        int height = heightField != null ? (int)heightField.GetValue(mb) : 4;

        wallUniqueId += string.Format("_rad_{0}_thick_{1}_deg_{2}_h_{3}", radius, thickness, degrees, height);
    }

    return wallUniqueId;
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

            Log.LogInfo(Json.Serialize(UnlockedLevels));
            Log.LogInfo(key);
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
          if (level==1)
          SendNewLocationCheck("menu - level:level"+level);
          else
          SendNewLocationCheck("level"+(level-1)+" - level:level"+level);
            // UnlockedLevels.Add(level);

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
    internal static Collider FindTriggerCollider(Component comp)
{
    var col = comp.GetComponent<Collider>();
    if (col != null) return col;

    col = comp.GetComponentInChildren<Collider>();
    if (col == null)
    {
        Log.LogWarning("[AP] No Collider found on or under '" + comp.gameObject.name + "' - can't gate this trigger area.");
    }
    return col;
}
}

/// <summary>
/// Holds AP-unlock flags that gate gameplay elements (e.g. whether the
/// positive/negative light-area trigger colliders are currently active).
/// Set these from your ArchipelagoClient.OnGiveItem handler as the
/// matching items come in.
/// </summary>
public static class ApState
{
    public static bool LightAreaUnlocked = false;

    // One flag per HoopType, gating NumberHoop's trigger collider the
    // same way LightAreaUnlocked gates PositiveNegativeLightArea's.
    public static bool HoopMultiplyUnlocked = false;
    public static bool HoopAddUnlocked = false;
    public static bool HoopExponentUnlocked = false;

    public static bool IsHoopTypeUnlocked(HoopType ht)
    {
        switch (ht)
        {
            case HoopType.Multiply: return HoopMultiplyUnlocked;
            case HoopType.Add: return HoopAddUnlocked;
            case HoopType.Exponent: return HoopExponentUnlocked;
            default: return false;
        }
    }
}

public static class PositiveNegativeLightArea_StartPatch
{
    public static void Postfix(PositiveNegativeLightArea __instance)
    {
        var col = MBMod.FindTriggerCollider(__instance);
        if (col != null)
        {
            col.enabled = ApState.LightAreaUnlocked;
        }
    }
}

public static class NumberHoop_StartPatch
{
    public static void Postfix(NumberHoop __instance)
    {
        var col = MBMod.FindTriggerCollider(__instance);
        if (col != null)
        {
            col.enabled = ApState.IsHoopTypeUnlocked(__instance.ht);
        }
    }
}
