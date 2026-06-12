using System.Collections.Generic;
using UnityEngine;

namespace ONI_Together.Networking
{
	public class PlayerUtilityVisualizer
	{
		private const int FIRST_CELL_BITS = 17;
		private const int SEG_BITS = 4;
		private const int SEG_COUNT_BITS = 2;
		private const int MAX_SEGMENTS = 3;
		private const int MAX_LEN_PER_SEG = 4;
		private const int MAX_CELLS_PER_CHUNK = 1 + MAX_SEGMENTS * MAX_LEN_PER_SEG; // 13

		private Dictionary<int, GameObject> _visualizers = new Dictionary<int, GameObject>();
		private string _currentPrefabId = string.Empty;
		private BuildingDef _currentDef;

		private Color _color = Color.white;
		public Color Color
		{
			get => _color;
			set => _color = value;
		}

		public void UpdatePath(string prefabId, uint[] pathData, Color color)
		{
			_color = color;

			if (string.IsNullOrEmpty(prefabId) || pathData == null || pathData.Length == 0)
			{
				ClearPath();
				return;
			}

			if (prefabId != _currentPrefabId)
			{
				ClearPath();
				_currentPrefabId = prefabId;
				_currentDef = Assets.GetBuildingDef(prefabId);
				if (_currentDef == null)
				{
					_currentPrefabId = string.Empty;
					return;
				}
			}

			int[] cells = DecodePath(pathData);
			if (cells == null || cells.Length == 0)
			{
				ClearPath();
				return;
			}

			HashSet<int> currentCells = new HashSet<int>(cells);

			List<int> toRemove = new List<int>();
			foreach (int cell in _visualizers.Keys)
			{
				if (!currentCells.Contains(cell))
					toRemove.Add(cell);
			}
			foreach (int cell in toRemove)
				DestroySingleVisualizer(cell);

			for (int i = 0; i < cells.Length; i++)
			{
				int cell = cells[i];

				if (!_visualizers.ContainsKey(cell))
					CreateSingleVisualizer(cell);

				UpdateSingleVisualizer(cell, i, cells);
			}
		}

		public void ClearPath()
		{
			foreach (var kvp in _visualizers)
				Util.KDestroyGameObject(kvp.Value);

			_visualizers.Clear();
			_currentPrefabId = string.Empty;
			_currentDef = null;
		}

		public static int[] DecodePath(uint[] pathData)
		{
			if (pathData == null || pathData.Length == 0)
				return null;

			List<int> cells = new List<int>(pathData.Length * MAX_CELLS_PER_CHUNK);

			foreach (uint chunk in pathData)
			{
				if (chunk == 0)
					continue;

				int[] chunkCells = DecodeChunk(chunk);
				if (chunkCells != null)
					cells.AddRange(chunkCells);
			}

			return cells.ToArray();
		}

		private static int[] DecodeChunk(uint data)
		{
			if (data == 0)
				return null;

			int firstCell = (int)(data & ((1 << FIRST_CELL_BITS) - 1));
			int segmentsPacked = (int)((data >> FIRST_CELL_BITS) & ((1 << (SEG_BITS * MAX_SEGMENTS)) - 1));
			int segmentCount = (int)((data >> (FIRST_CELL_BITS + SEG_BITS * MAX_SEGMENTS)) & ((1 << SEG_COUNT_BITS) - 1));

			if (!Grid.IsValidCell(firstCell))
				return null;

			List<int> cells = new List<int>(MAX_CELLS_PER_CHUNK);
			cells.Add(firstCell);
			int cell = firstCell;

			for (int s = 0; s < segmentCount && s < MAX_SEGMENTS; s++)
			{
				int seg = (segmentsPacked >> (s * SEG_BITS)) & 0xF;
				int dir = seg & 0x3;
				int len = ((seg >> 2) & 0x3) + 1;

				int delta;
				switch (dir)
				{
					case 0: delta = 1; break;
					case 1: delta = Grid.WidthInCells; break;
					case 2: delta = -1; break;
					case 3: delta = -Grid.WidthInCells; break;
					default: continue;
				}

				for (int i = 0; i < len; i++)
				{
					cell += delta;
					if (!Grid.IsValidCell(cell))
						break;
					cells.Add(cell);
				}
			}

			return cells.ToArray();
		}

		private void CreateSingleVisualizer(int cell)
		{
			if (_currentDef == null)
				return;

			Vector3 pos = Grid.CellToPosCBC(cell, _currentDef.SceneLayer);

			GameObject go = new GameObject("PlayerUtilityPathVis");
			go.SetActive(false);

			KBatchedAnimController anim = go.AddComponent<KBatchedAnimController>();
			anim.isMovable = true;
			anim.sceneLayer = _currentDef.SceneLayer;
			anim.AnimFiles = _currentDef.AnimFiles;
			anim.defaultAnim = "place";
			anim.visibilityType = KAnimControllerBase.VisibilityType.Always;
			anim.Offset = Vector3.zero;
			anim.SetLayer(LayerMask.NameToLayer("Place"));

			go.transform.SetPosition(pos);
			_visualizers[cell] = go;
		}

		private void UpdateSingleVisualizer(int cell, int index, int[] cells)
		{
			if (!_visualizers.TryGetValue(cell, out GameObject go))
				return;

			UtilityConnections connections = (UtilityConnections)0;

			if (index > 0)
				connections |= UtilityConnectionsExtensions.DirectionFromToCell(cell, cells[index - 1]);
			if (index < cells.Length - 1)
				connections |= UtilityConnectionsExtensions.DirectionFromToCell(cell, cells[index + 1]);

			// I don't think I need this tbh
			if (_currentDef != null)
			{
				GameObject existing = Grid.Objects[cell, (int)_currentDef.TileLayer];
				if (existing != null && existing.TryGetComponent<KAnimGraphTileVisualizer>(out var vis))
					connections |= vis.Connections;

				if (_currentDef.ReplacementLayer != ObjectLayer.NumLayers)
				{
					existing = Grid.Objects[cell, (int)_currentDef.ReplacementLayer];
					if (existing != null && existing.TryGetComponent<KAnimGraphTileVisualizer>(out vis))
						connections |= vis.Connections;
				}
			}

			string connStr = GetConnectionsString(connections);
			string animName = connStr + "_place";

			KBatchedAnimController kbac = go.GetComponent<KBatchedAnimController>();
			if (kbac.HasAnimation(animName))
				kbac.Play(animName);
			else
				kbac.Play(connStr);

			kbac.TintColour = Color.Lerp(_color, Color.white, 0.75f);
			go.SetActive(true);
		}

		private void DestroySingleVisualizer(int cell)
		{
			if (_visualizers.TryGetValue(cell, out GameObject go))
			{
				Util.KDestroyGameObject(go);
				_visualizers.Remove(cell);
			}
		}

		private static string GetConnectionsString(UtilityConnections connections)
		{
			string text = "";
			if ((connections & UtilityConnections.Left) != (UtilityConnections)0) text += "L";
			if ((connections & UtilityConnections.Right) != (UtilityConnections)0) text += "R";
			if ((connections & UtilityConnections.Up) != (UtilityConnections)0) text += "U";
			if ((connections & UtilityConnections.Down) != (UtilityConnections)0) text += "D";
			if (text == "") text = "None";
			return text;
		}
	}
}
