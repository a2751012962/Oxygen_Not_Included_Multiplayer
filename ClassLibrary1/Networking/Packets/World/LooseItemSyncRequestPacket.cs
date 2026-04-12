using System.IO;
using ONI_MP.Networking.Packets.Architecture;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Packets.World
{
	internal class LooseItemSyncRequestPacket : IPacket
	{
		public ulong RequesterId;
		public int X;
		public int Y;
		public int Width;
		public int Height;
		public int ShardIndex;
		public int ShardCount;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(RequesterId);
			writer.Write(X);
			writer.Write(Y);
			writer.Write(Width);
			writer.Write(Height);
			writer.Write(ShardIndex);
			writer.Write(ShardCount);
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			RequesterId = reader.ReadUInt64();
			X = reader.ReadInt32();
			Y = reader.ReadInt32();
			Width = reader.ReadInt32();
			Height = reader.ReadInt32();
			ShardIndex = reader.ReadInt32();
			ShardCount = reader.ReadInt32();
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsHost || RequesterId == 0 || Width <= 0 || Height <= 0 || ShardCount <= 0)
				return;

			if (!MultiplayerSession.ConnectedPlayers.TryGetValue(RequesterId, out var player) || player.Connection == null)
				return;

			var viewport = new RectInt(X, Y, Width, Height);
			var packet = LooseItemSyncPacket.BuildForViewport(viewport, ShardIndex, ShardCount);
			PacketSender.SendToPlayer(RequesterId, packet, PacketSendMode.Unreliable);
		}
	}
}
