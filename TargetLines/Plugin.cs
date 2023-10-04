using Dalamud.Plugin;
using System;
using ImGuiNET;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using System.IO;
using Dalamud.Plugin.Services;
using DrahsidLib;

namespace TargetLines;

public class Plugin : IDalamudPlugin {
    private DalamudPluginInterface PluginInterface;
    private IChatGui Chat { get; init; }
    private IClientState ClientState { get; init; }
    private ICommandManager CommandManager { get; init; }

    public string Name => "TargetLines";

    private const ImGuiWindowFlags OVERLAY_WINDOW_FLAGS =
          ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoNav;

    private Dictionary<uint, TargetLine> TargetLineDict;

    public Plugin(DalamudPluginInterface pluginInterface, ICommandManager commandManager, IChatGui chat, IClientState clientState) {
        PluginInterface = pluginInterface;
        Chat = chat;
        ClientState = clientState;
        CommandManager = commandManager;

        DrahsidLib.DrahsidLib.Initialize(pluginInterface, DrawTooltip);

        InitializeCommands();
        InitializeConfig();
        InitializeUI();

        TargetLineDict = new Dictionary<uint, TargetLine>();

        var texture_line_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLine.png");
        var texture_outline_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLineOutline.png");
        var texture_edge_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetEdge.png");
        Globals.LineTexture = PluginInterface.UiBuilder.LoadImage(texture_line_path);
        Globals.OutlineTexture = PluginInterface.UiBuilder.LoadImage(texture_outline_path);
        Globals.EdgeTexture = PluginInterface.UiBuilder.LoadImage(texture_edge_path);
    }

    public static void DrawTooltip(string text) {
        if (ImGui.IsItemHovered() && Globals.Config.HideTooltips == false) {
            ImGui.SetTooltip(text);
        }
    }

    private void InitializeCommands() {
        Commands.Initialize();
    }

    private void InitializeConfig() {
        Globals.Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Globals.Config.Initialize();
    }

    private void InitializeUI() {
        Windows.Initialize();
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += Commands.ToggleConfig;
    }

    private unsafe void DrawOverlay() {
        Globals.Runtime += Globals.Framework->FrameDeltaTime;

        if (Service.ClientState.LocalPlayer == null && TargetLineDict.Count > 0 || Service.ClientState.IsPvP) {
            TargetLineDict.Clear();
        }

        if (Service.ClientState.IsPvP) {
            return;
        }

        for (int index = 0; index < Service.ObjectTable.Length; index++) {
            GameObject obj = Service.ObjectTable[index];

            if (obj == null || !obj.IsValid()) {
                continue;
            }

            uint id = obj.ObjectId;
            TargetLine? targetLine = null;
            if (TargetLineDict.TryGetValue(id, out targetLine)) {
                if (targetLine.ShouldDelete) {
                    TargetLineDict.Remove(id);
                    continue;
                }
            }


            if (obj.TargetObject == null) {
                /*if (obj.TargetObjectId != 0) {
                    var tobj = Service.ObjectTable.SearchById(obj.TargetObjectId);
                    if (tobj != null) {
                        Service.Logger.Info($"{obj.ObjectId}: TargetObject is null, however, {obj.TargetObjectId} exists!");
                    }
                    else
                    {
                        Service.Logger.Info($"{obj.ObjectId}: TargetObject is null, {obj.TargetObjectId} is bogus!");
                    }
                }*/
                continue;
            }

            if (!obj.IsValid() || !obj.TargetObject.IsValid()) {
                //Service.Logger.Info("Validity");
                continue;
            }

            // testing
            GameObjectHelper gobj = new GameObjectHelper(obj);
#if !PROBABLY_BAD
            if (!obj.IsTargetable || !gobj.TargetIsTargetable()) {
                //Service.Logger.Info("Targetable");
                continue;
            }
#endif

            if (targetLine == null) {
                targetLine = new TargetLine(gobj);
                TargetLineDict.Add(id, targetLine);
            }

            targetLine.Draw();
        }
    }

    private void OnDraw() {
        Windows.System.Draw();

        bool combat_flag = Service.Condition[ConditionFlag.InCombat];
        bool runOverlay = !Globals.Config.saved.OnlyUnsheathed || (Globals.Config.saved.OnlyUnsheathed && (Service.ClientState.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.WeaponOut) != 0);

        if (runOverlay) {
            if (Globals.Config.saved.OnlyInCombat == InCombatOption.InCombat && combat_flag == false) {
                runOverlay = false;
            }
            if (Globals.Config.saved.OnlyInCombat == InCombatOption.NotInCombat && combat_flag == true) {
                runOverlay = false;
            }
        }

        if (runOverlay) {
            if (Globals.Config.saved.ToggledOff == false) {
                ImGuiUtils.WrapBegin("##TargetLinesOverlay", OVERLAY_WINDOW_FLAGS, DrawOverlay);
            }
        }
        else if (TargetLineDict.Count > 0) {
            TargetLineDict.Clear();
        }
    }

#region IDisposable Support
    protected virtual void Dispose(bool disposing) {
        if (!disposing) {
            return;
        }

        PluginInterface.SavePluginConfig(Globals.Config);

        PluginInterface.UiBuilder.Draw -= OnDraw;
        Windows.Dispose();
        PluginInterface.UiBuilder.OpenConfigUi -= Commands.ToggleConfig;

        Commands.Dispose();

        Globals.LineTexture.Dispose();
        Globals.OutlineTexture.Dispose();
        Globals.EdgeTexture.Dispose();
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
#endregion
}
