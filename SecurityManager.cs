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
            return new X509Certificate2(certFile)
            {
                PrivateKey = CreateRSAFromFile(keyFile)
            };
        }

        private static RSACryptoServiceProvider CreateRSAFromFile(string filename)
        {
            byte[] pvk = null;
            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                pvk = new byte[fs.Length];
                fs.Read(pvk, 0, pvk.Length);
            }

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.ImportCspBlob(pvk);

            return rsa;
        }
    }
}
