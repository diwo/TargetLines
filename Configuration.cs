﻿using Dalamud.Configuration;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.Collections.Generic;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

public enum InCombatOption {
    None = 0,
    InCombat,
    NotInCombat,
    Count
}

public class TargetSettings {
    public TargetFlags Flags = 0;
    public UInt64 Jobs = 0; // bitfield from ClassJob

    public TargetSettings() {
        Flags = 0;
        Jobs = 0;
    }

    public TargetSettings(TargetFlags flags, UInt64 jobs = 0) {
        Flags = flags;
        Jobs = jobs;
    }
};

public class ABGR {
    public byte a;
    public byte b;
    public byte g;
    public byte r;

    public ABGR(byte A, byte B, byte G, byte R) {
        a = A;
        b = B;
        g = G;
        r = R;
    }

    public Vector4 Color {
        get {
            return new Vector4((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f, (float)a / 255.0f);
        }
        set {
            a = (byte)(value.W * 255.0f);
            b = (byte)(value.Z * 255.0f);
            g = (byte)(value.Y * 255.0f);
            r = (byte)(value.X * 255.0f);
        }
    }

    public void CopyValues(ABGR from) {
        a = from.a;
        b = from.b;
        g = from.g;
        r = from.r;
    }

    public uint GetRaw() {
        return (uint)((a << 24) | (b << 16) | (g << 8) | r);
    }
}

public class LineColor {
    public ABGR Color = new ABGR(0x80, 0x00, 0xBF, 0xFF);
    public ABGR OutlineColor = new ABGR(0x80, 0x00, 0x00, 0x00);
    public bool Visible = true;
    public bool UseQuad = false;

    public LineColor() {
        Color = new ABGR(0x80, 0x00, 0xBF, 0xFF);
        OutlineColor = new ABGR(0x80, 0x00, 0x00, 0x00);
        Visible = true;
        UseQuad = false;
    }

    public LineColor(ABGR color, ABGR outline, bool visible = true, bool usequad = false) {
        Color = color;
        OutlineColor = outline;
        Visible = visible;
        UseQuad = usequad;
    }

    public LineColor(ABGR color, bool visible = true, bool usequad = false) {
        Color = color;
        Visible = visible;
        UseQuad = usequad;
    }
}

public class TargetSettingsPair {
    public TargetSettings From;
    public TargetSettings To;
    public LineColor LineColor;

    public TargetSettingsPair(TargetSettings from, TargetSettings to, LineColor lineColor) {
        From = from;
        To = to;
        LineColor = lineColor;
    }
}

public class SavedConfig {
    public float ArcHeightScalar = 1.0f;
    public float PlayerHeightBump = 0.0f;
    public float EnemyHeightBump = 0.0f;
    public float LineThickness = 16.0f;
    public float OutlineThickness = 20.0f;
    public float NewTargetEaseTime = 0.25f;
    public float NoTargetFadeTime = 0.25f;
    public float WaveAmplitudeOffset = 0.175f;
    public float WaveFrequencyScalar = 3.0f;
    public float FadeToEndScalar = 0.2f;
    public float HeightScale = 1.0f;
    public int TextureCurveSampleCount = 48;
    public InCombatOption OnlyInCombat = InCombatOption.None;
    public bool OnlyUnsheathed = false;
    public bool SolidColor = false;
    public bool FadeToEnd = true;
    public bool ToggledOff = false;
    public bool OcclusionCulling = false;
    public bool PulsingEffect = true;
    public bool BreathingEffect = true;
    public LineColor LineColor = new LineColor(new ABGR(0xC0, 0x80, 0x80, 0x80), new ABGR(0x80, 0x00, 0x00, 0x00), true); // fallback color

    [Obsolete] public ABGR PlayerPlayerLineColor;
    [Obsolete] public ABGR PlayerEnemyLineColor;
    [Obsolete] public ABGR EnemyPlayerLineColor;
    [Obsolete] public ABGR EnemyEnemyLineColor;
    [Obsolete] public ABGR OtherLineColor;
    [Obsolete] public ABGR OutlineColor;
    [Obsolete] public bool OnlyTargetingPC = false;
}

public class Configuration : IPluginConfiguration {
    int IPluginConfiguration.Version { get; set; }

    #region Saved configuration values
    public SavedConfig saved = new SavedConfig();
    public List<TargetSettingsPair> LineColors;
    #endregion


    private DalamudPluginInterface PluginInterface;

    public void InitializeDefaultLineColorsConfig() {
        LineColors = new List<TargetSettingsPair>() {
                // player -> player default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Player),
                    new TargetSettings(TargetFlags.Player),
                    new LineColor(new ABGR(0xC0, 0, 0xFF, 0))
                ),
                // player -> enemy default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Player),
                    new TargetSettings(TargetFlags.Enemy),
                    new LineColor(new ABGR(0xC0, 0x20, 0x40, 0xFF))
                ), 
                // enemy -> player default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Enemy),
                    new TargetSettings(TargetFlags.Player),
                    new LineColor(new ABGR(0xC0, 0, 0, 0xFF), true, true)
                ),
                // enemy -> enemy default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Enemy),
                    new TargetSettings(TargetFlags.Enemy),
                    new LineColor(new ABGR(0x80, 0, 0x80, 0x80), true, true)
                )
            };
    }

    public void Initialize(DalamudPluginInterface pluginInterface) {
        int LineColorsWasNull = 0;
        PluginInterface = pluginInterface;

        // manage upgrades
        if (saved == null) {
            saved = new SavedConfig();
        }

        if (LineColors == null) {
            LineColors = new List<TargetSettingsPair>();
            LineColorsWasNull = 1;
        }

        if (saved.OnlyInCombat is bool) {
            saved.OnlyInCombat = InCombatOption.None;
            Globals.Chat.Print("Warning! If you had the \"OnlyInCombat\" setting set, you will have to reenable it!");
        }

        if (saved.PlayerPlayerLineColor != null) {
            TargetSettings from =  new TargetSettings(TargetFlags.Player, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Player, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.PlayerPlayerLineColor, saved.OutlineColor, true)));

            saved.PlayerPlayerLineColor = null;
        }

        if (saved.PlayerEnemyLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Player, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Enemy, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.PlayerEnemyLineColor, saved.OutlineColor, true)));
            saved.PlayerEnemyLineColor = null;
        }

        if (saved.EnemyPlayerLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Enemy, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Player, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.EnemyPlayerLineColor, saved.OutlineColor, true)));
            saved.EnemyPlayerLineColor = null;
        }

        if (saved.EnemyEnemyLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Enemy, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Enemy, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.EnemyEnemyLineColor, saved.OutlineColor, true)));
            saved.EnemyEnemyLineColor = null;
        }

        // Initialize values if we didn't upgrade from an old config
        if (LineColorsWasNull == 1) {
            InitializeDefaultLineColorsConfig();
        }
    }

    public void Save() {
        PluginInterface.SavePluginConfig(this);
    }
}
