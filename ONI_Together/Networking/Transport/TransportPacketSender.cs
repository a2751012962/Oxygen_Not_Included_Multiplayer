using System.Collections.Generic;
using ONI_Together.Networking.Packets.Architecture;
using UnityEngine;

namespace ONI_Together.Networking.Transport
{
    public abstract class TransportPacketSender
    {
        private readonly Dictionary<object, Queue<(IPacket packet, PacketSendMode sendMode)>> _pendingQueues = new Dictionary<object, Queue<(IPacket packet, PacketSendMode sendMode)>>();
        private readonly List<object> _emptyConnections = new List<object>();
        public int MaxPacketsPerSecond { get; set; } = 0;   // 0 = unlimited

        public bool SendToConnection(object conn, IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate)
        {
            if (MaxPacketsPerSecond <= 0)
                return SendPacket(conn, packet, sendType);
            // queue it
            if (!_pendingQueues.TryGetValue(conn, out var queue))
                _pendingQueues[conn] = queue = new();
            queue.Enqueue((packet, sendType));
            return true;
        }

        public void Flush()
        {
            // Unlimited mode, there shouldn't be any pending but just in case.
            if (MaxPacketsPerSecond <= 0)
            {
                foreach (var kvp in _pendingQueues)
                    while (kvp.Value.Count > 0)
                    {
                        var (packet, sendType) = kvp.Value.Dequeue();
                        SendPacket(kvp.Key, packet, sendType);
                    }
                _pendingQueues.Clear();
                return;
            }

            int maxThisTick = (int)(MaxPacketsPerSecond * Time.unscaledDeltaTime);
            if (maxThisTick < 1) maxThisTick = 1;

            // Limited mode, drain up to maxThisTick per connection
            _emptyConnections.Clear();
            foreach (var kvp in _pendingQueues)
            {
                int sent = 0;
                while (kvp.Value.Count > 0 && sent < maxThisTick)
                {
                    var (packet, sendType) = kvp.Value.Dequeue();
                    SendPacket(kvp.Key, packet, sendType);
                    sent++;
                }
                if (kvp.Value.Count == 0)
                    _emptyConnections.Add(kvp.Key);
            }

            foreach (var key in _emptyConnections)
                _pendingQueues.Remove(key);
        }

        public abstract bool SendPacket(object conn, IPacket packet, PacketSendMode sendType = PacketSendMode.ReliableImmediate);

    }
}
