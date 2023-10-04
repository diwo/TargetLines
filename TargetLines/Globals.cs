using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using FFXIVClientStructs.FFXIV.Common.Math;
using DrahsidLib;
using Dalamud.Interface.Internal;

namespace TargetLines;

internal class Globals {
    public static double Runtime = 0.0;
    public static Configuration Config { get; set; } = null!;
    public static IDalamudTextureWrap LineTexture { get; set; } = null!;
    public static IDalamudTextureWrap OutlineTexture { get; set; } = null!;
    public static IDalamudTextureWrap EdgeTexture { get; set; } = null!;


    private static unsafe FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* _Framework { get; set; } = null!;
    public static unsafe FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* Framework {
        get {
            if (_Framework == null) {
                _Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
            }
            return _Framework;
        }
    }

    public static unsafe Vector3 WorldCamera_GetPos() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 0);
        }

        return new Vector3(
            Service.CameraManager->Camera->CameraBase.X,
            Service.CameraManager->Camera->CameraBase.Z,
            Service.CameraManager->Camera->CameraBase.Y
        );
    }

    public static unsafe Vector3 WorldCamera_GetLookAtPos() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 0);
        }

        return new Vector3(Service.CameraManager->Camera->CameraBase.LookAtX, Service.CameraManager->Camera->CameraBase.LookAtZ, Service.CameraManager->Camera->CameraBase.LookAtY);
    }

    public static unsafe Vector3 WorldCamera_GetForward() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 1);
        }

        return Vector3.Normalize(WorldCamera_GetPos() - WorldCamera_GetLookAtPos());
    }

    public static unsafe bool IsVisible(Vector3 position, bool occlusion) {
        Vector3 cam = WorldCamera_GetPos();
        Vector3 forward = WorldCamera_GetForward();
        Vector3 to_camera = cam - position;

        // behind camera
        if (Vector3.Dot(to_camera, forward) < 0) {
            return false;
        }

        if (occlusion) {
            var direction = position - cam;
            var length = direction.Magnitude;

            if (length != 0) {
                var flags = stackalloc int[] { 0x4000, 0x4000 }; // should probably figure out what these mean
                var hit = stackalloc RaycastHit[1];
                var result = Framework->BGCollisionModule->RaycastEx(hit, cam, direction.Normalized, length, 1, flags);
                return result == false;
            }

            return false;
        }
        else {
            return true;
        }
    }
}
