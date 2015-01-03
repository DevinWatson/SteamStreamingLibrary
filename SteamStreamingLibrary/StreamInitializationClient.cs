using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Security;
using ProtoBuf;
using steammessages_remoteclient;
using steammessages_remoteclient_discovery;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SteamStreamingLibrary
{
  public delegate void ExceptionHandler(Exception e);
  public delegate void ConnectionResponseHandler(bool Connected);
  public delegate void StreamStartedResponseHandler(IPAddress IPAddress, UInt16 Port, Byte[] AuthToken);

  public class StreamInitializationClient
  {
    private const int BufferSize = 8192;
    private const string PSKIdentity = "steam";
    private readonly byte[] MagicBytes = new byte[] { 0x56, 0x54, 0x30, 0x31 }; //VT01

    private IPAddress IPAddress;
    private Stream TLSStream;
    private UInt64 ClientID;

    private Thread IncomingMessageProcessingThread;
    private bool IncomingMessageProcessingThreadAlive;
    private Thread OutgoingMessageProcessingThread;
    private bool OutgoingMessageProcessingThreadAlive;
    private Thread PingingThread;
    private bool PingingThreadAlive;

    private readonly Dictionary<Type, MsgRemoteClient> MsgRemoteClientDictionary = new Dictionary<Type, MsgRemoteClient>() { 
    {typeof(CMsgRemoteClientAuth), MsgRemoteClient.CMsgRemoteClientAuth} ,
    {typeof(CMsgRemoteClientAuthResponse), MsgRemoteClient.CMsgRemoteClientAuthResponse} ,
    {typeof(CMsgRemoteClientAppStatus), MsgRemoteClient.CMsgRemoteClientAppStatus} ,
    {typeof(CMsgRemoteClientPing), MsgRemoteClient.CMsgRemoteClientPing} ,
    {typeof(CMsgRemoteClientPingResponse), MsgRemoteClient.CMsgRemoteClientPingResponse} ,
    {typeof(CMsgRemoteClientStartStream), MsgRemoteClient.CMsgRemoteClientStartStream} ,
    {typeof(CMsgRemoteClientStartStreamResponse), MsgRemoteClient.CMsgRemoteClientStartStreamResponse}};

    private List<object> OutgoingMessages;

    private ExceptionHandler ExceptionHandler;
    private ConnectionResponseHandler ConnectionResponseHandler;
    private StreamStartedResponseHandler StreamStartedResponseHandler;

    private bool Initialized = false;

    public StreamInitializationClient(ExceptionHandler ExceptionHandler)
    {
      if (ExceptionHandler == null)
        throw new ArgumentNullException("ExceptionHandler");
      this.ExceptionHandler = ExceptionHandler;
      AppDomain.CurrentDomain.UnhandledException += HandleException;

      OutgoingMessages = new List<object>();


      IncomingMessageProcessingThread = new Thread(new ThreadStart(IncomingMessageProcessing));
      OutgoingMessageProcessingThread = new Thread(new ThreadStart(OutgoingMessageProcessing));
      PingingThread = new Thread(new ThreadStart(PeriodicPingingThread));

      Initialized = true;
    }

    private void AssertInitialized()
    {
      if (!Initialized)
        throw new Exception("Library not initialized");
    }

    public void Connect(IPAddress IPAddress, UInt16 Port, String PSK, UInt64 ClientID, ConnectionResponseHandler ConnectHandler)
    {
      AssertInitialized();
      if (IPAddress == null)
        throw new ArgumentNullException("IPAddress");
      if (Port == 0)
        throw new ArgumentOutOfRangeException("Port", "Must be greater than 0");
      if (PSK.Length != 64)
        throw new Exception("Invalid PSK string length");
      if (ClientID == 0)
        throw new ArgumentOutOfRangeException("ClientID", "Must be greater than 0");
      if (ConnectHandler == null)
        throw new ArgumentNullException("ConnectionHandler");
      this.IPAddress = IPAddress;
      this.ConnectionResponseHandler += ConnectHandler;
      Connect(IPAddress, Port, Utils.StringToByteArray(PSK), ClientID);
    }

    public void Connect(IPAddress IPAddress, UInt16 Port, byte[] PSK, UInt64 ClientID)
    {
      AssertInitialized();
      if (IPAddress == null)
        throw new ArgumentNullException("IPAddress");
      if (Port == 0)
        throw new ArgumentOutOfRangeException("Port", "Must be greater than 0");
      if (PSK.Length != 32)
        throw new ArgumentException("Must be 32 bytes", "PSK");
      if (ClientID == 0)
        throw new ArgumentOutOfRangeException("ClientID", "Must be greater than 0");
      this.IPAddress = IPAddress;
      try
      {
        this.ClientID = ClientID;
        TcpClient TcpClient = new TcpClient(IPAddress.ToString(), Port);
        TlsClientProtocol TlsClientProtocol = new TlsClientProtocol(TcpClient.GetStream(), new SecureRandom());
        SteamPSKTLSIdentity SteamPSKTLSIdentity = new SteamPSKTLSIdentity(Encoding.UTF8.GetBytes(PSKIdentity), PSK);
        SteamPSKTLSClient Client = new SteamPSKTLSClient(SteamPSKTLSIdentity);
        TlsClientProtocol.Connect(Client);
        TLSStream = TlsClientProtocol.Stream;
        IncomingMessageProcessingThread.Start();
        OutgoingMessageProcessingThread.Start();
      }
      catch (Exception e)
      {
        ExceptionHandler(e);
      }
    }

    public void StartStream(UInt32 AppID, StreamStartedResponseHandler StreamStartedResponseHandler)
    {
      AssertInitialized();
      if (AppID == 0)
        throw new ArgumentOutOfRangeException("AppID", "Must be greater than 0");
      if (StreamStartedResponseHandler == null)
        throw new ArgumentNullException("StreamStartedResponseHandler");
      this.StreamStartedResponseHandler = StreamStartedResponseHandler;
      CMsgRemoteClientStartStream StartStreamRequest = new CMsgRemoteClientStartStream();
      StartStreamRequest.app_id = (UInt32)AppID;
      AddOutgoingMessageToQueue(StartStreamRequest);
    }

    private void IncomingMessageProcessing()
    {
      IncomingMessageProcessingThreadAlive = true;
      byte[] bytes = new byte[BufferSize];
      while (IncomingMessageProcessingThreadAlive)
      {
        try
        {
          if (TLSStream.CanRead)
          {
            int BytesRead = TLSStream.Read(bytes, 0, BufferSize);

            if (BytesRead > 0)
            {
              int offset = 0;
              int Length = BitConverter.ToInt32(bytes, offset); offset += 4;
              String Magic = Encoding.UTF8.GetString(bytes.Skip(offset).Take(4).ToArray()); offset += 4;
              int Emesg = BitConverter.ToInt32(bytes, offset); offset += 4;
              int Empty = BitConverter.ToInt32(bytes, offset); offset += 4;

              using (var stream = new MemoryStream(bytes.Skip(offset).Take(Length - 8).ToArray()))
              {
                MsgRemoteClient MessageType = (MsgRemoteClient)(Emesg & 0x7fffffff);
#if DEBUG
                Utils.ColoredConsoleWrite(ConsoleColor.Magenta, "<<" + MessageType + Environment.NewLine);
#endif
                switch (MessageType)
                {
                  case MsgRemoteClient.CMsgRemoteClientAppStatus:

                    CMsgRemoteClientAppStatus CMsgRemoteClientAppStatus;
                    CMsgRemoteClientAppStatus = Serializer.Deserialize<CMsgRemoteClientAppStatus>(stream);
#if DEBUG

#endif
                    break;

                  case MsgRemoteClient.CMsgRemoteClientAuth:
                    CMsgRemoteClientAuth CMsgRemoteClientAuth;
                    CMsgRemoteClientAuth = Serializer.Deserialize<CMsgRemoteClientAuth>(stream);
#if DEBUG
                    Console.Write("Client ID: " + CMsgRemoteClientAuth.client_id.ToString() + Environment.NewLine +
                                  "Hostname: " + CMsgRemoteClientAuth.status.hostname + Environment.NewLine +
                                  "OS Type: " + (EOSType)CMsgRemoteClientAuth.status.ostype + Environment.NewLine +
                                  "64 Bit: " + CMsgRemoteClientAuth.status.is64bit + Environment.NewLine +
                                  "Universe: " + (EUniverse)CMsgRemoteClientAuth.status.euniverse + Environment.NewLine);
                    foreach (CMsgRemoteClientBroadcastStatus.User User in CMsgRemoteClientAuth.status.users)
                    {
                      Console.Write("User ID: " + User.steamid + Environment.NewLine +
                                    "Auth Key ID: " + User.auth_key_id + Environment.NewLine);
                    }
#endif
                    CMsgRemoteClientAuthResponse AuthResponse = new CMsgRemoteClientAuthResponse();
                    AuthResponse.eresult = 1;

                    CMsgRemoteClientAuth AuthRequest = new CMsgRemoteClientAuth();
                    AuthRequest.client_id = ClientID;
                    AuthRequest.status = new CMsgRemoteClientBroadcastStatus();
                    AuthRequest.status.hostname = Dns.GetHostName();
                    AuthRequest.status.ostype = (int)Utils.GetOSType();
                    AuthRequest.status.is64bit = Environment.Is64BitOperatingSystem;
                    AuthRequest.status.euniverse = (int)EUniverse.Public;
                    AuthRequest.status.timestamp = (UInt32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    // Just copy what the remote client sent 
                    // TODO Fix up with real values
                    AuthRequest.status.connect_port = CMsgRemoteClientAuth.status.connect_port;
                    AuthRequest.status.remote_control_port = CMsgRemoteClientAuth.status.remote_control_port;
                    AuthRequest.status.enabled_services = CMsgRemoteClientAuth.status.enabled_services;
                    AuthRequest.status.min_version = CMsgRemoteClientAuth.status.min_version;
                    AuthRequest.status.version = CMsgRemoteClientAuth.status.version;
                    foreach (CMsgRemoteClientBroadcastStatus.User User in CMsgRemoteClientAuth.status.users)
                    {
                      AuthRequest.status.users.Add(User);
                    }
                    AddOutgoingMessageToQueue(AuthResponse); // Accept incomming auth request
                    AddOutgoingMessageToQueue(AuthRequest); // Send auth request
                    break;

                  case MsgRemoteClient.CMsgRemoteClientAuthResponse:
                    CMsgRemoteClientAuthResponse CMsgRemoteClientAuthResponse;
                    CMsgRemoteClientAuthResponse = Serializer.Deserialize<CMsgRemoteClientAuthResponse>(stream);
                    if (CMsgRemoteClientAuthResponse.eresult == 1)
                    {
                      PingingThread.Start(); //We're connected so start the pinging thread
                      ConnectionResponseHandler(true);
                    }
                    else
                      ConnectionResponseHandler(false);
                    break;

                  case MsgRemoteClient.CMsgRemoteClientPing:
                    CMsgRemoteClientPing CMsgRemoteClientPing;
                    CMsgRemoteClientPing = Serializer.Deserialize<CMsgRemoteClientPing>(stream);
                    AddOutgoingMessageToQueue(new CMsgRemoteClientPingResponse());
                    break;

                  case MsgRemoteClient.CMsgRemoteClientPingResponse:
                    CMsgRemoteClientPingResponse CMsgRemoteClientPingResponse;
                    CMsgRemoteClientPingResponse = Serializer.Deserialize<CMsgRemoteClientPingResponse>(stream);
                    break;

                  case MsgRemoteClient.CMsgRemoteClientStartStream:
                    CMsgRemoteClientStartStream CMsgRemoteClientStartStream;
                    CMsgRemoteClientStartStream = Serializer.Deserialize<CMsgRemoteClientStartStream>(stream);
                    break;

                  case MsgRemoteClient.CMsgRemoteClientStartStreamResponse:
                    CMsgRemoteClientStartStreamResponse CMsgRemoteClientStartStreamResponse;
                    CMsgRemoteClientStartStreamResponse = Serializer.Deserialize<CMsgRemoteClientStartStreamResponse>(stream);
#if DEBUG
                    Console.Write("Launch Result: " + CMsgRemoteClientStartStreamResponse.e_launch_result.ToString() + Environment.NewLine +
                                  "Auth Token: " + Utils.ByteArrayToString(CMsgRemoteClientStartStreamResponse.auth_token) + Environment.NewLine +
                                  "Stream Port: " + CMsgRemoteClientStartStreamResponse.stream_port + Environment.NewLine);
#endif
                    if (CMsgRemoteClientStartStreamResponse.e_launch_result == 1) //Success, hand off stream control information
                    {
                      StreamStartedResponseHandler(IPAddress, (UInt16)CMsgRemoteClientStartStreamResponse.stream_port, CMsgRemoteClientStartStreamResponse.auth_token);
                    }
                    else
                      throw new Exception("Failed to start stream EResult: " + CMsgRemoteClientStartStreamResponse.e_launch_result);
                    break;
                  default:
                    ExceptionHandler(new Exception("Unknown message type: " + (Emesg & 0x7fffffff)));
                    break;
                }
              }
            }
          }
        }
        catch (Exception e)
        {
          ExceptionHandler(e);
        }
      }
    }

    private void AddOutgoingMessageToQueue(object Message)
    {
      lock (OutgoingMessages)
      {
        OutgoingMessages.Add(Message);
      }
    }

    private void OutgoingMessageProcessing()
    {
      OutgoingMessageProcessingThreadAlive = true;
      while (OutgoingMessageProcessingThreadAlive)
      {
        try
        {
          if (OutgoingMessages.Count > 0)
          {
            lock (OutgoingMessages)
            {
              var Message = OutgoingMessages.First();
              OutgoingMessages.Remove(OutgoingMessages.First());
              using (var stream = new MemoryStream())
              {
                if (Message is CMsgRemoteClientAuth)
                  ProcessOutgoingMessage<CMsgRemoteClientAuth>(Message);
                else if (Message is CMsgRemoteClientAuthResponse)
                  ProcessOutgoingMessage<CMsgRemoteClientAuthResponse>(Message);
                else if (Message is CMsgRemoteClientPing)
                  ProcessOutgoingMessage<CMsgRemoteClientPing>(Message);
                else if (Message is CMsgRemoteClientPingResponse)
                  ProcessOutgoingMessage<CMsgRemoteClientPingResponse>(Message);
                else if (Message is CMsgRemoteClientStartStream)
                  ProcessOutgoingMessage<CMsgRemoteClientStartStream>(Message);
                else if (Message is CMsgRemoteClientStartStreamResponse)
                  ProcessOutgoingMessage<CMsgRemoteClientStartStream>(Message);
                else if (Message is CMsgRemoteClientAppStatus)
                  ProcessOutgoingMessage<CMsgRemoteClientAppStatus>(Message);
              }
            }
          }
        }
        catch (Exception e)
        {
          ExceptionHandler(e);
        }
      }
    }

    private void ProcessOutgoingMessage<T>(object Message)
    {
#if DEBUG
      Utils.ColoredConsoleWrite(ConsoleColor.Green, ">>" + MsgRemoteClientDictionary[typeof(T)] + Environment.NewLine);
#endif
      List<byte> OutgoingMessageData = new List<byte>();
      using (var stream = new MemoryStream())
      {
        Serializer.Serialize<T>(stream, (T)Message);
        byte[] MessageData = stream.GetBuffer().Take((Int32)stream.Length).ToArray();
        OutgoingMessageData.AddRange(BitConverter.GetBytes((Int32)(MessageData.Length + 8))); // Length
        OutgoingMessageData.AddRange(MagicBytes); // Magic Bytes
        OutgoingMessageData.AddRange(BitConverter.GetBytes((Int32)((UInt16)MsgRemoteClientDictionary[typeof(T)] | 0x80000000))); // EMsg Value
        OutgoingMessageData.AddRange(BitConverter.GetBytes((Int32)0)); // Legacy Header
        OutgoingMessageData.AddRange(MessageData);
        TLSStream.Write(OutgoingMessageData.ToArray(), 0, OutgoingMessageData.Count);
        TLSStream.Flush();
      }
    }

    // Steam client pings every 30 seconds, so we'll do the same
    private void PeriodicPingingThread()
    {
      PingingThreadAlive = true;
      while (PingingThreadAlive)
      {
        Thread.Sleep(new TimeSpan(0, 0, 30));
        AddOutgoingMessageToQueue(new CMsgRemoteClientPing());
      }
    }

    private void HandleException(object Sender, UnhandledExceptionEventArgs e)
    {
      ExceptionHandler((Exception)e.ExceptionObject);
    }
  }
}
