using System;
using KSerialization;
using ONI_MP.DebugTools;
using ONI_MP.Networking.Packets.World;
using ONI_MP.Patches.GamePatches;
using ONI_MP.Patches.World;
using Shared.Profiling;
using UnityEngine;
using static EnergyGenerator;
using static STRINGS.UI.OVERLAYS;

namespace ONI_MP.Networking.Components
{
	public class StructureStateSyncer : KMonoBehaviour
	{
		public enum StructureType
		{
			UNCATEGORIZED,
			BATTERY,
			GENERATOR
		}

        public enum GeneratorType
        {
            UNKNOWN,
            MANUAL,
            ENERGY,
            MODULE,
            STATERPILLER
        }

		private float sendInterval = 0.5f; // Sync every 500ms
		private float timer;

		private Battery battery;
		private Generator generator;
		private Operational operational;
		private int cell;

		private float lastSentValue;
		private bool lastSentActive;
        private float[] lastOptionalValues;

		// Grace period
		private bool _initialized = false;
		private float _initializationTime;
		private const float INITIAL_DELAY = 5f;

		public StructureType structureType = StructureType.UNCATEGORIZED;

        // Generator specific
        public GeneratorType generatorType = GeneratorType.UNKNOWN;
        public object generatorInstance;

		public override void OnSpawn()
		{
			using var _ = Profiler.Scope();

			base.OnSpawn();
            cell = Grid.PosToCell(this);
            operational = GetComponent<Operational>();
        }

		public void InitalizeAsStructure(StructureType structureType)
		{
			this.structureType = structureType;
			switch(structureType)
			{
				case StructureType.BATTERY:
					battery = GetComponent<Battery>();
					break;
				case StructureType.GENERATOR:
					generator = GetComponent<Generator>();
                    generatorType = DetermineGeneratorType(this.gameObject, out var gen);
                    generatorInstance = gen;
                    break;
                case StructureType.UNCATEGORIZED:
                default:
                    break;
			}
		}

		private void Update()
		{
			using var _ = Profiler.Scope();

            if (!MultiplayerSession.InSession)
                return;

			if (MultiplayerSession.IsHost)
			{
				// Skip if no clients connected
			  //  if (!MultiplayerSession.SessionHasPlayers)
					//return;

				// Grace period after world load
				if (!_initialized)
				{
					_initializationTime = Time.unscaledTime;
					_initialized = true;
					return;
				}

				if (Time.unscaledTime - _initializationTime < INITIAL_DELAY)
					return;

				HostUpdate();
			}
		}

        private void HostUpdate()
        {
            using var _ = Profiler.Scope();

            try
            {
                timer += Time.unscaledDeltaTime;
                if (timer < sendInterval) return;
                timer = 0f;

                float currentValue = 0f;
                bool currentActive = false;
                float[] optionalValues = [];

                if (operational != null)
                    currentActive = operational.IsActive;

                switch (structureType)
                {
                    case StructureType.BATTERY:
                        if (battery != null)
                        {
                            currentValue = battery.JoulesAvailable;
                        }
                        break;

                    case StructureType.GENERATOR:
                        if (generator != null)
                        {
                            switch(generatorType)
                            {
                                case GeneratorType.ENERGY:
                                    EnergyGenerator gen = generatorInstance as EnergyGenerator;
                                    if (gen != null)
                                    {
                                        // Coal generator
                                        if (gen.hasMeter)
                                        {
                                            InputItem inputItem = gen.formula.inputs[0];
                                            float mass = gen.storage.GetMassAvailable(inputItem.tag);
                                            float storedMass = inputItem.maxStoredMass;

                                            optionalValues = new float[2];
                                            optionalValues[0] = mass;
                                            optionalValues[1] = storedMass;
                                        }
                                    }
                                    break;
                                default:
                                    break;
                            }
                            currentValue = generator.JoulesAvailable;
                        }
                        break;

                    case StructureType.UNCATEGORIZED:
                    default:
                        break;
                }

                // Sync if changed significantly
                if (Mathf.Abs(currentValue - lastSentValue) > 0.1f || currentActive != lastSentActive || OptionalValuesChanged(optionalValues, lastOptionalValues))
                {
                    lastSentValue = currentValue;
                    lastSentActive = currentActive;
                    lastOptionalValues = optionalValues;

                    var packet = new StructureStatePacket
                    {
                        Cell = cell,
                        Value = currentValue,
                        IsActive = currentActive,
                        StructureType = structureType,
                        GeneratorType = generatorType,
                        OptionalValues = optionalValues
                    };

                    PacketSender.SendToAllClients(packet, PacketSendMode.Unreliable);
                }
            }
            catch (System.Exception)
            {
                // Silent fail - Structure may not be ready
            }
        }

        private bool OptionalValuesChanged(float[] a, float[] b, float epsilon = 0.01f)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            if (a.Length != b.Length) return true;

            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > epsilon)
                    return true;
            }

            return false;
        }

        public static void HandlePacket(StructureStatePacket packet)
		{
			using var _ = Profiler.Scope();
			if (!Grid.IsValidCell(packet.Cell)) return;

			GameObject go = Grid.Objects[packet.Cell, (int)ObjectLayer.Building];
			if (go == null) return;

            switch(packet.StructureType)
            {
                case StructureType.BATTERY:
                    ApplyBatteryState(go, packet);
                    break;
                case StructureType.GENERATOR:
                    ApplyGeneratorState(go, packet);
                    break;
                case StructureType.UNCATEGORIZED:
                default:
                    break;
            }
            ApplyOperationalState(go, packet);
		}

        #region Battery
        private static void ApplyBatteryState(GameObject go, StructureStatePacket packet)
        {
            var battery = go.GetComponent<Battery>();
            if (battery == null) return;

            battery.joulesAvailable = packet.Value;
            RefreshBatteryTracker(go);
			UpdateBatteryMeter(battery, packet.Value);
        }

        private static void UpdateBatteryMeter(Battery battery, float joules)
		{
			try
			{
                var meter = battery.meter;
                if (meter == null) return;

                if (battery.capacity <= 0f) return;

                float percent = Mathf.Clamp01(joules / battery.capacity);
                meter.SetPositionPercent(percent);
            } catch(Exception ex)
			{
                DebugConsole.LogError($"[StructureStateSyncer] Meter update failed: {ex}");
            }
		}

        private static void RefreshBatteryTracker(GameObject go)
        {
            var tracker = go.GetComponent<BatteryTracker>();
            if (tracker == null) return;

            using var allowClientRefresh = BatteryTrackerPatch.AllowClientRefresh();
            tracker.UpdateData();
        }
        #endregion

        #region Generator
        public static void ApplyGeneratorState(GameObject go, StructureStatePacket packet)
        {
            var generator = go.GetComponent<Generator>();
            if(generator == null) return;

            switch(packet.GeneratorType)
            {
                case GeneratorType.ENERGY:
                    if (packet.OptionalValues.Length > 0)
                    {
                        EnergyGenerator gen = generator as EnergyGenerator;
                        if (gen != null)
                        {
                            float mass = packet.OptionalValues[0];
                            float storedMass = packet.OptionalValues[1];
                            // TODO: update the stored mass later
                            UpdateEnergyGeneratorMeter(gen, mass, storedMass);
                        }
                    }
                    break;
                // Don't need to do anything with these yet
                case GeneratorType.MANUAL:
                case GeneratorType.MODULE:
                case GeneratorType.STATERPILLER:
                default:
                    break;
            }

            generator.AssignJoulesAvailable(packet.Value);
        }

        private static void UpdateEnergyGeneratorMeter(EnergyGenerator generator, float mass, float storedMass)
        {
            if (generator.hasMeter)
            {
                float meterPercent = Mathf.Clamp01(mass / storedMass);
                generator.meter.SetPositionPercent(meterPercent);
            }
        }

        private static GeneratorType DetermineGeneratorType(GameObject go, out object generator)
        {
            ManualGenerator manualGenerator = go.GetComponent<ManualGenerator>();
            if (manualGenerator != null)
            {
                generator = manualGenerator;
                return GeneratorType.MANUAL;
            }

            EnergyGenerator energyGenerator = go.GetComponent<EnergyGenerator>();
            if (energyGenerator != null)
            {
                generator = energyGenerator;
                return GeneratorType.ENERGY;
            }

            ModuleGenerator moduleGenerator = go.GetComponent<ModuleGenerator>();
            if (moduleGenerator != null)
            {
                generator = moduleGenerator;
                return GeneratorType.MODULE;
            }

                StaterpillarGenerator statepillerGenerator = go.GetComponent<StaterpillarGenerator>();
            if (moduleGenerator != null)
            {
                generator = statepillerGenerator;
                return GeneratorType.STATERPILLER;
            }

            generator = null;
            return GeneratorType.UNKNOWN;
        }
        #endregion

        private static void ApplyOperationalState(GameObject go, StructureStatePacket packet)
		{
            var operational = go.GetComponent<Operational>();
            if (operational == null) return;

            operational.SetActive(packet.IsActive);
        }
	}
}
