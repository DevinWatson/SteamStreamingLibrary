using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamStreamingLibrary
{
  static class Utils
  {
    public static EOSType GetOSType()
    {
      var osVer = Environment.OSVersion;
      var ver = osVer.Version;

      switch (osVer.Platform)
      {
        case PlatformID.Win32Windows:
          {
            switch (ver.Minor)
            {
              case 0:
                return EOSType.Win95;

              case 10:
                return EOSType.Win98;

              case 90:
                return EOSType.WinME;

              default:
                return EOSType.WinUnknown;
            }
          }

        case PlatformID.Win32NT:
          {
            switch (ver.Major)
            {
              case 4:
                return EOSType.WinNT;

              case 5:
                switch (ver.Minor)
                {
                  case 0:
                    return EOSType.Win200;

                  case 1:
                    return EOSType.WinXP;

                  case 2:
                    // Assume nobody runs Windows XP Professional x64 Edition
                    // It's an edition of Windows Server 2003 anyway.
                    return EOSType.Win2003;
                }

                goto default;

              case 6:
                switch (ver.Minor)
                {
                  case 0:
                    return EOSType.WinVista; // Also Server 2008

                  case 1:
                    return EOSType.Windows7; // Also Server 2008 R2

                  case 2:
                    return EOSType.Windows8; // Also Server 2012

                  // Note: The OSVersion property reports the same version number (6.2.0.0) for both Windows 8 and Windows 8.1.- http://msdn.microsoft.com/en-us/library/system.environment.osversion(v=vs.110).aspx
                  // In practice, this will only get hit if the application targets Windows 8.1 in the app manifest.
                  // See http://msdn.microsoft.com/en-us/library/windows/desktop/dn481241(v=vs.85).aspx for more info.
                  case 3:
                    return EOSType.Windows81; // Also Server 2012 R2
                }

                goto default;

              default:
                return EOSType.WinUnknown;
            }
          }

        case PlatformID.Unix:
          return EOSType.LinuxUnknown; // this _could_ be mac, but we're gonna just go with linux for now

        default:
          return EOSType.Unknown;
      }
    }

    public static void ColoredConsoleWrite(ConsoleColor color, string text)
    {
      ConsoleColor originalColor = Console.ForegroundColor;
      Console.ForegroundColor = color;
      Console.Write(text);
      Console.ForegroundColor = originalColor;
    }

    public static byte[] StringToByteArray(string hex)
    {
      return Enumerable.Range(0, hex.Length)
                       .Where(x => x % 2 == 0)
                       .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                       .ToArray();
    }

  }
}
