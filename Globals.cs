using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;

namespace TargetLines
{
    internal class Globals
    {
        public static Configuration Config;
        public static ChatGui Chat;
        public static ClientState ClientState;
        public static CommandManager CommandManager;
        public static PluginCommandManager<Plugin> PluginCommandManager;
        public static WindowSystem WindowSystem;
        public static unsafe CameraManager* CameraManager;
        public static double Runtime = 0.0;
        public static ImGuiScene.TextureWrap LineTexture;
        public static ImGuiScene.TextureWrap EdgeTexture;

        public static unsafe Vector3 WorldCamera_GetPos() {
            if (CameraManager == null) {
                return new Vector3(0, 0, 0);
            }
            if (CameraManager->WorldCamera == null) {
                return new Vector3(0, 0, 0);
            }

            return new Vector3(CameraManager->WorldCamera->X, CameraManager->WorldCamera->Z, CameraManager->WorldCamera->Y);
        }

        public static unsafe Vector3 WorldCamera_GetLookAtPos() {
            if (CameraManager == null)
            {
                return new Vector3(0, 0, 0);
            }
            if (CameraManager->WorldCamera == null)
            {
                return new Vector3(0, 0, 0);
            }

            return new Vector3(CameraManager->WorldCamera->LookAtX, CameraManager->WorldCamera->LookAtZ, CameraManager->WorldCamera->LookAtY);
        }

        public static unsafe Vector3 WorldCamera_GetForward() {
            if (CameraManager == null) {
                return new Vector3(0, 0, 1);
            }
            if (CameraManager->WorldCamera == null) {
                return new Vector3(0, 0, 1);
            }



            return Vector3.Normalize(WorldCamera_GetPos() - WorldCamera_GetLookAtPos());
        }
    }
}
