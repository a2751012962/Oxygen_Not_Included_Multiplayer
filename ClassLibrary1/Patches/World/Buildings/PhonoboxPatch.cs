using HarmonyLib;
using ONI_MP.Networking;
using Shared.Profiling;

namespace ONI_MP.Patches.World.Buildings
{
	internal class PhonoboxPatch
	{
		[HarmonyPatch(typeof(Phonobox), nameof(Phonobox.UpdateChores))]
		public class Phonobox_UpdateChores_Patch
		{
			public static bool Prefix()
			{
				using var _ = Profiler.Scope();
				return !MultiplayerSession.IsClient;
			}
		}
	}
}
