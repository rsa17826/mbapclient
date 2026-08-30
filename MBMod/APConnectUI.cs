using UnityEngine;
using ArchipelagoNet;

/// <summary>
/// Minimal on-screen connect form (host/port/slot/password) for the
/// ArchipelagoClient. Uses OnGUI so it needs no Canvas/prefab setup -
/// just add this component once (e.g. from MBMod.Awake) and it'll draw
/// itself every frame until the user connects.
///
/// Usage from your plugin:
///   var uiObj = new GameObject("APConnectUI");
///   UnityEngine.Object.DontDestroyOnLoad(uiObj);
///   var ui = uiObj.AddComponent&lt;APConnectUI&gt;();
///   ui.OnConnectRequested += (hostname, port, game, playerName, password) => {
///       var client = new ArchipelagoClient(hostname, port, game, playerName, password);
///       client.OnLog += msg => Log.LogInfo(msg);
///       client.OnError += msg => Log.LogError(msg);
///       client.Connect();
///   };
/// </summary>
public class APConnectUI : MonoBehaviour
{
    public delegate void ConnectRequestedHandler(string hostname, int? port, string game, string playerName, string password);
    public event ConnectRequestedHandler OnConnectRequested;

    // Toggle with a hotkey so it doesn't sit on screen once connected.
    public KeyCode ToggleKey = KeyCode.F8;
    public bool Visible = true;

    private string _host = "archipelago.gg";
    private string _port = "38281";
    private string _game = "Mathbreakers";
    private string _playerName = "";
    private string _password = "";
    private string _status = "";

    private Rect _windowRect = new Rect(20, 20, 340, 220);

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            Visible = !Visible;
        }
    }

    // Unity ships a built-in default skin; many games replace GUI.skin
    // globally with their own (often with transparent/no text color, or
    // a font that doesn't cover this UI), which makes stock GUI.Label /
    // GUI.TextField calls render as blank boxes. Cache the built-in
    // default once and force it while drawing our window so text is
    // always visible regardless of what the game has set as the skin.
    private GUISkin _defaultSkin;
    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;

    private void EnsureStyles()
    {
        if (_defaultSkin != null) return;

        _defaultSkin = GUI.skin; // whatever's active right now, as a fallback
        // GUIUtility has an internal way to get the true built-in skin,
        // but that's not public API - so instead we explicitly style
        // everything we draw rather than relying on any ambient skin.
        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.normal.textColor = Color.white;

        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (!Visible) return;

        EnsureStyles();

        var oldSkin = GUI.skin;
        GUI.skin = null; // fall back to Unity's actual built-in skin for this window

        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Connect to Archipelago");

        GUI.skin = oldSkin;
    }

    private void DrawWindow(int id)
    {
        GUI.Label(new Rect(10, 20, 100, 20), "Host", _labelStyle);
        _host = GUI.TextField(new Rect(90, 20, 230, 20), _host);

        GUI.Label(new Rect(10, 45, 100, 20), "Port", _labelStyle);
        _port = GUI.TextField(new Rect(90, 45, 230, 20), _port);

        GUI.Label(new Rect(10, 70, 100, 20), "Game", _labelStyle);
        _game = GUI.TextField(new Rect(90, 70, 230, 20), _game);

        GUI.Label(new Rect(10, 95, 100, 20), "Slot Name", _labelStyle);
        _playerName = GUI.TextField(new Rect(90, 95, 230, 20), _playerName);

        GUI.Label(new Rect(10, 120, 100, 20), "Password", _labelStyle);
        _password = GUI.PasswordField(new Rect(90, 120, 230, 20), _password, '*');

        if (GUI.Button(new Rect(90, 150, 150, 30), "Connect"))
        {
            TryConnect();
        }

        if (!string.IsNullOrEmpty(_status))
        {
            GUI.Label(new Rect(10, 185, 320, 30), _status, _labelStyle);
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    private void TryConnect()
    {
        if (string.IsNullOrEmpty(_host))
        {
            _status = "Host is required.";
            return;
        }
        if (string.IsNullOrEmpty(_playerName))
        {
            _status = "Slot name is required.";
            return;
        }

        int? port = null;
        if (!string.IsNullOrEmpty(_port))
        {
            int parsedPort;
            if (!int.TryParse(_port, out parsedPort))
            {
                _status = "Port must be a number.";
                return;
            }
            port = parsedPort;
        }

        _status = "Connecting...";

        if (OnConnectRequested != null)
        {
            OnConnectRequested(_host, port, _game, _playerName, _password);
        }
    }

    /// <summary>Call this from your OnLog/OnError handlers to reflect connection
    /// state back into the window (marshal to the main thread first).</summary>
    public void SetStatus(string status)
    {
        _status = status;
    }
}
