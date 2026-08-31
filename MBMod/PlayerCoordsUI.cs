using UnityEngine;

/// <summary>
/// Displays the player's current world position on screen using raw
/// immediate-mode GUI commands to avoid Unity 4 window layout crashes.
/// </summary>
public class PlayerCoordsUI : MonoBehaviour
{
    private Transform playerTransform;
    private Rect _windowRect = new Rect(20, 260, 220, 80);
    private const int FontScale = 2;
    private static readonly Color TextColor = Color.white;
    private Texture2D _bgTexture;

    private void Start()
    {
        // Create a simple semi-transparent background texture for the panel
        _bgTexture = new Texture2D(1, 1);
        _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
        _bgTexture.Apply();
    }

    private void OnGUI()
    {
        // Dynamically find the player if reference is lost or null
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        // Draw background box panel
        GUI.DrawTexture(_windowRect, _bgTexture, ScaleMode.StretchToFill);

        // Draw header and border look
        DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 8, 200, 18), "Player Position");

        if (playerTransform != null)
        {
            Vector3 pos = playerTransform.position;
            string coordText = string.Format("X: {0:F1}  Y: {1:F1}  Z: {2:F1}", pos.x, pos.y, pos.z);
            DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 36, 200, 20), coordText);
//             Transform[] allTransforms = FindObjectsOfType(typeof(Transform)) as Transform[];
// int i = 0;
// foreach (Transform t in allTransforms)
// {
//     if (t != null && t.name == "easter egg")
//     {
//         DrawText(
//             new Rect(_windowRect.x + 10, _windowRect.y + 54 + (18 * i), 200, 20),
//             string.Format("!X: {0:F1}  Y: {1:F1}  Z: {2:F1}",
//                 t.position.x,
//                 t.position.y,
//                 t.position.z)
//         );
//         i++;
//     }
// }
        }
        else
        {
            DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 36, 200, 20), "Searching for player...");
        }
    }

    private void DrawText(Rect rect, string text)
    {
        var tex = TextRasterizer.GetTexture(text, FontScale, TextColor);
        var drawRect = new Rect(rect.x, rect.y, Mathf.Min(tex.width, rect.width), rect.height);
        GUI.DrawTexture(drawRect, tex, ScaleMode.ScaleToFit);
    }
}
