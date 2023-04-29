using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.Runtime.InteropServices;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal unsafe class GameObjectHelper {
    public GameObject Object;
    public TargetSettings Settings = new TargetSettings();

    public GameObjectHelper(GameObject obj) {
        Object = obj;
        Settings.Flags = TargetFlags.Any;

        if (Globals.ClientState.LocalPlayer  != null ) {
            if (Object.ObjectId == Globals.ClientState.LocalPlayer.ObjectId) {
                Settings.Flags |= TargetFlags.Self;
            }
        }

        if (IsPlayerCharacter()) {
            GroupManager* gm = GroupManager.Instance();
            Settings.Flags |= TargetFlags.Player;
            foreach (PartyMember member in gm->PartyMembersSpan) {
                if (member.ObjectID == Object.ObjectId) {
                    Settings.Flags |= TargetFlags.Party;
                }
            }

            if ((Settings.Flags & TargetFlags.Party) == 0) {
                foreach (PartyMember member in gm->AllianceMembersSpan) {
                    if (member.ObjectID == Object.ObjectId) {
                        Settings.Flags |= TargetFlags.Alliance;
                    }
                }
            }

            ClassJob ID = (ClassJob)PlayerCharacter.ClassJob.Id;
            Settings.Jobs = (ulong)(1 << (int)ID);
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
        else if (IsBattleChara()) {
            Settings.Flags |= TargetFlags.Enemy;
        }
        else {
            Settings.Flags |= TargetFlags.NPC;
        }
    }

    public Vector3 GetHeadPosition() {
        Vector3 pos = Position;
        pos.Y += GetHeight() - 0.1f;
        return pos;
    }

    public float GetHeight() {
        float height = Marshal.PtrToStructure<float>(Object.Address + 0x124);

        if (height > 0) {
            height -= 0.1f;
        }

        return height;
    }

    public bool IsBattleChara() {
        return Object is BattleChara;
    }

    public bool IsPlayerCharacter() {
        return Object.ObjectKind == ObjectKind.Player;
    }

    public bool IsVisible(bool occlusion) {
        Vector3 safePos = Position;
        safePos.Y += 0.1f;

        if (RealObject->Scale == 0.0f) {
            return false;
        }

        if (Globals.IsVisible(safePos, occlusion)) {
            return Globals.IsVisible(GetHeadPosition(), occlusion);
        }

        return false;
    }

    public unsafe bool IsTargetable() {
        return RealObject->GetIsTargetable();
    }

    public unsafe FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject* RealObject {
        get { return (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)Object.Address; }
    }

    public ObjectKind Kind {
        get { return Object.ObjectKind; }
    }

    public Vector3 Position {
        get { return Object.Position; }
    }

    public float Rotation {
        get {  return Object.Rotation; }
    }

    public float HitboxRadius {
        get { return Object.HitboxRadius; }
    }

    public ulong ObjectId {
        get { return Object.ObjectId; }
    }

    public ulong TargetObjectId {
        get { return Object.TargetObjectId; }
    }

    public GameObject? TargetObject {
        get { return Object.TargetObject; }
    }

    public GameObjectHelper? Target {
        get {
            if (Object.TargetObject == null) {
                return null;
            }

            return new GameObjectHelper(Object.TargetObject);
        }
    }

    public BattleChara BattleChara {
        get { return Object as BattleChara; }
    }

    public PlayerCharacter PlayerCharacter {
        get { return Object as PlayerCharacter; }
    }
}
