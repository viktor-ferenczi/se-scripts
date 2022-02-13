        /*
        Robot10'S SHIP OPERATIONS SCRIPT

        The goal of this script is to provide a large amount of utility functions
         for ships for my fleet catalog in one Programmable Block.

        I really don't know what I'm doing and it probably shows!

        Implemented so far:
        -Getting available inventory from tagged cargo containers.
        -Setting item quotas, getting shortages.
        -Resupplying ship based on shortages; on argument only when docked.
        -LCD display of inventory and shortages.
        -Get missile reloads based on current inventory.
        -Basic Scheduler

        Planned:
        -Break up Resupply function over more ticks
        -Displaying available PMW reloads to cockpit LCD
        -Calculate max takeoff weight with ship in current gravity
        -Display current weight as a percentage of max takeoff weight in cockpit
        -Automatically turn off all drills when approaching max takeoff weight in gravity, notify in cockpit
        -Ship info display including: 
            -Total H tank fill
            -Ship Dry Mass, Cargo Mass, Total Mass, Max liftof Mass, % of max liftoff mass
            -Ammo levels            

        Arguments
            arguments : list arguments
            resupply : If docked, resupplies ship to inventory quotas
            rescan : rescans ship for block changes
        */

        List<InvItem> Inventory = new List<InvItem>();
        string cargoPrefix = "[CARGO]";     //Put this prefix at the beginning of the name of each cargo container that will be tracked and resupplied.

        string ComponentLCDPrefix = "[CompLCD]";
        List<IMyTextPanel> ComponentLCDs = new List<IMyTextPanel>();
        string OreLCDPrefix = "[OreLCD]";
        List<IMyTextPanel> OreLCDs = new List<IMyTextPanel>();
        string IngotLCDPrefix = "[IngotLCD]";
        List<IMyTextPanel> IngotLCDs = new List<IMyTextPanel>();
        string ItemLCDPrefix = "[ItemLCD]";
        List<IMyTextPanel> ItemLCDs = new List<IMyTextPanel>();
        string ReloadLCDPrefix = "[ReloadLCD]";
        List<IMyTextPanel> ReloadLCDs = new List<IMyTextPanel>();
        string InfoLCDPrefix = "[InfoLCD]";
        List<IMyTextPanel> InfoLCDs = new List<IMyTextPanel>();
        public Program()
        {
            //Add all items you want tracked/resupplied.  See TypeId/SubTypeId listing link.
            //https://github.com/malware-dev/MDK-SE/wiki/Type-Definition-Listing
            //--------------------------------TypeId-------------------SubTypeId------------name-------quota--perPMW
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "BulletproofGlass",   "BpGlass",     100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Canvas",             "Canvas",      100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Computer",           "Computer",    100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Construction",       "Construct",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Detector",           "Detector",    100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Display",            "Display",     100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Explosives",         "Splodeo's",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Girder",             "Girder",      100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "GravityGenerator",   "GravComp",    100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "InteriorPlate",      "InterPlate",  100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "LargeTube",          "LargeTube",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Medical",            "Medical",     100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "MetalGrid",          "MetalGrid",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Motor",              "Motor",       100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "PowerCell",          "PowerCell",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "RadioCommunication", "Radio",       100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Reactor",            "Reactor",     100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "SmallTube",          "SmallTube",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "SolarCell",          "SolarCell",   100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "SteelPlate",         "SteelPlate",  100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Superconductor",     "Supercon",    100,   10));
            Inventory.Add(new InvItem("MyObjectBuilder_Component", "Thrust",             "Thruster",    100,   10));

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Echo("------INITIALIZING------");
            FindController();
            FindLCDs();
            Resupply();
            UpdateInventory();
            UpdateShortages();
            UpdateMissileReloads();
            UpdateMass();
            UpdateLCDs();
            MainStep = 1;
            resupplyStep = 0;
        }
        //game runs 60ticks/sec at 1.0sim speed.  For update100, 20 ticks = 33.3333 seconds

        //DO NOT CHANGE program variables below.  
        float DryMass = 0;
        float TotalMass = 0;
        float CargoMass = 0;

        int ShortageCount = 0;
        int MissileReloads = 0;

        double MaxRunTime = 0;

        string LastAction = null;
        string LastMessage = null;
        string Message1 = null;
        string Message2 = null;
        string Message3 = null;
        string Message4 = null;
        string Message5 = null;

        bool init = true;
        int MainStep = 1; // 1=UpdateInventory  2=UpdateShortages   3=UpdateReloads  4=UpdateMass   5=UpdateLCDs

        public void Main(string argument, UpdateType updateSource)
        {
            if (init == false)
            {
                if (Runtime.LastRunTimeMs > MaxRunTime)
                { MaxRunTime = Runtime.LastRunTimeMs; }
                Echo("LastRun:" + Runtime.LastRunTimeMs + "ms  MaxRun:" + MaxRunTime + "ms");
            }

            LastAction = "------Waiting";
            LastMessage = null;

            if (argument.Length > 0) { HandleArguments(argument); }
            else if (resupplyStep > 0) { Resupply(); } 
            else    //normal update loop
            {
                if (MainStep == 1) { UpdateInventory(); }
                else if (MainStep == 2) { UpdateShortages(); }
                else if (MainStep == 3) { UpdateMissileReloads(); }
                else if (MainStep == 4) { UpdateMass(); }
                else if (MainStep == 5) { UpdateLCDs(); }
            }

            if (LastMessage != null)
            { Message5 = Message4; Message4 = Message3; Message3 = Message2; Message2 = Message1; Message1 = LastMessage; }

            Echo("-" + StringOffset(LastAction, 35, "-") + (MainStep) + "/" + "6");
            Echo(Message5);
            Echo(Message4);
            Echo(Message3);
            Echo(Message2);
            Echo(Message1);

            if (MainStep > 5) { MainStep = 1; init = false; }
        }

        //--------Methods to do stuff below--------
        void HandleArguments(string argument)
        {
            switch (argument.ToLower().Trim())
            {
                case "arguments":
                    Echo("------List of Arguments------");
                    Echo("rescan - scans grid for block changes");
                    Echo("resupply - Resupply when docked");
                    break;

                case "resupply":
                    Resupply();
                    break;

                case "rescan":
                    FindController();
                    FindLCDs();
                    break;

                case "return":
                    Echo("Script working, probably");
                    break;
            }
        }

        List<IMyCargoContainer> myCargoContainers = new List<IMyCargoContainer>();
        List<MyInventoryItem> newItems = new List<MyInventoryItem>();
        int UpdateInventoryStep = 0;
        void UpdateInventory()
        {
            LastAction = "------Updating Inventory";
            if (UpdateInventoryStep == 0)
            {
                myCargoContainers.Clear(); newItems.Clear();
                //Create list of tagged cargo containers on the same mechanical grid/subgrid as the PB this script runs on.
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(myCargoContainers, block => (block.CustomName.StartsWith(cargoPrefix) && block.IsSameConstructAs(Me)));
                LastMessage = "Found " + myCargoContainers.Count + " Cargo Containers";
                UpdateInventoryStep = 1;
                return;
            }
            if (UpdateInventoryStep == 1)
            {
                //Create list of new items from all cargo containers
                myCargoContainers.ForEach(x => { x.GetInventory().GetItems(newItems); });
                LastMessage = "Found " + newItems.Count + " Item Stacks";
                UpdateInventoryStep = 2;
                return;
            }
            if (UpdateInventoryStep == 2)
            {
                for (int j = 0; j < Inventory.Count(); j++)    //Zero all current inventory quantities before recount.
                { Inventory[j].setQty(0); }
                UpdateInventoryStep = 3;
                return;
            }
            if (UpdateInventoryStep == 3)
            {
                for (int k = 0; k < newItems.Count; k++)    //Add list of new items to total items in Inventory.
                {
                    for (int l = 0; l < Inventory.Count(); l++)    //Go through each inventory stack looking for Inventory array matches.                    {
                    {
                        if (newItems[k].Type.SubtypeId == Inventory[l].getSubTypeId())
                        {
                            Inventory[l].setQty(Inventory[l].getQty() + (float)newItems[k].Amount);
                            break;
                        }
                    }
                }
                UpdateInventoryStep = 0; MainStep++;
            }
        }

        void UpdateShortages()
        {
            LastAction = "------Updating Shortages";
            ShortageCount = 0;  //reset before recount
            for (int i = 0; i < Inventory.Count(); i++)
            {
                Inventory[i].updateShortage();
                if (Inventory[i].getShortage() > 0) { ShortageCount++; }
            }
            LastMessage = "Found " + ShortageCount + " Shortages";
            MainStep++;
        }

        void UpdateMissileReloads()
        {
            LastAction = "------Updating Reloads";
            double currentvar = 0;
            double lowestvar = 0;
            for (int i = 0; i < Inventory.Count(); i++)
            {
                if (Inventory[i].getQty() == 0) { continue; }
                if (Inventory[i].getPerPMW() == 0) { continue; }

                currentvar = Math.Floor(Inventory[i].getQty() / Inventory[i].getPerPMW());
                if (lowestvar == 0 && currentvar > lowestvar) { lowestvar = currentvar; }
                if (lowestvar != 0 && currentvar < lowestvar) { lowestvar = currentvar; }
            }
            MissileReloads = (int)lowestvar;
            LastMessage = MissileReloads + " Missile Reloads Remaining";
            MainStep++;
        }

        //Variables to be used by Resupply in between ticks and calls.
        List<IMyCargoContainer> DestinationContainers = new List<IMyCargoContainer>();
        List<IMyCargoContainer> SourceContainers = new List<IMyCargoContainer>();
        float tempShortage = 0;
        MyItemType tempShortageType = new MyItemType();
        int inv = 0;    //Inventory array index
        int src = 0;    //Source Container index
        int dest = 0;   //Destination Container index
        IMyInventory SourceInventory = null;
        float tempPullQty = 0;
        bool AttemptedPull = false;
        int resupplyStep = 0;
        void Resupply()
        {
            LastAction = "------Reuspplying Ship";
            if (ShortageCount == 0) { LastMessage = "--No shortages, resupply unnecessary"; return; }    //catch resupply unnecessary

            if (resupplyStep == 0)  //Get list of destination containers on my ship
            {
                DestinationContainers.Clear(); SourceContainers.Clear(); tempShortage = 0; inv = 0;   //clear values from previous runs

                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(DestinationContainers, block => (block.CustomName.StartsWith(cargoPrefix) && block.IsSameConstructAs(Me)));
                //Echo("DEBUG: Found " + DestinationContainers.Count + " Destination Containers");
                if (DestinationContainers.Count == 0) { LastMessage = "-No destination containers found on this ship"; Echo(LastMessage); resupplyStep = 0; return; }
                else { resupplyStep = 1; return; }
            }

            if (resupplyStep == 1)  //Get list of source containers that are not on my ship but are connected
            {
                GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(SourceContainers, block => (!(block.IsSameConstructAs(Me))));
                //Echo("DEBUG: Found " + SourceContainers.Count + " Source Containers");
                if (SourceContainers.Count == 0) { LastMessage = "-No source containers found.\n-Are you even docked?"; Echo(LastMessage); resupplyStep = 0; return; }
                else { resupplyStep = 2; return; }
            }

            if (resupplyStep == 2)   //Get a shortage, look for it in source containers.
            {
                while (inv < Inventory.Count())    //Go through list of Inventory looking for shortages.
                {
                    tempShortage = Inventory[inv].getShortage();
                    if (tempShortage > 0)      //If there is a shortage of this item, look for that item to pull
                    {
                        tempShortageType = new MyItemType(Inventory[inv].getTypeId(), Inventory[inv].getSubTypeId());
                        LastMessage = "Searching for  " + tempShortage + "  " + Inventory[inv].getName();
                        for (src = 0; src < SourceContainers.Count; src++)      //Go through each source container looking for shortage
                        {
                            SourceInventory = SourceContainers[src].GetInventory();    //Get inventory in current source container
                            if (SourceInventory.FindItem(tempShortageType) != null)    //If shortage found, pull it
                            {
                                float SourceQty = (float)SourceInventory.GetItemAmount(tempShortageType);

                                if (SourceQty >= tempShortage)      //If entire shortage found, set entire shortage to pull, quit looking for shortage
                                { tempPullQty = tempShortage; }
                                else                                //if partial shortage found, set amount available to pull
                                { tempPullQty = SourceQty; }

                                LastMessage = LastMessage + "\nFound " + SourceQty + " " + Inventory[inv].getName();
                                resupplyStep = 3; return;
                            }
                        }
                        LastMessage = LastMessage + "\nNo " + Inventory[inv].getName() + " found to pull";
                    }
                    inv++;
                }
                LastMessage = LastMessage + "\n--RESUPPLY ATTEMPTED \n--check for remaining shortages";
                resupplyStep = 0; return;
            }

            if (resupplyStep == 3)   //Get destination containers, pull items from source
            {
                int FullDestinationContainers = 0; LastMessage = "";
                for (dest = 0; dest < DestinationContainers.Count; dest++)  //Look for Destination containers that aren't full
                {
                    IMyInventory DestinationInventory = DestinationContainers[dest].GetInventory();
                    if (DestinationInventory.IsFull == true) { FullDestinationContainers++; continue; }    //catch full container
                    if (SourceInventory.CanTransferItemTo(DestinationInventory, tempShortageType) == false) { LastMessage = "Souce is not conveyored to Destination"; continue; }
                    if (DestinationInventory.CanItemsBeAdded((MyFixedPoint)Double.Parse(tempPullQty.ToString()), tempShortageType) == true)
                    {
                        AttemptedPull = true; LastMessage = "Pulling " + tempPullQty + " " + Inventory[inv].getName();
                        SourceInventory.TransferItemTo(DestinationInventory, SourceInventory.FindItem(tempShortageType).Value, (MyFixedPoint)Double.Parse(tempPullQty.ToString()));
                        break;
                    }
                    else if (DestinationInventory.CanItemsBeAdded((MyFixedPoint)Double.Parse((tempPullQty / 2).ToString()), tempShortageType) == true)
                    {
                        AttemptedPull = true; LastMessage = "Pulling " + tempPullQty / 2 + " " + Inventory[inv].getName();
                        SourceInventory.TransferItemTo(DestinationInventory, SourceInventory.FindItem(tempShortageType).Value, (MyFixedPoint)Double.Parse((tempPullQty / 2).ToString()));
                        break;
                    }
                }
                if (FullDestinationContainers == DestinationContainers.Count()) { LastMessage = "All Destination Containers Full \n--RESUPPLY TERMINATED"; resupplyStep = 0; return; }
                if (AttemptedPull == false) { Echo("Not enough room in Containers."); resupplyStep = 2; inv++; return; }
                if (AttemptedPull == true) { AttemptedPull = false; resupplyStep = 2; inv++; return; }
            }

        }

        void FindLCDs()
        {
            LastAction = "------Finding LCDs";
            
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(ComponentLCDs, block => (block.CustomName.StartsWith(ComponentLCDPrefix) && block.IsSameConstructAs(Me)));
            Echo("Found " + ComponentLCDs.Count + " Component LCDs");
            foreach (IMyTextPanel ComponentLCD in ComponentLCDs) { ComponentLCD.ContentType = ContentType.TEXT_AND_IMAGE; ComponentLCD.Font = "Monospace"; ComponentLCD.FontSize = 0.74f; ComponentLCD.TextPadding = 1; }

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(OreLCDs, block => (block.CustomName.StartsWith(OreLCDPrefix) && block.IsSameConstructAs(Me)));
            Echo("Found " + OreLCDs.Count + " Ore LCDs");
            foreach (IMyTextPanel OreLCD in OreLCDs) { OreLCD.ContentType = ContentType.TEXT_AND_IMAGE; OreLCD.Font = "Monospace"; OreLCD.FontSize = 0.74f; OreLCD.TextPadding = 1; }

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(IngotLCDs, block => (block.CustomName.StartsWith(IngotLCDPrefix) && block.IsSameConstructAs(Me)));
            Echo("Found " + IngotLCDs.Count + " Ingot LCDs");
            foreach (IMyTextPanel IngotLCD in IngotLCDs) { IngotLCD.ContentType = ContentType.TEXT_AND_IMAGE; IngotLCD.Font = "Monospace"; IngotLCD.FontSize = 0.74f; IngotLCD.TextPadding = 1; }

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(ItemLCDs, block => (block.CustomName.StartsWith(ItemLCDPrefix) && block.IsSameConstructAs(Me)));
            Echo("Found " + ItemLCDs.Count + " Item LCDs");
            foreach (IMyTextPanel ItemLCD in ItemLCDs) { ItemLCD.ContentType = ContentType.TEXT_AND_IMAGE; ItemLCD.Font = "Monospace"; ItemLCD.FontSize = 0.74f; ItemLCD.TextPadding = 1; }

        }

        IMyShipController controller = null;
        List<IMyShipController> cockpits = new List<IMyShipController>();
        List<IMyShipController> controlStations = new List<IMyShipController>();
        void FindController()
        {
            LastAction = "------Find Controller";
            List<IMyTerminalBlock> list = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(list);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].CubeGrid == Me.CubeGrid && !list[i].CustomName.ToLower().Contains("cryo")
                    && !list[i].CustomName.ToLower().Contains("passenger"))
                {
                    controller = list[i] as IMyShipController;
                    Echo("DEBUG: Found a controller");
                    break;
                }
                if (i >= list.Count) { Echo("DEBUG: No ship controller found."); }
            }
        }

        void UpdateMass()
        {
            LastAction = "------Updating Mass";
            if (controller == null) { LastMessage = "No cockpit found \nmass calculation requires one"; return; }

            MyShipMass ShipMasses = controller.CalculateShipMass();

            DryMass = ShipMasses.BaseMass;      //Echo("DEBUG: DryMass = " + DryMass + "Kg");
            TotalMass = ShipMasses.TotalMass;   //Echo("DEBUG: TotalMass = " + TotalMass + "Kg");
            CargoMass = TotalMass - DryMass;    //Echo("DEBUG: CargoMass = " + CargoMass + "Kg");
            MainStep++;
        }

        int updateLCDstep = 0;
        void UpdateLCDs()
        {
            LastAction = "------Updating LCDs";
            string ItemDisplayHeader = "-Item-------Qty.---Shortage-Quota--";

            if (updateLCDstep == 0)
            {
                int ComponentOffset = 8;
                foreach (IMyTextPanel ComponentLCD in ComponentLCDs)
                {
                    ComponentLCD.WriteText(ItemDisplayHeader);
                    int linesWritten = 0;
                    for (int i = 0; i < Inventory.Count(); i++)
                    {
                        if (Inventory[i].getTypeId() == "MyObjectBuilder_Component")
                        {
                            string text = ("\n" + " " + StringOffset(Inventory[i].getName(), 11, " ") + StringOffset(Inventory[i].getQty().ToString(), ComponentOffset, " ") + StringOffset(Inventory[i].getShortage().ToString(), ComponentOffset, " ") + Inventory[i].getQuota().ToString());
                            ComponentLCD.WriteText(text, true); linesWritten = linesWritten + 1;
                        }
                    }
                    if (linesWritten == 0) { ComponentLCD.WriteText("\n\nNo components tracked in Inventory \nCheck script for instructions to \nadd Components or remove this LCD", true); }
                }
                updateLCDstep = 1; return;

            }

            if (updateLCDstep == 1)
            {
                int IngotOffset = 8;
                foreach (IMyTextPanel IngotLCD in IngotLCDs)
                {
                    IngotLCD.WriteText(ItemDisplayHeader);
                    int linesWritten = 0;
                    for (int i = 0; i < Inventory.Count(); i++)
                    {
                        if (Inventory[i].getTypeId() == "MyObjectBuilder_Ingot")
                        {
                            string text = ("\n" + " " + StringOffset(Inventory[i].getName(), 11, " ") + StringOffset(Inventory[i].getQty().ToString(), IngotOffset, " ") + StringOffset(Inventory[i].getShortage().ToString(), IngotOffset, " ") + Inventory[i].getQuota().ToString());
                            IngotLCD.WriteText(text, true); linesWritten = linesWritten + 1;
                        }
                    }
                    if (linesWritten == 0) { IngotLCD.WriteText("\n\nNo ingots tracked in Inventory \nCheck script for instructions to \nadd ingots or remove this LCD", true); }
                }
                updateLCDstep = 2; return;
            }

            if (updateLCDstep == 2)
            {
                int OreOffset = 8;
                foreach (IMyTextPanel OreLCD in OreLCDs)
                {
                    OreLCD.WriteText(ItemDisplayHeader);
                    int linesWritten = 0;
                    for (int i = 0; i < Inventory.Count(); i++)
                    {
                        if (Inventory[i].getTypeId() == "MyObjectBuilder_Ore")
                        {
                            string text = ("\n" + " " + StringOffset(Inventory[i].getName(), 11, " ") + StringOffset(Inventory[i].getQty().ToString(), OreOffset, " ") + StringOffset(Inventory[i].getShortage().ToString(), OreOffset, " ") + Inventory[i].getQuota().ToString());
                            OreLCD.WriteText(text, true); linesWritten = linesWritten + 1;
                        }
                    }
                    if (linesWritten == 0) { OreLCD.WriteText("\n\nNo ores tracked in Inventory \nCheck script for instructions to \nadd ores or remove this LCD", true); }
                }
                updateLCDstep = 3; return;
            }

            if (updateLCDstep == 3)
            {
                int ItemOffset = 8;
                foreach (IMyTextPanel ItemLCD in ItemLCDs)
                {
                    ItemLCD.WriteText(ItemDisplayHeader);
                    int linesWritten = 0;
                    for (int i = 0; i < Inventory.Count(); i++)
                    {
                        if (Inventory[i].getTypeId() != "MyObjectBuilder_Ore" && Inventory[i].getTypeId() != "MyObjectBuilder_Ingot" && Inventory[i].getTypeId() != "MyObjectBuilder_Component")
                        {
                            string text = ("\n" + " " + StringOffset(Inventory[i].getName(), 11, " ") + StringOffset(Inventory[i].getQty().ToString(), ItemOffset, " ") + StringOffset(Inventory[i].getShortage().ToString(), ItemOffset, " ") + Inventory[i].getQuota().ToString());
                            ItemLCD.WriteText(text, true); linesWritten = linesWritten + 1;
                        }
                    }
                    if (linesWritten == 0) { ItemLCD.WriteText("\n\nNo items tracked in Inventory \nCheck script for instructions to \nadd items or remove this LCD", true); }
                }
                updateLCDstep = 0;  MainStep++; return;
            }
        }


        string StringOffset(string String, int offset, string character)   //Returns a string of spaces of a certain amount to evenly space characters on an LCD
        {
            string spaces = null;
            for (int i = 0; i < (offset - String.Length); i++)
            { spaces = spaces + character; }
            return (String + spaces);
        }

        /*
        void UpdatePanels()
        {
            cockpit = (IMyCockpit)GridTerminalSystem.GetBlockWithName(name_of_cockpit);
            panel = (IMyTextSurface)cockpit.GetSurface(cockpit_lcd_index);
            panel.WriteText(text, false);
        }
        */




        public class InvItem
        {
            public string TypeId;
            public string SubTypeId;
            public string ShortName;
            public float Quantity;
            public float Quota;
            public float Shortage;
            public float perPMW;

            public InvItem(string typeid, string subtypeid, string shortname, float quota, float per)
            {
                TypeId = typeid;
                SubTypeId = subtypeid;
                ShortName = shortname;
                Quantity = 0;
                Quota = quota;
                Shortage = 0;
                perPMW = per;
            }

            public string getTypeId()
            { return TypeId; }
            public string getSubTypeId()
            { return SubTypeId; }
            public string getName()
            { return ShortName; }
            public float getQty()
            { return Quantity; }
            public void setQty(float qty)
            { Quantity = qty; }
            public float getQuota()
            { return Quota; }
            public float getShortage()
            { return Shortage; }
            public void updateShortage()
            { Shortage = Quota - Quantity; }
            public float getPerPMW()
            { return perPMW; }
        }