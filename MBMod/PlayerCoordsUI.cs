using UnityEngine;

/// <summary>
/// Displays the player's current world position and distances to specific points.
/// </summary>
public class PlayerCoordsUI : MonoBehaviour
{
  private Transform playerTransform;
  private bool gotEgg1 = false;
  private bool gotEgg2 = false;

  // Expanded window height to fit player pos and two distances
  private Rect _windowRect = new Rect(20, 260, 240, 110);
  private const int FontScale = 2;
  private static readonly Color TextColor = Color.white;
  private Texture2D _bgTexture;

  // Define the two target points
  private readonly Vector3 point1 = new Vector3(47.3f, 70.1f, 641.7f);
  private readonly Vector3 point2 = new Vector3(149.7f, 18.4f, 906.0f);

  private void Start()
  {
    _bgTexture = new Texture2D(1, 1);
    _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
    _bgTexture.Apply();
  }

  private void OnGUI()
  {
    if (playerTransform == null)
    {
      GameObject player = GameObject.FindWithTag("Player");
      if (player != null)
      {
        playerTransform = player.transform;
      }
    }

    GUI.DrawTexture(_windowRect, _bgTexture, ScaleMode.StretchToFill);

    DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 8, 220, 18), "Player Position");

    if (playerTransform != null)
    {
      Vector3 pos = playerTransform.position;
      string coordText = string.Format("X: {0:F1}  Y: {1:F1}  Z: {2:F1}", pos.x, pos.y, pos.z);
      DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 32, 220, 20), coordText);

      // Calculate distances from player to both points
      float dist1 = Vector3.Distance(pos, point1);
      if (!gotEgg1 && dist1 < 10)
      {
        MBMod.SendNewLocationCheck("level" + Application.loadedLevel + " - egg:47.3 70.1 641.7");
      }
      float dist2 = Vector3.Distance(pos, point2);
      if (!gotEgg2 && dist2 < 10)
      {
        MBMod.SendNewLocationCheck("level" + Application.loadedLevel + " - egg:149.7 18.4 906.0");
      }

      string dist1Text = string.Format("Dist to Pt 1: {0:F1}m", dist1);
      string dist2Text = string.Format("Dist to Pt 2: {0:F1}m", dist2);

      DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 56, 220, 20), dist1Text);
      DrawText(new Rect(_windowRect.x + 10, _windowRect.y + 80, 220, 20), dist2Text);
    }
    else
    {
      DrawText(
        new Rect(_windowRect.x + 10, _windowRect.y + 32, 220, 20),
        "Searching for player..."
      );
    }
  }

  private void DrawText(Rect rect, string text)
  {
    var tex = TextRasterizer.GetTexture(text, FontScale, TextColor);
    var drawRect = new Rect(rect.x, rect.y, Mathf.Min(tex.width, rect.width), rect.height);
    GUI.DrawTexture(drawRect, tex, ScaleMode.ScaleToFit);
  }
}
