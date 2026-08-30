using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;

class Program
{
    private const int Port = 38741;

    static void Main()
    {
        Console.WriteLine("================================");
        Console.WriteLine("APConnectUI");
        Console.WriteLine("================================");
        Console.WriteLine();

        TcpClient client = new TcpClient();

        try
        {
            Console.WriteLine("Connecting to MBMod...");

            client.Connect("127.0.0.1", Port);

            Console.WriteLine("Connected to MBMod.");
            Console.WriteLine();

            NetworkStream stream = client.GetStream();

            StreamReader reader =
                new StreamReader(
                    stream,
                    Encoding.UTF8
                );

            StreamWriter writer =
                new StreamWriter(
                    stream,
                    Encoding.UTF8
                );

            writer.AutoFlush = true;

            // Test connection.
            SendPing(writer);

            // Example connection information.
            //
            // CHANGE THESE VALUES.
            SendConnect(
                writer,
                "localhost",
                38281,
                "Mathbreakers",
                "Nyix",
                ""
            );

            while (true)
            {
                string line = reader.ReadLine();

                if (line == null)
                    break;

                HandleMessage(line);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Connection failed:");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("Press ENTER to exit.");
        Console.ReadLine();
    }

    private static void SendPing(StreamWriter writer)
    {
        JObject message = new JObject();

        message["type"] = "ping";
        message["data"] = new JObject();

        writer.WriteLine(
            message.ToString(
                Newtonsoft.Json.Formatting.None
            )
        );
    }

    private static void SendConnect(
        StreamWriter writer,
        string server,
        int port,
        string game,
        string player,
        string password)
    {
        JObject data = new JObject();

        data["server"] = server;
        data["port"] = port;
        data["game"] = game;
        data["player"] = player;
        data["password"] = password;

        JObject message = new JObject();

        message["type"] = "connect";
        message["data"] = data;

        Console.WriteLine(
            "Requesting Archipelago connection..."
        );

        writer.WriteLine(
            message.ToString(
                Newtonsoft.Json.Formatting.None
            )
        );
    }

    private static void HandleMessage(string line)
    {
        try
        {
            JObject message =
                JObject.Parse(line);

            string type =
                (string)message["type"];

            JObject data =
                (JObject)message["data"];

            if (type == "state")
            {
                Console.WriteLine(
                    "[STATE] connected=" +
                    (bool)data["connected"]
                );
            }
            else if (type == "connecting")
            {
                Console.WriteLine(
                    "[AP] Connecting to " +
                    (string)data["server"] +
                    ":" +
                    (int)data["port"]
                );
            }
            else if (type == "archipelago_connected")
            {
                Console.WriteLine(
                    "[AP] CONNECTED as " +
                    (string)data["player"]
                );
            }
            else if (type == "disconnected")
            {
                Console.WriteLine(
                    "[AP] Disconnected."
                );
            }
            else if (type == "item")
            {
                Console.WriteLine(
                    "[ITEM] " +
                    (string)data["name"]
                );
            }
            else if (type == "deathlink")
            {
                Console.WriteLine(
                    "[DEATHLINK] " +
                    (string)data["cause"]
                );
            }
            else if (type == "hints")
            {
                Console.WriteLine(
                    "[HINTS] Updated."
                );
            }
            else if (type == "pong")
            {
                Console.WriteLine(
                    "[MBMod] Pong."
                );
            }
            else if (type == "log")
            {
                Console.WriteLine(
                    "[AP] " +
                    (string)data["message"]
                );
            }
            else if (type == "error")
            {
                Console.WriteLine(
                    "[ERROR] " +
                    (string)data["message"]
                );
            }
            else
            {
                Console.WriteLine(
                    "[MBMod] " + line
                );
            }
        }
        catch
        {
            Console.WriteLine(
                "[MBMod] " + line
            );
        }
    }
}
