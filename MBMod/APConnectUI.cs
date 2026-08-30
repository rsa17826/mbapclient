using UnityEngine;

public class APConnectUI : MonoBehaviour
{
private static APConnectUI _instance;

private bool _visible = true;

private string _archipelago = "";
private string _server = "localhost";
private string _port = "38281";
private string _player = "";
private string _password = "";

private string _status = "Disconnected";

private Rect _window =
    new Rect(40f, 40f, 420f, 330f);

private GUIStyle _titleStyle;
private GUIStyle _labelStyle;
private GUIStyle _statusStyle;

public static APConnectUI Instance
{
    get
    {
        return _instance;
    }
}

private void Awake()
{
    if (_instance != null &&
        _instance != this)
    {
        Destroy(gameObject);
        return;
    }

    _instance = this;

    DontDestroyOnLoad(gameObject);

    Debug.Log(
        "[APConnectUI] Standalone AP UI started."
    );
}

private void Start()
{
    CreateStyles();
}

private void CreateStyles()
{
    _titleStyle =
        new GUIStyle(GUI.skin.label);

    _titleStyle.fontSize = 24;
    _titleStyle.fontStyle =
        FontStyle.Bold;

    _labelStyle =
        new GUIStyle(GUI.skin.label);

    _labelStyle.fontSize = 16;

    _statusStyle =
        new GUIStyle(GUI.skin.label);

    _statusStyle.fontSize = 16;

    _statusStyle.normal.textColor =
        new Color(
            0.2f,
            0.7f,
            1f,
            1f
        );
}

private void OnGUI()
{
    if (!_visible)
        return;

    /*
     * Dark translucent background.
     */
    GUI.color =
        new Color(
            0f,
            0f,
            0f,
            0.85f
        );

    GUI.Box(
        _window,
        ""
    );

    GUI.color = Color.white;

    GUILayout.BeginArea(
        new Rect(
            _window.x + 20f,
            _window.y + 15f,
            _window.width - 40f,
            _window.height - 30f
        )
    );

    GUILayout.Label(
        "ARCHIPELAGO",
        _titleStyle
    );

    GUILayout.Space(10f);

    GUILayout.Label(
        "Server",
        _labelStyle
    );

    _server =
        GUILayout.TextField(
            _server,
            GUILayout.Height(28f)
        );

    GUILayout.Space(6f);

    GUILayout.Label(
        "Port",
        _labelStyle
    );

    _port =
        GUILayout.TextField(
            _port,
            GUILayout.Height(28f)
        );

    GUILayout.Space(6f);

    GUILayout.Label(
        "Player",
        _labelStyle
    );

    _player =
        GUILayout.TextField(
            _player,
            GUILayout.Height(28f)
        );

    GUILayout.Space(6f);

    GUILayout.Label(
        "Password",
        _labelStyle
    );

    _password =
        GUILayout.PasswordField(
            _password,
            '*',
            GUILayout.Height(28f)
        );

    GUILayout.Space(12f);

    GUILayout.BeginHorizontal();

    if (GUILayout.Button(
        "CONNECT",
        GUILayout.Height(35f)
    ))
    {
        Connect();
    }

    if (GUILayout.Button(
        "DISCONNECT",
        GUILayout.Height(35f)
    ))
    {
        Disconnect();
    }

    GUILayout.EndHorizontal();

    GUILayout.Space(10f);

    GUILayout.Label(
        "Status: " + _status,
        _statusStyle
    );

    GUILayout.EndArea();
}

private void Connect()
{
    Debug.Log(
        "[APConnectUI] Connect requested."
    );

    _status = "Connecting...";

    /*
     * Hook this into your existing
     * ArchipelagoClient here.
     */
    if (ArchipelagoClient.Instance != null)
    {
        ArchipelagoClient.Instance.Connect(
            _server,
            int.Parse(_port),
            _player,
            _password
        );

        _status = "Connecting...";
    }
    else
    {
        _status =
            "Archipelago client unavailable.";

        Debug.LogError(
            "[APConnectUI] ArchipelagoClient.Instance is null."
        );
    }
}

private void Disconnect()
{
    Debug.Log(
        "[APConnectUI] Disconnect requested."
    );

    if (ArchipelagoClient.Instance != null)
    {
        ArchipelagoClient.Instance.Disconnect();
    }

    _status = "Disconnected";
}

public void SetStatus(
    string status)
{
    _status = status;
}

public void SetServer(
    string server)
{
    _server = server;
}

public void SetPort(
    string port)
{
    _port = port;
}

public void SetPlayer(
    string player)
{
    _player = player;
}

public void SetPassword(
    string password)
{
    _password = password;
}

private void Update()
{
    /*
     * F1 toggles the AP window.
     */
    if (Input.GetKeyDown(
        KeyCode.F1))
    {
        _visible =
            !_visible;
    }

    /*
     * Escape can hide it without affecting
     * the game's own menus.
     */
    if (Input.GetKeyDown(
        KeyCode.F2))
    {
        _visible = false;
    }
}


}
