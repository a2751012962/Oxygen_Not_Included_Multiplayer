using System.Collections.Generic;
using ONI_Together.Misc;
using ONI_Together.Networking.Packets.World;
using UnityEngine;

namespace ONI_Together.Networking.Components.StructureStateSyncers;

public class ReactorStateSyncer : StructureSyncerBase
{
    private Reactor reactor;
    private Reactor.StatesInstance smi;
    private Storage supplyStorage;
    private Storage reactionStorage;
    private Storage wasteStorage;

    private float lastFuelTemp;
    private int lastMajorState = -1;
    private float tempChangeThreshold = 5f;
    
    protected override void Initialize()
    {
        reactor = GetComponent<Reactor>();
        smi = reactor.smi;
        supplyStorage = reactor.supplyStorage;
        reactionStorage = reactor.reactionStorage;
        wasteStorage = reactor.wasteStorage;
        checkOptionalsValuesForChanges = false;
    }

    protected override void SampleState(out Variant value, out bool active, out List<Variant> optionalValues)
    {
        float fuelTemp = reactor.FuelTemperature;
        value = fuelTemp >= 0f ? fuelTemp : 0f;
        active = false;

        var sm = smi.sm;
        int majorState = 0;
        if (smi.IsInsideState(sm.dead)) majorState = 3;
        else if (smi.IsInsideState(sm.meltdown)) majorState = 2;
        else if (smi.IsInsideState(sm.on)) majorState = 1;

        optionalValues = new List<Variant>();
        optionalValues.Add(majorState);
        optionalValues.Add(sm.reactionUnderway.Get(smi));
        optionalValues.Add(sm.meltingDown.Get(smi));
        optionalValues.Add(sm.melted.Get(smi));
        optionalValues.Add(sm.meltdownMassRemaining.Get(smi));
        optionalValues.Add(sm.timeSinceMeltdown.Get(smi));
        optionalValues.Add(sm.canVent.Get(smi));
        optionalValues.Add(reactor.spentFuel);
        optionalValues.Add(reactor.numCyclesRunning);
        optionalValues.Add(reactor.fuelDeliveryEnabled);
        optionalValues.Add(reactor.timeSinceMeltdownEmit);
        optionalValues.Add(reactor.radEmitter.emitRads);
        
        PrimaryElement storedCoolant = smi.master.GetStoredCoolant();
        float waterMeterPercent = 0f;
        if (storedCoolant)
            waterMeterPercent = storedCoolant.Mass / 90f;
        
        optionalValues.Add(waterMeterPercent);
        
        PrimaryElement activeFuel = smi.master.GetActiveFuel();
        float temperaturePercent = 0f;
        if (activeFuel != null)
            temperaturePercent = Mathf.Clamp01(activeFuel.Temperature / 3000f) / Reactor.meterFrameScaleHack;
        
        optionalValues.Add(temperaturePercent);
        
        int supplyCount = 0, reactionCount = 0, wasteCount = 0;
        if (supplyStorage)
        {
            var encoded = new List<Variant>();
            BuildingUtils.EncodeStorageContents(supplyStorage, out encoded);
            supplyCount = encoded.Count;
            optionalValues.Add(supplyCount);
            optionalValues.AddRange(encoded);
        }
        else
        {
            optionalValues.Add(0);
        }

        if (reactionStorage)
        {
            var encoded = new List<Variant>();
            BuildingUtils.EncodeStorageContents(reactionStorage, out encoded);
            reactionCount = encoded.Count;
            optionalValues.Add(reactionCount);
            optionalValues.AddRange(encoded); 
        }
        else
        {
            optionalValues.Add(0);
        }
        
        if (wasteStorage != null)
        {
            var encoded = new List<Variant>();
            BuildingUtils.EncodeStorageContents(wasteStorage, out encoded);
            wasteCount = encoded.Count;
            optionalValues.Add(wasteCount);
            optionalValues.AddRange(encoded);
        }
        else
        {
            optionalValues.Add(0);
        }
        
        lastFuelTemp = fuelTemp;
        lastMajorState = majorState;
    }

    protected override void ApplyState(StructureStatePacket packet)
    {
        if (reactor == null || smi == null) return;

            var opt = packet.OptionalValues;
            int idx = 0;

            int targetState = opt[idx++].Int;
            bool reactionUnderway = opt[idx++].Boolean;
            bool meltingDown = opt[idx++].Boolean;
            bool melted = opt[idx++].Boolean;
            float meltdownMassRemaining = opt[idx++].Float;
            float timeSinceMeltdown = opt[idx++].Float;
            bool canVent = opt[idx++].Boolean;

            float spentFuel = opt[idx++].Float;
            int numCyclesRunning = opt[idx++].Int;
            bool fuelDeliveryEnabled = opt[idx++].Boolean;
            float timeSinceMeltdownEmit = opt[idx++].Float;
            float rads = opt[idx++].Float;
            float waterMeterPos = opt[idx++].Float;
            float tempMeterPos = opt[idx++].Float;

            int supplyCount = opt[idx++].Int;
            var supplyData = opt.GetRange(idx, supplyCount);
            idx += supplyCount;
            if (supplyCount >= 2) BuildingUtils.RebuildStorageFromData(supplyStorage, supplyData);

            int reactionCount = opt[idx++].Int;
            var reactionData = opt.GetRange(idx, reactionCount);
            idx += reactionCount;
            if (reactionCount >= 2)
            {
                BuildingUtils.RebuildStorageFromData(reactionStorage, reactionData);
                var fuel = reactionStorage.FindFirst(SimHashes.EnrichedUranium.CreateTag());
                if (fuel != null)
                {
                    var pe = fuel.GetComponent<PrimaryElement>();
                    if (pe != null) pe.Temperature = packet.Value.Float;
                }
            }

            int wasteCount = opt[idx++].Int;
            var wasteData = opt.GetRange(idx, wasteCount);
            if (wasteCount >= 2) BuildingUtils.RebuildStorageFromData(wasteStorage, wasteData);

            var sm = smi.sm;
            sm.reactionUnderway.Set(reactionUnderway, smi);
            sm.meltingDown.Set(meltingDown, smi);
            sm.melted.Set(melted, smi);
            sm.meltdownMassRemaining.Set(meltdownMassRemaining, smi);
            sm.timeSinceMeltdown.Set(timeSinceMeltdown, smi);
            sm.canVent.Set(canVent, smi);

            reactor.spentFuel = spentFuel;
            reactor.numCyclesRunning = numCyclesRunning;
            reactor.fuelDeliveryEnabled = fuelDeliveryEnabled;
            reactor.timeSinceMeltdownEmit = timeSinceMeltdownEmit;
            reactor.radEmitter.emitRads = rads;
            reactor.waterMeter.SetPositionPercent(waterMeterPos);
            reactor.temperatureMeter.SetPositionPercent(tempMeterPos);

            int currentMajorState = 0;
            if (smi.IsInsideState(sm.dead)) currentMajorState = 3;
            else if (smi.IsInsideState(sm.meltdown)) currentMajorState = 2;
            else if (smi.IsInsideState(sm.on)) currentMajorState = 1;

            if (currentMajorState != targetState)
                ForceStateTransition(targetState, meltdownMassRemaining, spentFuel, rads);
    }
    
    private void ForceStateTransition(int targetState, float meltdownMass, float spentFuel, float rads)
    {
        var sm = smi.sm;

        switch (targetState)
        {
            case 0:
                if (smi.IsInsideState(sm.on))
                    smi.GoTo(sm.off);
                break;
            case 1:
                if (smi.IsInsideState(sm.off))
                    sm.reactionUnderway.Set(true, smi);
                break;
            case 2:
                if (!smi.IsInsideState(sm.meltdown) && !smi.IsInsideState(sm.dead))
                {
                    supplyStorage.ConsumeAllIgnoringDisease();
                    reactionStorage.ConsumeAllIgnoringDisease();
                    wasteStorage.ConsumeAllIgnoringDisease();
                    float totalMass = supplyStorage.MassStored() + reactionStorage.MassStored() + wasteStorage.MassStored();
                    sm.meltdownMassRemaining.Set(10f + totalMass + spentFuel, smi);
                    smi.GoTo(sm.meltdown.pre);
                }
                break;
            case 3:
                if (smi.IsInsideState(sm.meltdown))
                    sm.meltdownMassRemaining.Set(0f, smi);
                else if (!smi.IsInsideState(sm.dead))
                    smi.GoTo(sm.dead);
                break;
        }
    }

    protected override bool ShouldForceSync()
    {
        if (reactor == null) return false;
        float currentTemp = reactor.FuelTemperature;
        if (currentTemp >= 0f && Mathf.Abs(currentTemp - lastFuelTemp) > tempChangeThreshold)
            return true;
        int currentState = 0;
        var sm = smi.sm;
        if (smi.IsInsideState(sm.dead)) currentState = 3;
        else if (smi.IsInsideState(sm.meltdown)) currentState = 2;
        else if (smi.IsInsideState(sm.on)) currentState = 1;
        if (currentState != lastMajorState) return true;
        return false;
    }
}