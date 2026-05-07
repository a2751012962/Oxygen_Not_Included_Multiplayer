using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.Architecture;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared.Profiling;
using UnityEngine;
using static PathFinder;
using static STRINGS.MISC;

namespace ONI_MP.Networking.Packets.Tools.Build
{
    public class BuildPacket : IPacket
    {
        private const int MaxMaterialTagCount = 64;

        private string PrefabID;
        private int Cell;
        private Orientation Orientation;
        private List<string> MaterialTags = new List<string>();
        private PrioritySetting Priority;
        private ObjectLayer ObjectLayer;

        public BuildPacket()
        {
        }

        public BuildPacket(string prefabID, int cell, Orientation orientation, IEnumerable<Tag> materials, ObjectLayer objectLayer)
        {
            using var _ = Profiler.Scope();

            PrefabID = prefabID;
            Cell = cell;
            Orientation  = orientation;
            MaterialTags = materials.Select(t => t.ToString()).ToList();

            if (PlanScreen.Instance)
                Priority = PlanScreen.Instance.GetBuildingPriority();

            ObjectLayer = objectLayer;
        }

        public void Serialize(BinaryWriter writer)
        {
            using var _ = Profiler.Scope();

            writer.Write(PrefabID);
            writer.Write(Cell);
            writer.Write((int)Orientation);
            writer.Write(MaterialTags.Count);
            foreach (var tag in MaterialTags)
                writer.Write(tag);

            writer.Write((int)Priority.priority_class);
            writer.Write(Priority.priority_value);

            writer.Write((int)ObjectLayer);
        }

        public void Deserialize(BinaryReader reader)
        {
            using var _ = Profiler.Scope();

            PrefabID = reader.ReadString();
            Cell = reader.ReadInt32();
            Orientation = (Orientation)reader.ReadInt32();
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxMaterialTagCount)
            {
                DebugConsole.LogWarning($"[BuildPacket] Invalid material tag count: {count}");
                Cell = Grid.InvalidCell;
                MaterialTags = [];
                return;
            }
            MaterialTags = new List<string>();
            for (int i = 0; i < count; i++)
                MaterialTags.Add(reader.ReadString());

            Priority = new PrioritySetting((PriorityScreen.PriorityClass)reader.ReadInt32(), reader.ReadInt32());
            ObjectLayer = (ObjectLayer) reader.ReadInt32();
        }

        public void OnDispatched()
        {
            using var _ = Profiler.Scope();

            if (!Grid.IsValidCell(Cell))
            {
                DebugConsole.LogWarning($"[BuildPacket] Invalid cell: {Cell}");
                return;
            }

            var def = Assets.GetBuildingDef(PrefabID);
            if (def == null)
            {
                DebugConsole.LogWarning($"[BuildPacket] Unknown building def: {PrefabID}");
                return;
            }

            var tags = MaterialTags.Select(t => new Tag(t)).ToList();
            Vector3 pos  = Grid.CellToPosCBC(Cell, Grid.SceneLayer.Building);

            GameObject gameObject = null;
            bool shouldReplace = CanReplace(def, out GameObject existingObject);

            if (!shouldReplace)
            {
                GameObject visualizer = Util.KInstantiate(def.BuildingPreview, pos);
                gameObject = def.TryPlace(visualizer, pos, Orientation, tags, "DEFAULT_FACADE"); // Build like normal
            } 
            else
            {
                // Something here went wrong
                if (existingObject == null)
                    return;

                gameObject = def.TryReplaceTile(existingObject, pos, Orientation, tags, "DEFAULT_FACADE"); // Try replace
            }
                SetPriority(gameObject);
        }

        private bool CanReplace(BuildingDef newDef, out GameObject existingObject)
        {
            existingObject = null;

            // Only check the primary building layer (no iteration version)
            var obj = Grid.Objects[Cell, (int)ObjectLayer];
            if (obj == null)
                return false;

            var building = obj.GetComponent<Building>();
            if (building == null)
                return false;

            var existingDef = building.Def;
            if (existingDef == null)
                return false;

            existingObject = obj;

            // same building
            if (existingDef == newDef)
                return true;

            bool existingIsTile = existingDef.IsTilePiece;
            bool newIsTile = newDef.IsTilePiece;

            bool existingIsFoundation = existingDef.IsFoundation;
            bool newIsFoundation = newDef.IsFoundation;

            bool existingIsReplaceable = existingDef.Replaceable;
            bool newIsReplaceable = newDef.Replaceable;

            // Tile on tile
            if (existingIsTile && newIsTile)
                return true;

            // Foundation on foundation
            if (existingIsFoundation && newIsFoundation)
                return true;

            // Cross type checks
            if (existingIsTile != newIsTile || existingIsFoundation != newIsFoundation)
                return false;

            // Replacable
            if (existingIsReplaceable && newIsReplaceable)
                return true;

            return false;
        }

        private void SetPriority(GameObject gameObject)
        {
            Prioritizable prioritizable = gameObject?.GetComponent<Prioritizable>();
            prioritizable?.SetMasterPriority(Priority);
        }

        // TODO: Implement later when sandbox eventually gets done
        private void InstantBuild(BuildingDef def, List<Tag> tags)
        {
            // default to 30 degrees
            def.Build(Cell, Orientation, null, tags, 30f, "DEFAULT_FACADE", playsound: false, GameClock.Instance.GetTime());
        }

    }
}
