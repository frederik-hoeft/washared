using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;

namespace washared
{
    public class PacketParser : IDisposable
    {
        private bool _disposed = false;
        private readonly Client client;
        private readonly bool releaseResources;
        #region Constructor
        public PacketParser(Client client)
        {
            this.client = client;
            releaseResources = true;
        }
        public PacketParser(Client client, bool releaseResources)
        {
            this.client = client;
            this.releaseResources = releaseResources;
        }
        #endregion

        /// <summary>
        /// (Blocking) Begin parsing incoming packets. Create a new thread for each packet.
        /// </summary>
        /// <param name="PacketActionCallback">The function to be called for each parsed packet.</param>
        public void BeginParse(Action<byte[]> PacketActionCallback)
        {
            BeginParse(PacketActionCallback, true);
        }

        /// <summary>
        /// (Blocking) Begin parsing incoming packets.
        /// </summary>
        /// <param name="PacketActionCallback">The function to be called for each parsed packet.</param>
        /// <param name="useMultiThreading">True if a new thread should be created for each incoming packet. False otherwise</param>
        public void BeginParse(Action<byte[]> PacketActionCallback, bool useMultiThreading)
        {
            // Initialize buffer for huge packets (>32 kb)
            List<byte> buffer = new List<byte>();
            // Initialize 32 kb receive buffer for incoming data
            int bufferSize = 32768;
            byte[] data = new byte[bufferSize];
            // Run until thread is terminated
            while (true)
            {
                bool receiving = true;
                // Initialize list to store all packets found in receive buffer
                List<byte[]> dataPackets = new List<byte[]>();
                while (receiving)
                {
                    // Receive and dump to buffer until EOT flag (used to terminate packets in custom protocol --> hex value 0x04) is found
                    int connectionDropped = client.SslStream.Read(data);
                    if (connectionDropped == 0)
                    {
                        // Connection was dropped.
                        throw new ConnectionDroppedException();
                    }
                    // ----------------------------------------------------------------
                    //      HANDLE CASES OF MORE THAN ONE PACKET IN RECEIVE BUFFER
                    // ----------------------------------------------------------------
                    // Remove any null bytes from buffer
                    data = data.Where(b => b != 0x00).ToArray();
                    // Check if packet contains EOT flag and if the buffer for big packets is empty
                    if (data.Contains<byte>(0x04) && buffer.Count == 0)
                    {
                        // Split packets on EOT flag (might be more than one packet)
                        List<byte[]> rawDataPackets = data.Separate(new byte[] { 0x04 });
                        // Grab the last packet
                        byte[] lastDataPacket = rawDataPackets[rawDataPackets.Count - 1];
                        // Move all but the last packet into the 2d packet array list
                        List<byte[]> tempRawDataPackets = new List<byte[]>(rawDataPackets);
                        tempRawDataPackets.Remove(tempRawDataPackets.Last());
                        dataPackets = new List<byte[]>(tempRawDataPackets);
                        // In case the last packet contains data too --> move it in buffer for next "receiving round"
                        if (lastDataPacket.Length != 0 && lastDataPacket.Any(b => b != 0))
                        {
                            buffer.AddRange(new List<byte>(lastDataPacket));
                        }
                        // Stop receiving and break the loop
                        receiving = false;
                    }
                    // Check if packet contains EOT flag and the buffer is not empty
                    else if (data.Contains<byte>(0x04) && buffer.Count != 0)
                    {
                        // Split packets on EOT flag (might be more than one packet)
                        List<byte[]> rawDataPackets = data.Separate(new byte[] { 0x04 });
                        // Append content of buffer to the first packet
                        List<byte> firstPacket = new List<byte>();
                        firstPacket.AddRange(buffer);
                        firstPacket.AddRange(new List<byte>(rawDataPackets[0]));
                        rawDataPackets[0] = firstPacket.ToArray();
                        // Reset the buffer
                        buffer = new List<byte>();
                        // Grab the last packet
                        byte[] lastDataPacket = rawDataPackets[rawDataPackets.Count - 1];
                        // Move all but the last packet into the 2d packet array list
                        List<byte[]> tempRawDataPackets = new List<byte[]>(rawDataPackets);
                        tempRawDataPackets.Remove(tempRawDataPackets.Last());
                        dataPackets = new List<byte[]>(tempRawDataPackets);
                        // In case the last packet contains data too --> move it in buffer for next "receiving round"
                        if (lastDataPacket.Length != 0 && lastDataPacket.Any(b => b != 0))
                        {
                            buffer.AddRange(new List<byte>(lastDataPacket));
                        }
                        // Stop receiving and break the loop
                        receiving = false;
                    }
                    // The buffer does not contain any EOT flag
                    else
                    {
                        // Damn that's a huge packet. append the whole thing to the buffer and repeat until EOT flag is found
                        buffer.AddRange(new List<byte>(data));
                    }
                    // Reset the data buffer
                    data = new byte[bufferSize];
                }
                for (int i = 0; i < dataPackets.Count; i++)
                {
                    byte[] packet = dataPackets[i];
                    // Check if packets have a valid entrypoint / start of heading
                    if (packet[0] != 0x01)
                    {
                        // Check if there's a valid entry point in the packet
                        if (packet.Where(currentByte => currentByte.Equals(0x01)).Count() == 1)
                        {
                            int index = Array.IndexOf(packet, 0x01);
                            byte[] temp = new byte[packet.Length - index];
                            Array.Copy(packet, index, temp, 0, packet.Length - index);
                            packet = temp;
                        }
                        // This packet is oficially broken (containing several entry points). Hope that it wasn't too important and continue with the next one
                        else
                        {
                            continue;
                        }
                    }
                    if (packet.Length <= 1)
                    {
                        continue;
                    }
                    // Remove entry point marker byte (0x01)
                    byte[] parsedData = new byte[packet.Length - 1];
                    Array.Copy(packet, 1, parsedData, 0, packet.Length - 1);
                    if (useMultiThreading)
                    {
                        new Thread(() => PacketActionCallback(parsedData)).Start();
                    }
                    else
                    {
                        PacketActionCallback(parsedData);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (releaseResources)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        client.SslStream.Close();
                        client.SslStream.Dispose();
                    }
                    catch (ObjectDisposedException) { }
                }

                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }
    }
}
