using System;
using System.Collections.Generic;
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
        private Thread subscribedThread = null;

        private Action<byte[]> packetActionCallback = null;
        private bool releaseResources = true;
        private bool useBackgroundParsing = false;
        private bool useMultiThreading = true;
        private bool interactive = false;
        private int interactiveTimeout = 10000;

        public PacketParser(NetworkInterface networkInterface)
        {
            this.networkInterface = networkInterface;
        }

        public bool IsDead { get; private set; } = false;

        /// <summary>
        /// Specifies the timeout in milliseconds after which GetPacketAsync automatically terminates. Default 10.
        /// </summary>
        public int PacketTimeoutMillis
        {
            get { return interactiveTimeout; }
            set
            {
                if (!isRunning)
                {
                    interactiveTimeout = value;
                }
            }
        }
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
                new Thread(() => 
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
                }).Start();
            }
            else
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
                    IsDead = true;
                }
            }
        }

        public Task<byte[]> GetPacketAsync() => Task.Run(() => GetPacket());

        public byte[] GetPacket()
        {
            if (IsDead)
            {
                throw new ConnectionDroppedException();
            }
            if (dataQueue.Count > 0)
            {
                return dataQueue.Dequeue();
            }
            subscribedThread = Thread.CurrentThread;
            try
            {
                Thread.Sleep(interactiveTimeout);
            }
            catch (ThreadInterruptedException)
            {
                return dataQueue.Dequeue();
            }
            return Array.Empty<byte>();
        }

        public async Task ShutdownAsync()
        {
            await networkInterface.SslStream.ShutdownAsync();
            Dispose();
        }

        private void Parse()
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
                    int connectionDropped = 0;
                    try
                    {
                        connectionDropped = networkInterface.SslStream.Read(data);
                    }
                    catch (IOException) 
                    { 
                        throw new ConnectionDroppedException(); 
                    }
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
                        if (subscribedThread != null && (subscribedThread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) == System.Threading.ThreadState.WaitSleepJoin)
                        {
                            subscribedThread.Interrupt();
                        }
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
                        networkInterface.SslStream.Close();
                        networkInterface.SslStream.Dispose();
                    }
                    catch (ObjectDisposedException) { }
                }

                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }
    }
}
