using BepInEx;
using UnityEngine;

[BepInPlugin("nyix.mathbreakers.test", "Mathbreakers Test Mod", "1.0.0")]
public class MBMod : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo("================================");
        Logger.LogInfo("Mathbreakers Test Mod loaded!");
        Logger.LogInfo("================================");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Logger.LogInfo("F8 pressed!");
        }
    }
}
