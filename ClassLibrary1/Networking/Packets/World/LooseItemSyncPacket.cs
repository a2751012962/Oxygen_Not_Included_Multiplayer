using System.Collections.Generic;
using System.IO;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Components;
using ONI_MP.Networking.Packets.Architecture;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Packets.World
{
	internal class LooseItemSyncPacket : IPacket
	{
		internal struct LooseItemEntry
		{
			public int NetId;
			public string PrefabName;
			public Vector3 Position;
			public Quaternion Rotation;
			public string ObjectName;
			public int GameLayer;
			public bool HasPrimaryElement;
			public int ElementHash;
			public float Mass;
			public float Temperature;
			public byte DiseaseIndex;
			public int DiseaseCount;
		}

		public int X;
		public int Y;
		public int Width;
		public int Height;
		public int ShardIndex;
		public int ShardCount;
		public List<LooseItemEntry> Entries = [];

		public static LooseItemSyncPacket BuildForViewport(RectInt viewport, int shardIndex, int shardCount)
		{
			using var _ = Profiler.Scope();

			var packet = new LooseItemSyncPacket
			{
				X = viewport.xMin,
				Y = viewport.yMin,
				Width = viewport.width,
				Height = viewport.height,
				ShardIndex = shardIndex,
				ShardCount = shardCount
			};

			foreach (var pickupable in Object.FindObjectsByType<Pickupable>(FindObjectsSortMode.None))
			{
				if (pickupable == null)
					continue;
				if (TryBuildEntry(pickupable.gameObject, viewport, shardIndex, shardCount, out var entry))
					packet.Entries.Add(entry);
			}

			return packet;
		}

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(X);
			writer.Write(Y);
			writer.Write(Width);
			writer.Write(Height);
			writer.Write(ShardIndex);
			writer.Write(ShardCount);
			writer.Write(Entries.Count);
			foreach (var entry in Entries)
			{
				writer.Write(entry.NetId);
				writer.Write(entry.PrefabName ?? "");
				writer.Write(entry.Position.x);
				writer.Write(entry.Position.y);
				writer.Write(entry.Position.z);
				writer.Write(entry.Rotation.x);
				writer.Write(entry.Rotation.y);
				writer.Write(entry.Rotation.z);
				writer.Write(entry.Rotation.w);
				writer.Write(entry.ObjectName ?? "");
				writer.Write(entry.GameLayer);
				writer.Write(entry.HasPrimaryElement);
				writer.Write(entry.ElementHash);
				writer.Write(entry.Mass);
				writer.Write(entry.Temperature);
				writer.Write(entry.DiseaseIndex);
				writer.Write(entry.DiseaseCount);
			}
		}

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			X = reader.ReadInt32();
			Y = reader.ReadInt32();
			Width = reader.ReadInt32();
			Height = reader.ReadInt32();
			ShardIndex = reader.ReadInt32();
			ShardCount = reader.ReadInt32();
			int count = reader.ReadInt32();
			Entries = new List<LooseItemEntry>(count);
			for (int i = 0; i < count; i++)
			{
				Entries.Add(new LooseItemEntry
				{
					NetId = reader.ReadInt32(),
					PrefabName = reader.ReadString(),
					Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
					Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
					ObjectName = reader.ReadString(),
					GameLayer = reader.ReadInt32(),
					HasPrimaryElement = reader.ReadBoolean(),
					ElementHash = reader.ReadInt32(),
					Mass = reader.ReadSingle(),
					Temperature = reader.ReadSingle(),
					DiseaseIndex = reader.ReadByte(),
					DiseaseCount = reader.ReadInt32()
				});
			}
		}

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost)
				return;

			ReconcileVisibleShard();
		}

		private void ReconcileVisibleShard()
		{
			using var _ = Profiler.Scope();

			var viewport = new RectInt(X, Y, Width, Height);
			var authoritativeEntries = new Dictionary<int, LooseItemEntry>(Entries.Count);
			foreach (var entry in Entries)
			{
				if (entry.NetId != 0)
					authoritativeEntries[entry.NetId] = entry;
			}

			var localByNetId = new Dictionary<int, GameObject>(authoritativeEntries.Count);
			foreach (var pickupable in Object.FindObjectsByType<Pickupable>(FindObjectsSortMode.None))
			{
				if (pickupable == null)
					continue;

				GameObject go = pickupable.gameObject;
				if (!IsLooseCandidate(go, out int cell))
					continue;
				if (!IsInViewport(cell, viewport) || !MatchesShard(cell, ShardIndex, ShardCount))
					continue;

				if (!TryGetNetId(go, out int netId) || !authoritativeEntries.TryGetValue(netId, out var entry))
				{
					// Treat unknown local pickupables in the shard as phantoms and rebuild them from the host snapshot.
					go.DeleteObject();
					continue;
				}

				if (localByNetId.ContainsKey(netId))
				{
					go.DeleteObject();
					continue;
				}

				ApplyEntry(go, entry);
				localByNetId[netId] = go;
			}

			foreach (var entry in Entries)
			{
				if (entry.NetId == 0 || localByNetId.ContainsKey(entry.NetId))
					continue;

				InstantiateEntry(entry);
			}
		}

		private static bool TryBuildEntry(GameObject go, RectInt viewport, int shardIndex, int shardCount, out LooseItemEntry entry)
		{
			using var _ = Profiler.Scope();

			entry = default;
			if (!IsLooseCandidate(go, out int cell))
				return false;
			if (!IsInViewport(cell, viewport) || !MatchesShard(cell, shardIndex, shardCount))
				return false;
			if (!go.TryGetComponent<KPrefabID>(out var prefabId) || !prefabId.PrefabTag.IsValid)
				return false;

			var identity = go.AddOrGet<NetworkIdentity>();
			if (identity.NetId == 0)
				identity.RegisterIdentity();

			entry = new LooseItemEntry
			{
				NetId = identity.NetId,
				PrefabName = prefabId.PrefabTag.Name,
				Position = go.transform.position,
				Rotation = go.transform.rotation,
				ObjectName = go.name,
				GameLayer = go.layer
			};

			if (go.TryGetComponent<PrimaryElement>(out var primaryElement))
			{
				entry.HasPrimaryElement = true;
				entry.ElementHash = (int)primaryElement.ElementID;
				entry.Mass = primaryElement.Mass;
				entry.Temperature = primaryElement.Temperature;
				entry.DiseaseIndex = primaryElement.DiseaseIdx;
				entry.DiseaseCount = primaryElement.DiseaseCount;
			}

			return true;
		}

		private static void InstantiateEntry(LooseItemEntry entry)
		{
			using var _ = Profiler.Scope();

			GameObject prefab = Assets.GetPrefab(entry.PrefabName);
			if (prefab == null)
			{
				DebugConsole.LogWarning($"[LooseItemSyncPacket] Missing prefab '{entry.PrefabName}'");
				return;
			}

			GameObject obj = Object.Instantiate(prefab, entry.Position, entry.Rotation);
			if (obj == null)
			{
				DebugConsole.LogWarning($"[LooseItemSyncPacket] Failed to instantiate prefab '{entry.PrefabName}'");
				return;
			}

			if (entry.GameLayer != 0)
				obj.SetLayerRecursively(entry.GameLayer);

			obj.name = entry.ObjectName ?? prefab.name;

			KPrefabID id = obj.GetComponent<KPrefabID>();
			if (id != null)
			{
				id.InstanceID = KPrefabID.GetUniqueID();
				KPrefabIDTracker.Get().Register(id);
				id.InitializeTags(force_initialize: true);

				KPrefabID source = prefab.GetComponent<KPrefabID>();
				if (source != null)
				{
					id.CopyTags(source);
					id.CopyInitFunctions(source);
				}

				id.RunInstantiateFn();
			}

			ApplyEntry(obj, entry);
			obj.AddOrGet<NetworkIdentity>().OverrideNetId(entry.NetId);
			obj.SetActive(true);
		}

		private static void ApplyEntry(GameObject go, LooseItemEntry entry)
		{
			using var _ = Profiler.Scope();

			go.transform.SetPositionAndRotation(entry.Position, entry.Rotation);
			go.name = entry.ObjectName ?? go.name;
			if (entry.GameLayer != 0)
				go.SetLayerRecursively(entry.GameLayer);

			if (!entry.HasPrimaryElement || !go.TryGetComponent<PrimaryElement>(out var primaryElement))
				return;

			Element element = ElementLoader.FindElementByHash((SimHashes)entry.ElementHash);
			if (element != null)
				primaryElement.SetElement(element.id, true);

			primaryElement.Temperature = entry.Temperature;
			primaryElement.Mass = entry.Mass;

			if (entry.DiseaseCount <= 0)
			{
				if (primaryElement.DiseaseCount > 0)
					primaryElement.ModifyDiseaseCount(-primaryElement.DiseaseCount, "MP-Mod.LooseItemSync");
				return;
			}

			if (primaryElement.DiseaseIdx != entry.DiseaseIndex)
				primaryElement.AddDisease(entry.DiseaseIndex, entry.DiseaseCount, "MP-Mod.LooseItemSync");
			else if (primaryElement.DiseaseCount != entry.DiseaseCount)
				primaryElement.ModifyDiseaseCount(entry.DiseaseCount - primaryElement.DiseaseCount, "MP-Mod.LooseItemSync");
		}

		private static bool TryGetNetId(GameObject go, out int netId)
		{
			using var _ = Profiler.Scope();

			netId = 0;
			if (!go.TryGetComponent<NetworkIdentity>(out var identity) || identity == null)
				return false;

			netId = identity.NetId;
			return netId != 0;
		}

		private static bool IsLooseCandidate(GameObject go, out int cell)
		{
			using var _ = Profiler.Scope();

			cell = Grid.InvalidCell;
			if (go == null || !go.activeInHierarchy)
				return false;
			if (!go.TryGetComponent<Pickupable>(out var pickupable))
				return false;
			if (go.HasTag(GameTags.Stored))
				return false;

			cell = Grid.PosToCell(go);
			return Grid.IsValidCell(cell);
		}

		private static bool IsInViewport(int cell, RectInt viewport)
		{
			using var _ = Profiler.Scope();

			Grid.CellToXY(cell, out int x, out int y);
			return x >= viewport.xMin
				&& x < viewport.xMax
				&& y >= viewport.yMin
				&& y < viewport.yMax;
		}

		private static bool MatchesShard(int cell, int shardIndex, int shardCount)
		{
			using var _ = Profiler.Scope();

			if (shardCount <= 0)
				return true;

			int normalizedShard = ((shardIndex % shardCount) + shardCount) % shardCount;
			int normalizedCell = ((cell % shardCount) + shardCount) % shardCount;
			return normalizedCell == normalizedShard;
		}
	}
}
