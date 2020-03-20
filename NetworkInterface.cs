using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Text;

namespace washared
{
    public abstract class NetworkInterface
    {
        public virtual Network Network { get; set; }
        public virtual SslStream SslStream { get; set; }
    }
}
