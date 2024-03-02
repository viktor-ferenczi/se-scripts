const string components = "Components";
const string ignore = "Ignore";
const string tools = "Tools";
List<IMyTerminalBlock> Cargo = new List<IMyTerminalBlock>();
List<IMyCargoContainer> Containers = new List<IMyCargoContainer>();
void Main(string args)
{
    GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(Containers);
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(Cargo);
    for (int c = 0; c < Containers.Count; c++)
    {
        if ((Containers[c].HasInventory) && (Containers[c].GetInventory() != null))
        {
            for (int a = 0; a < Cargo.Count; a++)
            {
                if ((Cargo[a].HasInventory == true) && (Cargo[a].CustomData != ignore) && (Cargo[a].CustomData != tools) && (Cargo[a].CustomData != components))
                {
                    if (Cargo[a].GetInventory() != null)
                    {
                        var Items = Cargo[a].GetInventory().GetItems();
                        for (int b = 0; b < Items.Count; b++)
                        {
                            if ((Items[b].Content.TypeId.ToString() == "MyObjectBuilder_Component") && (Containers[c].GetInventory().IsFull == false) && (Containers[c].CustomData == components))
                            {
                                Cargo[a].GetInventory().TransferItemTo(Containers[c].GetInventory(), b, null, true);

                            }

                            if ((Items[b].Content.TypeId.ToString() == "MyObjectBuilder_PhysicalGunObject") && (Containers[c].GetInventory().IsFull == false) && (Containers[c].CustomData == tools))
                            {
                                Cargo[a].GetInventory().TransferItemTo(Containers[c].GetInventory(), b, null, true);
                            }
                        }
                    }
                }

            }
        }
    }
}