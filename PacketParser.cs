using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace washared
{
    public class PacketParser : IDisposable
    {
        private bool _disposed = false;
        private readonly NetworkInterface networkInterface;
        private bool isRunning = false;
        private readonly Queue<byte[]> dataQueue = new Queue<byte[]>();

        private Action<byte[]> packetActionCallback = null;
        private bool releaseResources = true;
        private bool useBackgroundParsing = false;
        private bool useMultiThreading = true;
        private bool interactive = false;
        private bool isAborted = false;
        private readonly ManualResetEventSlim suspendEvent = new ManualResetEventSlim(true);

        public PacketParser(NetworkInterface networkInterface)
        {
            this.networkInterface = networkInterface;
        }

        public bool IsDead { get; private set; } = false;

        /// <summary>
        /// Executes the specified function for each received packet. If null option will be ignored. Cannot be changed once BeginParse() is called.
        /// </summary>
        public Action<byte[]> PacketActionCallback
        {
            get { return packetActionCallback; }
            set
            {
                if (!isRunning)
                {
                    packetActionCallback = value;
                }
            }
        }
        /// <summary>
        /// Specifies whether a new thread will be started to handle each packet. Option will be ignored if PacketActionCallback is null. Cannot be changed once BeginParse() is called.
        /// </summary>
        public bool UseMultiThreading
        {
            get { return useMultiThreading; }
            set
            {
                if (!isRunning)
                {
                    useMultiThreading = value;
                }
            }
        }
        /// <summary>
        /// Specifies whether packets will be handled in the background or if the current thread should be used for packet handling. Cannot be changed once BeginParse() is called.
        /// </summary>
        public bool UseBackgroundParsing
        {
            get { return useBackgroundParsing; }
            set
            {
                if (!isRunning)
                {
                    useBackgroundParsing = value;
                }
            }
        }
        /// <summary>
        /// Specifies whether the client's SslStream should be released once this object is being disposed. Cannot be changed once BeginParse() is called.
        /// </summary>
        public bool ReleaseResources
        {
            get { return releaseResources; }
            set
            {
                if (!isRunning)
                {
                    releaseResources = value;
                }
            }
        }

        private void Abort()
        {
            isAborted = true;
        }

        /// <summary>
        /// Specifies whether packets should be added to the DataQueue to be handled manually. Cannot be changed once BeginParse() is called.
        /// </summary>
        public bool Interactive
        {
            get { return interactive; }
            set
            {
                if (!isRunning)
                {
                    interactive = value;
                }
            }
        }

        public void BeginParse()
        {
            if (isRunning)
            {
                return;
            }
            isRunning = true;
            if (useBackgroundParsing)
            {
                new Thread(TryParse).Start();
            }
            else
            {
                TryParse();
            }
        }

        private void TryParse()
        {
            try
            {
                Parse();
            }
            catch (Exception ex)
            {
                Dispose();
                if (!(ex is ConnectionDroppedException))
                {
                    throw;
                }
                IsDead = false;
            }
        }

        public async Task<byte[]> GetPacket() => await GetPacket(Timeout.Infinite);

        public async Task<byte[]> GetPacket(int millisTimeout)
        {
            suspendEvent.Reset();
            if (IsDead)
            {
                throw new ConnectionDroppedException();
            }
            if (dataQueue.Count > 0)
            {
                return dataQueue.Dequeue();
            }
            await Task.Run(() => suspendEvent.Wait(millisTimeout));
            suspendEvent.Reset();
            if (dataQueue.Count > 0)
            {
                return dataQueue.Dequeue();
            }
            return Array.Empty<byte>();
        }

        private void Parse()
        {
            // Initialize buffer for huge packets (>32 KB)
            List<byte> buffer = new List<byte>();
            // Initialize 32 KB receive buffer for incoming data
            const int bufferSize = 32768;
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
                    int connectionDropped = 0;
                    try
                    {
                        connectionDropped = networkInterface.SslStream.Read(data);
                    }
                    catch (IOException)
                    {
                        if (isAborted)
                        {
                            return;
                        }
                        throw new ConnectionDroppedException();
                    }
                    if (connectionDropped == 0)
                    {
                        if (isAborted)
                        {
                            return;
                        }
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
                        byte[] lastDataPacket = rawDataPackets[^1];
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
                        byte[] lastDataPacket = rawDataPackets[^1];
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
                    // Check if packets have a valid entry point / start of heading
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
                        // This packet is officially broken (containing several entry points). Hope that it wasn't too important and continue with the next one
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
                    // Handle packets
                    if (packetActionCallback != null)
                    {
                        if (useMultiThreading)
                        {
                            new Thread(() => packetActionCallback(parsedData)).Start();
                        }
                        else
                        {
                            packetActionCallback(parsedData);
                        }
                    }
                    if (interactive)
                    {
                        dataQueue.Enqueue(parsedData);
                        suspendEvent.Set();
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
                    Abort();
                }

                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }
    }
}
