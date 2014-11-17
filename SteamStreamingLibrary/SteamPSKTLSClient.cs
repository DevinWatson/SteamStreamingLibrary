using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamStreamingLibrary
{
  internal class SteamPSKTLSClient : PskTlsClient
  {
    public SteamPSKTLSClient(TlsPskIdentity TlsPskIdentity)
      : base(TlsPskIdentity)
    {

    }

    public override TlsAuthentication GetAuthentication()
    {
      return null;
    }

    public override int[] GetCipherSuites()
    {
      return new int[] { CipherSuite.TLS_PSK_WITH_AES_128_CBC_SHA };
    }
  }
}
