﻿using System;
using System.Collections.Generic;
using System.Text;
using Inventory;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace ShipInventory
{
    class Program: SpaceEngineersProgram
    {

        #region CodeEditor

        // ShipInventory
        const string Prefix = "";

        //Created by Sp[a]cemarine with inspiration from Xeracles
        //If you find anything useful in the code, you are allowed to make use of it.


        //START: USER VARIABLES - YOU CAN MODIFY STUFF HERE

        //If you change your font, you can set this value
        //so that the screens are fully used (currently we can't acces the fontsize)
        const float DefaultFontSize = 1.0f;
        const string ResourcePanelsPrefix = Prefix + "Resource";
        const string OrePanelsPrefix = Prefix + "Ore";
        const string IngotPanelsPrefix = Prefix + "Ingot";
        const string ComponentPanelsPrefix = Prefix + "Component";
        const string AmmoPanelsPrefix = Prefix + "Ammo";
        const string StatusPanelsPrefix = Prefix + "Status";
        const string BlockInvPanelsPrefix = Prefix + "BlockInv";

        //END: USER VARIABLES - DO NOT CHANGE ANYTHING BELOW
        const string NumberFormat = "#,0.0##";

        const string NotEnughPanelsMessage = @" Not enough panels to display all entries.
Thank you for using this script.
Do not forget to rate it if you like it.

Regards,
Spacemarine";

        const string CannotFindInvMessage = @" Cannot find inventory.
Either there is no block with an
inventory behind the panel
or the given name of the block is wrong.

Please also check if the name
of this panel is valid.";

        Dictionary<string, float> Ores;
        Dictionary<string, float> Ingots;
        Dictionary<string, float> Components;
        Dictionary<string, float> Ammo;

        private List<string> OreText = new List<string>();
        private List<string> IngotText = new List<string>();
        private List<string> ComponentText = new List<string>();
        private List<string> AmmoText = new List<string>();
        private List<string> StatusText = new List<string>();

        string[] AllPrefixes = { ResourcePanelsPrefix, OrePanelsPrefix, IngotPanelsPrefix, ComponentPanelsPrefix, AmmoPanelsPrefix, StatusPanelsPrefix };

        List<List<List<IMyTextPanel>>> AllPanels;
        List<List<IMyTextPanel>> RessourcesPanels;
        List<List<IMyTextPanel>> OrePanels;
        List<List<IMyTextPanel>> IngotPanels;
        List<List<IMyTextPanel>> ComponentPanels;
        List<List<IMyTextPanel>> AmmoPanels;

        List<List<IMyTextPanel>> StatusPanels;
// Lists contain a list of panels for each index -> AmmoPanels[index][i]

        Dictionary<IMyTerminalBlock, List<List<IMyTextPanel>>> BlockInvPanels;
        Dictionary<IMyTerminalBlock, List<string>> BlockInvContents;

        bool Debug = false;
        StringBuilder DebugString = new StringBuilder();
        Exception e = new Exception("Fine till here.");

        List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
        List<IMyBlockGroup> BlockGroups = new List<IMyBlockGroup>();

//time elapsed since last execution
        private TimeSpan timeSinceLastExecution;
        private const int timeBetweenExecutions = 1;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
            timeSinceLastExecution = TimeSpan.Zero;
        }

        void Main(string arguments)
        {
            GridTerminalSystem.GetBlockGroups(BlockGroups);
            GridTerminalSystem.GetBlocks(Blocks);

            Ores = new Dictionary<string, float>();
            Ingots = new Dictionary<string, float>();
            Components = new Dictionary<string, float>();
            Ammo = new Dictionary<string, float>();

            for (int i = 0; i < Blocks.Count; i++)
            {
                var block = Blocks[i];
                if (block.HasInventory)
                {
                    for (int j = 0; j < block.InventoryCount; j++)
                    {
                        var inv = block.GetInventory(j);
                        EvaluateInv(inv);
                    }
                }
            }

            AllPanels = new List<List<List<IMyTextPanel>>>();

            RessourcesPanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(RessourcesPanels);

            OrePanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(OrePanels);

            IngotPanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(IngotPanels);

            ComponentPanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(ComponentPanels);

            AmmoPanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(AmmoPanels);

            StatusPanels = new List<List<IMyTextPanel>>();
            AllPanels.Add(StatusPanels);

            SortPanels();

            EvaluateBlockInvPanels(GetTextPanels());


            int panelCount = 0;
            //clear all panels first
            for (int i = 0; i < AllPanels.Count; i++)
                for (int j = 0; j < AllPanels[i].Count; j++)
                    for (int k = 0; k < AllPanels[i][j].Count; k++)
                    {
                        IMyTextPanel panel = AllPanels[i][j][k];
                        // panel.ShowPublicTextOnScreen();
                        panel.WriteText("");
                        panelCount++;
                    }

            List<IMyTerminalBlock> keys = new List<IMyTerminalBlock>(BlockInvPanels.Keys);
            for (int i = 0; i < keys.Count; i++)
                for (int j = 0; j < BlockInvPanels[keys[i]].Count; j++)
                    for (int k = 0; k < BlockInvPanels[keys[i]][j].Count; k++)
                    {
                        IMyTextPanel panel = BlockInvPanels[keys[i]][j][k];
                        // panel.ShowPublicTextOnScreen();
                        panel.WriteText("");
                        panelCount++;
                    }



            if (panelCount == 0)
            {
                Echo("No Panels found. :(");
                return;
            }

            DebugString.AppendLine(string.Format("Counts -> Ore: {0}, Ing: {1}, Com: {2}, Amm: {3}", OrePanels.Count, IngotPanels.Count, ComponentPanels.Count, AmmoPanels.Count));

            if (Ores.Count > 0)
            {
                SortedDictionary<string, float> sortedOres = new SortedDictionary<string, float>(Ores);

                OreText.Add("Ores:");
                OreText.Add("");

                List<string> oreKeys = new List<string>(sortedOres.Keys);
                for (int i = 0; i < oreKeys.Count; i++)
                {
                    string key = oreKeys[i];
                    OreText.Add(string.Format("{0}: {1}", key, sortedOres[key].ToString(NumberFormat)));
                }

            }
            else
            {
                OreText.Add("No ores found.");
            }

            if (Ingots.Count > 0)
            {
                SortedDictionary<string, float> sortedIngots = new SortedDictionary<string, float>(Ingots);

                IngotText.Add("Ingots:");
                IngotText.Add("");

                List<string> ingotKeys = new List<string>(sortedIngots.Keys);
                for (int i = 0; i < ingotKeys.Count; i++)
                {
                    string key = ingotKeys[i];
                    IngotText.Add(string.Format("{0}: {1}", key, sortedIngots[key].ToString(NumberFormat)));
                }

            }
            else
            {
                IngotText.Add("No ingots found.");
            }

            if (Components.Count > 0)
            {
                SortedDictionary<string, float> sortedComponents = new SortedDictionary<string, float>(Components);

                ComponentText.Add("Components:");
                ComponentText.Add("");

                List<string> componentKeys = new List<string>(sortedComponents.Keys);
                for (int i = 0; i < componentKeys.Count; i++)
                {
                    string key = componentKeys[i];
                    ComponentText.Add(string.Format("{0}: {1}", key, sortedComponents[key].ToString(NumberFormat)));
                }
            }
            else
            {
                ComponentText.Add("No components found.");
            }

            if (Ammo.Count > 0)
            {
                SortedDictionary<string, float> sortedAmmo = new SortedDictionary<string, float>(Ammo);

                AmmoText.Add("Ammo:");
                AmmoText.Add("");

                List<string> ammoKeys = new List<string>(sortedAmmo.Keys);
                for (int i = 0; i < ammoKeys.Count; i++)
                {
                    string key = ammoKeys[i];
                    AmmoText.Add(string.Format("{0}: {1}", key, sortedAmmo[key].ToString(NumberFormat)));
                }
            }
            else
            {
                AmmoText.Add("No ammo found.");
            }

            StatusText.Add(string.Format("Oxygen tanks: {0:0.00%}", GetStockpiledOxygen()));
            StatusText.Add(string.Format("Batteries: {0:0.00%}", GetBatteryPower()));
            //StatusText.Add(string.Format("Container: {0:0.00%}", GetCargoSpace()));

            Display();

            if (Debug)
            {
		        DebugString.AppendLine("EOF");
                Echo(DebugString.ToString());
                DebugString.Clear();
            }

            OreText.Clear();
            IngotText.Clear();
            ComponentText.Clear();
            AmmoText.Clear();
            StatusText.Clear();
        }

        private void Display()
        {
	        DebugString.AppendLine("Display");

            var allPanels = new List<List<IMyTextPanel>>[] { OrePanels, IngotPanels, ComponentPanels, AmmoPanels, StatusPanels };
            var allTexts = new List<string>[] { OreText, IngotText, ComponentText, AmmoText, StatusText };

//	        for(int i = 0; i < allPanels.Length; i++)
//		        DebugString.AppendLine(string.Format("allPanels[{0}].Length = {1}", i, allPanels[i].Count));
//
//	        for(int i = 0; i < allTexts.Length; i++)
//		        DebugString.AppendLine(string.Format("allTexts[{0}]: {1}", i, String.Join("; ", allTexts[i])));

            if (allPanels.Length != allTexts.Length)
                throw new Exception("Error: 'allPanels' and 'allTexts' in 'Display()' do not have the same length.");

            for (int index = 0; index < allPanels.Length; index++)
            {
                List<List<IMyTextPanel>> panels = allPanels[index];
                List<string> texts = allTexts[index];

                Display(panels, texts);
            }

            List<string> ressourcePanelsTexts = new List<string>(OreText);
            ressourcePanelsTexts.Add("");
            ressourcePanelsTexts.AddRange(IngotText);
            ressourcePanelsTexts.Add("");
            ressourcePanelsTexts.AddRange(ComponentText);
            ressourcePanelsTexts.Add("");
            ressourcePanelsTexts.AddRange(AmmoText);

            Display(RessourcesPanels, ressourcePanelsTexts);

            List<IMyTerminalBlock> keys = new List<IMyTerminalBlock>(BlockInvPanels.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var panels = BlockInvPanels[key];
                var content = BlockInvContents[key];

                Display(panels, content);
            }
        }

        private static void Display(List<List<IMyTextPanel>> panels, List<string> texts)
        {
            int linesPerPanel = (int)Math.Ceiling(17 / DefaultFontSize);

            if (panels.Count > 0)
            {
                // use font size of first panel
                if (panels[0].Count > 0)
                {
                    var firstpanel = panels[0][0];
                    if (firstpanel != null)
                    {
                        linesPerPanel = (int)Math.Ceiling(17 / firstpanel.FontSize);
                    }
                }
                StringBuilder text = new StringBuilder();
                int lineIndex = 0;
                bool finish = false;

                for (int i = 0; i < panels.Count; i++)
                {
                    List<IMyTextPanel> panelsAtIndex = panels[i];

                    for (int j = 0; j < linesPerPanel; j++)
                    {
                        if (lineIndex < texts.Count)
                        {
                            text.AppendLine(" " + texts[lineIndex]);
                            lineIndex++;
                        }
                        else
                        {
                            finish = true;
                            break;
                        }
                    }

                    if (i == panels.Count - 1 && lineIndex < texts.Count - 1)
                        text = new StringBuilder(NotEnughPanelsMessage);

                    for (int panelIndex = 0; panelIndex < panelsAtIndex.Count; panelIndex++)
                    {
                        IMyTextPanel panel = panelsAtIndex[panelIndex];
                        // panel.ShowPublicTextOnScreen();
                        panel.WriteText(text.ToString());
                    }

                    text.Clear();

                    if (finish)
                        break;
                }
            }
        }

        private void EvaluateBlockInvPanels(List<IMyTextPanel> blockInvPanels)
        {
            BlockInvPanels = new Dictionary<IMyTerminalBlock, List<List<IMyTextPanel>>>();
            BlockInvContents = new Dictionary<IMyTerminalBlock, List<string>>();

            for (int i = 0; i < blockInvPanels.Count; i++)
            {
                IMyTextPanel panel = blockInvPanels[i];
                IMyTerminalBlock block;

                if (!panel.CustomName.StartsWith(BlockInvPanelsPrefix) && !panel.GetPublicTitle().StartsWith(BlockInvPanelsPrefix))
                    continue;

                int startIndexName = panel.CustomName.IndexOf('"') + 1;
                int lastIndexName = panel.CustomName.LastIndexOf('"');

                int startIndexTitle = panel.GetPublicTitle().IndexOf('"') + 1;
                int lastIndexTitle = panel.GetPublicTitle().LastIndexOf('"');

                if (panel.CustomName.StartsWith(BlockInvPanelsPrefix) && startIndexName >= 0 && lastIndexName > 0 && startIndexName < lastIndexName)
                {
                    int length = lastIndexName - startIndexName;

                    string blockname = panel.CustomName.Substring(startIndexName, length);

                    panel.WriteText(string.Format("Cannot find {0} or.\n\r", blockname));
                    panel.WriteText(string.Format("{0} does not have an inventory.", blockname), true);

                    if (GetBlockWithExactName(blockname, out block) && block.HasInventory)
                    {

                        if (!BlockInvPanels.ContainsKey(block))
                        {
                            BlockInvPanels.Add(block, new List<List<IMyTextPanel>>());
                            BlockInvPanels[block].Add(new List<IMyTextPanel>());
                            BlockInvContents.Add(block, EvaluateInvOwner(block));
                        }

                        BlockInvPanels[block][0].Add(panel);
                    }
                }
                else if (panel.GetPublicTitle().StartsWith(BlockInvPanelsPrefix) && startIndexTitle >= 0 && lastIndexTitle > 0 && startIndexTitle < lastIndexTitle)
                {
                    int length = lastIndexTitle - startIndexTitle;

                    string blockname = panel.GetPublicTitle().Substring(startIndexTitle, length);

                    panel.WriteText(string.Format("Cannot find {0} or.\n\r", blockname));
                    panel.WriteText(string.Format("{0} does not have an inventory.", blockname), true);

                    if (GetBlockWithExactName(blockname, out block) && block.HasInventory)
                    {

                        if (!BlockInvPanels.ContainsKey(block))
                        {
                            BlockInvPanels.Add(block, new List<List<IMyTextPanel>>());
                            BlockInvPanels[block].Add(new List<IMyTextPanel>());
                            BlockInvContents.Add(block, EvaluateInvOwner(block));
                        }

                        BlockInvPanels[block][0].Add(panel);
                    }
                }
                else if (TryGetBlockWithInvBehind(panel, out block))
                {
                    if (!BlockInvPanels.ContainsKey(block))
                    {
                        BlockInvPanels.Add(block, new List<List<IMyTextPanel>>());
                        BlockInvPanels[block].Add(new List<IMyTextPanel>());
                        BlockInvContents.Add(block, EvaluateInvOwner(block));
                    }

                    BlockInvPanels[block][0].Add(panel);
                }
                else
                {
                    panel.WriteText(CannotFindInvMessage);
                }
            }

            //sort them
            List<IMyTerminalBlock> keys = new List<IMyTerminalBlock>(BlockInvPanels.Keys);

            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var value = BlockInvPanels[key][0];

                SortedDictionary<int, List<IMyTextPanel>> sortedDict = new SortedDictionary<int, List<IMyTextPanel>>();

                for (int j = 0; j < value.Count; j++)
                {
                    IMyTextPanel panel = value[j];

                    if (!panel.IsFunctional || !panel.IsWorking)
                        continue;

                    string[] nameParts = panel.CustomName.Trim().Split(' ');
                    string[] titleParts = panel.GetPublicTitle().Trim().Split(' ');

                    string indexPart = "";

                    int index;
                    int test;

                    if (nameParts.Length >= 2 && panel.CustomName.StartsWith(BlockInvPanelsPrefix))
                    {
                        // workaround for stupid people that cant follow instructions properly
                        if (nameParts[1].Length < 2)
                            if (nameParts.Length >= 3 && nameParts[1].Equals("#") && int.TryParse(nameParts[2], out test))
                                indexPart = nameParts[2];
                            else
                                continue;
                        else
                            indexPart = nameParts[1].Substring(1);

                        if (nameParts[1].StartsWith("#") && int.TryParse(indexPart, out index) && index >= 0)
                        {
                            if (!sortedDict.ContainsKey(index))
                            {
                                sortedDict.Add(index, new List<IMyTextPanel>());
                                DebugString.AppendLine(string.Format("[Name]BIP New list at index: {0}", index));
                            }
                            sortedDict[index].Add(panel);
                            DebugString.AppendLine(string.Format("[Name]BIP Add panel at index: {0}", index));
                        }
                    }
                    else if (titleParts.Length >= 2 && panel.GetPublicTitle().StartsWith(BlockInvPanelsPrefix))
                    {
                        DebugString.AppendLine(string.Format("[Title]BIP trying to add panel {0}", panel.CustomName));
                        // workaround for stupid people that cant follow instructions properly
                        if (titleParts[1].Length < 2)
                            if (titleParts.Length >= 3 && titleParts[1].Equals("#") && int.TryParse(titleParts[2], out test))
                                indexPart = titleParts[2];
                            else
                                continue;
                        else
                            indexPart = titleParts[1].Substring(1);

                        if (titleParts[1].StartsWith("#") && int.TryParse(indexPart, out index) && index >= 0)
                        {
                            if (!sortedDict.ContainsKey(index))
                            {
                                sortedDict.Add(index, new List<IMyTextPanel>());
                                DebugString.AppendLine(string.Format("[Title]BIP New list at index: {0}", index));
                            }
                            sortedDict[index].Add(panel);
                            DebugString.AppendLine(string.Format("[Title]BIP Add panel at index: {0}", index));
                        }
                    }
                }

                BlockInvPanels[key] = new List<List<IMyTextPanel>>(sortedDict.Values);
            }
        }

        private bool TryGetBlockWithInvBehind(IMyTextPanel panel, out IMyTerminalBlock block)
        {
            block = null;
            Matrix result;
            panel.Orientation.GetMatrix(out result);

            if (result != null)
            {
                Vector3I backwardPosition = panel.Position - new Vector3I(result.Backward.X, result.Backward.Y, result.Backward.Z);

                var slimBlock = panel.CubeGrid.GetCubeBlock(backwardPosition);
                if (slimBlock == null)
                    return false;

                block = slimBlock.FatBlock  as IMyTerminalBlock;
                if (block == null || !block.HasInventory)
                    return false;

                return true;
            }
            return false;
        }

        void EvaluateInv(IMyInventory inv)
        {
            List<MyInventoryItem> items = new List<MyInventoryItem>();
            inv.GetItems(items);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Amount > 0)
                {
                    if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Ore"))
                    {
                        if (Ores.ContainsKey(item.Type.SubtypeId.ToString()))
                        {
                            Ores[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                            //DebugString.AppendLine(string.Format("+= {0}: {1} = {2}", item.Content.SubtypeName, (float)item.Amount, Ores[item.Content.SubtypeName]));
                            continue;
                        }

                        Ores.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        //DebugString.AppendLine(string.Format("Add {0}: {1}", item.Content.SubtypeName, (float)item.Amount));
                    }
                    else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Ingot"))
                    {
                        var subtypeName = item.Type.SubtypeId.ToString();
                        if (subtypeName.Equals("Stone"))
                            subtypeName = "Gravel";
                        if (Ingots.ContainsKey(subtypeName))
                        {
                            Ingots[subtypeName] += (float)item.Amount;
                            //DebugString.AppendLine(string.Format("+= {0}: {1} = {2}", subtypeName, (float)item.Amount, Ingots[item.Content.SubtypeName]));
                            continue;
                        }

                        Ingots.Add(subtypeName, (float)item.Amount);
                        //DebugString.AppendLine(string.Format("Add {0}: {1}", item.Content.SubtypeName, (float)item.Amount));
                    }
                    else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Component"))
                    {
                        if (Components.ContainsKey(item.Type.SubtypeId.ToString()))
                        {
                            Components[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                            //DebugString.AppendLine(string.Format("+= {0}: {1} = {2}", item.Content.SubtypeName, (float)item.Amount, Components[item.Content.SubtypeName]));
                            continue;
                        }

                        Components.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        //DebugString.AppendLine(string.Format("Add {0}: {1}", item.Content.SubtypeName, (float)item.Amount));
                    }
                    else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_AmmoMagazine"))
                    {
                        if (Ammo.ContainsKey(item.Type.SubtypeId.ToString()))
                        {
                            Ammo[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                            //DebugString.AppendLine(string.Format("+= {0}: {1} = {2}", item.Content.SubtypeName, (float)item.Amount, Ammo[item.Content.SubtypeName]));
                            continue;
                        }

                        Ammo.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        //DebugString.AppendLine(string.Format("Add {0}: {1}", item.Content.SubtypeName, (float)item.Amount));
                    }
                }
            }
        }

        private List<string> EvaluateInvOwner(IMyTerminalBlock block)
        {
            List<string> result = new List<string>();

            if (!block.HasInventory)
                return result;

            var inventoryCount = block.InventoryCount;

            Dictionary<string, float> ores = new Dictionary<string, float>();
            Dictionary<string, float> ingots = new Dictionary<string, float>();
            Dictionary<string, float> components = new Dictionary<string, float>();
            Dictionary<string, float> ammo = new Dictionary<string, float>();

            for (int invCount = 0; invCount < inventoryCount; invCount++)
            {
                var inv = block.GetInventory(invCount);
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                inv.GetItems(items);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item.Amount > 0)
                    {
                        if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Ore"))
                        {
                            if (ores.ContainsKey(item.Type.SubtypeId.ToString()))
                            {
                                ores[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                                continue;
                            }

                            ores.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        }
                        else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Ingot"))
                        {
                            var subtypeName = item.Type.SubtypeId.ToString();
                            if (subtypeName.Equals("Stone"))
                                subtypeName = "Gravel";
                            if (ingots.ContainsKey(subtypeName))
                            {
                                ingots[subtypeName] += (float)item.Amount;
                                continue;
                            }

                            ingots.Add(subtypeName, (float)item.Amount);
                        }
                        else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_Component"))
                        {
                            if (components.ContainsKey(item.Type.SubtypeId.ToString()))
                            {
                                components[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                                continue;
                            }

                            components.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        }
                        else if (item.Type.TypeId.ToString().Equals("MyObjectBuilder_AmmoMagazine"))
                        {
                            if (ammo.ContainsKey(item.Type.SubtypeId.ToString()))
                            {
                                ammo[item.Type.SubtypeId.ToString()] += (float)item.Amount;
                                continue;
                            }

                            ammo.Add(item.Type.SubtypeId.ToString(), (float)item.Amount);
                        }
                    }
                }
            }

            SortedDictionary<string, float> sortedOres = new SortedDictionary<string, float>(ores);

            if (sortedOres.Count > 0)
            {
                result.Add("Ores:");
                result.Add("");

                List<string> oreKeys = new List<string>(sortedOres.Keys);
                for (int i = 0; i < oreKeys.Count; i++)
                {
                    string key = oreKeys[i];
                    result.Add(string.Format("{0}: {1}", key, sortedOres[key].ToString(NumberFormat)));
                }

                result.Add("");
            }

            SortedDictionary<string, float> sortedIngots = new SortedDictionary<string, float>(ingots);

            if (sortedIngots.Count > 0)
            {
                result.Add("Ingots:");
                result.Add("");

                List<string> ingotKeys = new List<string>(sortedIngots.Keys);
                for (int i = 0; i < ingotKeys.Count; i++)
                {
                    string key = ingotKeys[i];
                    result.Add(string.Format("{0}: {1}", key, sortedIngots[key].ToString(NumberFormat)));
                }

                result.Add("");
            }

            SortedDictionary<string, float> sortedComponents = new SortedDictionary<string, float>(components);

            if (sortedComponents.Count > 0)
            {
                result.Add("Components:");
                result.Add("");

                List<string> componentKeys = new List<string>(sortedComponents.Keys);
                for (int i = 0; i < componentKeys.Count; i++)
                {
                    string key = componentKeys[i];
                    result.Add(string.Format("{0}: {1}", key, sortedComponents[key].ToString(NumberFormat)));
                }

                result.Add("");
            }

            SortedDictionary<string, float> sortedAmmo = new SortedDictionary<string, float>(ammo);

            if (sortedAmmo.Count > 0)
            {
                result.Add("Ammo:");
                result.Add("");

                List<string> ammoKeys = new List<string>(sortedAmmo.Keys);
                for (int i = 0; i < ammoKeys.Count; i++)
                {
                    string key = ammoKeys[i];
                    result.Add(string.Format("{0}: {1}", key, sortedAmmo[key].ToString(NumberFormat)));
                }
            }

            return result;
        }

        void SortPanels()
        {
            for (int i = 0; i < AllPrefixes.Length; i++)
            {
                List<IMyTextPanel> panels = GetTextPanels();
                DebugString.AppendLine(string.Format("Found {0} TextPanels.", panels.Count));
                if (panels.Count == 0)
                    continue;

                SortedDictionary<int, List<IMyTextPanel>> sortedDict = SortPanelsWithName(AllPrefixes[i], panels);

                AllPanels[i] = new List<List<IMyTextPanel>>(sortedDict.Values);

                //for (int index = 0; index < AllPanels[i].Count; index++)
                //DebugString.AppendLine(string.Format("{2}: At index {0} there are {1} panels.", index, AllPanels[i][index].Count, AllPrefixes[i]));
                DebugString.AppendLine(string.Format("Found {0} panels for '{1}'", AllPanels[i].Count, AllPrefixes[i]));

            }

            RessourcesPanels = AllPanels[0];
            OrePanels = AllPanels[1];
            IngotPanels = AllPanels[2];
            ComponentPanels = AllPanels[3];
            AmmoPanels = AllPanels[4];
            StatusPanels = AllPanels[5];
        }

        private SortedDictionary<int, List<IMyTextPanel>> SortPanelsWithName(string name, List<IMyTextPanel> panels)
        {
            SortedDictionary<int, List<IMyTextPanel>> sortedDict = new SortedDictionary<int, List<IMyTextPanel>>();
            for (int i = 0; i < panels.Count; i++)
            {
                IMyTextPanel panel = panels[i];

                if (!panel.IsFunctional || !panel.IsWorking)
                    continue;

                string[] nameParts = panel.CustomName.Trim().Split(' ');
                string[] titleParts = panel.GetPublicTitle().Trim().Split(' ');
                DebugString.AppendLine(string.Format("Title Parts: {0}", string.Join(", ", titleParts)));

                string indexPart = "";

                int ind;
                int test;

                if (nameParts.Length >= 2 && nameParts[0].Equals(name.Split(' ')[0]))
                {
                    // workaround for stupid people that cant follow instructions properly
                    if (nameParts[1].Length < 2)
                        if (nameParts.Length >= 3 && nameParts[1].Equals("#") && int.TryParse(nameParts[2], out test))
                            indexPart = nameParts[2];
                        else
                            continue;
                    else
                        indexPart = nameParts[1].Substring(1);

                    if (nameParts[1].StartsWith("#") && int.TryParse(indexPart, out ind) && ind >= 0)
                    {
                        if (!sortedDict.ContainsKey(ind))
                        {
                            sortedDict.Add(ind, new List<IMyTextPanel>());
                            DebugString.AppendLine(string.Format("New list at index: {0}", ind));
                        }
                        sortedDict[ind].Add(panel);
                        DebugString.AppendLine(string.Format("Add panel at index: {0}", ind));
                    }
                }
                else if (titleParts.Length >= 2 && titleParts[0].Equals(name.Split(' ')[0]))
                {
                    // workaround for stupid people that cant follow instructions properly
                    if (titleParts[1].Length < 2)
                        if (titleParts.Length >= 3 && titleParts[1].Equals("#") && int.TryParse(titleParts[2], out test))
                            indexPart = titleParts[2];
                        else
                            continue;
                    else
                        indexPart = titleParts[1].Substring(1);

                    if (titleParts[1].StartsWith("#") && int.TryParse(indexPart, out ind) && ind >= 0)
                    {
                        if (!sortedDict.ContainsKey(ind))
                        {
                            sortedDict.Add(ind, new List<IMyTextPanel>());
                            DebugString.AppendLine(string.Format("New list at index: {0}", ind));
                        }
                        sortedDict[ind].Add(panel);
                        DebugString.AppendLine(string.Format("Add panel at index: {0}", ind));
                    }
                }
            }
            return sortedDict;
        }

        IMyBlockGroup GetBlockGroup(string name)
        {
            for (int i = 0; i < BlockGroups.Count; i++)
            {
                if (name.Equals(BlockGroups[i].Name))
                    //return the first group which name matches with the given string
                    return BlockGroups[i];
            }
            //return null if no group can be found, don't forget to check whether the groups exist via nullcheck
            return null;
        }

        bool GetBlockWithExactName(string name, out IMyTerminalBlock block)
        {
            block = null;
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < Blocks.Count; i++)
            {
                if (Blocks[i].CustomName.Equals(name))
                {
                    block = Blocks[i];
                    return true;
                }
            }
            return false;
        }

        private List<IMyTextPanel> GetTextPanels(List<IMyTerminalBlock> list = null)
        {
            List<IMyTerminalBlock> source = list ?? Blocks;
            List<IMyTextPanel> result = new List<IMyTextPanel>();
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] is IMyTextPanel)
                {
                    result.Add((IMyTextPanel)source[i]);
                }
            }

            return result;
        }

        private double GetStockpiledOxygen(List<IMyTerminalBlock> oxyTanks = null)
        {
            double result = 0;
            int count = 0;
            var source = oxyTanks ?? new List<IMyTerminalBlock>();

            if (oxyTanks == null)
                GridTerminalSystem.GetBlocksOfType<IMyGasTank>(source);

            for (int i = 0; i < source.Count; i++)
            {
                IMyGasTank oxygenTank = source[i] as IMyGasTank;
                if (oxygenTank == null)
                    continue;

                result += oxygenTank.FilledRatio;
                count++;
            }
            return count != 0 ? result / count : 0;
        }

        private float GetBatteryPower(List<IMyTerminalBlock> batteries = null)
        {
            float result = 0;
            int count = 0;
            var source = batteries ?? new List<IMyTerminalBlock>();

            if (batteries == null)
                GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(source);

            for (int i = 0; i < source.Count; i++)
            {
                IMyBatteryBlock batteryBlock = source[i] as IMyBatteryBlock;
                if (batteryBlock == null)
                    continue;

                result += batteryBlock.CurrentStoredPower/batteryBlock.MaxStoredPower;
                count++;
            }

            return count != 0 ? result / count : 0f;
        }

        private float GetCargoSpace(List<IMyTerminalBlock> cargoContainers = null)
        {
            float result = 0;
            int count = 0;
            var source = cargoContainers ?? new List<IMyTerminalBlock>();

            if (cargoContainers == null)
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(source);

            for (int i = 0; i < source.Count; i++)
            {
                IMyCargoContainer cargoContainer = source[i] as IMyCargoContainer;
                if (cargoContainer == null)
                    continue;

                IMyInventory inventory = cargoContainer.GetInventory(0);

                float currentVolume = inventory.CurrentVolume.RawValue;
                float maxVolume = inventory.MaxVolume.RawValue;

                result +=  currentVolume / maxVolume;
                DebugString.AppendLine(string.Format("{0} / {1} = {2}", currentVolume, maxVolume, currentVolume / maxVolume));
                count++;
            }

            return count != 0 ? result / count : 0f;
        }

        #endregion
    }
}