using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System;
using ImGuiNET;
using Dalamud.Game.ClientState.Objects.Types;
using System.Collections.Generic;
using Dalamud.Logging;
using Dalamud.Game.ClientState.Conditions;
using System.IO;
using TargetLines.Attributes;

[assembly: System.Reflection.AssemblyVersion("1.2.7")]

namespace TargetLines;

public class Plugin : IDalamudPlugin {
    public string Name => "TargetLines";
    private DalamudPluginInterface PluginInterface;

    private const ImGuiWindowFlags OVERLAY_WINDOW_FLAGS =
          ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoNav;

    private Dictionary<uint, TargetLine> TargetLineDict;

    public Plugin(DalamudPluginInterface pluginInterface, CommandManager commandManager, ChatGui chat, ClientState clientState) {
        PluginInterface = pluginInterface;
        Globals.Chat = chat;
        Globals.ClientState = clientState;

        Globals.Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Globals.Config.Initialize(PluginInterface);

        Globals.WindowSystem = new WindowSystem(typeof(Plugin).AssemblyQualifiedName);
        Globals.WindowSystem.AddWindow(new ConfigWindow());
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.Create<Service>();

        Globals.CommandManager = commandManager;
        Globals.PluginCommandManager = new PluginCommandManager<Plugin>(this, commandManager);

        TargetLineDict = new Dictionary<uint, TargetLine>();
        InitializeCamera();

        var texture_line_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLine.png");
        var texture_outline_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLineOutline.png");
        var texture_edge_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetEdge.png");
        Globals.LineTexture = PluginInterface.UiBuilder.LoadImage(texture_line_path);
        Globals.OutlineTexture = PluginInterface.UiBuilder.LoadImage(texture_outline_path);
        Globals.EdgeTexture = PluginInterface.UiBuilder.LoadImage(texture_edge_path);
    }

    [Command("/ptlines")]
    [HelpMessage("Toggle configuration window")]
    private void On_pbshadows(string command, string args) {
        ToggleConfig();
    }

    [Command("/ttl")]
    [HelpMessage("Toggle target line overlay")]
    private void On_ttl(string command, string args)
    {
        string str = "on";
        Globals.Config.saved.ToggledOff = !Globals.Config.saved.ToggledOff;

        if (Globals.Config.saved.ToggledOff) {
            str = "off";
        }

        Globals.Chat.Print($"Target Lines overlay toggled {str}");
    }

    private void ToggleConfig() {
        Globals.WindowSystem.GetWindow(ConfigWindow.ConfigWindowName).IsOpen = !Globals.WindowSystem.GetWindow(ConfigWindow.ConfigWindowName).IsOpen;
    }

    private unsafe void InitializeCamera() {
        Globals.CameraManager = (CameraManager*)Service.SigScanner.GetStaticAddressFromSig("4C 8D 35 ?? ?? ?? ?? 85 D2"); // g_ControlSystem_CameraManager
    }

    private unsafe void DrawOverlay() {
        FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        Globals.Runtime += framework->FrameDeltaTime;

        if (Globals.ClientState.LocalPlayer == null && TargetLineDict.Count > 0) {
            TargetLineDict.Clear();
        }

        for (int index = 0; index < Service.ObjectTable.Length; index++) {
            GameObject obj = Service.ObjectTable[index];
            bool should_delete = false;

            if (obj == null || !obj.IsValid()) {
                continue;
            }

            uint id = obj.ObjectId;

            TargetLine targetLine = null;
            if (TargetLineDict.TryGetValue(id, out targetLine)) {
                if (targetLine.ShouldDelete) {
                    should_delete = true;
                }
            }

            if (should_delete) {
                TargetLineDict.Remove(id);
                continue;
            }

            GameObjectHelper gobj = new GameObjectHelper(obj);
            bool valid = gobj.Object.IsValid() && gobj.TargetObject != null && gobj.TargetObject.IsValid() && targetLine == null;
            // testing
#if (!PROBABLY_BAD)
            valid = valid && gobj.TargetIsTargetable();
#endif
            if (valid) {
                targetLine = new TargetLine(gobj);
                TargetLineDict.Add(id, targetLine);
            }

            if (Globals.ClientState.IsPvP || targetLine == null) {
                continue;
            }

            if (targetLine.ThisObject.Object.IsValid() && targetLine.ThisObject.IsTargetable()) {
                targetLine.Draw();
            }
        }
    }

    private void OnDraw() {
        Globals.WindowSystem.Draw();

        bool combat_flag = Service.Condition[ConditionFlag.InCombat];
        bool runOverlay = !Globals.Config.saved.OnlyUnsheathed || (Globals.Config.saved.OnlyUnsheathed && (Globals.ClientState.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.WeaponOut) != 0);

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
        if (!disposing) return;

        Globals.PluginCommandManager.Dispose();

        PluginInterface.SavePluginConfig(Globals.Config);

        PluginInterface.UiBuilder.Draw -= OnDraw;
        Globals.WindowSystem.RemoveAllWindows();

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
