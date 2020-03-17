using System;
using System.Collections.Generic;
using System.Text;

namespace washared
{
    /// <summary>
    /// Client specific network api (preventing race conditions)
    /// </summary>
    public class Network
    {
        private readonly Client client;
        private readonly Queue<string> networkQueue = new Queue<string>();
        private bool isProcessing = false;
        private bool abort = false;
        public Encoding Encoding { get; set; }
        #region Constructor
        public Network(Client client)
        {
            this.client = client;
            Encoding = Encoding.UTF8;
        }
        public Network(Client client, Encoding encoding)
        {
            this.client = client;
            Encoding = encoding;
        }
        #endregion
        #region Public Methods
        public void Abort()
        {
            abort = true;
        }

        public void Reset()
        {
            abort = false;
        }

        public bool IsAborted
        {
            get { return abort; }
        }

        public void Send(string data)
        {
            networkQueue.Enqueue(data);
            ProcessNetworkQueue();
        }

        public int GetQueueLength()
        {
            return networkQueue.Count;
        }
        #endregion
        private void ProcessNetworkQueue()
        {
            if (isProcessing)
            {
                return;
            }
            while (networkQueue.Count > 0 && !abort)
            {
                string rawData = networkQueue.Dequeue();
                byte[] data = Encoding.GetBytes(rawData);
                byte[] buffer = new byte[data.Length + 2];
                buffer[0] = 0x01;
                Array.Copy(data, 0, buffer, 1, data.Length);
                buffer[^1] = 0x04;
                client.SslStream.Write(buffer);
                client.SslStream.Flush();
            }
            isProcessing = false;
        }
    }
}
