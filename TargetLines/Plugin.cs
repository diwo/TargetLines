using Dalamud.Plugin;
using System;
using ImGuiNET;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using System.IO;
using Dalamud.Plugin.Services;
using DrahsidLib;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Group;

namespace TargetLines;

public class Plugin : IDalamudPlugin {
    private IDalamudPluginInterface PluginInterface;
    private IChatGui Chat { get; init; }
    private IClientState ClientState { get; init; }
    private ICommandManager CommandManager { get; init; }

    public string Name => "TargetLines";

    private bool WasInPvP = false;
    private LineActor? testLine = null;

    private const ImGuiWindowFlags OVERLAY_WINDOW_FLAGS =
          ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoNav;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IChatGui chat, IClientState clientState) {
        PluginInterface = pluginInterface;
        Chat = chat;
        ClientState = clientState;
        CommandManager = commandManager;

        DrahsidLib.DrahsidLib.Initialize(pluginInterface, DrawTooltip);

        InitializeCommands();
        InitializeConfig();
        InitializeUI();

        // as it turns out there's some folks making "true pvp" builds of this plugin, so let's have some fun with them
        if (pluginInterface.InternalName.ToLower().Contains("pvp")) {
            Globals.HandlePvP = true;
        }

        Globals.TargetLineDict = new Dictionary<uint, TargetLine>();

        var texture_line_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLine.png");
        var texture_outline_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLineOutline.png");
        var texture_edge_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetEdge.png");


        Globals.LineTexture = Service.TextureProvider.GetFromFile(texture_line_path);
        Globals.OutlineTexture = Service.TextureProvider.GetFromFile(texture_outline_path);
        Globals.EdgeTexture = Service.TextureProvider.GetFromFile(texture_edge_path);

        if (Globals.Config.saved.DebugDXLines) {
            Vector3Extensions.Tests();

            try {
                SwapChainResolver.Setup();
            }
            catch (Exception ex) {
                Service.Logger.Error(ex.Message);
            }

            try {
                SwapChainHook.Setup();
            }
            catch (Exception ex) {
                Service.Logger.Error(ex.Message);
            }
        }
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

        if (Globals.TargetLineDict == null) {
            Service.Logger.Verbose("TargetLineDict null?!");
            return;
        }

        if (Globals.HandlePvP)
        {
            if (WasInPvP != Service.ClientState.IsPvP)
            {
                Globals.HandlePvPTime = 0.0f;
                WasInPvP = Service.ClientState.IsPvP;
            }

            if (Service.ClientState.IsPvP)
            {
                Globals.HandlePvPTime += Globals.Framework->FrameDeltaTime;
            }
        }

        if (Globals.Config.saved.DebugDXLines && ShaderSingleton.Initialized && testLine != null && Service.ClientState.LocalPlayer != null) {
            testLine.Source = Service.ClientState.LocalPlayer.GetHeadPosition().DXVector3();

            if (Service.ClientState.LocalPlayer.TargetObject != null)
            {
                testLine.Destination = Service.ClientState.LocalPlayer.TargetObject.GetHeadPosition().DXVector3();
            }
            else
            {
                testLine.Destination = new SharpDX.Vector3(1.0f, 0.1f, 5.0f);
            }

            Service.GameGui.WorldToScreen(testLine.Source.Vector3(), out Vector2 source);
            Service.GameGui.WorldToScreen(testLine.Destination.Vector3(), out Vector2 dest);
            ImGui.GetWindowDrawList().AddCircleFilled(source, 9, 0xFF00FF00);
            ImGui.GetWindowDrawList().AddCircleFilled(dest, 7, 0xFF0000FF);
        }

        if ((Service.ClientState.LocalPlayer == null || Service.ClientState.IsPvP) && Globals.TargetLineDict.Count > 0 ) {
            Globals.TargetLineDict.Clear();
        }

        if (Service.ClientState.LocalPlayer == null || Service.ClientState.IsPvP) {
            return;
        }

        for (int index = 0; index < Service.ObjectTable.Length; index++) {
            IGameObject obj = Service.ObjectTable[index];

            if (obj == null || !obj.IsValid()) {
                continue;
            }

            uint id = obj.EntityId;
            TargetLine? targetLine = null;
            if (Globals.TargetLineDict.TryGetValue(id, out targetLine)) {
                if (targetLine.ShouldDelete) {
                    Globals.TargetLineDict.Remove(id);
                    continue;
                }
            }

            bool has_target = obj.TargetObject != null;
            if (!obj.IsValid()) {
                continue;
            }

#if !PROBABLY_BAD
            if (!obj.IsTargetable) {
                continue;
            }

            if (has_target && !obj.TargetIsTargetable()) {
                continue;
            }
#endif

            if (targetLine == null && has_target) {
                var group = GroupManager.Instance();

                switch (Globals.Config.saved.LinePartyMode)
                {
                    default:
                    case LinePartyMode.None:
                        targetLine = new TargetLine((IGameObject*)&obj);
                        Globals.TargetLineDict.Add(id, targetLine);
                        break;
                    case LinePartyMode.PartyOnly:
                        if (group->MainGroup.IsEntityIdInParty(obj.EntityId) || group->MainGroup.IsEntityIdInParty((uint)obj.TargetObjectId))
                        {
                            targetLine = new TargetLine((IGameObject*)&obj);
                            Globals.TargetLineDict.Add(id, targetLine);
                        }
                        break;
                    case LinePartyMode.PartyOnlyInAlliance:
                        if (group->MainGroup.IsAlliance)
                        {
                            if (group->MainGroup.IsEntityIdInParty(obj.EntityId) || group->MainGroup.IsEntityIdInParty((uint)obj.TargetObjectId))
                            {
                                targetLine = new TargetLine((IGameObject*)&obj);
                                Globals.TargetLineDict.Add(id, targetLine);
                            }
                        }
                        else
                        {
                            targetLine = new TargetLine((IGameObject*)&obj);
                            Globals.TargetLineDict.Add(id, targetLine);
                        }
                        break;
                    case LinePartyMode.AllianceOnly:
                        if (group->MainGroup.IsEntityIdInAlliance(obj.EntityId) || group->MainGroup.IsEntityIdInAlliance((uint)obj.TargetObjectId)) {
                            targetLine = new TargetLine((IGameObject*)&obj);
                            Globals.TargetLineDict.Add(id, targetLine);
                        }
                        break;
                }
            }

            if (targetLine != null) {
                targetLine.Draw();
            }
        }

        UICollision.DrawDebugOutlines();
    }

    private void OnDraw() {
        Windows.System.Draw();

        if (Globals.Config.saved.DebugDXLines) {
            if (ShaderSingleton.Initialized && testLine == null) {
                testLine = new LineActor(SwapChainHook.Scene.Device, SwapChainHook.Scene.SwapChain);
            }
        }

        if (Service.ClientState.LocalPlayer == null)
        {
            return;
        }

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
        else if (Globals.TargetLineDict.Count > 0) {
            Globals.TargetLineDict.Clear();
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
        SwapChainHook.Dispose();
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
#endregion
}
