using UnityEngine;

public class APConnectUI : MonoBehaviour
{
    private static APConnectUI _instance;

    private CCText _source;
    private GameObject _uiRoot;

    private CCText _archipelago;
    private CCText _server;
    private CCText _port;
    private CCText _player;
    private CCText _password;
    private CCText _connect;

    private bool _panelCreated;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        DontDestroyOnLoad(gameObject);

        Debug.Log(
            "[APConnectUI] Persistent instance created."
        );
    }

    private void Update()
    {
        /*
         * Once the panel exists, DO NOT depend on the menu
         * continuing to exist.
         *
         * The AP UI is now completely persistent.
         */
        if (_panelCreated)
            return;

        FindAndCreatePanel();
    }

    private void FindAndCreatePanel()
    {
        CCText[] texts =
            FindObjectsOfType(typeof(CCText)) as CCText[];

        if (texts == null)
            return;

        for (int i = 0; i < texts.Length; i++)
        {
            CCText text = texts[i];

            if (text == null)
                continue;

            if (!text.gameObject.activeSelf)
                continue;

            if (text.Text != "Factor Hammer")
                continue;

            _source = text;

            Debug.Log(
                "[APConnectUI] Menu source found: Factor Hammer"
            );

            CreatePersistentPanel();

            return;
        }
    }

    private void CreatePersistentPanel()
    {
        if (_source == null)
            return;

        /*
         * Create an independent root.
         */
        _uiRoot =
            new GameObject(
                "MBMod_AP_UI_ROOT"
            );

        DontDestroyOnLoad(_uiRoot);

        Debug.Log(
            "[APConnectUI] Persistent AP UI root created."
        );

        /*
         * Use Factor Hammer only to determine the initial
         * position and orientation.
         */
        Vector3 basePosition =
            _source.transform.position;

        Quaternion rotation =
            _source.transform.rotation;

        Vector3 scale =
            _source.transform.lossyScale;

        _archipelago = CreateText(
            "AP ARCHIPELAGO",
            basePosition +
                new Vector3(0f, 1.5f, 0f),
            rotation,
            scale
        );

        _server = CreateText(
            "AP SERVER",
            basePosition +
                new Vector3(0f, 1.0f, 0f),
            rotation,
            scale
        );

        _port = CreateText(
            "AP PORT",
            basePosition +
                new Vector3(0f, 0.5f, 0f),
            rotation,
            scale
        );

        _player = CreateText(
            "AP PLAYER",
            basePosition +
                new Vector3(0f, 0.0f, 0f),
            rotation,
            scale
        );

        _password = CreateText(
            "AP PASSWORD",
            basePosition +
                new Vector3(0f, -0.5f, 0f),
            rotation,
            scale
        );

        _connect = CreateText(
            "AP CONNECT",
            basePosition +
                new Vector3(0f, -1.0f, 0f),
            rotation,
            scale
        );

        _panelCreated = true;

        Debug.Log(
            "[APConnectUI] Persistent AP panel created."
        );
    }

    private CCText CreateText(
        string text,
        Vector3 worldPosition,
        Quaternion rotation,
        Vector3 scale)
    {
        GameObject clone =
            (GameObject)Instantiate(
                _source.gameObject
            );

        clone.name =
            "MBMod_AP_" + text;

        /*
         * Remove the clone from the menu hierarchy.
         */
        clone.transform.parent = null;

        /*
         * The clone itself must also survive scene changes.
         */
        DontDestroyOnLoad(clone);

        CCText ccText =
            (CCText)clone.GetComponent(
                typeof(CCText)
            );

        if (ccText == null)
        {
            Debug.LogError(
                "[APConnectUI] Clone has no CCText: " +
                text
            );

            Destroy(clone);
            return null;
        }

        ccText.Text = text;

        ccText.Color =
            new Color(
                0f,
                0.2f,
                1f,
                1f
            );

        clone.transform.position =
            worldPosition;

        clone.transform.rotation =
            rotation;

        /*
         * Use the original object's scale.
         */
        clone.transform.localScale =
            _source.transform.localScale;

        clone.SetActive(true);

        MeshRenderer renderer =
            (MeshRenderer)clone.GetComponent(
                typeof(MeshRenderer)
            );

        if (renderer != null)
            renderer.enabled = true;

        Debug.Log(
            "[APConnectUI] Created persistent text: " +
            text +
            " at " +
            clone.transform.position
        );

        return ccText;
    }
}
