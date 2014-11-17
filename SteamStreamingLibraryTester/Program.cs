using SteamStreamingLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SteamStreamingLibraryTester
{
  class Program
  {
    static StreamingClient StreamingClient;
    static UInt32 AppID;

    static void Main(string[] args)
    {
      StreamingClient = new StreamingClient();
      StreamingClient.Initialize(ExceptionHandler);
      IPAddress IPAddress;
      if (!IPAddress.TryParse(args[0], out IPAddress))
      {
        Console.WriteLine("Unable to parse IP address");
        Console.ReadLine();
        return;
      }
      UInt16 Port;
      if(!UInt16.TryParse(args[1], out Port))
      {
        Console.WriteLine("Unable to parse port");
        Console.ReadLine();
        return;
      }
      if (!UInt32.TryParse(args[3], out AppID))
      {
        Console.WriteLine("Unable to parse app id");
        Console.ReadLine();
        return;
      }
      Random rnd = new Random();
      byte[] bytes = new byte[8];
      rnd.NextBytes(bytes);
      UInt64 ClientID = BitConverter.ToUInt64(bytes, 0);
      StreamingClient.Connect(IPAddress, Port, args[2], ClientID, ConnectionHandler);
    }

    private static void ConnectionHandler(bool Connected)
    {
      if (Connected)
        StreamingClient.StartStream(233150);
    }

    private static void ExceptionHandler(Exception e)
    {
      Console.WriteLine(e.Message + Environment.NewLine + e.StackTrace);
    }
  }
}
