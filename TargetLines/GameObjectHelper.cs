using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal unsafe class GameObjectHelper : GameObjectWrapper {
    public TargetSettings Settings = new TargetSettings();

    public GameObjectHelper(GameObject obj) : base(obj) {
        UpdateTargetSettings();
    }

    public GameObjectHelper(IntPtr address) : base(address) {
        UpdateTargetSettings();
    }

    public void UpdateTargetSettings() {
        Settings.Flags = TargetFlags.Any;

        if (Service.ClientState.LocalPlayer != null) {
            if (ObjectId == Service.ClientState.LocalPlayer.ObjectId) {
                Settings.Flags |= TargetFlags.Self;
            }
        }

        if (IsPlayerCharacter) {
            GroupManager* gm = GroupManager.Instance();
            Settings.Flags |= TargetFlags.Player;
            foreach (PartyMember member in gm->PartyMembersSpan) {
                if (member.ObjectID == ObjectId) {
                    Settings.Flags |= TargetFlags.Party;
                }
            }

            if ((gm->AllianceFlags & 1) != 0 && (Settings.Flags & TargetFlags.Party) != 0) {
                foreach (PartyMember member in gm->AllianceMembersSpan) {
                    if (member.ObjectID == ObjectId) {
                        Settings.Flags |= TargetFlags.Alliance;
                    }
                }
            }

            ClassJob ID = (ClassJob)PlayerCharacter.ClassJob.Id;
            Settings.Jobs = ClassJobToBit(ID);
            if (DPSJobs.Contains(ID)) {
                Settings.Flags |= TargetFlags.DPS;
                if (MeleeDPSJobs.Contains(ID)) {
                    Settings.Flags |= TargetFlags.MeleeDPS;
                }
                else if (PhysicalRangedDPSJobs.Contains(ID)) {
                    Settings.Flags |= TargetFlags.PhysicalRangedDPS;
                }
                else if (MagicalRangedDPSJobs.Contains(ID)) {
                    Settings.Flags |= TargetFlags.MagicalRangedDPS;
                }
            }
            else if (HealerJobs.Contains(ID)) {
                Settings.Flags |= TargetFlags.Healer;
                if (PureHealerJobs.Contains(ID)) {
                    Settings.Flags |= TargetFlags.PureHealer;
                }
                else if (ShieldHealerJobs.Contains(ID)) {
                    Settings.Flags |= TargetFlags.ShieldHealer;
                }
            }
            else if (TankJobs.Contains(ID)) {
                Settings.Flags |= TargetFlags.Tank;
            }
            else if (CrafterGathererJobs.Contains(ID)) {
                Settings.Flags |= TargetFlags.CrafterGatherer;
            }
        }
        else if (IsBattleChara) {
            Settings.Flags |= TargetFlags.Enemy;
        }
        else {
            Settings.Flags |= TargetFlags.NPC;
        }
    }

    public bool IsVisible(bool occlusion) {
        Vector3 safePos = Position;
        safePos.Y += 0.1f;

        if (Struct->Scale == 0.0f) {
            return false;
        }

        return Globals.IsVisible(GetHeadPosition(), occlusion);
    }
}

