using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;

namespace TargetLines;

internal class ObjectTableHelper {
    public static GameObjectHelper GetObjectByID(uint id) {
        GameObjectHelper gobj = null;
        foreach (GameObject obj in Service.ObjectTable) {
            if (obj.ObjectId == id) {
                gobj = new GameObjectHelper(obj);
                return gobj;
                break;
            }
        }

        return gobj;
    }
}
