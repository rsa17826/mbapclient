using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

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

            while (true)
            {
                string line = reader.ReadLine();

                if (line == null)
                    break;

                Console.WriteLine("[MBMod] " + line);
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
}

