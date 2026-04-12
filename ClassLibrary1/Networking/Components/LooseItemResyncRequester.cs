using ONI_MP.Misc;
using ONI_MP.Networking.Packets.World;
using Shared.Profiling;
using UnityEngine;

namespace ONI_MP.Networking.Components
{
	internal class LooseItemResyncRequester : MonoBehaviour
	{
		private const float InitialDelay = 1f;
		private const float TickInterval = 0.5f;
		private const int ShardCount = 5;

		private float _nextRequestTime = float.MaxValue;
		private int _currentShard;

		private void Update()
		{
			using var _ = Profiler.Scope();

			if (!MultiplayerSession.IsClient || !MultiplayerSession.InSession || !Utils.IsInGame())
			{
				_nextRequestTime = float.MaxValue;
				_currentShard = 0;
				return;
			}

			if (_nextRequestTime == float.MaxValue)
				_nextRequestTime = Time.unscaledTime + InitialDelay;

			if (Time.unscaledTime < _nextRequestTime)
				return;

			RequestVisibleShard();
			_nextRequestTime = Time.unscaledTime + TickInterval;
		}

		private void RequestVisibleShard()
		{
			using var _ = Profiler.Scope();

			if (!WorldStateSyncer.TryGetLocalViewport(out var viewport, 2))
				return;

			// Rotate the visible scan so each request stays small even in debris-heavy rooms.
			PacketSender.SendToHost(new LooseItemSyncRequestPacket
			{
				RequesterId = MultiplayerSession.LocalUserID,
				X = viewport.xMin,
				Y = viewport.yMin,
				Width = viewport.width,
				Height = viewport.height,
				ShardIndex = _currentShard,
				ShardCount = ShardCount
			}, PacketSendMode.Unreliable);

			_currentShard = (_currentShard + 1) % ShardCount;
		}
	}
}
