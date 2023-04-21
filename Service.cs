using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;

namespace TargetLines;

internal class Service
{
    [PluginService] public static GameGui Gui { get; private set; } = null!;
    [PluginService] public static TargetManager Targets { get; private set; } = null!;
    [PluginService] public static ObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static Condition Condition { get; private set; } = null!;
    [PluginService] public static DataManager DataManager { get; private set; } = null!;
    [PluginService] public static Framework Framework { get; private set; } = null!;
    [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
}
