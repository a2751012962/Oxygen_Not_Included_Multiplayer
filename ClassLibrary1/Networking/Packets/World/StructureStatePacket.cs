using ONI_MP.Networking.Packets.Architecture;
using System.IO;
using Shared.Profiling;
using ONI_MP.Networking.Components;
using static TUNING.NOISE_POLLUTION;

namespace ONI_MP.Networking.Packets.World
{
	public class StructureStatePacket : IPacket
	{
		public int Cell;
		public float Value; // Joules for Battery, Progress for others

		public float[] OptionalValues = []; // Extra things (such as EnergyGenerator mass, storage amount etc)

		public bool IsActive; // Operational active state
		public StructureStateSyncer.StructureType StructureType = StructureStateSyncer.StructureType.UNCATEGORIZED;
		public StructureStateSyncer.GeneratorType GeneratorType = StructureStateSyncer.GeneratorType.UNKNOWN;

		public void Serialize(BinaryWriter writer)
		{
			using var _ = Profiler.Scope();

			writer.Write(Cell);
			writer.Write(Value);
			writer.Write(IsActive);
			writer.Write((int)StructureType);
			writer.Write((int)GeneratorType);

            writer.Write(OptionalValues.Length);
            for (int i = 0; i < OptionalValues.Length; i++)
            {
                writer.Write(OptionalValues[i]);
            }
        }

		public void Deserialize(BinaryReader reader)
		{
			using var _ = Profiler.Scope();

			Cell = reader.ReadInt32();
			Value = reader.ReadSingle();
			IsActive = reader.ReadBoolean();
			StructureType = (StructureStateSyncer.StructureType)reader.ReadInt32();
			GeneratorType = (StructureStateSyncer.GeneratorType)reader.ReadInt32();

            int length = reader.ReadInt32();
            OptionalValues = new float[length];
            for (int i = 0; i < length; i++)
            {
                OptionalValues[i] = reader.ReadSingle();
            }
        }

		public void OnDispatched()
		{
			using var _ = Profiler.Scope();

			if (MultiplayerSession.IsHost) return;

			// Handled by StructureStateSyncer on client
			StructureStateSyncer.HandlePacket(this);
		}
	}
}
