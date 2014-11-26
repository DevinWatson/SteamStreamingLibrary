using stream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SteamStreamingLibrary
{
  public class StreamControlClient
  {
    private const int BufferSize = 8192;

    private IPAddress IPAddress;
    private UInt16 Port;
    private Byte[] AuthToken;
    private UdpClient UdpClient;
    private Stream TcpStream;

    private ExceptionHandler ExceptionHandler;

    private Thread IncomingMessageProcessingThread;
    private bool IncomingMessageProcessingThreadAlive;

    private readonly byte SenderID = 0x80;
    private byte RecieverID = 0x00;
    private Int16[] PacketSquenceIDs = new Int16[10];

    public StreamControlClient(ExceptionHandler ExceptionHandler)
    {
      this.ExceptionHandler = ExceptionHandler;
      AppDomain.CurrentDomain.UnhandledException += HandleException;
      IncomingMessageProcessingThread = new Thread(new ThreadStart(IncomingMessageProcessing));
    }

    public void Connect(IPAddress IPAddress, UInt16 Port, Byte[] AuthToken)
    {
      try
      {
        this.IPAddress = IPAddress;
        this.Port = Port;
        this.AuthToken = AuthToken;
        UdpClient = new UdpClient();
        UdpClient.Connect(IPAddress.ToString(), Port);
        WriteConnectMessage();
        IncomingMessageProcessingThread.Start();
      }
      catch (Exception e)
      {
        ExceptionHandler(e);
      }
    }

    private void WriteConnectMessage()
    {
      WritePacket(StreamingPacketType.Connect, EStreamChannel.k_EStreamChannelDiscovery, new byte[0]);
    }

    private void WritePacket(StreamingPacketType StreamingPacketType, EStreamChannel EStreamChannel, byte[] Data)
    {
      List<byte> Output = new List<byte>();
      Output.Add((byte)StreamingPacketType);
      Output.Add(0); //Repeat count
      Output.Add(SenderID);
      Output.Add(RecieverID);
      Output.Add((byte)EStreamChannel);
      Output.AddRange(BitConverter.GetBytes((Int16)0)); //Unknown
      Output.AddRange(BitConverter.GetBytes(PacketSquenceIDs[(int)StreamingPacketType]));
      Output.AddRange(BitConverter.GetBytes(Environment.TickCount));
      UdpClient.Send(Output.ToArray(), Output.Count);
    }

    private void ReadPacket(byte[] MessageData)
    {
      try
      {
        StreamingPacketType PacketType = (StreamingPacketType)MessageData[0];
        switch (PacketType)
        {
          case StreamingPacketType.Connect:
            RecieverID = (byte)(MessageData[2] & 0xff);
            break;
          case StreamingPacketType.ConnectResponse:
            break;
          case StreamingPacketType.Control:
            break;
          case StreamingPacketType.ControlAcknoledge:
            break;
          case StreamingPacketType.ControlContinued:
            break;
          case StreamingPacketType.Data:
            break;
          case StreamingPacketType.Disconnect:
            break;
        }
      }
      catch(Exception e)
      {
        ExceptionHandler(e);
      }
    }

    private void IncomingMessageProcessing()
    {
      IncomingMessageProcessingThreadAlive = true;
      IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress, Port);
      while (IncomingMessageProcessingThreadAlive)
      {
        try
        {
          byte[] receiveBytes = UdpClient.Receive(ref RemoteIpEndPoint);
          if(receiveBytes.Length > 0)
          {
            ReadPacket(receiveBytes);
          }
        }
        catch (Exception e)
        {
          Console.WriteLine(e.ToString());
        }
      }
    }

    private void ProcessOutgoingMessage<T>(object Message)
    {
    }

    private void HandleException(object Sender, UnhandledExceptionEventArgs e)
    {
      ExceptionHandler((Exception)e.ExceptionObject);
    }
  }
}
