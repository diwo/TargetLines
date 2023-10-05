using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.Runtime.InteropServices;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

public static class GameObjectExtensions {
    public static unsafe float GetScale(this GameObject obj) {
        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* _obj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address;
        return _obj->Scale;
    }

    public static bool IsVisible(this GameObject obj, bool occlusion) {
        Vector3 safePos = obj.Position;
        safePos.Y += 0.1f;

        if (obj.GetScale() == 0.0f) {
            return false;
        }

        return Globals.IsVisible(obj.GetHeadPosition(), occlusion);
    }

    public static unsafe bool TargetIsTargetable(this GameObject obj) {
        if (obj.TargetObject == null) {
            return false;
        }
        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* targetobj = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.TargetObject.Address;
        return targetobj->GetIsTargetable();
    }

    public static Vector3 GetHeadPosition(this GameObject obj) {
        Vector3 pos = obj.Position;
        pos.Y += obj.GetCursorHeight() - 0.2f;
        return pos;
    }

    public static float GetCursorHeight(this GameObject obj) {
        return Marshal.PtrToStructure<float>(obj.Address + 0x124);
    }

    public static bool GetIsPlayerCharacter(this GameObject obj) {
        return obj.ObjectKind == ObjectKind.Player;
    }

    public static bool GetIsBattleNPC(this GameObject obj) {
        return obj.ObjectKind == ObjectKind.BattleNpc;
    }

    public static bool GetIsBattleChara(this GameObject obj) {
        return obj is BattleChara;
    }

    public static PlayerCharacter GetPlayerCharacter(this GameObject obj) {
        return obj as PlayerCharacter;
    }

    public static unsafe TargetSettings GetTargetSettings(this GameObject obj) {
        TargetSettings settings = new TargetSettings();
        settings.Flags = TargetFlags.Any;

        if (Service.ClientState.LocalPlayer != null) {
            if (obj.ObjectId == Service.ClientState.LocalPlayer.ObjectId) {
                settings.Flags |= TargetFlags.Self;
            }
        }

        if (obj.GetIsPlayerCharacter()) {
            GroupManager* gm = GroupManager.Instance();
            settings.Flags |= TargetFlags.Player;
            foreach (PartyMember member in gm->PartyMembersSpan) {
                if (member.ObjectID == obj.ObjectId) {
                    settings.Flags |= TargetFlags.Party;
                }
            }

            if ((gm->AllianceFlags & 1) != 0 && (settings.Flags & TargetFlags.Party) != 0) {
                foreach (PartyMember member in gm->AllianceMembersSpan) {
                    if (member.ObjectID == obj.ObjectId) {
                        settings.Flags |= TargetFlags.Alliance;
                    }
                }
            }

            ClassJob ID = (ClassJob)obj.GetPlayerCharacter().ClassJob.Id;
            settings.Jobs = ClassJobToBit(ID);
            if (DPSJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.DPS;
                if (MeleeDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.MeleeDPS;
                }
                else if (PhysicalRangedDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.PhysicalRangedDPS;
                }
                else if (MagicalRangedDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.MagicalRangedDPS;
                }
            }
            else if (HealerJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.Healer;
                if (PureHealerJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.PureHealer;
                }
                else if (ShieldHealerJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.ShieldHealer;
                }
            }
            else if (TankJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.Tank;
            }
            else if (CrafterGathererJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.CrafterGatherer;
            }
        }
        else if (obj.GetIsBattleNPC()) {
            settings.Flags |= TargetFlags.Enemy;
        }
        else {
            settings.Flags |= TargetFlags.NPC;
        }

        return settings;
    }
}

