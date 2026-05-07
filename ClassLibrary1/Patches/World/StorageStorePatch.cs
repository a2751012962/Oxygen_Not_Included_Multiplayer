using HarmonyLib;
using ONI_MP.DebugTools;
using ONI_MP.Networking;
using ONI_MP.Networking.Components;
using ONI_MP.Networking.Packets.World;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Patches.World
{
	// Pickupable.OnCleanUp only fires when the object is destroyed. Items that are
	// reparented into Storage (seeds into planters, eggs into incubators, live
	// critters, non-stackable items) stay alive and never trigger OnCleanUp, so
	// clients keep rendering them on the ground. Mirror the pickup on Store() so
	// the existing GroundItemPickedUpPacket path removes the client-side ghost.
	[HarmonyPatch(typeof(Storage), nameof(Storage.Store), new System.Type[] { typeof(GameObject), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
	public static class StorageStorePatch
	{
		public static void Postfix(Storage __instance, GameObject go, bool hide_popups, bool block_events, bool do_disease_transfer, bool is_deserializing)
		{
			using var _ = Profiler.Scope();
			try
			{
				if (!MultiplayerSession.IsHost || !MultiplayerSession.InSession)
					return;
				if (go == null)
					return;

				var identity = go.GetNetIdentity();
				if (identity == null || identity.NetId == 0)
					return;

                //PacketSender.SendToAllClients(new GroundItemPickedUpPacket { NetId = identity.NetId });

				var storageIdentity = __instance.GetNetIdentity();
				if (storageIdentity == null || storageIdentity.NetId == 0)
					return;

				PacketSender.SendToAllClients(new StoreItemPacket { 
					NetId = identity.NetId,
					StorageNetId = storageIdentity.NetId,
					DoDiseaseTransfer = do_disease_transfer,
					FxPrefix = Storage.FXPrefix.Delivered
				});
            }
            catch (System.Exception ex)
			{
				DebugConsole.LogError($"[StorageStorePatch] Exception: {ex}");
			}
		}
	}
}
