using Dalamud.Interface.Utility;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TargetLines;

public static class UICollision {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool GetPointIntersectsRect(Vector2 point, Vector2 rect_pos, Vector2 rect_size) {
        return point.X >= rect_pos.X &&
                   point.Y >= rect_pos.Y &&
                   point.X <= rect_pos.X + rect_size.X &&
                   point.Y <= rect_pos.Y + rect_size.Y;
    }

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
            Vector2* pos_size = stackalloc Vector2[2];
            pos_size[0].X = unit->X;
            pos_size[0].Y = unit->Y;
            pos_size[1].X = unit->GetScaledWidth(true);
            pos_size[1].Y = unit->GetScaledHeight(true);

            if (GetPointIntersectsRect(p1, pos_size[0], pos_size[1])) {
                return true;
            }

            if (GetPointIntersectsRect(p2, pos_size[0], pos_size[1])) {
                return true;
            }

            if (GetPointIntersectsRect(p3, pos_size[0], pos_size[1])) {
                return true;
            }

            if (GetPointIntersectsRect(p4, pos_size[0], pos_size[1])) {
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool CheckSegmentIsOccluded_AtkResNode(AtkResNode* node, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4) {
        if (GetNodeVisible(node) && (node->NodeFlags & NodeFlags.RespondToMouse) != 0) {
            Vector2 pos = GetNodePosition(node);
            Vector2 size = GetNodeScaledSize(node);

            if (GetPointIntersectsRect(p1, pos, size)) {
                return true;
            }

            if (GetPointIntersectsRect(p2, pos, size)) {
                return true;
            }

            if (GetPointIntersectsRect(p3, pos, size)) {
                return true;
            }

            if (GetPointIntersectsRect(p4, pos, size)) {
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                                DrawOutline(node, $"[{index}] {name}->{node->NodeID}", 0x01102010, 0xD08080A0);
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
}
