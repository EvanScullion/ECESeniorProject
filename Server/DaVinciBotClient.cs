﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Client class for the DaVinci Bot.  Loads commands from gcode file and passes them one by one to the esp32 access point.
/// </summary>
public class DaVinciBotClient
{
	static void Main(string[] args)
    {
        TcpClient client = new TcpClient();
        Console.WriteLine("Connecting...");

        client.Connect("192.168.4.1", 80);
        //client.Connect(IPAddress.Loopback, 80);

        Console.WriteLine("Connected");

        string[] commands = File.ReadAllLines(@"..\..\commands.gco");

        int numCommands = commands.Length;

        NetworkStream stream = client.GetStream();

        int count = 0;
        int index = 0;

        foreach(string line in commands)
        {
            //Console.WriteLine(line);
            byte[] command = Encoding.UTF8.GetBytes(line + "\n");

            if (count == 0)
            {
                byte[] request = new byte[5];

                while (request.Select(x => int.Parse(x.ToString())).Sum() == 0) { stream.Read(request, 0, request.Length); }

                string result = System.Text.Encoding.UTF8.GetString(request);

                //Console.WriteLine(Convert.ToUInt32(result));

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(request);

                count = Convert.ToInt32(result);
            }

            stream.Write(command, 0, command.Length);

            count -= command.Length;
            index++;

            Console.WriteLine((double)index / (double)numCommands);
        }

        string endMessage = "Transmission Complete\n";

        stream.Write(Encoding.UTF8.GetBytes(endMessage), 0, endMessage.Length);
        stream.Read(new byte[1], 0, 1);

        stream.Close();
        client.Close();

        Console.Read();
    }
}
