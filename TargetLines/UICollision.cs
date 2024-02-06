using Dalamud.Interface.Utility;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TargetLines;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct UICollisionInfo {
    public AtkResNode* node;
    public AtkUnitBase* unit;
    public int index;
}

[StructLayout(LayoutKind.Explicit, Size = 0x28)]
internal struct UIRect {
    [FieldOffset(0x00)] public Vector2 pos;
    [FieldOffset(0x08)] public Vector2 size;
    [FieldOffset(0x00)] public Vector2 tl; // identical to pos
    [FieldOffset(0x10)] public Vector2 tr;
    [FieldOffset(0x18)] public Vector2 bl;
    [FieldOffset(0x20)] public Vector2 br;

    public void Initialize() {
        tr.X = tl.X + size.X;
        tr.Y = tl.Y;

        bl.X = tl.X;
        bl.Y = tl.Y + size.Y;

        br.X = tl.X + size.X;
        br.Y = tl.Y + size.Y;
    }

    // returns true if the point is within the rect's bounds
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DoesPointIntersect(Vector2 point) {
        return point.X >= tl.X && point.X <= tr.X &&
               point.Y >= tl.Y && point.Y <= br.Y;
    }

    // returns true if a point within quad is within the rect's bounds
    public bool DoesQuadIntersect(ref Vector2[] quad) {
        foreach (var corner in quad) {
            if (DoesPointIntersect(corner)) {
                return true;
            }
        }

        return false;
    }

    // returns true if the line (p1->p2) intersects the rect
    public bool LineSegmentIntersectsRect(Vector2 p1, Vector2 p2) {
        return LineSegmentsIntersect(p1, p2, tl, tr) ||
               LineSegmentsIntersect(p1, p2, tr, br) ||
               LineSegmentsIntersect(p1, p2, br, bl) ||
               LineSegmentsIntersect(p1, p2, bl, tl);
    }

    // returns true if the lines (a1->a2) and (b1->b2) intersect
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool LineSegmentsIntersect(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2) {
        const float epsilon = 1e-6f;
        float det = (a2.X - a1.X) * (b2.Y - b1.Y) - (b2.X - b1.X) * (a2.Y - a1.Y);
        if (Math.Abs(det) < epsilon) { return false; } // Parallel lines

        float lambda = ((b2.Y - b1.Y) * (b2.X - a1.X) + (b1.X - b2.X) * (b2.Y - a1.Y)) / det;
        float gamma = ((a1.Y - a2.Y) * (b2.X - a1.X) + (a2.X - a1.X) * (b2.Y - a1.Y)) / det;
        return (0 < lambda && lambda < 1) && (0 < gamma && gamma < 1);
    }

    // returns true if the quad is occluded by this rect
    public bool CheckSegmentIsOccluded(ref Vector2[] quad_points) {
        if (DoesQuadIntersect(ref quad_points)) {
            return true;
        }

        for (int index = 0; index < quad_points.Length; index++) {
            Vector2 start_point = quad_points[index];
            Vector2 end_point = quad_points[(index + 1) % quad_points.Length];

            if (LineSegmentIntersectsRect(start_point, end_point)) {
                return true;
            }
        }

        return false;
    }
}

public static class UICollision {
    public static List<UICollisionInfo> CollisionDebug = new List<UICollisionInfo>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool GetNodeVisible(AtkResNode* node) {
        if (node == null) {
            return false;
        }

        while (node != null) {
            if ((node->NodeFlags & NodeFlags.Visible) != NodeFlags.Visible) {
                return false;
            }
            if ((node->NodeFlags & NodeFlags.Enabled) != NodeFlags.Enabled) {
                return false;
            }
            if (node->Color.A == 0) {
                return false;
            }
            if (node->Alpha_2 == 0) {
                return false;
            }

            node = node->ParentNode;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodePosition(AtkResNode* node) {
        Vector2 pos = new Vector2(node->X, node->Y);
        AtkResNode* parent = node->ParentNode;

        while (parent != null) {
            pos *= new Vector2(parent->ScaleX, parent->ScaleY);
            pos += new Vector2(parent->X, parent->Y);
            parent = parent->ParentNode;
        }
        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodeScale(AtkResNode* node) {
        if (node == null) {
            Service.Logger.Warning("Node is null");
            return new Vector2(1, 1);
        }

        Vector2 scale = new Vector2(node->ScaleX, node->ScaleY);
        while (node->ParentNode != null) {
            node = node->ParentNode;
            scale *= new Vector2(node->ScaleX, node->ScaleY);
        }
        return scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodeScaledSize(AtkResNode* node) {
        if (node == null) {
            Service.Logger.Warning("Node is null");
            return new Vector2(1, 1);
        }

        Vector2 scale = GetNodeScale(node);
        Vector2 size = new Vector2(node->Width, node->Height) * scale;
        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool CheckSegmentIsOccluded_AtkUnitBase(AtkUnitBase* unit, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
        if (unit->IsVisible) {
            Vector2[] quad_points = { p1, p2, p3, p4 };
            UIRect* rect = stackalloc UIRect[1];
            rect->pos.X = unit->X;
            rect->pos.Y = unit->Y;
            rect->size.X = unit->GetScaledWidth(true);
            rect->size.Y = unit->GetScaledHeight(true);
            rect->Initialize();

            return rect->CheckSegmentIsOccluded(ref quad_points);
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool CheckSegmentIsOccluded_AtkResNode(AtkResNode* node, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
        if (GetNodeVisible(node) && (node->NodeFlags & NodeFlags.RespondToMouse) != 0) {
            Vector2[] quad_points = { p1, p2, p3, p4 };
            UIRect* rect = stackalloc UIRect[1];
            rect->pos = GetNodePosition(node);
            rect->size = GetNodeScaledSize(node);
            rect->Initialize();

            return rect->CheckSegmentIsOccluded(ref quad_points);
        }
        return false;
    }

    public static unsafe bool OcclusionCheck(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
        RaptureAtkUnitManager* manager = AtkStage.GetSingleton()->RaptureAtkUnitManager;
        if (manager == null) {
            return false;
        }

        var group = GroupManager.Instance();
        byte isAlliance = 0;
        if (group != null) {
            isAlliance = Marshal.ReadByte((IntPtr)group + 0x3D5E);
        }

        foreach (var _entry in manager->AtkUnitManager.AllLoadedUnitsList.EntriesSpan) {
            if (_entry.Value == null) {
                continue;
            }

            var entry = _entry.Value;
            string? name = Marshal.PtrToStringAnsi(new IntPtr(entry->Name));
            if (name == "NamePlate") {
                continue;
            }
                
            if (isAlliance == 0) {
                if (name == "_AllianceList1" || name == "_AllianceList2") {
                    continue;
                }
            }

            if (CheckSegmentIsOccluded_AtkUnitBase(entry, p1, p2, p3, p4)) {
                for (int index = 0; index < entry->CollisionNodeListCount; index++) {
                    var node = entry->CollisionNodeList[index];
                    if (node != null) {
                        if (CheckSegmentIsOccluded_AtkResNode(node, p1, p2, p3, p4)) {
                            if (Globals.Config.saved.DebugUICollision) {
                                CollisionDebug.Add(new UICollisionInfo { node = node, index = index, unit = entry });
                            }
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    public static unsafe void DrawOutline(AtkResNode* node, string name, uint bg, uint outline) {
        Vector2 position = GetNodePosition(node);
        Vector2 size = GetNodeScaledSize(node);

        position += ImGuiHelpers.MainViewport.Pos;
        ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRectFilled(position, position + size, bg);
        ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(position, position + size, outline);
        ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddText(position, outline, name);
    }

    public static unsafe void DrawDebugOutlines() {
        if (Globals.Config.saved.DebugUICollision && CollisionDebug != null) {
            foreach (var col in CollisionDebug) {
                if (col.unit == null || col.node == null) { continue; }
                string? name = Marshal.PtrToStringAnsi(new IntPtr(col.unit->Name));
                DrawOutline(col.node, $"[{col.index}] {name}->{col.node->NodeID}", 0x01102010, 0xD08080A0);
            }
        }
        CollisionDebug?.Clear();
    }
}
