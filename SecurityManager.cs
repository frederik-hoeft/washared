using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace washared
{
    /// <summary>
    /// Hosting all security related methods
    /// </summary>
    public static class SecurityManager
    {
        public static X509Certificate2 CreateFromCertFile(string certFile, string keyFile)
        {
            var cert = new X509Certificate2(certFile)
            {
                PrivateKey = CreateRSAFromFile(keyFile)
            };

            return cert;
        }

        private static RSACryptoServiceProvider CreateRSAFromFile(string filename)
        {
            byte[] pvk = null;
            using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                pvk = new byte[fs.Length];
                fs.Read(pvk, 0, pvk.Length);
            }

            var rsa = new RSACryptoServiceProvider();
            rsa.ImportCspBlob(pvk);

            return rsa;
        }
    }
}
