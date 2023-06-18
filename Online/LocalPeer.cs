using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace RainMeadow {
	static class LocalPeer {
		static UdpClient debugClient;
		public enum PacketType : byte {
			Reliable,
			Unreliable,
			UnreliableOrdered,
			Acknowledge,
			Termination,
		}

		// Normally we would keep track of who is ment to receive the reliable packets
		// Since we are just dealing with one other client for debugging purposes, we can omit
		public class SequencedPacket {
			public ulong index;
			public byte[] packet; // Raw packet data
			public Action OnAcknowledged;
			public Action OnFailed;
			public int attemptsLeft;
			
			public SequencedPacket(ulong index, byte[] data, int attempts = -1, bool termination = false) {
				this.index = index;
				this.attemptsLeft = attempts;

				packet = new byte[9 + data.Length];
				MemoryStream stream = new MemoryStream(packet);
				BinaryWriter writer = new BinaryWriter(stream);

				if (termination) {
					WriteTerminationHeader(writer, index);
				} else {
					WriteReliableHeader(writer, index);
				}
				writer.Write(data);
			}
		}

		// Everything here will refer to the other local client
		// Only one other will be only running to test communication
		// Otherwise this will all be grouped with the communicating endpoint
		// Dictionary<IPEndpoint, ...>
	
		public static ulong packetIndex { get; private set; } // Increments for each reliable packet sent
		static ulong unreliablePacketIndex; // Increments for each unreliable ordered packet sent
		static Queue<SequencedPacket> outgoingPackets = new Queue<SequencedPacket>(); // Keep track of packets we want to send while we wait for responses
		public static SequencedPacket latestOutgoingPacket { get; private set; }
		const int TIMEOUT_TICKS = 40 * 30; // about 30 seconds
		const int HEARTBEAT_TICKS = 40 * 5; // about 5 seconds
		static int ticksSinceLastPacket;
		const int RESEND_TICKS = 20; // about 0.5 seconds
		static int ticksToResend = RESEND_TICKS;
		static ulong lastAckedPacketIndex;
		static ulong lastUnreliablePacketIndex;
		static bool waitingForTermination;

		//

		static readonly IPEndPoint localHostEndpoint = new IPEndPoint(IPAddress.Loopback, 5001);
		static readonly IPEndPoint localClientEndpoint = new IPEndPoint(IPAddress.Loopback, 5002);
		static bool isHost;

		public static Action<IPEndPoint> PeerTerminated;

		public static void Startup(bool host) {
			if (debugClient != null)
				return;

			// Create debugging client for local connection
			debugClient = new UdpClient();
			
			// With this set, it will be truely connectionless
			debugClient.Client.IOControl(
				(IOControlCode)(-1744830452), // SIO_UDP_CONNRESET
				new byte[] { 0, 0, 0, 0 }, 
				null
			);

			debugClient.Client.Bind(host ? localHostEndpoint : localClientEndpoint);
			isHost = host;
		}

		// This process it pretty jank rn - it works for the member but not the owner
		// Don't use for actual gameplay, DEBUG USE ONLY
		public static void Shutdown() {
			RainMeadow.DebugMe();
			if (!isHost)
				SendTermination();
			else 
				CleanUp();
		}

		static void CleanUp() {
			if (!debugClient.Client.Connected || outgoingPackets.Count > 0)
				return;
			
			RainMeadow.DebugMe();
			debugClient.Client.Shutdown(SocketShutdown.Both);
			debugClient = null;
			isHost = false;
			waitingForTermination = false;
		}

		public static void Update() {
			if (debugClient == null)
				return;

			ticksSinceLastPacket++;
			if (ticksSinceLastPacket > HEARTBEAT_TICKS) {
				// Send for heartbeat
				//Send(new byte[0], 0, PacketType.Reliable);
			}

			if (ticksSinceLastPacket > TIMEOUT_TICKS) {
				// Peer timed out and assume disconnected
				outgoingPackets.Clear();
			}

			// Try to resend packets that have not been acknowledge on the other end
			if (outgoingPackets.Count > 0) {
				ticksToResend--;
				if (ticksToResend <= 0) {
					IPEndPoint remoteEndpoint = !isHost ? localHostEndpoint : localClientEndpoint;
					SequencedPacket outgoingPacket = outgoingPackets.Peek();
					byte[] packetData = outgoingPacket.packet;
					
					if (outgoingPacket.attemptsLeft == 0)
						outgoingPackets.Dequeue().OnFailed?.Invoke();

					debugClient.Send(packetData, packetData.Length, remoteEndpoint);
					outgoingPacket.attemptsLeft--;

					ticksToResend = RESEND_TICKS;
				}
			}
		}

		public static bool Read(out BinaryReader netReader, out IPEndPoint remoteEndpoint) {
			remoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
			MemoryStream netStream = new MemoryStream(debugClient.Receive(ref remoteEndpoint));
			netReader = new BinaryReader(netStream);

			ulong receivedPacketIndex;
			switch ((PacketType)netReader.ReadByte()) {
				case PacketType.Reliable:
					receivedPacketIndex = ReadReliableHeader(netReader);
					SendAcknowledge(receivedPacketIndex); // Return Message

					// Process data if it is new
					if (receivedPacketIndex > lastAckedPacketIndex) {
						lastAckedPacketIndex = receivedPacketIndex;
						return true;
					}
					break;

				case PacketType.Acknowledge:
					if (outgoingPackets.Count == 0)
						// Nothing left to acknowledge
						return false;

					receivedPacketIndex = ReadAcknowledge(netReader);
					if (outgoingPackets.Peek().index == receivedPacketIndex) {
						SequencedPacket acknowledgedPacket = outgoingPackets.Dequeue();
						acknowledgedPacket.OnAcknowledged?.Invoke();

						if (acknowledgedPacket == latestOutgoingPacket) {
							latestOutgoingPacket = null;
						}
						
						// Attempt to send the next reliable one if any
						if (outgoingPackets.Count > 0) {
							byte[] packetData = outgoingPackets.Peek().packet;
							debugClient.Send(packetData, packetData.Length, remoteEndpoint);
							ticksToResend = RESEND_TICKS;
						}
					}
					break;
				
				case PacketType.Unreliable:
					return true;
				
				case PacketType.UnreliableOrdered:
					receivedPacketIndex = ReadUnreliableOrderedHeader(netReader);

					// Process data if it is latest
					if (receivedPacketIndex > lastUnreliablePacketIndex) {
						lastUnreliablePacketIndex = receivedPacketIndex;
						return true;
					}
					break;

				case PacketType.Termination:
					receivedPacketIndex = ReadTerminationHeader(netReader);
					SendAcknowledge(receivedPacketIndex); // Return Message

					// Process data if it is new
					if (receivedPacketIndex > lastAckedPacketIndex) {
						lastAckedPacketIndex = receivedPacketIndex;
						outgoingPackets.Clear();
						RainMeadow.Debug("Peer Terminated Connection");
						PeerTerminated(remoteEndpoint);
						return false;
					}
					break;
			}

			return false;
		}

		public static bool IsPacketAvailable() {
			return debugClient != null && debugClient.Available > 0;
		}

		public static void Send(byte[] data, int length, PacketType packetType, PacketDataType dataType = PacketDataType.Internal) {
			if (debugClient == null || waitingForTermination)
				return;

			// Insert data type because there is concept of channels here
			byte[] typedData = new byte[data.Length + 1];
			typedData[0] = (byte)dataType;
			Buffer.BlockCopy(data, 0, typedData, 1, data.Length);
			data = typedData;

			IPEndPoint remoteEndpoint = !isHost ? localHostEndpoint : localClientEndpoint;
			byte[] buffer = null;
			byte[] packetData;
			switch (packetType) {
				case PacketType.Reliable:
					latestOutgoingPacket = new SequencedPacket(++packetIndex, data);
					outgoingPackets.Enqueue(latestOutgoingPacket);

					SequencedPacket outgoingPacket = outgoingPackets.Peek();
					buffer = outgoingPacket.packet;
					break;
				
				case PacketType.Unreliable:
					buffer = new byte[1 + data.Length];
					MemoryStream stream = new MemoryStream(buffer);
					BinaryWriter writer = new BinaryWriter(stream);

					WriteUnreliableHeader(writer);
					writer.Write(data);

					break;
				
				case PacketType.UnreliableOrdered:
					buffer = new byte[9 + data.Length];
					stream = new MemoryStream(9 + data.Length);
					writer = new BinaryWriter(stream);

					WriteUnreliableOrderedHeader(writer, ++unreliablePacketIndex);
					writer.Write(data);

					break;
			}

			if (buffer == null)
				return;
			
			debugClient.Send(buffer, buffer.Length, remoteEndpoint);
		}

		static void SendAcknowledge(ulong index) {
			IPEndPoint remoteEndpoint = !isHost ? localHostEndpoint : localClientEndpoint;
			byte[] buffer = new byte[9];
			MemoryStream stream = new MemoryStream(buffer);
			BinaryWriter writer = new BinaryWriter(stream);

			WriteAcknowledge(writer, index);
			debugClient.Send(buffer, 9, remoteEndpoint);
		}

		static void SendTermination() {
			outgoingPackets.Clear();
			
			IPEndPoint remoteEndpoint = !isHost ? localHostEndpoint : localClientEndpoint;
			latestOutgoingPacket = new SequencedPacket(++packetIndex, new byte[0], 3, true);
			outgoingPackets.Enqueue(latestOutgoingPacket);

			latestOutgoingPacket.OnAcknowledged += CleanUp;
			latestOutgoingPacket.OnFailed += CleanUp;

			byte[] packetData = outgoingPackets.Peek().packet;
			debugClient.Send(packetData, packetData.Length, remoteEndpoint);

			waitingForTermination = true;
		}

		//

		/// <summary>Writes 9 bytes</summary>
		static void WriteReliableHeader(BinaryWriter writer, ulong index) {
			writer.Write((byte)PacketType.Reliable);
			writer.Write(index);
		}

		static ulong ReadReliableHeader(BinaryReader reader) {
			// Ignore type
			ulong index = reader.ReadUInt64();
			return index;
		}

		//

		/// <summary>Writes 1 byte</summary>
		static void WriteUnreliableHeader(BinaryWriter writer) {
			writer.Write((byte)PacketType.Unreliable);
		}

		static void ReadUnreliableHeader(BinaryReader reader) {
			// Ignore type
		}

		//

		/// <summary>Writes 9 bytes</summary>
		static void WriteUnreliableOrderedHeader(BinaryWriter writer, ulong index) {
			writer.Write((byte)PacketType.UnreliableOrdered);
			writer.Write(index);
		}

		static ulong ReadUnreliableOrderedHeader(BinaryReader reader) {
			// Ignore type
			ulong index = reader.ReadUInt64();
			return index;
		}

		//

		/// <summary>Writes 9 bytes</summary>
		static void WriteAcknowledge(BinaryWriter writer, ulong index) {
			writer.Write((byte)PacketType.Acknowledge);
			writer.Write(index);
		}

		static ulong ReadAcknowledge(BinaryReader reader) {
			// Ignore type
			ulong index = reader.ReadUInt64();
			return index;
		}

		//

		/// <summary>Writes 9 bytes</summary>
		static void WriteTerminationHeader(BinaryWriter writer, ulong index) {
			writer.Write((byte)PacketType.Termination);
			writer.Write(index);
		}

		static ulong ReadTerminationHeader(BinaryReader reader) {
			// Ignore type
			ulong index = reader.ReadUInt64();
			return index;
		}

		//
	}
}