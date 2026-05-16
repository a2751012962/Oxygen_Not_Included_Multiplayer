using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ONI_MP.Misc
{
    public static class SideScreenUtils
    {
        public static void TryRefreshSideScreen(SideScreenContent screen, GameObject go)
        {
            switch (screen)
            {
                case ThresholdSwitchSideScreen ts: TryRefreshThresholdSwitchSideScreen(ts, go); return;
                case ValveSideScreen vs: TryRefreshValveSideScreen(vs, go); return;
                case TimerSideScreen timer: TryRefreshTimerSideScreen(timer, go); return;
                case CapacityControlSideScreen cap: TryRefreshCapacityControlSideScreen(cap, go); return;
                case ActiveRangeSideScreen ar: TryRefreshActiveRangeSideScreen(ar, go); return;
                case DoorToggleSideScreen door: TryRefreshDoorToggleSideScreen(door, go); return;
                case PlayerControlledToggleSideScreen toggle: TryRefreshPlayerControlledToggleSideScreen(toggle, go); return;
                case SingleSliderSideScreen sss: TryRefreshSingleSliderSideScreen(sss, go); return;
                case IntSliderSideScreen iss: TryRefreshIntSliderSideScreen(iss, go); return;
                case MultiSliderSideScreen mss: TryRefreshMultiSliderSideScreen(mss, go); return;
                case LimitValveSideScreen lv: TryRefreshLimitValveSideScreen(lv, go); return;
                case AlarmSideScreen alarm: TryRefreshAlarmSideScreen(alarm, go); return;
                case CounterSideScreen counter: TryRefreshCounterSideScreen(counter, go); return;
                case GeoTunerSideScreen geoTuner when go.GetSMI<GeoTuner.Instance>() != null: TryRefreshGeoTunerSideScreen(geoTuner, go); return;
                case RemoteWorkTerminalSidescreen rwt when go.TryGetComponent(out RemoteWorkTerminal _): TryRefreshRemoteWorkTerminalSidescreen(rwt, go); return;
                case CheckboxListGroupSideScreen cbGroup: TryRefreshCheckboxListGroupSideScreen(cbGroup, go); return;
                case RelatedEntitiesSideScreen related: TryRefreshRelatedEntitiesSideScreen(related, go); return;
                case TreeFilterableSideScreen treeFilter when go.TryGetComponent(out TreeFilterable _): TryRefreshTreeFilterableSideScreen(treeFilter, go); return;
                case FilterSideScreen filter when go.TryGetComponent(out Filterable _): TryRefreshFilterSideScreen(filter, go); return;
            }

            DetailsScreen.Instance.RefreshTitle();
        }

        public static void TryRefreshThresholdSwitchSideScreen(ThresholdSwitchSideScreen ts, GameObject go)
        {
            if (!go.TryGetComponent(out IThresholdSwitch thresholdComp)) return;
            if (ts.thresholdSlider != null)
                ts.thresholdSlider.value = ts.thresholdSlider.GetPercentageFromValue(thresholdComp.Threshold);
            if (ts.numberInput != null)
                ts.numberInput.SetDisplayValue(thresholdComp.Format(thresholdComp.Threshold, false) + thresholdComp.ThresholdValueUnits());
            if (ts.unitsLabel != null)
                ts.unitsLabel.text = (string)thresholdComp.ThresholdValueUnits();
            if (ts.belowToggle != null && ts.aboveToggle != null)
            {
                ts.belowToggle.isOn = thresholdComp.ActivateAboveThreshold;
                ts.aboveToggle.isOn = !thresholdComp.ActivateAboveThreshold;
            }
        }

        public static void TryRefreshValveSideScreen(ValveSideScreen vs, GameObject go)
        {
            if (!go.TryGetComponent(out Valve valveComp)) return;
            if (vs.flowSlider != null)
                vs.flowSlider.value = valveComp.DesiredFlow;
            if (vs.numberInput != null)
                vs.numberInput.SetDisplayValue(GameUtil.GetFormattedMass(
                    Mathf.Max(0f, valveComp.DesiredFlow),
                    GameUtil.TimeSlice.PerSecond,
                    GameUtil.MetricMassFormat.Gram, false, "{0:0.#####}"));
        }

        public static void TryRefreshTimerSideScreen(TimerSideScreen timer, GameObject go)
        {
            if (!go.TryGetComponent(out LogicTimerSensor timerComp)) return;
            bool cyclesMode = timerComp.displayCyclesMode;
            if (cyclesMode)
            {
                if (timer.onDurationSlider != null) timer.onDurationSlider.value = timerComp.onDuration / 600f;
                if (timer.offDurationSlider != null) timer.offDurationSlider.value = timerComp.offDuration / 600f;
                if (timer.onDurationNumberInput != null) timer.onDurationNumberInput.SetAmount(timerComp.onDuration / 600f);
                if (timer.offDurationNumberInput != null) timer.offDurationNumberInput.SetAmount(timerComp.offDuration / 600f);
            }
            else
            {
                if (timer.onDurationSlider != null) timer.onDurationSlider.value = timerComp.onDuration;
                if (timer.offDurationSlider != null) timer.offDurationSlider.value = timerComp.offDuration;
                if (timer.onDurationNumberInput != null) timer.onDurationNumberInput.SetAmount(timerComp.onDuration);
                if (timer.offDurationNumberInput != null) timer.offDurationNumberInput.SetAmount(timerComp.offDuration);
            }
            timer.ReconfigureRingVisuals();
        }

        public static void TryRefreshCapacityControlSideScreen(CapacityControlSideScreen cap, GameObject go)
        {
            if (!go.TryGetComponent(out IUserControlledCapacity capComp)) return;
            if (cap.slider != null) cap.slider.value = capComp.UserMaxCapacity;
            if (cap.numberInput != null) cap.numberInput.SetDisplayValue(capComp.UserMaxCapacity.ToString());
            if (cap.unitsLabel != null) cap.unitsLabel.text = (string)capComp.CapacityUnits;
        }

        public static void TryRefreshActiveRangeSideScreen(ActiveRangeSideScreen ar, GameObject go)
        {
            if (!go.TryGetComponent(out IActivationRangeTarget arComp)) return;
            if (ar.activateValueSlider != null) ar.activateValueSlider.value = arComp.ActivateValue;
            if (ar.deactivateValueSlider != null) ar.deactivateValueSlider.value = arComp.DeactivateValue;
            if (ar.activateValueLabel != null) ar.activateValueLabel.SetDisplayValue(arComp.ActivateValue.ToString());
            if (ar.deactivateValueLabel != null) ar.deactivateValueLabel.SetDisplayValue(arComp.DeactivateValue.ToString());
        }

        public static void TryRefreshDoorToggleSideScreen(DoorToggleSideScreen door, GameObject go)
        {
            if (!go.TryGetComponent(out Door doorComp)) return;
            door.Refresh();
        }

        public static void TryRefreshPlayerControlledToggleSideScreen(PlayerControlledToggleSideScreen toggle, GameObject go)
        {
            if (!go.TryGetComponent(out IPlayerControlledToggle toggleComp)) return;
            bool state = toggleComp.ToggleRequested ? !toggleComp.ToggledOn() : toggleComp.ToggledOn();
            toggle.UpdateVisuals(state, false);
        }

        public static void TryRefreshSingleSliderSideScreen(SingleSliderSideScreen sss, GameObject go)
        {
            if (!(go.TryGetComponent(out ISingleSliderControl sssComp) || (sssComp = go.GetSMI<ISingleSliderControl>()) != null)) return;
            RefreshSliderSets(sss.sliderSets, idx => sssComp.GetSliderValue(idx));
        }

        public static void TryRefreshIntSliderSideScreen(IntSliderSideScreen iss, GameObject go)
        {
            if (!(go.TryGetComponent(out IIntSliderControl issComp) || (issComp = go.GetSMI<IIntSliderControl>()) != null)) return;
            RefreshSliderSets(iss.sliderSets, idx => issComp.GetSliderValue(idx));
        }

        public static void TryRefreshMultiSliderSideScreen(MultiSliderSideScreen mss, GameObject go)
        {
            if (!go.TryGetComponent(out IMultiSliderControl mssComp)) return;
            if (mss.sliderSets != null && mssComp.sliderControls != null)
            {
                int count = Mathf.Min(mss.sliderSets.Count, mssComp.sliderControls.Length);
                for (int i = 0; i < count; i++)
                {
                    var set = mss.sliderSets[i];
                    var ctrl = mssComp.sliderControls[i];
                    if (set == null || ctrl == null) continue;
                    float val = ctrl.GetSliderValue(i);
                    if (set.valueSlider != null) set.valueSlider.value = val;
                    if (set.numberInput != null)
                        set.numberInput.SetDisplayValue((Mathf.Round(val * 10f) / 10f).ToString());
                }
            }
        }

        public static void TryRefreshLimitValveSideScreen(LimitValveSideScreen lv, GameObject go)
        {
            if (!go.TryGetComponent(out LimitValve lvComp)) return;
            if (lv.limitSlider != null) lv.limitSlider.value = lv.limitSlider.GetPercentageFromValue(lvComp.Limit);
            if (lv.numberInput != null)
            {
                if (lvComp.displayUnitsInsteadOfMass)
                    lv.numberInput.SetDisplayValue(GameUtil.GetFormattedUnits(
                        Mathf.Max(0f, lvComp.Limit), displaySuffix: false,
                        floatFormatOverride: LimitValveSideScreen.FLOAT_FORMAT));
                else
                    lv.numberInput.SetDisplayValue(GameUtil.GetFormattedMass(
                        Mathf.Max(0f, lvComp.Limit),
                        massFormat: GameUtil.MetricMassFormat.Kilogram,
                        includeSuffix: false, floatFormat: LimitValveSideScreen.FLOAT_FORMAT));
            }
            lv.UpdateAmountLabel();
        }

        public static void TryRefreshAlarmSideScreen(AlarmSideScreen alarm, GameObject go)
        {
            if (!go.TryGetComponent(out LogicAlarm alarmComp)) return;
            if (alarm.nameInputField != null) alarm.nameInputField.SetDisplayValue(alarmComp.notificationName);
            if (alarm.tooltipInputField != null) alarm.tooltipInputField.SetDisplayValue(alarmComp.notificationTooltip);
            if (alarm.pauseCheckmark != null) alarm.pauseCheckmark.enabled = alarmComp.pauseOnNotify;
            if (alarm.zoomCheckmark != null) alarm.zoomCheckmark.enabled = alarmComp.zoomOnNotify;
        }

        public static void TryRefreshCounterSideScreen(CounterSideScreen counter, GameObject go)
        {
            if (!go.TryGetComponent(out LogicCounter counterComp)) return;
            if (counter.maxCountInput != null) counter.maxCountInput.SetAmount((float)counterComp.maxCount);
            if (counter.advancedModeCheckmark != null) counter.advancedModeCheckmark.enabled = counterComp.advancedMode;
        }

        public static void TryRefreshGeoTunerSideScreen(GeoTunerSideScreen geoTuner, GameObject go)
        {
            geoTuner.RefreshOptions();
        }

        public static void TryRefreshRemoteWorkTerminalSidescreen(RemoteWorkTerminalSidescreen rwt, GameObject go)
        {
            rwt.RefreshOptions();
        }

        public static void TryRefreshCheckboxListGroupSideScreen(CheckboxListGroupSideScreen cbGroup, GameObject go)
        {
            cbGroup.Refresh();
        }

        public static void TryRefreshRelatedEntitiesSideScreen(RelatedEntitiesSideScreen related, GameObject go)
        {
            related.RefreshOptions();
        }

        public static void TryRefreshTreeFilterableSideScreen(TreeFilterableSideScreen treeFilter, GameObject go)
        {
            foreach (var kvp in treeFilter.tagRowMap)
            {
                var row = kvp.Value;
                row.RefreshRowElements();
                row.UpdateCheckBoxVisualState();
            }
            treeFilter.UpdateAllCheckBoxVisualState();
        }

        public static void TryRefreshFilterSideScreen(FilterSideScreen filter, GameObject go)
        {
            filter.RefreshUI();
        }

        public static void RefreshSliderSets(List<SliderSet> sliderSets, Func<int, float> getValue)
        {
            if (sliderSets == null) return;
            for (int i = 0; i < sliderSets.Count; i++)
            {
                var set = sliderSets[i];
                if (set == null) continue;
                float val = getValue(i);
                if (set.valueSlider != null) set.valueSlider.value = val;
                if (set.numberInput != null)
                    set.numberInput.SetDisplayValue((Mathf.Round(val * 10f) / 10f).ToString());
            }
        }
    }
}
