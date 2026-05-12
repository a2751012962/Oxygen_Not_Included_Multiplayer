using HarmonyLib;
using ONI_MP.Networking;
using Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ONI_MP.Patches.GamePatches
{
	internal class BlockTileRenderer_Patches
	{

        [HarmonyPatch(typeof(BlockTileRenderer ), nameof(BlockTileRenderer.GetCellColour))]
        public class BlockTileRenderer_TargetMethod_Patch
		{
			public static void Postfix(int cell, SimHashes element, ref Color __result)
			{
				if (element == SimHashes.Void && PlayerBuildingVisualizer.ColoredCells.ContainsKey(cell))
				{
					__result = PlayerBuildingVisualizer.ColoredCells[cell];
				}
			}
		}
	}
}
