using UnityEngine;

public class APConnectUI : MonoBehaviour
{
    private GameObject _textObject;
    private CCText _ccText;

    private void Start()
    {
        Debug.Log("[APConnectUI] ===== CCText CLONE TEST =====");

        GameObject template = GameObject.Find(
            "levelselect/selectorParent/prototype text"
        );

        if (template == null)
        {
            Debug.LogError(
                "[APConnectUI] Could not find CCText template!"
            );
            return;
        }

        Debug.Log(
            "[APConnectUI] Found template: " +
            template.name
        );

        CCText original =
            template.GetComponent<CCText>();

        if (original == null)
        {
            Debug.LogError(
                "[APConnectUI] Template has no CCText!"
            );
            return;
        }

        Debug.Log(
            "[APConnectUI] Original text: " +
            original.Text
        );

        _textObject = (GameObject)Instantiate(template);

        _textObject.name = "MBMod_APText_Test";

        _ccText = _textObject.GetComponent<CCText>();

        if (_ccText == null)
        {
            Debug.LogError(
                "[APConnectUI] Clone has no CCText!"
            );
            return;
        }

        // Put it somewhere very obvious.
        _textObject.transform.position =
            new Vector3(0f, 0f, 0f);

        _textObject.transform.localScale =
            Vector3.one;

        _ccText.Color =
            new Color(1f, 0f, 0f, 1f);

        _ccText.Alignment =
            CCText.AlignmentMode.Center;

        _ccText.Text =
            "ARCHIPELAGO TEST";

        // Explicitly force the game's text generator.
        _ccText.UpdateText();

        Debug.Log(
            "[APConnectUI] Clone created."
        );

        Debug.Log(
            "[APConnectUI] Text now: " +
            _ccText.Text
        );

        Debug.Log(
            "[APConnectUI] Font: " +
            (_ccText.Font != null
                ? _ccText.Font.name
                : "NULL")
        );

        Debug.Log(
            "[APConnectUI] Renderer: " +
            (_ccText.renderer != null
                ? _ccText.renderer.name
                : "NULL")
        );

        Debug.Log(
            "[APConnectUI] ===== END TEST ====="
        );
    }
}
