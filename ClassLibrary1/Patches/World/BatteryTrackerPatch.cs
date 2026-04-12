using HarmonyLib;
using ONI_MP.Networking;
using Shared.Profiling;

namespace ONI_MP.Patches.World
{
	[HarmonyPatch(typeof(BatteryTracker), "UpdateData")]
	public static class BatteryTrackerPatch
	{
		public static bool Prefix(BatteryTracker __instance)
		{
			using var _ = Profiler.Scope();

			if (GameClient.IsHardSyncInProgress)
				return false;

			return true;
		}
	}
}
