using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using ONI_Together.Misc;
using ONI_Together.Networking;
using ONI_Together.Networking.Packets.Architecture;
using Shared.Profiling;
using Steamworks;
using UnityEngine;

namespace ONI_Together.DebugTools
{
    public class PacketTracker
    {
        private static PacketTracker _instance;
        public int _nextTrackId;
        private bool showWindow = false;

        private string outgoing_filter = string.Empty;
        private string incoming_filter = string.Empty;

        // Used for imgui packet tracking
        public struct PacketTrackData
        {
            public IPacket packet;
            public int size;
            public int TrackId;
        }

        private List<PacketTrackData> incoming_tracked = new List<PacketTrackData>();
        private List<PacketTrackData> outgoing_tracked = new List<PacketTrackData>();
        private const int MAX_TRACKED_LIMIT = 100;

        private int incoming_count;
        private int outgoing_count;
        private float incoming_pps;
        private float outgoing_pps;
        private float last_pps_time;

        private class InspectWindowState
        {
            public int Id;
            public PacketTrackData Data;
            public bool Open = true;
        }
        private List<InspectWindowState> _inspectWindows = new();
        private int _nextWindowId;

        public static PacketTracker Init()
        {
            using var _ = Profiler.Scope();

            if (_instance != null)
                return _instance;

            _instance = new PacketTracker();
            return _instance;
        }

        public static void TrackSent(PacketTrackData data)
        {
            using var _ = Profiler.Scope();

            data.TrackId = _instance._nextTrackId++;
            _instance.outgoing_tracked.Add(data);
            _instance.outgoing_count++;

            if (_instance.outgoing_tracked.Count > MAX_TRACKED_LIMIT)
            {
                int overflow = _instance.outgoing_tracked.Count - MAX_TRACKED_LIMIT;
                _instance.outgoing_tracked.RemoveRange(0, overflow);
            }

        }

        public static void TrackIncoming(PacketTrackData data)
        {
            using var _ = Profiler.Scope();

            data.TrackId = _instance._nextTrackId++;
            _instance.incoming_tracked.Add(data);
            _instance.incoming_count++;

            if (_instance.incoming_tracked.Count > MAX_TRACKED_LIMIT)
            {
                int overflow = _instance.incoming_tracked.Count - MAX_TRACKED_LIMIT;
                _instance.incoming_tracked.RemoveRange(0, overflow);
            }
        }

        public void Clear()
        {
            using var _ = Profiler.Scope();

            _instance.outgoing_tracked.Clear();
            _instance.incoming_tracked.Clear();
            _instance.outgoing_count = 0;
            _instance.incoming_count = 0;
            _instance.incoming_pps = 0;
            _instance.outgoing_pps = 0;
            _instance._inspectWindows.Clear();
        }

        private void CalculatePPS()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            float elapsed = now - last_pps_time;

            if (elapsed >= 1f)
            {
                incoming_pps = incoming_count / elapsed;
                outgoing_pps = outgoing_count / elapsed;
                incoming_count = 0;
                outgoing_count = 0;
                last_pps_time = now;
            }
        }

        public void Toggle()
        {
            using var _ = Profiler.Scope();

            showWindow = !showWindow;
        }

        public void ShowWindow()
        {
            using var _ = Profiler.Scope();

            if (!showWindow)
                return;

            if (ImGui.Begin("Packet Tracker", ref showWindow))
            {
                if (!MultiplayerSession.InSession)
                {
                    if (outgoing_tracked.Count > 0)
                        Clear();

                    ImGui.TextDisabled("Not in a session!");
                }
                else
                {
                    CalculatePPS();
                    ImGui.Text($"In: {incoming_pps:F1} pps   Out: {outgoing_pps:F1} pps");
                    if (ImGui.CollapsingHeader("Incoming Packets"))
                    {
                        ImGui.InputText("Filter", ref incoming_filter, 64);
                        ImGui.Separator();

                        AddTable("incoming_packets_table", incoming_tracked, incoming_filter);
                    }

                    if (ImGui.CollapsingHeader("Outgoing Packets"))
                    {
                        ImGui.InputText("Filter", ref outgoing_filter, 64);
                        ImGui.Separator();

                        AddTable("outgoing_packets_table", outgoing_tracked, outgoing_filter);
                    }

                    DrawInspectWindows();
                }
            }

            ImGui.End();
        }

        public void ShowInTab()
        {
            using var _ = Profiler.Scope();

            if (!MultiplayerSession.InSession)
            {
                if (outgoing_tracked.Count > 0)
                    Clear();

                ImGui.TextDisabled("Not in a session!");
                return;
            }

            CalculatePPS();
            ImGui.Text($"In: {incoming_pps:F1} pps   Out: {outgoing_pps:F1} pps");
            ImGui.Separator();

            if (ImGui.CollapsingHeader("Incoming Packets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.InputText("Filter##Incoming", ref incoming_filter, 64);
                ImGui.Separator();

                AddTable(
                    "incoming_packets_table_tab",
                    incoming_tracked,
                    incoming_filter
                );
            }

            ImGui.Separator();

            if (ImGui.CollapsingHeader("Outgoing Packets", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.InputText("Filter##Outgoing", ref outgoing_filter, 64);
                ImGui.Separator();

                AddTable(
                    "outgoing_packets_table_tab",
                    outgoing_tracked,
                    outgoing_filter
                );
            }

            DrawInspectWindows();
        }

        private void OpenInspectWindow(PacketTrackData data)
        {
            _inspectWindows.RemoveAll(w => !w.Open);
            _inspectWindows.Add(new InspectWindowState
            {
                Id = _nextWindowId++,
                Data = data,
                Open = true
            });
        }

        private void DrawInspectWindows()
        {
            _inspectWindows.RemoveAll(w => !w.Open);
            foreach (var win in _inspectWindows)
            {
                if (!win.Open) continue;

                var data = win.Data;
                if (data.packet == null) { win.Open = false; continue; }

                var type = data.packet.GetType();
                string title = $"{type.Name}##inspect_{win.Id}";

                if (ImGui.Begin(title, ref win.Open))
                {
                    ImGui.TextDisabled($"Track ID: {data.TrackId}  |  Size: {Utils.FormatBytes(data.size)}");
                    ImGui.Separator();

                    var ifaces = type.GetInterfaces()
                        .Where(i => i != typeof(IPacket))
                        .Select(i => i.Name)
                        .ToList();
                    if (ifaces.Count > 0)
                        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f),
                            $"Implements: {string.Join(", ", ifaces)}");
                    ImGui.Spacing();

                    if (ImGui.BeginTable("pf", 4,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
                    {
                        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("Size", ImGuiTableColumnFlags.WidthFixed, 50);
                        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();

                        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .OrderBy(f => f.Name)
                            .ToList();

                        if (fields.Count == 0)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.TextDisabled("(no public instance fields)");
                        }

                        foreach (var field in fields)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();
                            ImGui.Text(field.Name);
                            ImGui.TableNextColumn();
                            ImGui.Text(GetFriendlyTypeName(field.FieldType));
                            ImGui.TableNextColumn();
                            ImGui.Text(GetFieldSizeHint(field.FieldType));
                            ImGui.TableNextColumn();
                            try
                            {
                                ImGui.Text(FormatFieldValue(field.GetValue(data.packet), field.FieldType));
                            }
                            catch
                            {
                                ImGui.TextDisabled("?");
                            }
                        }

                        ImGui.EndTable();
                    }
                }
                ImGui.End();
            }
        }

        private void AddTable(string str_id, List<PacketTrackData> dataset, string filter)
        {
            using var _ = Profiler.Scope();

            if (ImGui.BeginTable(str_id, 3,
                    ImGuiTableFlags.Borders |
                    ImGuiTableFlags.RowBg |
                    ImGuiTableFlags.ScrollY, new Vector2(0, 400)))
            {
                ImGui.TableSetupColumn("Packet Type");
                ImGui.TableSetupColumn("Packet ID");
                ImGui.TableSetupColumn("Size (bytes)");

                ImGui.TableHeadersRow();

                for (int i = dataset.Count - 1; i >= 0; i--)
                {
                    var entry = dataset[i];

                    string typeName = entry.packet.GetType().Name;
                    string idString = entry.packet.GetType().GetHashCode().ToString();

                    if (!string.IsNullOrEmpty(filter))
                    {
                        bool matchesType =
                            typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                        bool matchesId =
                            idString.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!matchesType && !matchesId)
                            continue;
                    }

                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    ImGui.PushID(entry.TrackId);
                    if (ImGui.Selectable(typeName, false, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        OpenInspectWindow(entry);
                    }
                    ImGui.PopID();

                    ImGui.TableNextColumn();
                    ImGui.Text(idString);

                    ImGui.TableNextColumn();
                    ImGui.Text(Utils.FormatBytes(entry.size));
                }

                ImGui.EndTable();
            }
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var name = type.GetGenericTypeDefinition().Name;
                name = name.Substring(0, name.IndexOf('`'));
                var args = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
                return $"{name}<{args}>";
            }
            if (type.IsEnum) return "enum";
            return type.Name;
        }

        private static string GetFieldSizeHint(Type type)
        {
            if (type == typeof(bool) || type == typeof(byte) || type == typeof(sbyte)) return "1";
            if (type == typeof(short) || type == typeof(ushort))                   return "2";
            if (type == typeof(int) || type == typeof(uint) || type == typeof(float)) return "4";
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return "8";
            if (type == typeof(Vector2))  return "8";
            if (type == typeof(Vector3))  return "12";
            if (type == typeof(Vector4) || type == typeof(Quaternion)) return "16";
            if (type == typeof(Color))    return "16";
            if (type == typeof(string))   return "~";
            if (type.IsEnum) return GetFieldSizeHint(Enum.GetUnderlyingType(type));
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return "~";
            if (type.IsArray)  return "~";
            if (!type.IsValueType) return "~";
            return "?";
        }

        private static string FormatFieldValue(object val, Type type)
        {
            if (val == null) return "null";
            if (type == typeof(Vector3))
            {
                var v = (Vector3)val;
                return $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
            }
            if (type == typeof(Vector2))
            {
                var v = (Vector2)val;
                return $"({v.x:F3}, {v.y:F3})";
            }
            if (type == typeof(Color))
            {
                var c = (Color)val;
                return $"({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";
            }
            if (type == typeof(string))
            {
                var s = (string)val;
                if (string.IsNullOrEmpty(s)) return "\"\"";
                if (s.Length > 60) return $"\"{s.Substring(0, 60)}...\"";
                return $"\"{s}\"";
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = (System.Collections.IList)val;
                return $"[Count: {list?.Count ?? 0}]";
            }
            if (type.IsArray)
            {
                var arr = (Array)val;
                return $"[Length: {arr?.Length ?? 0}]";
            }
            if (type.IsEnum) return $"{val}";
            return val.ToString() ?? "";
        }
    }
}
