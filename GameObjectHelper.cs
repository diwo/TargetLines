using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TargetLines
{
    internal class GameObjectHelper {
        public GameObject Object;

        public GameObjectHelper(GameObject obj) {
            Object = obj;
        }

        public bool IsBattleChara() {
            return Object is BattleChara;
        }

        public bool IsPlayerCharacter() {
            return Object.ObjectKind == ObjectKind.Player;
        }

        public BattleChara BattleChara {
            get { return Object as BattleChara; }
        }

        public PlayerCharacter PlayerCharacter {
            get { return Object as PlayerCharacter; }
        }
    }
}
