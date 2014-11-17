using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamStreamingLibrary
{
  public enum MsgRemoteClient
  {
    CMsgRemoteClientAuth = 9500,
    CMsgRemoteClientAuthResponse = 9501,
    CMsgRemoteClientAppStatus = 9502,
    CMsgRemoteClientStartStream = 9503,
    CMsgRemoteClientStartStreamResponse = 9504,
    CMsgRemoteClientPing = 9505,
    CMsgRemoteClientPingResponse = 9506
  }

  enum EOSType
  {
    Unknown = -1,
    UMQ = -400,
    PS3 = -300,

    MacOSUnknown = -102,
    MacOS104 = -101,
    MacOS105 = -100,
    MacOS1058 = -99,
    MacOS106 = -95,
    MacOS1063 = -94,
    MacOS1064_slgu = -93,
    MacOS1067 = -92,
    MacOS107 = -90,
    MacOS108 = -89,
    MacOS109 = -88,

    LinuxUnknown = -203,
    Linux22 = -202,
    Linux24 = -201,
    Linux26 = -200,
    Linux32 = -199,
    Linux35 = -198,
    Linux36 = -197,
    Linux310 = -196,

    WinUnknown = 0,
    Win311 = 1,
    Win95 = 2,
    Win98 = 3,
    WinME = 4,
    WinNT = 5,
    Win200 = 6,
    WinXP = 7,
    Win2003 = 8,
    WinVista = 9,
    Win7 = 10,
    Windows7 = 10,
    Win2008 = 11,
    Win2012 = 12,
    Windows8 = 13,
    Windows81 = 14,

    WinMAX = 15,

    Max = 26
  }

  enum EUniverse
  {
    Invalid = 0,

    Public = 1,
    Beta = 2,
    Internal = 3,
    Dev = 4,

    Max = 5,
  };


}
