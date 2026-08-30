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
    // globally with their own, which makes stock GUI.Label / GUI.TextField
    // calls render as blank boxes. More importantly, many modern Unity
    // builds strip the legacy built-in "Arial.ttf" resource entirely, so
    // any GUIStyle relying on the skin's default font silently draws zero
    // glyphs. We work around both by creating our own dynamic OS font and
    // assigning it explicitly to every style we use.
    private Font _font;
    private GUIStyle _labelStyle;
    private GUIStyle _textFieldStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _windowStyle;

    private void EnsureStyles()
    {
        if (_labelStyle != null) return;

        // new Font(name) can return a valid-looking object that still
        // renders no glyphs (e.g. no OS font access in this build). Far
        // more reliable: grab a font the game itself already has loaded
        // and is demonstrably using to render text somewhere.
        _font = null;
        var loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
        if (loadedFonts != null && loadedFonts.Length > 0)
        {
            // Prefer one that already has characters baked in, over any
            // that are just empty placeholder Font objects.
            foreach (var f in loadedFonts)
            {
                if (f != null && f.characterInfo != null && f.characterInfo.Length > 0)
                {
                    _font = f;
                    break;
                }
            }
            if (_font == null) _font = loadedFonts[0];
        }

        if (_font == null)
        {
            foreach (var candidate in new[] { "Arial", "Liberation Sans", "Helvetica", "Segoe UI" })
            {
                try
                {
                    _font = new Font(candidate);
                    if (_font != null) break;
                }
                catch
                {
                    // Font not available on this system; try the next candidate.
                }
            }
        }

        // Setting fontSize on a style only works for dynamic fonts, and
        // this game's (older) UnityEngine.dll doesn't even expose a
        // Font.dynamic property to check first - so skip sizing entirely
        // and just let each font render at its native size.
        _labelStyle = new GUIStyle(GUI.skin.label);
        _labelStyle.font = _font;
        _labelStyle.normal.textColor = Color.white;

        _textFieldStyle = new GUIStyle(GUI.skin.textField);
        _textFieldStyle.font = _font;
        _textFieldStyle.normal.textColor = Color.white;
        _textFieldStyle.focused.textColor = Color.white;

        _buttonStyle = new GUIStyle(GUI.skin.button);
        _buttonStyle.font = _font;
        _buttonStyle.normal.textColor = Color.white;
        _buttonStyle.hover.textColor = Color.white;

        _windowStyle = new GUIStyle(GUI.skin.window);
        _windowStyle.font = _font;
        _windowStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (!Visible) return;

        EnsureStyles();

        _windowRect = GUI.Window(GetInstanceID(), _windowRect, DrawWindow, "Connect to Archipelago", _windowStyle);
    }

    private void DrawWindow(int id)
    {
        GUI.Label(new Rect(10, 20, 100, 20), "Host", _labelStyle);
        _host = GUI.TextField(new Rect(90, 20, 230, 20), _host, _textFieldStyle);

        GUI.Label(new Rect(10, 45, 100, 20), "Port", _labelStyle);
        _port = GUI.TextField(new Rect(90, 45, 230, 20), _port, _textFieldStyle);

        GUI.Label(new Rect(10, 70, 100, 20), "Game", _labelStyle);
        _game = GUI.TextField(new Rect(90, 70, 230, 20), _game, _textFieldStyle);

        GUI.Label(new Rect(10, 95, 100, 20), "Slot Name", _labelStyle);
        _playerName = GUI.TextField(new Rect(90, 95, 230, 20), _playerName, _textFieldStyle);

        GUI.Label(new Rect(10, 120, 100, 20), "Password", _labelStyle);
        _password = GUI.PasswordField(new Rect(90, 120, 230, 20), _password, '*', _textFieldStyle);

        if (GUI.Button(new Rect(90, 150, 150, 30), "Connect", _buttonStyle))
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
