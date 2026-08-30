using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace ArchipelagoNet
{
    /// <summary>
    /// A .NET 3.5 / Unity-Mono-compatible port of the JS ArchipelagoClient
    /// (native WebSocket implementation of the Archipelago Network Protocol).
    ///
    /// .NET 3.5 has no async/await, no Task, no System.Net.WebSockets, and no
    /// ConcurrentDictionary, so this version is fully event-driven (like the
    /// original JS) instead of async: call Connect(), then subscribe to the
    /// OnXxx events. All socket callbacks run on websocket-sharp's own thread;
    /// marshal back to the Unity main thread yourself if you touch UnityEngine
    /// APIs from a handler (e.g. via a queue drained in Update()).
    ///
    /// Requires:
    ///   - websocket-sharp (NuGet: WebSocketSharp, targets net35)
    ///   - Newtonsoft.Json (NuGet: Newtonsoft.Json, use a 9.x/10.x build for net35)
    /// </summary>
    public class ArchipelagoClient
    {
        // ---- Public events (replace window.onX arrays) ----
        public event Action OnConnectedEvent;
        public event Action OnReceivedItemsEvent;
        public event Action OnRoomUpdateEvent;
        public event Action<string> OnLog;
        public event Action<string> OnWarn;
        public event Action<string> OnError;
        public event Action<string> OnDeathLinkReceived;
        public event Action<JArray> OnHintsUpdated;

        /// <summary>Fired once per newly-received item: (itemName, itemPayload).</summary>
        public event Action<string, JToken> OnGiveItem;

        // ---- Connection config ----
        public string Hostname { get; private set; }
        public int? Port { get; private set; }
        public string Game { get; private set; }
        public string PlayerName { get; private set; }
        public string Password { get; private set; }
        public string Url { get; private set; }
        public bool UseWss { get; private set; }

        // ---- Protocol state ----
        public bool IsAuthenticated { get; private set; }
        public bool DoneConnecting { get; private set; }
        public int Team { get; private set; }
        public int Slot { get; private set; }
        public List<int> MissingLocations = new List<int>();
        public List<int> CheckedLocations = new List<int>();
        public Dictionary<int, SlotInfo> SlotInfoBySlot = new Dictionary<int, SlotInfo>();
        public List<PlayerInfo> Players = new List<PlayerInfo>();
        public JObject SlotData = new JObject();

        public Dictionary<string, Dictionary<long, string>> ItemIdToName = new Dictionary<string, Dictionary<long, string>>();
        public Dictionary<string, Dictionary<long, string>> LocationIdToName = new Dictionary<string, Dictionary<long, string>>();
        public Dictionary<int, ScoutedItem> ScoutedItems = new Dictionary<int, ScoutedItem>();

        public bool DeathLinkEnabled = true;

        public int LastProcessedIndex = 0;
        public int ItemCount = 0;
        public int LastReceivedItem = 0;
        private bool _goalCompleteSent = false;

        /// <summary>Set this to true once your player/save data has finished loading.
        /// Packets that arrive before that are queued in WaitingPackets.</summary>
        public bool PlayerLoaded = false;
        public readonly List<JObject> WaitingPackets = new List<JObject>();

        // Locations checked locally but not yet acknowledged by the server.
        // Plain Dictionary + lock, since .NET 3.5 has no ConcurrentDictionary.
        private readonly Dictionary<int, byte> _checksInFlight = new Dictionary<int, byte>();
        private readonly object _checksInFlightLock = new object();

        private WebSocket _socket;
        private bool _isFallbackMode;
        private double _lastDeathLinkReceivedTime = -1;
        private readonly Random _rng = new Random();

        public ArchipelagoClient(string hostname, int? port, string game, string playerName, string password)
        {
            Hostname = hostname;
            Port = port;
            Game = game;
            PlayerName = playerName;
            Password = password ?? "";
            UseWss = true;
            Url = BuildUrl(UseWss);
        }

        private string BuildUrl(bool wss)
        {
            var scheme = wss ? "wss" : "ws";
            var portPart = (Port.HasValue && Port.Value != 0) ? (":" + Port.Value) : "";
            return scheme + "://" + Hostname + portPart;
        }

        public void AddToChecksInFlight(int locationId)
        {
            lock (_checksInFlightLock) { _checksInFlight[locationId] = 1; }
        }

        private void RemoveFromChecksInFlight(int locationId)
        {
            lock (_checksInFlightLock) { _checksInFlight.Remove(locationId); }
        }

        // ---------------------------------------------------------------
        // Connection lifecycle
        // ---------------------------------------------------------------

        /// <summary>Opens the socket. Non-blocking: connection/handshake progress
        /// is reported via the OnLog/OnConnectedEvent/OnError events.</summary>
        public void Connect()
        {
            _socket = new WebSocket(Url);

            _socket.OnOpen += (sender, e) =>
            {
                Log("WebSocket connection established (" + Url.Split(':')[0] + "). Awaiting 'RoomInfo' from server...");
            };

            _socket.OnMessage += (sender, e) =>
            {
                try
                {
                    var packets = JArray.Parse(e.Data);
                    foreach (var packet in packets.OfType<JObject>())
                    {
                        HandlePacket(packet);
                    }
                }
                catch (Exception ex)
                {
                    Error("Failed to parse incoming JSON payload: " + ex.Message);
                }
            };

            _socket.OnClose += (sender, e) =>
            {
                Warn("[WARNING] Disconnected from Archipelago server. Code: " + e.Code);
            };

            _socket.OnError += (sender, e) =>
            {
                Error("WebSocket network error: " + e.Message);

                // If secure connection fails and we haven't shifted to ws:// yet,
                // flip once and retry (mirrors the JS client's fallback logic).
                if (!_isFallbackMode)
                {
                    UseWss = !UseWss;
                    _isFallbackMode = true;
                    Url = BuildUrl(UseWss);
                    Warn("Retrying with " + Url);

                    // Connect() below creates a brand-new WebSocket instance,
                    // so the old socket's handlers are simply abandoned here
                    // (events can't be cleared with '=' from outside the class).
                    Connect();
                }
                else
                {
                    _isFallbackMode = false;
                }
            };

            _socket.ConnectAsync();
        }

        public void Disconnect()
        {
            if (_socket != null && _socket.IsAlive)
            {
                _socket.Close();
            }
        }

        public void SendPackets(params object[] packetsArray)
        {
            var json = JsonConvert.SerializeObject(packetsArray);
            Log("SENDING TO SERVER: " + json);

            if (_socket != null && _socket.IsAlive)
            {
                _socket.Send(json);
            }
            else
            {
                Error("Cannot send packet; WebSocket connection is closed.");
            }
        }

        // ---------------------------------------------------------------
        // Packet routing
        // ---------------------------------------------------------------

        private void HandlePacket(JObject packet)
        {
            var cmd = (string)packet["cmd"];
            switch (cmd)
            {
                case "RoomInfo": OnRoomInfo(packet); break;
                case "Connected": OnConnected(packet); break;
                case "DataPackage": OnDataPackage(packet); break;
                case "ConnectionRefused": OnConnectionRefused(packet); break;
                case "ReceivedItems": OnReceivedItems(packet); break;
                case "PrintJSON": OnPrintJSON(packet); break;
                case "RoomUpdate": OnRoomUpdate(packet); break;
                case "LocationInfo": OnLocationInfo(packet); break;
                case "Bounced": OnBounced(packet); break;
                case "Retrieved": OnRetrieved(packet); break;
                case "SetReply": OnSetReply(packet); break;
                case "InvalidPacket":
                    Error("Archipelago Server rejected payload: type=" + packet["type"] +
                          ", reason=" + packet["text"] + ", originalCommand=" + packet["original_cmd"]);
                    break;
                default:
                    Log("Received unhandled protocol command: " + cmd);
                    break;
            }
        }

        private void OnRoomUpdate(JObject packet)
        {
            if (!PlayerLoaded)
            {
                WaitingPackets.Add(packet);
                return;
            }

            Log("[Archipelago] Room state updated by server.");

            var checkedLocationsToken = packet["checked_locations"];
            if (checkedLocationsToken != null)
            {
                var checkedLocations = checkedLocationsToken.ToObject<List<int>>();
                foreach (var loc in checkedLocations)
                {
                    if (!CheckedLocations.Contains(loc))
                        CheckedLocations.Add(loc);

                    MissingLocations.RemoveAll(m => m == loc);
                    RemoveFromChecksInFlight(loc);
                }
            }

            if (OnRoomUpdateEvent != null) OnRoomUpdateEvent();
        }

        private void OnRoomInfo(JObject packet)
        {
            var seedName = (string)packet["seed_name"];
            Log("RoomInfo received. Multiworld Seed: " + seedName);

            var connectPayload = new JObject();
            connectPayload["cmd"] = "Connect";
            connectPayload["password"] = Password;
            connectPayload["game"] = Game;
            connectPayload["name"] = PlayerName;
            connectPayload["uuid"] = GenerateUUID();
            var version = new JObject();
            version["major"] = 0;
            version["minor"] = 6;
            version["build"] = 8;
            version["class"] = "Version";
            connectPayload["version"] = version;
            connectPayload["items_handling"] = 7;
            connectPayload["tags"] = DeathLinkEnabled ? new JArray("DeathLink") : new JArray();
            connectPayload["slot_data"] = true;

            Log("Authenticating with server...");

            var getDataPackage = new JObject();
            getDataPackage["cmd"] = "GetDataPackage";
            getDataPackage["games"] = packet["games"];

            SendPackets(getDataPackage, connectPayload);
        }

        private void OnDataPackage(JObject packet)
        {
            var games = (JObject)(packet["data"] != null ? packet["data"]["games"] : null);
            if (games == null) return;

            foreach (var gameProp in games.Properties())
            {
                var game = gameProp.Name;
                var gameData = (JObject)gameProp.Value;

                var itemMap = new Dictionary<long, string>();
                var itemNameToId = (JObject)gameData["item_name_to_id"];
                if (itemNameToId != null)
                {
                    foreach (var p in itemNameToId.Properties())
                        itemMap[(long)p.Value] = p.Name;
                }
                ItemIdToName[game] = itemMap;

                var locMap = new Dictionary<long, string>();
                var locNameToId = (JObject)gameData["location_name_to_id"];
                if (locNameToId != null)
                {
                    foreach (var p in locNameToId.Properties())
                        locMap[(long)p.Value] = p.Name;
                }
                LocationIdToName[game] = locMap;
            }

            Log("[Archipelago] Received DataPackage for games: " + string.Join(", ", games.Properties().Select(p => p.Name).ToArray()));
        }

        /// <summary>Resolve an item id to its display name, given which slot sent it.</summary>
        public string GetItemName(long itemId, int sendingSlot)
        {
            SlotInfo info;
            var game = SlotInfoBySlot.TryGetValue(sendingSlot, out info) ? info.Game : null;
            Dictionary<long, string> map;
            string name;
            if (game != null && ItemIdToName.TryGetValue(game, out map) && map.TryGetValue(itemId, out name))
                return name;
            return "Unknown Item " + game + " - (" + itemId + ")";
        }

        private void OnConnected(JObject packet)
        {
            Log("Successfully connected! Team: " + packet["team"] + ", Slot ID: " + packet["slot"]);

            IsAuthenticated = true;
            DoneConnecting = true;
            Team = (int)packet["team"];
            Slot = (int)packet["slot"];
            MissingLocations = packet["missing_locations"] != null ? packet["missing_locations"].ToObject<List<int>>() : new List<int>();
            CheckedLocations = packet["checked_locations"] != null ? packet["checked_locations"].ToObject<List<int>>() : new List<int>();

            SlotInfoBySlot.Clear();
            var slotInfo = (JObject)packet["slot_info"];
            if (slotInfo != null)
            {
                foreach (var p in slotInfo.Properties())
                {
                    var si = new SlotInfo();
                    si.Name = (string)p.Value["name"];
                    si.Game = (string)p.Value["game"];
                    si.Type = p.Value["type"] != null ? (int)p.Value["type"] : 0;
                    SlotInfoBySlot[int.Parse(p.Name)] = si;
                }
            }

            Players = packet["players"] != null ? packet["players"].ToObject<List<PlayerInfo>>() : new List<PlayerInfo>();
            SlotData = (JObject)(packet["slot_data"] ?? new JObject());

            if (OnConnectedEvent != null) OnConnectedEvent();

            SendStatusUpdate(10); // CLIENT_READY
            RequestHints();
        }

        private void OnConnectionRefused(JObject packet)
        {
            DoneConnecting = true;
            Error("Authentication rejected by server. Errors: " + packet["errors"]);
        }

        /// <summary>Scout locations to see what item they contain, optionally creating a hint.</summary>
        public void SendLocationScouts(IEnumerable<int> locationIds, int createAsHint)
        {
            if (!IsAuthenticated)
            {
                Error("Cannot scout locations yet. Waiting for authentication.");
                return;
            }

            var payload = new JObject();
            payload["cmd"] = "LocationScouts";
            payload["locations"] = new JArray(locationIds.Cast<object>().ToArray());
            payload["create_as_hint"] = createAsHint;

            SendPackets(payload);
        }

        private void OnLocationInfo(JObject packet)
        {
            SlotInfo mySlotInfo;
            var myGame = SlotInfoBySlot.TryGetValue(Slot, out mySlotInfo) ? mySlotInfo.Game : null;

            var locations = (JArray)packet["locations"];
            if (locations == null) return;

            foreach (var entryToken in locations)
            {
                var entry = (JObject)entryToken;
                var location = (int)entry["location"];
                var item = (long)entry["item"];
                var player = (int)entry["player"];
                var flags = entry["flags"] != null ? (int)entry["flags"] : 0;

                var itemName = GetItemName(item, player);
                string locationName;
                Dictionary<long, string> locMap;
                string ln;
                if (myGame != null && LocationIdToName.TryGetValue(myGame, out locMap) && locMap.TryGetValue(location, out ln))
                    locationName = ln;
                else
                    locationName = "Unknown Location (" + location + ")";

                var scouted = new ScoutedItem();
                scouted.ItemName = itemName;
                scouted.ItemPlayer = player;
                scouted.LocationName = locationName;
                scouted.Flags = flags;
                ScoutedItems[location] = scouted;
            }
        }

        /// <summary>Sends a DeathLink to every other connected client that opted in.</summary>
        public void SendDeathLink(string cause)
        {
            if (!DeathLinkEnabled) return;

            var time = (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            var data = new JObject();
            data["time"] = time;
            data["cause"] = cause;
            data["source"] = PlayerName;

            var payload = new JObject();
            payload["cmd"] = "Bounce";
            payload["tags"] = new JArray("DeathLink");
            payload["data"] = data;

            SendPackets(payload);
        }

        private void OnBounced(JObject packet)
        {
            var tagsToken = packet["tags"];
            var tags = tagsToken != null ? tagsToken.ToObject<List<string>>() : null;
            if (tags == null || !tags.Contains("DeathLink")) return;
            if (!DeathLinkEnabled) return;

            var data = (JObject)packet["data"];
            if (data == null) return;

            var time = data["time"] != null ? (double)data["time"] : 0;
            var cause = (string)data["cause"];
            var source = (string)data["source"];

            if (_lastDeathLinkReceivedTime == time) return;
            _lastDeathLinkReceivedTime = time;

            Log("[DeathLink] " + (string.IsNullOrEmpty(cause) ? (source + " died mysteriously") : cause));

            if (source == PlayerName) return;
            if (OnDeathLinkReceived != null) OnDeathLinkReceived(cause);
        }

        /// <summary>Requests a hint from the server using the in-game text command system.</summary>
        public void RequestItemHint(string searchString)
        {
            if (!IsAuthenticated)
            {
                Error("Cannot request hint yet. Waiting for authentication.");
                return;
            }

            var payload = new JObject();
            payload["cmd"] = "Say";
            payload["text"] = "!hint " + searchString;
            SendPackets(payload);
        }

        private void OnReceivedItems(JObject packet)
        {
            var items = (JArray)packet["items"];
            if (items == null) return;

            Log("Received packet containing " + items.Count + " items.");

            if (!PlayerLoaded)
            {
                WaitingPackets.Add(packet);
                return;
            }

            var index = (int)packet["index"];

            for (int offset = 0; offset < items.Count; offset++)
            {
                var item = (JObject)items[offset];
                ItemCount += 1;

                var itemId = (long)item["item"];
                var itemPlayer = (int)item["player"];
                var itemName = GetItemName(itemId, Slot);
                var senderPlayer = Players.FirstOrDefault(p => p.Slot == itemPlayer);
                var senderName = senderPlayer != null ? senderPlayer.Alias : null;
                var globalIndex = index + offset;

                bool isNew = ItemCount > LastReceivedItem;
                Log("[Item Received] ID: " + itemId + " (" + itemName + ")" +
                    (isNew ? "" : " - already received") + " - sent by " + senderName);

                if (isNew)
                {
                    if (ItemCount - 1 == LastReceivedItem)
                    {
                        if (OnGiveItem != null) OnGiveItem(itemName, item);
                    }
                    else
                    {
                        Warn("something went wrong with sending items!! " + LastReceivedItem + " " + ItemCount);
                    }
                    LastReceivedItem = ItemCount;
                }

                LastProcessedIndex = globalIndex + 1;
            }

            if (OnReceivedItemsEvent != null) OnReceivedItemsEvent();
        }

        /// <summary>
        /// Turns a single JSONMessagePart into displayable text, resolving id-based
        /// parts (player_id / item_id / location_id) to names.
        /// </summary>
        private string ResolveMessagePart(JObject part)
        {
            var type = (string)part["type"];
            var text = (string)part["text"] ?? "";

            if (type == "player_id")
            {
                var player = Players.FirstOrDefault(p => p.Slot.ToString() == text);
                return player != null ? (player.Alias ?? player.Name) : ("Player " + text);
            }
            if (type == "item_id")
            {
                var playerSlot = part["player"] != null ? (int)part["player"] : -1;
                SlotInfo info;
                var game = SlotInfoBySlot.TryGetValue(playerSlot, out info) ? info.Game : null;
                long id;
                Dictionary<long, string> map;
                string name;
                if (game != null && ItemIdToName.TryGetValue(game, out map) && long.TryParse(text, out id) && map.TryGetValue(id, out name))
                    return name;
                return "Item #" + text;
            }
            if (type == "location_id")
            {
                var playerSlot = part["player"] != null ? (int)part["player"] : -1;
                SlotInfo info;
                var game = SlotInfoBySlot.TryGetValue(playerSlot, out info) ? info.Game : null;
                long id;
                Dictionary<long, string> map;
                string name;
                if (game != null && LocationIdToName.TryGetValue(game, out map) && long.TryParse(text, out id) && map.TryGetValue(id, out name))
                    return name;
                return "Location #" + text;
            }
            return text;
        }

        private void OnPrintJSON(JObject packet)
        {
            var data = (JArray)packet["data"];
            if (data == null) return;

            var sb = new System.Text.StringBuilder();
            foreach (var partToken in data)
            {
                sb.Append(ResolveMessagePart((JObject)partToken));
            }

            Log("[Archipelago] " + sb.ToString());
        }

        /// <summary>Send items checked inside the game client to the multiworld server.</summary>
        public void SendLocationChecks(IEnumerable<int> locationIds)
        {
            if (!IsAuthenticated)
            {
                Error("Cannot send checks yet. Waiting for server authentication handshake to complete.");
                return;
            }

            var payload = new JObject();
            payload["cmd"] = "LocationChecks";
            payload["locations"] = new JArray(locationIds.Cast<object>().ToArray());

            SendPackets(payload);
        }

        /// <summary>Report this client's ClientStatus to the server. 10=ready, 20=playing, 30=goal complete.</summary>
        public void SendStatusUpdate(int status)
        {
            if (!IsAuthenticated)
            {
                Error("Cannot send status update yet. Waiting for server authentication handshake to complete.");
                return;
            }

            if (status == 30)
            {
                if (_goalCompleteSent) return;
                _goalCompleteSent = true;
            }

            var payload = new JObject();
            payload["cmd"] = "StatusUpdate";
            payload["status"] = status;
            SendPackets(payload);
        }

        private string GenerateUUID()
        {
            var bytes = new byte[8];
            _rng.NextBytes(bytes);
            var s = Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "").ToLowerInvariant();
            return s.Length > 13 ? s.Substring(0, 13) : s;
        }

        // Ask for + subscribe to this slot's hint list right after connecting.
        public void RequestHints()
        {
            var key = "_read_hints_" + Team + "_" + Slot;

            var getPayload = new JObject();
            getPayload["cmd"] = "Get";
            getPayload["keys"] = new JArray(key);

            var notifyPayload = new JObject();
            notifyPayload["cmd"] = "SetNotify";
            notifyPayload["keys"] = new JArray(key);

            SendPackets(getPayload, notifyPayload);
        }

        private void OnRetrieved(JObject packet)
        {
            var key = "_read_hints_" + Team + "_" + Slot;
            var keys = (JObject)packet["keys"];
            if (keys != null && keys[key] != null)
            {
                if (OnHintsUpdated != null) OnHintsUpdated((JArray)(keys[key] ?? new JArray()));
            }
        }

        private void OnSetReply(JObject packet)
        {
            var key = "_read_hints_" + Team + "_" + Slot;
            if ((string)packet["key"] == key)
            {
                if (OnHintsUpdated != null) OnHintsUpdated((JArray)(packet["value"] ?? new JArray()));
            }
        }

        // ---- logging helpers ----
        private void Log(string msg) { if (OnLog != null) OnLog(msg); }
        private void Warn(string msg) { if (OnWarn != null) OnWarn(msg); }
        private void Error(string msg) { if (OnError != null) OnError(msg); }
    }

    public class SlotInfo
    {
        public string Name;
        public string Game;
        public int Type;
    }

    public class PlayerInfo
    {
        [JsonProperty("team")] public int Team;
        [JsonProperty("slot")] public int Slot;
        [JsonProperty("alias")] public string Alias;
        [JsonProperty("name")] public string Name;
    }

    public class ScoutedItem
    {
        public string ItemName;
        public int ItemPlayer;
        public string LocationName;
        public int Flags;
    }
}
