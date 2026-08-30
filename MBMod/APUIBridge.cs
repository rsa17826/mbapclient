using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class APUIBridge
{
    private const int Port = 38741;

    private TcpListener _listener;
    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _thread;

    private readonly object _lock = new object();

    public void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, Port);
            _listener.Start();

            _thread = new Thread(ListenLoop);
            _thread.IsBackground = true;
            _thread.Start();

            Debug.Log("[APUIBridge] Listening on localhost:" + Port);
        }
        catch (Exception ex)
        {
            Debug.LogError("[APUIBridge] Failed to start: " + ex);
        }
    }

    private void ListenLoop()
    {
        while (true)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();

                lock (_lock)
                {
                    if (_client != null)
                    {
                        try { _client.Close(); }
                        catch { }
                    }

                    _client = client;
                    _stream = client.GetStream();
                }

                Debug.Log("[APUIBridge] UI connected.");

                SendState();
            }
            catch (Exception ex)
            {
                Debug.LogError("[APUIBridge] Listener error: " + ex);
                Thread.Sleep(500);
            }
        }
    }

    public void Send(string type, JObject data)
    {
        JObject message = new JObject();
        message["type"] = type;
        message["data"] = data;

        SendRaw(message.ToString(Newtonsoft.Json.Formatting.None));
    }

    private void SendRaw(string text)
    {
        lock (_lock)
        {
            if (_stream == null)
                return;

            try
            {
                byte[] bytes =
                    Encoding.UTF8.GetBytes(text + "\n");

                _stream.Write(bytes, 0, bytes.Length);
                _stream.Flush();
            }
            catch
            {
                try
                {
                    _client.Close();
                }
                catch { }

                _client = null;
                _stream = null;
            }
        }
    }

    private void SendState()
    {
        JObject state = new JObject();

        state["connected"] = false;
        state["archipelago"] = "";
        state["server"] = "";
        state["port"] = "";
        state["player"] = "";
        state["password"] = "";

        Send("state", state);
    }

    public void Stop()
    {
        try
        {
            if (_client != null)
                _client.Close();

            if (_listener != null)
                _listener.Stop();
        }
        catch
        {
        }
    }
}
