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
    static StreamInitializationClient StreamingInitializationClient;
    static StreamControlClient StreamControlClient;
    static UInt32 AppID;
    static UInt64 ClientID;

    static void Main(string[] args)
    {
      StreamingInitializationClient = new StreamInitializationClient(ExceptionHandler);
      IPAddress IPAddress;
      if (!IPAddress.TryParse(args[0], out IPAddress))
      {
        Console.WriteLine("Unable to parse IP address: " + args[0]);
        Console.ReadLine();
        return;
      }
      UInt16 Port;
      if(!UInt16.TryParse(args[1], out Port))
      {
        Console.WriteLine("Unable to parse port: " + args[1]);
        Console.ReadLine();
        return;
      }
      if (!UInt32.TryParse(args[3], out AppID))
      {
        Console.WriteLine("Unable to parse app id: " + args[3]);
        Console.ReadLine();
        return;
      }
      Random rnd = new Random();
      byte[] bytes = new byte[8];
      rnd.NextBytes(bytes);
      ClientID = BitConverter.ToUInt64(bytes, 0);
      StreamingInitializationClient.Connect(IPAddress, Port, args[2], ClientID, ConnectionResponseHandler);
    }

    private static void ConnectionResponseHandler(bool Connected)
    {
      Console.WriteLine("Connected: " + Connected);
      if (Connected)
        StreamingInitializationClient.StartStream(AppID, StreamStarted);
    }

    private static void StreamStarted(IPAddress IPAddress, UInt16 Port, Byte[] AuthToken)
    {
      Console.WriteLine(string.Format("Stream Started: {0}:{1}", IPAddress.ToString(), Port));
      StreamControlClient = new StreamControlClient(ExceptionHandler);
      StreamControlClient.Connect(IPAddress, Port, AuthToken);
    }

    private static void ExceptionHandler(Exception e)
    {
      Console.WriteLine(e.Message + Environment.NewLine + e.StackTrace);
      Console.ReadLine();
    }
  }
}
