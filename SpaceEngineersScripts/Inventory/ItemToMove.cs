using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public struct ItemToMove
    {
        public IMyInventory Inventory;
        public int ItemIndex;
        public string ItemType;
        public string ItemSubtype;
        public List<Container> TargetContainers;
    }
}