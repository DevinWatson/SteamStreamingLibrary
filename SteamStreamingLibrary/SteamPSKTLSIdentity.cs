using Org.BouncyCastle.Crypto.Tls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamStreamingLibrary
{
  internal class SteamPSKTLSIdentity : TlsPskIdentity
  {
    public byte[] Identity, PSK;

    public SteamPSKTLSIdentity(byte[] Identity, byte[] PSK)
    {
      this.Identity = Identity;
      this.PSK = PSK;
    }

    public byte[] GetPsk()
    {
      return PSK;
    }

    public byte[] GetPskIdentity()
    {
      return Identity;
    }

    public void NotifyIdentityHint(byte[] psk_identity_hint)
    {
#if DEBUG
      Console.WriteLine("Notify Identity Hint");
#endif
    }

    public void SkipIdentityHint()
    {
#if DEBUG
      Console.WriteLine("Skip Identity Hint");
#endif
    }
  }
}
