using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace washared
{
    public static class Extensions
    {
        public static List<byte[]> Separate(this byte[] source, byte[] separator)
        {
            var Parts = new List<byte[]>();
            var Index = 0;
            byte[] Part;
            for (var i = 0; i < source.Length; ++i)
            {
                for (int j = 0; j < separator.Length; j++)
                {
                    if (source[i].Equals(separator[j]))
                    {
                        Part = new byte[i - Index];
                        Array.Copy(source, Index, Part, 0, Part.Length);
                        Parts.Add(Part);
                        Index = i + separator.Length;
                        i += separator.Length - 1;
                    }
                }
            }
            Part = new byte[source.Length - Index];
            Array.Copy(source, Index, Part, 0, Part.Length);
            Parts.Add(Part);
            return Parts;
        }

        public static IPAddress GetLocalIPAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && (ip.ToString().StartsWith("192.168") || ip.ToString().StartsWith("10.") || ip.ToString().Equals("127.0.0.1")))
                {
                    return ip;
                }
            }
            throw new Exception("No IPv4 network adapters found!");
        }
    }
}
