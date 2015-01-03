using ProtoBuf;
using stream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    private Thread OutgoingMessageProcessingThread;
    private bool OutgoingMessageProcessingThreadAlive;

    private List<object> OutgoingMessages;

    private readonly byte SenderID = 0x80;
    private byte RecieverID = 0x00;
    private Int16[] PacketSquenceIDs = new Int16[10];

    private readonly Dictionary<Type, EStreamControlMessage> EStreamControlMessageDictionary = new Dictionary<Type, EStreamControlMessage>() {
    {typeof(CAuthenticationRequestMsg), EStreamControlMessage.k_EStreamControlAuthenticationRequest},
    {typeof(CAuthenticationResponseMsg), EStreamControlMessage.k_EStreamControlAuthenticationResponse},
    {typeof(CDeleteCursorMsg), EStreamControlMessage.k_EStreamControlDeleteCursor},
    {typeof(CGamepadRumbleMsg), EStreamControlMessage.k_EStreamControlGamepadRumble},
    {typeof(CGetCursorImageMsg), EStreamControlMessage.k_EStreamControlGetCursorImage},
    {typeof(CHideCursorMsg), EStreamControlMessage.k_EStreamControlHideCursor},
    {typeof(CInputControllerDetachedMsg), EStreamControlMessage.k_EStreamControlInputControllerDetached},
    {typeof(CInputControllerAttachedMsg), EStreamControlMessage.k_EStreamControlInputControllerAttached},
    {typeof(CInputControllerStateMsg), EStreamControlMessage.k_EStreamControlInputControllerState},
    {typeof(CInputGamepadAttachedMsg), EStreamControlMessage.k_EStreamControlInputGamepadAttached},
    {typeof(CInputGamepadDetachedMsg), EStreamControlMessage.k_EStreamControlInputGamepadDetached},
    {typeof(CInputGamepadEventMsg), EStreamControlMessage.k_EStreamControlInputGamepadEvent},
    {typeof(CInputKeyDownMsg), EStreamControlMessage.k_EStreamControlInputKeyDown},
    {typeof(CInputKeyUpMsg), EStreamControlMessage.k_EStreamControlInputKeyUp},
    {typeof(CInputLatencyTestMsg), EStreamControlMessage.k_EStreamControlInputLatencyTest},
    {typeof(CInputMouseDownMsg), EStreamControlMessage.k_EStreamControlInputMouseDown},
    {typeof(CInputMouseMotionMsg), EStreamControlMessage.k_EStreamControlInputMouseMotion},
    {typeof(CInputMouseUpMsg), EStreamControlMessage.k_EStreamControlInputMouseUp},
    {typeof(CInputMouseWheelMsg), EStreamControlMessage.k_EStreamControlInputMouseWheel},
    {typeof(CNegotiationCompleteMsg), EStreamControlMessage.k_EStreamControlNegotiationComplete},
    {typeof(CNegotiationInitMsg), EStreamControlMessage.k_EStreamControlNegotiationInit},
    {typeof(CNegotiationSetConfigMsg), EStreamControlMessage.k_EStreamControlNegotiationSetConfig},
    {typeof(COverlayEnabledMsg), EStreamControlMessage.k_EStreamControlOverlayEnabled},
    {typeof(CQuitRequest), EStreamControlMessage.k_EStreamControlQuitRequest},
    {typeof(CSetCursorMsg), EStreamControlMessage.k_EStreamControlSetCursor},
    {typeof(CSetCursorImageMsg), EStreamControlMessage.k_EStreamControlSetCursorImage},
    {typeof(CSetIconMsg), EStreamControlMessage.k_EStreamControlSetIcon},
    {typeof(CSetMaximumBitrateMsg), EStreamControlMessage.k_EStreamControlSetMaximumBitrate},
    {typeof(CSetMaximumFramerateMsg), EStreamControlMessage.k_EStreamControlSetMaximumFramerate},
    {typeof(CSetMaximumResolutionMsg), EStreamControlMessage.k_EStreamControlSetMaximumResolution},
    {typeof(CSetOverrideModeMsg), EStreamControlMessage.k_EStreamControlSetOverrideMode},
    {typeof(CSetQoSMsg), EStreamControlMessage.k_EStreamControlSetQoS},
    {typeof(CSetQualityPreferenceMsg), EStreamControlMessage.k_EStreamControlSetQualityPreference},
    {typeof(CSetTargetFramerateMsg), EStreamControlMessage.k_EStreamControlSetTargetFramerate},
    {typeof(CSetTitleMsg), EStreamControlMessage.k_EStreamControlSetTitle},
    {typeof(CShowCursorMsg), EStreamControlMessage.k_EStreamControlShowCursor},
    {typeof(CStartAudioDataMsg), EStreamControlMessage.k_EStreamControlStartAudioData},
    {typeof(CStartVideoDataMsg), EStreamControlMessage.k_EStreamControlStartVideoData},
    {typeof(CStopAudioDataMsg), EStreamControlMessage.k_EStreamControlStopAudioData},
    {typeof(CStopVideoDataMsg), EStreamControlMessage.k_EStreamControlStopVideoData},
    {typeof(CSystemInfoMsg), EStreamControlMessage.k_EStreamControlSystemInfo},
    {typeof(CTriggerHapticPulseMsg), EStreamControlMessage.k_EStreamControlTriggerHapticPulse},
    {typeof(CVideoDecoderInfoMsg), EStreamControlMessage.k_EStreamControlVideoDecoderInfo},
    };

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

    private void WriteAuthMessage()
    {
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
            CAuthenticationRequestMsg CAuthenticationRequestMsg = new CAuthenticationRequestMsg();
            CAuthenticationRequestMsg.version = EStreamVersion.k_EStreamVersionCurrent;
            CAuthenticationRequestMsg.token = AuthToken;
            ProcessOutgoingMessage<CAuthenticationRequestMsg>(StreamingPacketType.Control, CAuthenticationRequestMsg);
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
      catch (Exception e)
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
          if (receiveBytes.Length > 0)
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
      }
    }

    private void ProcessOutgoingMessage<T>(StreamingPacketType PacketType, object Message)
    {
      List<byte> OutgoingData = new List<byte>();
      using (var stream = new MemoryStream())
      {
        Serializer.Serialize<T>(stream, (T)Message);
        byte[] MessageData = stream.GetBuffer().Take((Int32)stream.Length).ToArray();
        switch (PacketType)
        {
          case StreamingPacketType.Control:
            OutgoingData.Add((byte)EStreamControlMessageDictionary[typeof(T)]);
            OutgoingData.AddRange(MessageData);
            WritePacket(StreamingPacketType.Control, EStreamChannel.k_EStreamChannelControl, OutgoingData.ToArray());
            break;

          case StreamingPacketType.ControlAcknoledge:
            break;

          default:
            throw new Exception("Unsupported message type");
        }
      }
    }

    private void HandleException(object Sender, UnhandledExceptionEventArgs e)
    {
      ExceptionHandler((Exception)e.ExceptionObject);
    }
  }
}