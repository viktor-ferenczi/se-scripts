// ReSharper disable ConvertConstructorToMemberInitializers
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable RedundantUsingDirective
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedType.Global
// ReSharper disable CheckNamespace

// Import everything available for PB scripts in-game

using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;


namespace KTZInv
{
    // https://pastebin.com/raw/dfFTfVrp
    // ReSharper disable once UnusedType.Global
    class Program : MyGridProgram
    {
/*
 * R e a d m e
 * -----------
 *
 * In this file you can include any instructions or other comments you want to have injected onto the
 * top of your final script. You can safely delete this file if you do not want any such comments.
 */

        static bool USE_SKITS = false; //whether to use survival kits in autocrafting
        static bool ASSEMBLE = true; //auto assemble items to the quota dictated by Autocraft LCD
        static bool DISASSEMBLE = false; //auto disassemble items in excess of quota
        static int ASSEMBLE_MARGIN = 50; //margin of error around quota before doing any of that
        static bool ASM_FLUSH = true; //whether to periodically clear inputs of an assembler that is not producing
        static bool ASM_SHUFFLE = false; //whether to periodically move the first item to back of queue if not producing


        static int MAX_TRANSFERS_PER_OP = 4;
//moving items is fundamentally expensive, moreso that it seems. this limits the amount of actual stacks moved in a given operation.
//Lower numbers make performance more consistent and thus reliable with things like pblimiter, but
//can result in it taking several passes over the grid to get things to their final destinations if things are sufficiently disordered.
        static double MAX_TRANSFER_MS = 0.05; //secondary sancheck

        static bool SORT = true; //whether to do any of that item sorting stuff at all.
        //Note: Even if sorting isn't otherwise used, input flushing of jammed assemblers won't
        //happen without a tagged cargo for Ingots, Components
        static bool MERGE_STACKS = true;
//whether to merge multiple stacks of the same itemtype in a given container when possible
//could cause desync, they say? idk. stacks should only be fragmented like this by player action

        static bool ISYCOMPAT = true;

//these are blocks by DefinitionDisplayNameText
//typically containers that cannot or should not be managed
        static string[] lockBlockTypes =
        {
        };
        static string[] hiddenBlockTypes =
        {
            "Cargo Crate",
            "Lockers",
            "Armory Lockers",
            "Armory",
            "Weapon Rack",
            "Control Seat",
            "Parachute Hatch",
            "Control Station",
            "Vending Machine"
        };

//PERFORMANCE CONTROLS
        static bool CARGODBG = false;
        static double maxScriptTimeMSPerSec = 0.1; //maximum ms per sec, exceeding causes the script to pause itself to compensate
        static public int blockInterval = 5; //ticks to wait for next block scan when nothing has been moved
        static public int blockIntervalMove = 15; //ticks to wait if the last op required moving items around



        static public Program gProgram = null;
        static public DateTime bootTime;

        public Program()
        {
            gProgram = this;
            resourceLoader = new ResourceLoader();
            resourceLoader.p = this;
            bootTime = DateTime.Now;

            log("BOOT", LT.LOG_N);
            //Config = new Config_();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means.
            //
            // This method is optional and can be removed if not
            // needed.
        }

        public static int tick = -1;
        static BurnoutTrack bt60 = new BurnoutTrack(60, maxScriptTimeMSPerSec);

        static Profiler initP = new Profiler("init");
        static Profiler mainP = new Profiler("main");

        public void Main(string arg, UpdateType upd)
        {
            tick += 1;
            if (bt60.burnoutpre()) return;

            if (tick % 20 == 0)
                if (Me.Closed)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    return;
                }
            mainP.start();
            main(arg, upd);
            mainP.stop();
            if (tick % 5 == 0)
            {
                Echo(tick.ToString());
                if (profileLog != null) profileLog.WriteText("name:ms1t:ms60t\n" + Profiler.getAllReports());
                if (gInv != null)
                {
                    Echo(gInv.lastStatus);
                }
            }
            if (consoleLog != null && tick % 5 == 0)
            {
                if (Logger.loggedMessagesDirty)
                {
                    Logger.updateLoggedMessagesRender();
                    consoleLog.WriteText(Logger.loggedMessagesRender);
                }
            }
            if (bt60.burnoutpost()) return;
        }


        static Inventory gInv = null;
        static Autocraft gAutocraft = null;
        static AssemblerMgr gAssemblerMgr = null;


        bool first = true;

        void main(string arg, UpdateType upd)
        {
            initP.start();
            if (tick % 10 == 0)
            {
                resourceLoader.update();
            }
            initP.stop();
            if (resourceLoader.neverFullyLoaded)
            {
                Echo("INITIALIZING: " + resourceLoader.step + "/11");
                if (statusLog != null) statusLog.WriteText("INIT: " + resourceLoader.step + "/11");
                return;
            }
            if (first)
            {
                first = false;

                gInv = new Inventory();
                gInv.updateContainers(inventoryBlocks);
                gAssemblerMgr = new AssemblerMgr();
                gAutocraft = new Autocraft();
                log("Basic structures initialized, pausing for 1 second.");
                bt60.setwait(60);
            }
            if (tick % 10 == 0) connectEvent2();
            if (SLEEPING) return;


            if (autocraftingLCD != null)
            {
                gAssemblerMgr.update();
            }
            gInv.update();
            if (tick % 60 * 5 == 0)
            {
                //if (statusLog != null) statusLog.WriteText(invInterface.listInv());

                if (autocraftingLCD != null)
                {
                    aclcd.s();
                    var txt = autocraftingLCD.GetText();
                    //if(txt.StartsWith(""))
                    gAutocraft.readLCD(txt);
                    aclcd.e();
                    aclcd2.s();
                    var o = gAutocraft.writeLCD();
                    autocraftingLCD.WriteText(o);
                    aclcd2.e();
                }
            }
            if (arg == "clearasm")
            {
                foreach (var asm in assemblers)
                {
                    asm.ClearQueue();
                }
            }
            if (arg == "test")
            {
                var asm = assemblers[0];
                var bp = Autocraft.blueprints.First().Value;


                List<MyProductionItem> pro = new List<MyProductionItem>();
                asm.GetQueue(pro);
                if (pro.Count == 0)
                {
                    asm.AddQueueItem(bp, (MyFixedPoint) 1000);
                }
                else
                {
                    var e = pro[0];
                    asm.InsertQueueItem(0, bp, (MyFixedPoint) (-500));
                }
            }
            else if (arg == "log")
            {
                Logger.writeSuperlog();
            }
        }

        static Profiler aclcd = new Profiler("aclcd1");
        static Profiler aclcd2 = new Profiler("aclcd2");

        bool SLEEPING = false;

        static string nosort = "No Sorting";
        static string nosort2 = "No IIM";

        class ConnectorInfo
        {
            bool lcon = false;
            public IMyShipConnector connector = null;
            public IMyShipConnector otherConnector = null;

            public bool sortConnected = false;
            //public bool blockConnected = false;

            public bool upd()
            {
                var con = connector.Status == MyShipConnectorStatus.Connected;
                var o = connector.OtherConnector;
                if (lcon != con)
                {
                    lcon = con;
                    if (!con) o = null;
                    otherConnector = o;
                    sortConnected = false;
                    if (otherConnector != null)
                    {
                        var n = connector.CustomName;
                        var n2 = otherConnector.CustomName;
                        if (n.Contains(nosort) ||
                            n.Contains(nosort2) ||
                            n2.Contains(nosort) ||
                            n2.Contains(nosort2))
                        {
                            sortConnected = false;
                        }
                        else sortConnected = true;
                    }
                    return true;
                }
                else return false;
            }
        }

        Dictionary<IMyShipConnector, ConnectorInfo> connectorState = new Dictionary<IMyShipConnector, ConnectorInfo>();

        Dictionary<IMyCubeGrid, IMyShipConnector> getCgridmap = new Dictionary<IMyCubeGrid, IMyShipConnector>();
        List<IMyCubeGrid> getCbl = new List<IMyCubeGrid>();
        int ltick = -1;

        IMyShipConnector getRelevantConnector(IMyTerminalBlock b)
        {
            if (ltick != tick)
            {
                getCgridmap.Clear();
                getCbl.Clear();
                foreach (var c in connectors)
                {
                    var o = c.OtherConnector;
                    if (o != null) getCgridmap[c.OtherConnector.CubeGrid] = c;
                }
            }
            IMyShipConnector match = null;
            getCgridmap.TryGetValue(b.CubeGrid, out match);
            if (match != null) return match;
            else
            {
                if (!getCbl.Contains(b.CubeGrid))
                {
                    foreach (var kvp in connectorState)
                    {
                        var v = kvp.Value;
                        ;
                        if (v.otherConnector != null && v.otherConnector.CubeGrid == b.CubeGrid)
                        {
                            if (v.sortConnected) return getCgridmap[b.CubeGrid] = v.connector;
                            else
                            {
                                getCbl.Add(b.CubeGrid);
                                return null;
                            }
                        }
                    }
                    foreach (var kvp in connectorState)
                    {
                        var v = kvp.Value;
                        ;
                        if (v.otherConnector != null && v.sortConnected)
                        {
                            if (b.IsSameConstructAs(v.otherConnector))
                            {
                                return getCgridmap[b.CubeGrid] = v.connector;
                            }
                        }
                    }
                }
                else return null;
            }
            getCbl.Add(b.CubeGrid);
            return null;
        }




        bool cnctE = false;

        public void connectEvent2()
        {
            try
            {
                bool evnt = false;
                foreach (var c in connectors)
                {
                    ConnectorInfo v = null;
                    connectorState.TryGetValue(c, out v);
                    if (v == null)
                    {
                        v = new ConnectorInfo();
                        v.connector = c;
                        connectorState[c] = v;
                    }
                    evnt = v.upd() || evnt;
                }

                if (evnt)
                {

                    log("connector change");
                    List<IMyProgrammableBlock> pgms = new List<IMyProgrammableBlock>();
                    GridTerminalSystem.GetBlocksOfType(pgms, b => b != Me && b.HasPlayerAccess(Me.OwnerId) && b.CustomData.StartsWith("KTZINV") && b.IsWorking);
                    bool awake = true;
                    if (pgms.Count > 0)
                    {
                        bool stati = Me.CubeGrid.IsStatic;
                        foreach (var c in pgms)
                        {
                            if (c.CubeGrid.IsStatic && !stati)
                            {
                                awake = false;
                                break;
                            }
                        }
                        if (awake)
                        {
                            foreach (var c in pgms)
                            {
                                if (c.EntityId < Me.EntityId && (!stati || c.CubeGrid.IsStatic))
                                {
                                    awake = false;
                                    break;
                                }
                            }
                        }
                    }
                    SLEEPING = !awake;
                    if (SLEEPING)
                    {
                        gInv.lastStatus = "Sleeping while a connected KTZInv runs.";
                        if (statusLog != null) statusLog.WriteText(gInv.lastStatus);
                        return;
                    }

                    List<IMyTerminalBlock> blox = new List<IMyTerminalBlock>();
                    GridTerminalSystem.GetBlocksOfType(blox);
                    inventoryBlocks.Clear();
                    List<IMyCubeGrid> subGrids = new List<IMyCubeGrid>();
                    List<IMyCubeGrid> notSubGrids = new List<IMyCubeGrid>();
                    foreach (var b in blox)
                    {
                        if (b.HasInventory && b.HasPlayerAccess(Me.OwnerId))
                        {
                            if (b.CubeGrid == Me.CubeGrid) inventoryBlocks.Add(b);
                            else
                            {
                                if (!notSubGrids.Contains(b.CubeGrid))
                                {
                                    if (b.IsSameConstructAs(Me)) subGrids.Add(b.CubeGrid);
                                    else notSubGrids.Add(b.CubeGrid);
                                }
                                if (subGrids.Contains(b.CubeGrid)) inventoryBlocks.Add(b);
                                else
                                {
                                    var rc = getRelevantConnector(b);
                                    if (rc != null)
                                    {
                                        ConnectorInfo v = null;
                                        connectorState.TryGetValue(rc, out v);
                                        if (v != null && v.sortConnected)
                                        {
                                            inventoryBlocks.Add(b);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    gInv.updateContainers(inventoryBlocks);
                }
            }
            catch (Exception e) //because torch or something i forget
            {
                if (!cnctE)
                {
                    cnctE = true;
                    log("Exception: " + e.ToString());
                }
            }
        }

        class AssemblerMgr
        {
            //List<BPLearn2> bplearners = new List<BPLearn2>();
            List<Asmstate> asmstates = new List<Asmstate>();

            class Asmstate
            {
                public BPLearn2 bpl = null;
                public int flushTick = 0;
                public int lastProduced = 0;
            }






            public AssemblerMgr()
            {
                foreach (var a in Program.assemblers)
                {
                    var l = new BPLearn2();
                    l.asm = a;
                    var s = new Asmstate();
                    s.bpl = l;
                    asmstates.Add(s);
                    //bplearners.Add(l);
                }
            }

            Dictionary<MyDefinitionId, List<IMyAssembler>> bpassemblers = new Dictionary<MyDefinitionId, List<IMyAssembler>>();

            int shuffleidx = 0;

            public void shuffleAssemblers()
            {
                shuffleidx = (shuffleidx + 1) % assemblers.Count;
                if (tick % 10 == 0 && assemblers.Count > 0)
                {
                    var state = asmstates[shuffleidx];
                    var asm = assemblers[shuffleidx];

                    if (asm.IsQueueEmpty || asm.IsProducing || !asm.Enabled)
                    {
                        state.flushTick = state.lastProduced = tick;
                    }
                    else
                    {
                        if (tick - state.flushTick > 60 * 17)
                        {
                            log("flushing " + asm.CustomName);
                            //IMyInventory input = null;
                            //if (asm.Mode == MyAssemblerMode.Assembly) input = asm.InputInventory;
                            //else input = asm.OutputInventory;
                            List<MyInventoryItem> itms = new List<MyInventoryItem>();
                            asm.InputInventory.GetItems(itms);
                            List<MyInventoryItem> itms2 = new List<MyInventoryItem>();
                            asm.OutputInventory.GetItems(itms2);
                            itms.AddRange(itms2);
                            foreach (var itm in itms)
                            {
                                Inventory.BlockInventory bi = Inventory.BlockInventory.getBI(asm);
                                Inventory.expel(bi, itm.Type, itm.Amount, true);
                            }
                            state.flushTick = tick;
                        }
                        if (tick - state.lastProduced > 60 * 30)
                        {
                            log("rear shuffling " + asm.CustomName);
                            List<MyProductionItem> queue = new List<MyProductionItem>();
                            asm.GetQueue(queue);
                            if (queue.Count > 1)
                            {
                                var po = queue[0];
                                asm.RemoveQueueItem(0, po.Amount);
                                asm.AddQueueItem(po.BlueprintId, po.Amount);
                            }
                            //asm.MoveQueueItemRequest(queue[0].ItemId, queue.Count-1);
                            state.flushTick = state.lastProduced = tick;
                        }
                    }
                }
            }

            //int lastshuffle = 0;
            //int shufflefreq = 60 * 2;
            public void balanceAssemblers()
            {

                /*


                        if (tick % 60 == 0)
                        {
                            for (int i = 0; i < assemblers.Count; i++)
                            {
                                var state = asmstates[i];
                                var asm = assemblers[i];


                            }
                        }
                        */
                balanceAssemblers(MyAssemblerMode.Assembly);
                balanceAssemblers(MyAssemblerMode.Disassembly);
            }

            //i.e. if you say steel plate, assembling, it will erase any steel plate disassembly jobs
            //since disassembling directly contradicts the goal of assembling
            public void clearContradictingJobs(MyDefinitionId bp, MyAssemblerMode m)
            {
                foreach (var a in assemblers)
                {
                    if (a.Mode != m)
                    {
                        List<MyProductionItem> queue = new List<MyProductionItem>();
                        a.GetQueue(queue);
                        for (var i = 0; i < queue.Count; i++)
                        {
                            var itm = queue[i];
                            if (itm.BlueprintId == bp)
                            {
                                a.RemoveQueueItem(i, itm.Amount);
                                queue.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }


            public void balanceAssemblers(MyAssemblerMode m, Dictionary<MyDefinitionId, MyFixedPoint> set = null)
            {
                Dictionary<MyDefinitionId, MyFixedPoint> orders = new Dictionary<MyDefinitionId, MyFixedPoint>();

                //List<List<MyProductionItem>> queues = new List<List<MyProductionItem>>();
                Dictionary<IMyAssembler, List<MyProductionItem>> queues = new Dictionary<IMyAssembler, List<MyProductionItem>>();
                foreach (var a in assemblers)
                {
                    if (a.Mode == m)
                    {
                        List<MyProductionItem> queue = new List<MyProductionItem>();
                        a.GetQueue(queue);
                        for (var i = 0; i < queue.Count; i++)
                        {
                            var itm = queue[i];
                            if (itm.Amount < (MyFixedPoint) 1) //because keen. look idk man
                            {
                                a.RemoveQueueItem(i, itm.Amount);
                                queue.RemoveAt(i);
                                i--;
                            }
                            else
                            {
                                var bp = itm.BlueprintId;
                                if (orders.ContainsKey(bp)) orders[bp] += itm.Amount;
                                else orders[bp] = itm.Amount;
                            }
                        }
                        queues[a] = queue;
                    }
                }

                if (set != null)
                {
                    foreach (var kvp in set)
                    {
                        MyFixedPoint ct = 0;
                        orders.TryGetValue(kvp.Key, out ct);
                        if (m == MyAssemblerMode.Assembly && kvp.Value > ct) orders[kvp.Key] = kvp.Value;
                        else if (m == MyAssemblerMode.Disassembly && -kvp.Value > ct) orders[kvp.Key] = -kvp.Value;
                    }
                }

                foreach (var kvp in orders)
                {
                    var bp = kvp.Key;
                    var amt = kvp.Value;
                    List<IMyAssembler> relevant_assemblers = new List<IMyAssembler>();
                    foreach (var a in assemblers)
                    {
                        if (a.Mode == m && a.CanUseBlueprint(bp)) relevant_assemblers.Add(a);
                    }
                    if (relevant_assemblers.Count == 0) continue;
                    int divided = (int) amt / relevant_assemblers.Count;
                    int remainder = (int) amt - (divided * (relevant_assemblers.Count - 1));
                    for (int i = 0; i < relevant_assemblers.Count; i++)
                    {
                        var asm = relevant_assemblers[i];
                        var queue = queues[asm];

                        var t_v = i == (relevant_assemblers.Count - 1) ? remainder : divided;

                        if (t_v > -1 && t_v < 1) t_v = 0;

                        MyProductionItem citem = new MyProductionItem();
                        int idx = -1;

                        for (var e = 0; e < queue.Count; e++)
                        {
                            var n = queue[e];
                            if (n.BlueprintId == bp)
                            {
                                citem = n;
                                idx = e;
                                break;
                            }
                        }
                        if (idx != -1)
                        {
                            var c_v = citem.Amount;
                            var diff = t_v - c_v;

                            if (Math.Abs((int) diff) >= 3)
                            {
                                //log("doin it: "+diff);
                                asm.InsertQueueItem(idx, bp, diff);
                            }
                        }
                        else if (t_v != 0) asm.AddQueueItem(bp, (MyFixedPoint) t_v);
                    }
                }
            }

            static Profiler shufP = new Profiler("asmshuf");
            static Profiler balP = new Profiler("asmbal");
            int lbal = 0;

            public void update()
            {
                if (!gInv.hasUpdatedOnce) return;

                shufP.s();
                if (tick % 60 * 15 == 0)
                {
                    if (ASM_SHUFFLE) shuffleAssemblers();
                }
                shufP.e();
                balP.s();
                if (tick % 60 * 7 == 0)
                {
                    if (ASM_FLUSH) balanceAssemblers();
                }
                balP.e();

                foreach (var l in asmstates) l.bpl.update();

                if (tick % 60 == 0 && tick - gInv.lastUpdateTick <= 60)
                {
                    Dictionary<MyDefinitionId, MyFixedPoint> production = new Dictionary<MyDefinitionId, MyFixedPoint>();
                    foreach (var l in asmstates)
                    {
                        foreach (var i in l.bpl.lastQueue)
                        {
                            if (!production.ContainsKey(i.BlueprintId)) production.Add(i.BlueprintId, i.Amount);
                            else production[i.BlueprintId] += i.Amount;
                        }
                    }
                    Dictionary<MyDefinitionId, MyFixedPoint> orders = new Dictionary<MyDefinitionId, MyFixedPoint>();
                    int asmjobs = 0;
                    int dasmjobs = 0;
                    foreach (var kvp in Autocraft.quotas_bp)
                    {
                        var itembp = kvp.Key;
                        var recipebp = Autocraft.blueprints[itembp];

                        var desired_amt = kvp.Value;
                        //var assembling_amt = production.ContainsKey(recipebp) ? production[recipebp] : 0;
                        MyFixedPoint current_amt = 0;
                        Inventory.globalManifest.stuff.TryGetValue((MyItemType) itembp, out current_amt);

                        if (ASSEMBLE && current_amt + ASSEMBLE_MARGIN < desired_amt)
                        {
                            orders[recipebp] = desired_amt - current_amt;
                            asmjobs += 1;
                        }
                        if (DISASSEMBLE && current_amt - ASSEMBLE_MARGIN > desired_amt)
                        {
                            orders[recipebp] = desired_amt - current_amt;
                            dasmjobs += 1;
                        }
                    }

                    if (orders.Count > 0)
                    {
                        if (asmjobs == 0 && dasmjobs != 0)
                        {
                            foreach (var s in assemblers)
                                if (s.IsQueueEmpty)
                                    s.Mode = MyAssemblerMode.Disassembly;
                        }
                        else if (asmjobs != 0 && dasmjobs == 0)
                        {
                            foreach (var s in assemblers)
                                if (s.IsQueueEmpty)
                                    s.Mode = MyAssemblerMode.Assembly;
                        }

                        if (ASSEMBLE)
                        {
                            foreach (var kvp in orders)
                            {
                                if (kvp.Value > 0) clearContradictingJobs(kvp.Key, MyAssemblerMode.Assembly);
                            }
                            balanceAssemblers(MyAssemblerMode.Assembly, orders);
                        }
                        if (DISASSEMBLE)
                        {
                            foreach (var kvp in orders)
                            {
                                if (kvp.Value < 0) clearContradictingJobs(kvp.Key, MyAssemblerMode.Disassembly);
                            }
                            balanceAssemblers(MyAssemblerMode.Disassembly, orders);
                        }
                    }
                }
                //if(tick % 60*60*10 == 0)
                //{
                /*foreach(var asm in assemblers)
                        {
                            List<MyInventoryItem> items = new List<MyInventoryItem>();
                            //asm.GetInventory(0);
                            asm.InputInventory.GetItems(items);
                            foreach(var i in items) invInterface_noasm.TransferItemTo
                        }*/
                //invInterface_noasm


                /*List<MyInventoryItem> items = new List<MyInventoryItem>();

                        //tick2 += 1;
                        //tick3 += 1;


                        asm.GetQueue(queue);
                        //todo: if get queue and unknown recipe in it, flush the output inventory of the assembler



                        if (queue.Count != 0 || lastQueue.Count != 0)
                        {
                            asm.OutputInventory.GetItems(items);
                        }*/
            }
        }

        class Autocraft
        {
            //Dictionary<MyItemType, MyDefinitionId> bpcache;


            static public MyItemType nop = MyItemType.MakeComponent("SteelPlate");
            static Dictionary<string, MyItemType> typecast = new Dictionary<string, MyItemType>();

            public static bool canFind(string subtype, out MyItemType t)
            {
                if (typecast.ContainsKey(subtype))
                {
                    t = typecast[subtype];
                    return true;
                }
                foreach (var bpkvp in blueprints)
                {
                    if (bpkvp.Key.SubtypeId.ToString() == subtype)
                    {
                        typecast[subtype] = bpkvp.Key;
                        t = bpkvp.Key;
                        return true;
                    }
                }
                t = nop;
                return false;
            }



            static public Dictionary<string, int> quotas = new Dictionary<string, int>();
            static public Dictionary<MyDefinitionId, int> quotas_bp = new Dictionary<MyDefinitionId, int>();

            static public Dictionary<MyDefinitionId, MyDefinitionId> blueprints = new Dictionary<MyDefinitionId, MyDefinitionId>();

            static public void addBP(MyDefinitionId item, MyDefinitionId bp)
            {
                blueprints[item] = bp;
                writeCD();
            }

            static public void writeCD()
            {
                string newcd = "KTZINV;\nitemID;blueprintID";
                foreach (var kvp in blueprints)
                {
                    newcd += "\n" + kvp.Key.ToString() + ";" + kvp.Value.ToString();
                }
                gProgram.Me.CustomData = newcd;
            }


            public Autocraft()
            {
                var cd = gProgram.Me.CustomData;
                var spl = cd.Split('\n');
                foreach (var l in spl)
                {
                    var s2 = l.Split(';');
                    if (s2.Length >= 2)
                    {
                        try
                        {
                            var itembp = MyDefinitionId.Parse(s2[0]);
                            try
                            {
                                var recipebp = MyDefinitionId.Parse(s2[1]);
                                blueprints[itembp] = recipebp;
                            }
                            catch (Exception) { }
                        }
                        catch (Exception) { }
                    }
                }
            }

            //key=item, val=production bp
            static Profiler p1 = new Profiler("p1");
            static Profiler p2 = new Profiler("p2");
            static Profiler p3 = new Profiler("p3");

            public string writeLCD()
            {
                p1.s();
                Dictionary<string, int> avail = new Dictionary<string, int>();

                foreach (var kvp in Inventory.globalManifest.stuff)
                {
                    string subtype = kvp.Key.SubtypeId; //.Substring("MyObjectBuilder_".Length);//kvp.Key.SubtypeId.Replace("MyObjectBuilder_", "").Replace("_", " ");
                    if (!quotas.ContainsKey(subtype))
                    {
                        var nfo = kvp.Key.GetItemInfo();
                        if (!nfo.IsOre && !nfo.IsIngot) quotas[subtype] = 0;
                        else
                        {
                            MyItemType derp = Autocraft.nop;
                            if (canFind(subtype, out derp))
                            {
                                quotas[subtype] = 0;
                                break;
                            }

                            /*foreach (var bpkvp in blueprints)
                                    {
                                        if (bpkvp.Key.SubtypeId.ToString() == kvp.Key.SubtypeId)
                                        {
                                            quotas[name] = 0;
                                            break;
                                        }
                                    }*/
                        }
                        //todo check if we have a bp, if bp ignore type status, fusion fuel etc
                    }
                    avail[subtype] = (int) kvp.Value;
                }
                p1.e();
                p2.s();
                StringBuilder b = new StringBuilder("Component Current | Wanted\n");

                foreach (var kvp in quotas)
                {
                    int av = 0;
                    int quota = kvp.Value;
                    avail.TryGetValue(kvp.Key, out av);

                    b.Append(kvp.Key);
                    b.Append(" ");
                    b.Append(av);
                    b.Append(" ");

                    if (av < quota) b.Append("<");
                    else if (av == quota) b.Append("=");
                    else b.Append(">");

                    b.Append(" ");
                    b.Append(quota);

                    bool hasbp = false;
                    p3.s();
                    foreach (var bpkvp in blueprints)
                    {
                        if (bpkvp.Key.SubtypeId.ToString() == kvp.Key)
                        {
                            hasbp = true;
                            quotas_bp[bpkvp.Key] = quota;
                            break;
                        }
                    }
                    p3.e();
                    if (!hasbp) b.Append(" (no BP)");
                    b.Append("\n");
                }


                /*foreach (var kvp in quotas)
                        {
                            int av = 0;
                            int quota = kvp.Value;
                            avail.TryGetValue(kvp.Key, out av);
                            r += kvp.Key + " " + av+" ";
                            if (av < quota) r += "<";
                            else if (av == quota) r += "=";
                            else r += ">";
                            r += " " + quota;
                            bool hasbp = false;
                            p3.s();
                            foreach (var bpkvp in blueprints)
                            {
                                if(bpkvp.Key.SubtypeId.ToString() == kvp.Key)
                                {
                                    hasbp = true;
                                    break;
                                }
                            }
                            p3.e();
                            if (!hasbp) r += " (no BP)";
                            r += "\n";
                        }*/
                p2.e();
                return b.ToString();
            }

            string last = "";
            bool firstread = true;

            public void readLCD(string s)
            {
                if (last == s) return;

                last = s;
                var lines = s.Split('\n');
                foreach (var l in lines)
                {
                    if (l.Contains("|")) continue;

                    var tok = l.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tok.Length >= 4)
                    {
                        string key = tok[0];
                        string cur = tok[1];
                        char sym = tok[2][0];
                        if (sym == '<' || sym == '>' || sym == '=')
                        {
                            string want = tok[3];
                            int wnt = 0;
                            var isn = int.TryParse(want, out wnt);
                            if (isn)
                            {
                                quotas[key] = wnt;
                            }
                        }

                    }
                }
            }
        }

        //ITEMNAME avail 0 inf [hidden]
        class Autocraft2
        {
            static public MyItemType NOPIT = MyItemType.MakeComponent("SteelPlate"); //non-nullable type, and will crash if ever not valid.
            static MyDefinitionId NOPBP = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SpaceCredit");

            class CraftOrder
            {
                public MyItemType type = NOPIT;
                public bool htype = false;
                public string subtypeid = "";
                public MyDefinitionId blueprint = NOPBP;

                public CraftOrder(string subtypeid)
                {
                    this.subtypeid = subtypeid;
                    htype = subType2ItemType(subtypeid, out type);
                }

                public int min = 0;
                public int max = int.MaxValue;
                public bool hide = false;
            }


            static Dictionary<string, MyItemType> typecast = new Dictionary<string, MyItemType>();

            public static bool subType2ItemType(string subtype, out MyItemType t)
            {
                if (typecast.ContainsKey(subtype))
                {
                    t = typecast[subtype];
                    return true;
                }
                foreach (var bpkvp in blueprints)
                {
                    if (bpkvp.Key.SubtypeId.ToString() == subtype)
                    {
                        typecast[subtype] = bpkvp.Key;
                        t = bpkvp.Key;
                        return true;
                    }
                }
                t = NOPIT;
                return false;
            }

            static public Dictionary<MyDefinitionId, MyDefinitionId> blueprints = new Dictionary<MyDefinitionId, MyDefinitionId>();

            static public void addBP(MyDefinitionId item, MyDefinitionId bp)
            {
                blueprints[item] = bp;
                string newcd = "KTZINV\nitemID;blueprintID";
                foreach (var kvp in blueprints)
                {
                    newcd += "\n" + kvp.Key.ToString() + ";" + kvp.Value.ToString();
                }
                gProgram.Me.CustomData = newcd;
            }

            static void loadBP()
            {
                var cd = gProgram.Me.CustomData;
                var spl = cd.Split('\n');
                foreach (var l in spl)
                {
                    var s2 = l.Split(';');
                    if (s2.Length >= 2)
                    {
                        try
                        {
                            var itembp = MyDefinitionId.Parse(s2[0]);
                            try
                            {
                                var recipebp = MyDefinitionId.Parse(s2[1]);
                                blueprints[itembp] = recipebp;
                            }
                            catch (Exception) { }
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        class BPLearn2
        {
            public IMyAssembler asm = null;
            public int lastCraft = 0;

            static MyDefinitionId nop = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/SpaceCredit");


            public List<MyProductionItem> lastQueue = new List<MyProductionItem>();
            List<MyInventoryItem> lastItems = new List<MyInventoryItem>();
            float lastProgress = -1;
            static Profiler bpl = new Profiler("bpl");

            //int tick2 = 0;
            //int tick3 = 0;
            public void update()
            {
                bpl.s();
                //assemblers only tick once per second, so a faster observation is meaningless
                if (tick % 60 == 0 && asm.Mode == MyAssemblerMode.Assembly)
                {
                    var curProg = asm.CurrentProgress;
                    List<MyProductionItem> queue = new List<MyProductionItem>();
                    List<MyInventoryItem> items = new List<MyInventoryItem>();

                    //tick2 += 1;
                    //tick3 += 1;


                    asm.GetQueue(queue);
                    //todo: if get queue and unknown recipe in it, flush the output inventory of the assembler


                    if (queue.Count != 0 || lastQueue.Count != 0)
                    {
                        asm.OutputInventory.GetItems(items);

                        //this is because of nasty things like guns and tools that don't stack :|
                        List<MyItemType> types = new List<MyItemType>();
                        List<MyInventoryItem> itemsCompact = new List<MyInventoryItem>();
                        foreach (var i in items)
                        {
                            var t = i.Type;
                            if (types.Contains(t)) continue;
                            types.Add(t);

                            MyFixedPoint c = 0;
                            foreach (var i2 in items)
                            {
                                if (i2.Type == t) c += i2.Amount;
                            }
                            if (c > 0)
                            {
                                itemsCompact.Add(new MyInventoryItem(i.Type, i.ItemId, c));
                            }
                        }
                        items = itemsCompact;

                        if (curProg < lastProgress && lastQueue.Count > 0)
                        {
                            //log("tick2=" + tick2, LT.LOG_N);
                            //tick2 = 0;

                            MyDefinitionId recipe = nop;
                            //compare production queue to last known state.
                            //Check for any item with a decreased count or that has left the queue altogether
                            //and save it
                            foreach (var lastitem in lastQueue)
                            {
                                bool still_queued = false;
                                foreach (var curitem in queue)
                                {
                                    if (curitem.ItemId == lastitem.ItemId && curitem.BlueprintId == lastitem.BlueprintId)
                                    {
                                        still_queued = true;
                                        if (curitem.Amount < lastitem.Amount)
                                        {
                                            recipe = lastitem.BlueprintId;
                                            break;
                                        }
                                    }
                                }
                                if (recipe != nop) break;

                                if (!still_queued)
                                {
                                    recipe = lastitem.BlueprintId;
                                    break;
                                }
                            }

                            if (recipe != nop)
                            {

                                //assuming we found the above, check for a newly appearing or count-increased item in the output inventory.
                                //if progress has reset, a production order has shrunk or vanished, and a new item has appeared,
                                ///we can reasonably assume that the production order blueprint generates that item.
                                MyDefinitionId itemdef = nop;
                                foreach (var i in items)
                                {
                                    bool newitem = true;
                                    foreach (var o in lastItems)
                                    {
                                        if (o.Type == i.Type) newitem = false;

                                        if (o.Type == i.Type && o.Amount < i.Amount)
                                        {
                                            try
                                            {
                                                var n = MyDefinitionId.Parse(i.Type.TypeId + "/" + i.Type.SubtypeId);
                                                itemdef = n;
                                                newitem = false;
                                                break;
                                            }
                                            catch (Exception) { }
                                        }
                                        //if (itembp != nop) break;
                                    }


                                    //if we found no increased item (the bp is nop) but we did notice a new kind of item, it's that one
                                    if (newitem) // && itembp == nop)
                                    {
                                        try
                                        {
                                            var n = MyDefinitionId.Parse(i.Type.TypeId + "/" + i.Type.SubtypeId);
                                            itemdef = n;
                                        }
                                        catch (Exception) { }
                                    }
                                    if (itemdef != nop) break;
                                }
                                if (recipe != nop && itemdef != nop)
                                {
                                    lastCraft = tick;
                                    if (!Autocraft.blueprints.ContainsKey(itemdef))
                                    {
                                        log("Learned recipe " + itemdef.ToString() + ";" + recipe.ToString(), LT.LOG_N);
                                        Autocraft.addBP(itemdef, recipe);
                                    }
                                    //Autocraft.blueprints[itemdef] = recipe;
                                    //todo:flush output inventory here

                                }

                            }
                        }
                    }
                    if (lastQueue.Count == 0) curProg = 0;


                    lastProgress = curProg;
                    lastQueue = queue;
                    lastItems = items;
                }
                bpl.e();
            }
        }

        const double MAX_MS_PER_SEC_BOOT = 0.1;
        const double MAX_MS_PER_SEC = 0.1;
        const int PBLIMIT_STARTUPTICKS = 0; //20 by default

        class BurnoutTrack
        {
            public double maxmspersec = 0.25;
            public static double[] defertrack;
            public int len = 60;

            public BurnoutTrack(int l, double ms)
            {
                len = l;
                maxmspersec = ms;
                defertrack = new double[len];
            }

            int defercalls = 0;
            int deferpos = 0;
            static bool hangflag = false;
            int hangticks = 0;
            int hangtick = 0;
            bool fsdbg = false;
            DateTime bf = DateTime.Now;

            public bool burnoutpre()
            {
                bf = DateTime.Now;
                if (hangflag)
                {
                    if (tick > hangtick)
                    {
                        double avg = 0;
                        foreach (var d in defertrack) avg += d;
                        avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
                        if (avg > maxmspersec * len / 60)
                        {
                            defertrack[deferpos] = 0;
                            defercalls += 1;
                            deferpos = (deferpos + 1) % defertrack.Length;
                            return true;
                        }
                        else
                        {
                            hangflag = false;
                            //log("Resuming after " + (hangticks / 60.0d).ToString("0.0") + "s", LT.LOG_N);
                        }
                    }
                }
                return hangflag;
            }

            public double avg()
            {
                double avg = 0;
                foreach (var d in defertrack) avg += d;
                avg = avg / (defercalls > defertrack.Length ? defertrack.Length : defercalls);
                return avg;
            }

            public void setwait(int ticks)
            {
                hangticks = ticks;
                hangtick = tick + ticks;
                hangflag = true;
            }

            public bool burnoutpost()
            {
                double ms = (DateTime.Now - bf).TotalMilliseconds;
                defertrack[deferpos] = ms;
                defercalls += 1;
                deferpos = (deferpos + 1) % defertrack.Length;
                if (!hangflag)
                {
                    double p_avg = 0;
                    foreach (var d in defertrack) p_avg += d;
                    int divisor = defercalls > defertrack.Length ? defertrack.Length : defercalls;
                    var avg = p_avg / divisor;
                    var mtch = maxmspersec * len / 60;
                    if (avg > mtch)
                    {
                        int tickstodoom = PBLIMIT_STARTUPTICKS - tick;
                        if (tickstodoom > 0 && tickstodoom * maxmspersec < avg) return false;

                        int waitticks = 0;
                        while (p_avg / (divisor + waitticks) > mtch) waitticks++;

                        hangticks = waitticks;
                        hangtick = tick + waitticks;
                        hangflag = true;


                        var lstr = tick + ": " + avg.ToString("0.00") + ">" + (mtch).ToString("0.00") + "ms/s exec. Sleeping " + (hangticks / 60.0d).ToString("0.0") + "s";
                        log(lstr, LT.LOG_N);
                        /*var c = getCtrl();
                                if (c != null)
                                {
                                    if (!fsdbg)
                                    {
                                        c.CustomData = "";
                                        fsdbg = true;
                                    }
                                    c.CustomData += "\n\n" + lstr + "\n\n" + Profiler.getAllReports();
                                }
                                else getCtrlTick = -9000;*/

                        return true;
                    }
                }
                else return true;
                return false;
            }
        }

        class Inventory
        {
            public static InventoryManifest globalManifest = new InventoryManifest();
            public static Dictionary<string, MyFixedPoint> nonFractionalMinMarginByCat = new Dictionary<string, MyFixedPoint>();
            public static List<MyItemType> encounteredTypes = new List<MyItemType>();

            static Dictionary<string, MyItemType> typeTable = new Dictionary<string, MyItemType>();

            static MyItemType getType(string type, string subtype)
            {
                //can throw exceptions, MyItemType is fiddly
                var k = type + "/" + subtype;
                if (typeTable.ContainsKey(k)) return typeTable[k];
                else
                {
                    return typeTable[k] = new MyItemType(type, subtype);
                }
            }

            //there's some giga-fucky shit going on with tanks
            static List<string> unstackhardcode = new List<string>()
            {
                "MyObjectBuilder_OxygenContainerObject",
                "MyObjectBuilder_GasContainerObject",
                "MyObjectBuilder_PhysicalGunObject",
                "MyObjectBuilder_PhysicalObject",
                "MyObjectBuilder_Datapad",
            };

            static Dictionary<string, string> cattocargo = new Dictionary<string, string>()
            {
                { "MyObjectBuilder_OxygenContainerObject", "Bottles" },
                { "MyObjectBuilder_GasContainerObject", "Bottles" },

                { "MyObjectBuilder_PhysicalGunObject", "Tools" },
                { "MyObjectBuilder_PhysicalObject", "Tools" }, //space credit
                { "MyObjectBuilder_ConsumableItem", "Tools" }, //cola, coffee
                { "MyObjectBuilder_Datapad", "Tools" }, //datapad duh

                { "MyObjectBuilder_AmmoMagazine", "Ammo" },
                { "MyObjectBuilder_Ore", "Ores" },
                { "MyObjectBuilder_Ingot", "Ingots" },
                { "MyObjectBuilder_Component", "Components" },
            };

            public static string cargokeywordbytype(string type)
            {
                string r = "Unknown";
                cattocargo.TryGetValue(type, out r);
                return r;
            }

            //static bool sortProductionInput = false;
            static bool treatBlankAsAlltype = false;

            public class InventoryManifest
            {
                public InventoryManifest()
                {

                }

                public Dictionary<MyItemType, MyFixedPoint> stuff = new Dictionary<MyItemType, MyFixedPoint>();
                public MyFixedPoint maxVolume;
                public MyFixedPoint freeVolume;
                public Dictionary<string, MyFixedPoint> typeVolume = new Dictionary<string, MyFixedPoint>();

                public void set(BlockInventory bi)
                {
                    stuff.Clear();
                    maxVolume = freeVolume = 0;

                    var invs = bi.getSortedInventories(false);
                    int merges = 0;
                    //const int MAX_MERGES = 10;
                    DateTime b4 = DateTime.Now;
                    foreach (var nv in invs)
                    {
                        var mv = nv.MaxVolume;
                        var cv = nv.CurrentVolume;
                        maxVolume += mv;
                        freeVolume += mv - cv;
                        List<MyInventoryItem> itms = new List<MyInventoryItem>();
                        nv.GetItems(itms);
                        Dictionary<MyItemType, int> lItem = new Dictionary<MyItemType, int>();

                        for (int i = itms.Count - 1; i >= 0; i--)
                        {
                            var it = itms[i];
                            //stack deduplication
                            if (MERGE_STACKS && merges < MAX_TRANSFERS_PER_OP && lItem.ContainsKey(it.Type))
                            {
                                var stackable = !unstackhardcode.Contains(it.Type.TypeId);
                                if (stackable)
                                {
                                    var lpos = lItem[it.Type];
                                    var nfo = it.Type.GetItemInfo();
                                    var lit = itms[lpos];
                                    if (it.Amount + lit.Amount < nfo.MaxStackAmount)
                                    {
                                        log(it.Type.SubtypeId + " msa " + nfo.MaxStackAmount + " stacking now ");
                                        nv.TransferItemTo(nv, lpos, i, true);
                                        merges++;
                                        if ((DateTime.Now - b4).TotalMilliseconds > MAX_TRANSFER_MS) merges = MAX_TRANSFERS_PER_OP;
                                    }
                                }
                            }
                            lItem[it.Type] = i;

                            //manifest generate
                            if (!stuff.ContainsKey(it.Type)) stuff[it.Type] = it.Amount;
                            else stuff[it.Type] += it.Amount;
                            var t = cargokeywordbytype(it.Type.TypeId);
                            MyFixedPoint tv = 0;
                            typeVolume.TryGetValue(t, out tv);
                            tv += it.Type.GetItemInfo().Volume * it.Amount;
                            typeVolume[t] = tv;
                        }
                    }
                    if (merges > 0)
                    {
                        log(merges + " merges in " + bi.b.CustomName + " took " + (DateTime.Now - b4).TotalMilliseconds + "ms");
                    }

                    //very ugly.
                    foreach (var kvp in stuff)
                    {
                        var k = kvp.Key;
                        if (!encounteredTypes.Contains(k))
                        {
                            encounteredTypes.Add(k);
                            MyFixedPoint minVol = (MyFixedPoint) 0.01;
                            if (!k.GetItemInfo().UsesFractions) minVol = (MyFixedPoint) k.GetItemInfo().Volume;
                            var cat = cargokeywordbytype(k.TypeId);
                            MyFixedPoint kval = 0;
                            nonFractionalMinMarginByCat.TryGetValue(cat, out kval);
                            if (minVol > kval) kval = minVol;
                            nonFractionalMinMarginByCat[cat] = kval;
                        }
                    }
                }

                public void sub(InventoryManifest o)
                {
                    //if we don't even have the thing being subtracted nothing will be subtracted
                    if (o == null) return;

                    List<MyItemType> del = new List<MyItemType>();
                    foreach (var kvp in o.stuff)
                    {
                        if (stuff.ContainsKey(kvp.Key))
                        {
                            var nv = stuff[kvp.Key] - kvp.Value;
                            if (nv > 0) stuff[kvp.Key] = nv;
                            else del.Add(kvp.Key);
                        }
                    }
                    foreach (var k in del) stuff.Remove(k);
                }

                public void add(InventoryManifest o)
                {
                    if (o == null) return;
                    foreach (var kvp in o.stuff)
                    {
                        if (stuff.ContainsKey(kvp.Key)) stuff[kvp.Key] += kvp.Value;
                        else stuff[kvp.Key] = kvp.Value;
                    }
                }

                public bool equals(InventoryManifest o)
                {
                    if (o == null || this.stuff.Count != o.stuff.Count) return false;


                    foreach (var kvp in stuff)
                    {
                        MyFixedPoint v = 0;
                        if (!o.stuff.TryGetValue(kvp.Key, out v)) return false;
                        else if (kvp.Value != v) return false;
                    }
                    foreach (var kvp in o.stuff)
                    {
                        if (!stuff.ContainsKey(kvp.Key)) return false;
                    }
                    return true;
                }
            }

            static public List<PriorityAggregate> prAggs = new List<PriorityAggregate>();

            static public PriorityAggregate getPI(int p)
            {
                foreach (var pr in prAggs)
                    if (pr.priority == p)
                        return pr;
                var x = new PriorityAggregate();
                x.priority = p;
                prAggs.Add(x);
                prAggs.Sort();
                return x;
            }

            static PriorityAggregate higherPriorityWithRoomFor(BlockInventory bi, string category)
            {
                //PriorityAggregate pi = null;
                var pidx = 0;
                for (int i = 0; i < prAggs.Count; i++)
                {
                    var pr = prAggs[i];
                    if (pr.priority == bi.priority)
                    {
                        //pi = pr;
                        pidx = i;
                        break;
                    }
                }

                for (int i = 0; i < pidx; i++)
                {
                    var c = prAggs[i];
                    MyFixedPoint free = 0;
                    c.typeVolumeFree.TryGetValue(category, out free);

                    MyFixedPoint minmargin = 0;
                    nonFractionalMinMarginByCat.TryGetValue(category, out minmargin);
                    if (free > minmargin) return c;
                }
                return null;
            }

            public class PriorityAggregate : IComparable<PriorityAggregate>
            {
                int IComparable<PriorityAggregate>.CompareTo(PriorityAggregate y)
                {
                    var x = this;
                    return x.priority.CompareTo(y.priority);
                }

                public List<BlockInventory> bis = new List<BlockInventory>();
                public int priority = 0;
                public Dictionary<string, MyFixedPoint> typeVolumeFree = new Dictionary<string, MyFixedPoint>();
                public List<string> categories = new List<string>();

                public void update()
                {
                    typeVolumeFree.Clear();
                    categories.Clear();
                    foreach (var bi in bis)
                    {

                        foreach (var c in bi.categories)
                        {
                            if (!categories.Contains(c)) categories.Add(c);
                            MyFixedPoint v = 0;
                            typeVolumeFree.TryGetValue(c, out v);
                            //for our purposes, we are recording the largest free volume in a single container in the set that accepts this category.
                            if (bi.manifest.freeVolume > v)
                            {
                                typeVolumeFree[c] = bi.manifest.freeVolume;
                            }
                        }
                    }
                }
            }

            public class BlockInventory : IComparable<BlockInventory>
            {
                int IComparable<BlockInventory>.CompareTo(BlockInventory y)
                {
                    var x = this;
                    if (x.priority == y.priority)
                    {
                        return x.idx.CompareTo(y.idx);
                    }
                    return x.priority.CompareTo(y.priority);
                }

                public static List<BlockInventory> bPriorityList = new List<BlockInventory>();
                public static Dictionary<IMyTerminalBlock, BlockInventory> bIDict = new Dictionary<IMyTerminalBlock, BlockInventory>();

                public static BlockInventory getBI(IMyTerminalBlock b)
                {
                    BlockInventory r = null;
                    bIDict.TryGetValue(b, out r);
                    if (r == null) r = new BlockInventory(b);
                    return r;
                }

                const string bpprefix = "MyObjectBuilder_";
                const string everything = "alltypes";


                public static int idl = 0;
                public int idx = 0;

                public BlockInventory(IMyTerminalBlock b)
                {
                    this.b = b;

                    bPriorityList.Add(this);
                    bIDict[b] = this;
                    idx = idl;
                    idl++;


                    for (var i = 0; i < b.InventoryCount; i++)
                    {
                        sortedInventories.Add(b.GetInventory(i));
                    }
                    if (b is IMyProductionBlock)
                    {
                        isProduction = true;
                        var p = (IMyProductionBlock) b;
                        sortedInventoriesNoInput.Add(p.OutputInventory);
                        sortedInventoriesNoOutput.Add(p.InputInventory);
                        if (b is IMyAssembler)
                        {
                            isAssembler = true;
                            asmref = (IMyAssembler) b;
                        }
                    }
                    else
                    {
                        sortedInventoriesNoInput.AddRange(sortedInventories);
                    }
                }

                public IMyTerminalBlock b = null;
                public InventoryManifest manifest = null;

                public List<string> categories = new List<string>();

                List<IMyInventory> sortedInventoriesNoInput = new List<IMyInventory>();
                List<IMyInventory> sortedInventoriesNoOutput = new List<IMyInventory>();
                List<IMyInventory> sortedInventories = new List<IMyInventory>();

                public List<IMyInventory> getSortedInventories(bool inc_input)
                {
                    if (inc_input) return sortedInventories;
                    else
                    {
                        if (asmref != null && asmref.Mode == MyAssemblerMode.Disassembly) return sortedInventoriesNoOutput;
                        return sortedInventoriesNoInput;
                    }

                }

                public bool isProduction = false;
                public bool isAssembler = false;
                IMyAssembler asmref = null;


                public Dictionary<MyItemType, MyFixedPoint> stocktargets = new Dictionary<MyItemType, MyFixedPoint>();
                public bool special = false;
                public bool locked = true; //we don't move shit to shit until first updateP
                public bool hidden = false;
                public bool holdall = false;
                const int default_p = 100000;
                public int priority = int.MaxValue;
                public string lastCD = "-31234";
                public string lastN = "-234523";

                void _locked()
                {
                    locked = true;
                    special = false;
                }

                void _hidden()
                {
                    locked = true;
                    hidden = true;
                    special = false;
                }

                public void updateP()
                {
                    if (b.CustomName != lastN)
                    {
                        lastN = b.CustomName;

                        var lpriority = priority;
                        //var PI = getPI(priority);
                        //PI.bis.Remove(this);

                        priority = default_p;
                        special = false;
                        locked = false;
                        hidden = false;
                        holdall = false;
                        var t = lastN.Split(' ', '.');
                        categories.Clear();

                        if (lockBlockTypes.Contains(b.DefinitionDisplayNameText)) _locked();
                        if (hiddenBlockTypes.Contains(b.DefinitionDisplayNameText)) _hidden();

                        if (!hidden)
                        {
                            foreach (var tok in t)
                            {
                                var ltok = tok.ToLower();
                                if (ltok.StartsWith("[") && ltok.EndsWith("]"))
                                {
                                    ltok = ltok.Substring(1, ltok.Length - 2);
                                }
                                if (ltok == "special")
                                {
                                    special = true;
                                    priority -= 10000;

                                }
                                else if (ltok == "locked")
                                {
                                    _locked();
                                }
                                else if (ltok == "hidden")
                                {
                                    _hidden();
                                }
                                else if (ltok.StartsWith("p"))
                                {
                                    var ap = tok.Substring(1);
                                    if (ap == "max") priority = int.MinValue;
                                    else if (ap == "min") priority = int.MaxValue;
                                    else if (ap.All(char.IsDigit))
                                    {
                                        priority -= 10000;
                                        int c = 0;
                                        int.TryParse(ap, out c);
                                        if (c.ToString() == ap)
                                        {
                                            priority += c;
                                        }
                                    }
                                }
                                else if (ltok == everything)
                                {
                                    holdall = true;
                                }
                                else
                                {
                                    foreach (var kvp in cattocargo)
                                    {
                                        if (tok == kvp.Value)
                                        {
                                            if (!categories.Contains(tok)) categories.Add(tok);
                                            break;
                                        }
                                    }
                                }
                            }

                            if (treatBlankAsAlltype && !special && !locked && categories.Count == 0 && !isProduction)
                            {
                                holdall = true;
                                priority += 1;
                            }
                        }

                        if (special)
                        {
                            holdall = false;
                            categories.Clear();
                        }
                        if (holdall)
                        {
                            foreach (var kvp in cattocargo)
                            {
                                if (!categories.Contains(kvp.Value)) categories.Add(kvp.Value);
                            }
                        }
                        if (!special && categories.Count == 0 && APIWC.HasCoreWeapon(b))
                        {
                            locked = true;
                        }

                        if (lpriority != priority)
                        {
                            bPriorityList.Sort();

                            var PI = getPI(lpriority);
                            PI.bis.Remove(this);
                            PI.update();
                            PI = getPI(priority);
                            PI.bis.Add(this);
                            PI.update();
                        }
                        if (!special) stocktargets.Clear();
                    }
                    if (special && b.CustomData != lastCD)
                    {
                        if (special && b.CustomData == "")
                        {
                            List<MyItemType> alltypes = new List<MyItemType>();
                            List<MyItemType> t = new List<MyItemType>();
                            for (var i = 0; i < b.InventoryCount; i++)
                            {
                                b.GetInventory(i).GetAcceptedItems(t);
                                foreach (var e in t)
                                    if (!alltypes.Contains(e))
                                        alltypes.Add(e);
                            }
                            List<string> clinesNZ = new List<string>();
                            List<string> clines = new List<string>();
                            foreach (var e in alltypes)
                            {
                                MyFixedPoint amt = 0;
                                manifest.stuff.TryGetValue(e, out amt);
                                if (amt > 0) clinesNZ.Add(e.TypeId.Substring(bpprefix.Length) + "/" + e.SubtypeId + "=" + amt.ToString()); //\n";
                                else clines.Add(e.TypeId.Substring(bpprefix.Length) + "/" + e.SubtypeId + "=0");
                            }
                            clinesNZ.Sort();
                            clines.Sort();
                            if (clinesNZ.Count == 0) clinesNZ.AddRange(clines);

                            b.CustomData = String.Join("\n", clinesNZ);
                        }
                        if (ISYCOMPAT && b.CustomData.IndexOf("Special Container modes:") == -1)
                        {
                            b.CustomData = "@Special Container modes:\n- isycompat\n" + b.CustomData;
                        }
                        lastCD = b.CustomData;
                        stocktargets.Clear();
                        var lines = lastCD.Split('\n');
                        //var newlines = new List<string>();
                        foreach (var l in lines)
                        {
                            //bool kl = true;
                            var lr = l.Split('=');
                            if (lr.Length == 2)
                            {
                                var ids = lr[0].Split('/');
                                if (ids.Length == 2)
                                {
                                    try
                                    {
                                        var t = getType(bpprefix + ids[0], ids[1]);
                                        if (lr[1] == "all")
                                        {
                                            stocktargets[t] = int.MaxValue;
                                            //if (LOG) log(b.CustomName + " " + t.SubtypeId + "=all");
                                        }
                                        else
                                        {
                                            var c = (MyFixedPoint) double.Parse(lr[1]);
                                            if (c > 0)
                                            {
                                                stocktargets[t] = c;
                                                //if (LOG) log(b.CustomName + " " + t.SubtypeId + "=" + c);
                                            }
                                            else
                                            {
                                                //kl = false;
                                            }
                                        }
                                    }
                                    catch (Exception) { }
                                }
                            }
                        }
                    }
                }

                public void updateM()
                {
                    InventoryManifest nm = new InventoryManifest();
                    if (!hidden) nm.set(this);
                    if (manifest == null || !manifest.equals(nm))
                    {
                        if (manifest != null) Inventory.globalManifest.sub(manifest);
                        Inventory.globalManifest.add(nm);
                        manifest = nm;
                        getPI(this.priority).update();
                    }
                }

                public bool updateT()
                {
                    //updateT_incomplete = false;
                    if (locked) return false;

                    int transfers = transfer_count;

                    IDBG.set(this, null);

                    //int MOVES = 0;
                    //const int MAX_MOVES = 8;

                    //this should actually run always and first, i think
                    {
                        Dictionary<string, PriorityAggregate> targs = new Dictionary<string, PriorityAggregate>();
                        List<MyItemType> keys = new List<MyItemType>(manifest.stuff.Keys);
                        //this ensures the dict can be edited during our loop

                        foreach (var type in keys) //things we have
                        {
                            var cat = cargokeywordbytype(type.TypeId);
                            //this should only end up actually calling higherPriorityWithRoomFor once per relevant category tag.
                            PriorityAggregate pa = null;
                            if (!targs.ContainsKey(cat))
                            {
                                targs[cat] = pa = higherPriorityWithRoomFor(this, cat);
                            }
                            else pa = targs[cat];

                            int errchk = 0;

                            while (pa != null && errchk < 10) //there is a higher priority container in a PriorityAggregate that does want the item's category
                            {

                                MyFixedPoint amt = 0;

                                manifest.stuff.TryGetValue(type, out amt);
                                if (amt == 0) break;

                                MyFixedPoint goalstock = 0; //in case this is a special container, we don't want to push shit we should be keeping
                                stocktargets.TryGetValue(type, out goalstock);
                                amt -= goalstock;
                                if (amt == 0) break;

                                errchk++;
                                var margin = nonFractionalMinMarginByCat[cat];
                                BlockInventory dest = null;
                                foreach (var bi in pa.bis)
                                {
                                    if (bi.categories.Contains(cat) && bi.manifest.freeVolume >= margin)
                                    {
                                        //this cargo accepts this category and has more free space than the minimum margin for this category
                                        dest = bi;
                                        break;
                                    }
                                }
                                if (dest != null)
                                {
                                    //we should start transferring this item.
                                    IDBG.set(this, dest);
                                    if (amt > dest.manifest.freeVolume) amt = dest.manifest.freeVolume;
                                    var rem = transfer_item(this, dest, type, amt, false, true);
                                    if (transfer_count - transfers > MAX_TRANSFERS_PER_OP || transMS > MAX_TRANSFER_MS) return true;

                                    if (rem > 0)
                                    {
                                        IDBG.log("Unable to xfer " + rem + " of " + type.SubtypeId);
                                    }
                                    //...

                                    pa.update(); //recompute the PriorityAggregate values.
                                    //if we filled the destination beyond nonFractionalMaxMarginByCat,
                                    //we should delete entry in targs so that higherPriorityWithRoomFor is recomputed for next relevant item
                                    if (dest.manifest.freeVolume < margin)
                                    {
                                        targs[cat] = pa = higherPriorityWithRoomFor(this, cat);
                                    }
                                }
                            }
                            if (errchk == 10)
                            {
                                IDBG.log("errchk loop abort");
                            }

                            {
                                if (!categories.Contains(cat) /* && !holdall*/ && !special)
                                {
                                    MyFixedPoint amt = 0;

                                    if (manifest.stuff.TryGetValue(type, out amt) && amt > 0)
                                    {
                                        MyFixedPoint goalstock = 0; //in case this is a special container, we don't want to push shit we should be keeping
                                        stocktargets.TryGetValue(type, out goalstock);
                                        if (amt > goalstock)
                                        {
                                            //nobody higher wants it, but it's not supposed to be in this cargo either.
                                            //todo: search for equal or lower priority place that wants it. if we can't find one, generate error log message.
                                            expel(this, type, amt - goalstock);
                                            if (transfer_count - transfers > MAX_TRANSFERS_PER_OP || transMS > MAX_TRANSFER_MS) return true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (special)
                    {
                        List<MyItemType> keys = new List<MyItemType>(stocktargets.Keys);
                        foreach (var kvp in manifest.stuff)
                            if (!keys.Contains(kvp.Key))
                                keys.Add(kvp.Key);

                        foreach (var type in keys)
                        {
                            MyFixedPoint curstock = 0;
                            manifest.stuff.TryGetValue(type, out curstock);
                            MyFixedPoint goalstock = 0;
                            stocktargets.TryGetValue(type, out goalstock);
                            if (goalstock > curstock && manifest.freeVolume > (MyFixedPoint) type.GetItemInfo().Volume)
                            {
                                MyFixedPoint globalstock = 0;
                                globalManifest.stuff.TryGetValue(type, out globalstock);
                                if (globalstock > curstock)
                                {
                                    IDBG.set(type.SubtypeId);
                                    IDBG.log(b.CustomName + " globalchk " + type.SubtypeId + " pmove " + goalstock + " " + curstock);
                                    var r = retrieve(this, type, goalstock - curstock);

                                    if (transfer_count - transfers > MAX_TRANSFERS_PER_OP || transMS > MAX_TRANSFER_MS) return true;

                                    if (r > 0) IDBG.log("unable to satisfy by " + r);
                                }
                            }
                            else if (goalstock < curstock)
                            {
                                IDBG.set(type.SubtypeId);
                                IDBG.log("attempt expel " + type.SubtypeId + ": " + goalstock + " < " + curstock + " in " + this.b.CustomName);
                                expel(this, type, curstock - goalstock);

                                if (transfer_count - transfers > MAX_TRANSFERS_PER_OP || transMS > MAX_TRANSFER_MS) return true;
                            }
                        }
                    }


                    return transfer_count - transfers > 0;
                }
            }

            //todo review and update this one
            static public MyFixedPoint retrieve(BlockInventory dest, MyItemType t, MyFixedPoint v)
            {
                IDBG.set(dest, null);
                IDBG.set(t.SubtypeId);
                var nfo = t.GetItemInfo();
                int pidx = BlockInventory.bPriorityList.IndexOf(dest);
                IDBG.log("pidx=" + pidx);
                IDBG.log("BlockInventory.bPriorityList.Count=" + BlockInventory.bPriorityList.Count);
                for (var i = BlockInventory.bPriorityList.Count - 1; i > pidx; i--)
                {
                    var inv = BlockInventory.bPriorityList[i];
                    IDBG.set(dest, inv);
                    if (inv.manifest != null && inv.manifest.stuff.ContainsKey(t))
                    {

                        MyFixedPoint avail = inv.manifest.stuff[t];
                        IDBG.log(inv.b.CustomName + "has item, stock " + avail);
                        MyFixedPoint trns_amt = avail > v ? v : avail;
                        IDBG.log("tamt=" + trns_amt);

                        MyFixedPoint max_accept = (inv.manifest.freeVolume * (MyFixedPoint) (1 / nfo.Volume));
                        if (!nfo.IsOre && !nfo.IsIngot) max_accept = MyFixedPoint.Floor(max_accept + (MyFixedPoint) 0.001);
                        if (trns_amt > max_accept) trns_amt = max_accept;
                        IDBG.log("tamt_ma=" + trns_amt);
                        var rem = transfer_item(inv, dest, t, trns_amt);
                        v -= trns_amt;
                        v += rem;
                    }
                    if (v <= 0) break;
                }
                return v;
            }

            static public MyFixedPoint expel(BlockInventory origin, MyItemType type, MyFixedPoint amount, bool inputs = false)
            {
                var nfo = type.GetItemInfo();
                var kw = cargokeywordbytype(type.TypeId);
                IDBG.set(type.SubtypeId);
                for (var i = 0; i < BlockInventory.bPriorityList.Count; i++)
                {
                    var inv = BlockInventory.bPriorityList[i];
                    if (inv != origin && !inv.locked)
                    {
                        IDBG.set(origin, inv);
                        MyFixedPoint amt = 0;
                        MyFixedPoint max_accept = (inv.manifest.freeVolume * (MyFixedPoint) (1 / nfo.Volume));
                        if (!nfo.IsOre && !nfo.IsIngot) max_accept = MyFixedPoint.Floor(max_accept + (MyFixedPoint) 0.001);

                        if (!inv.special && (inv.categories.Contains(kw) /* || inv.holdall*/)) amt = max_accept;
                        else if (inv.special)
                        {
                            MyFixedPoint stock = 0;
                            inv.manifest.stuff.TryGetValue(type, out stock);
                            MyFixedPoint trg = 0;
                            inv.stocktargets.TryGetValue(type, out trg);
                            if (trg > stock)
                            {
                                amt = trg - stock;
                                if (amt > max_accept) amt = max_accept;
                            }
                        }
                        if (amt > 0)
                        {
                            IDBG.log("maxaccept=" + max_accept);
                            IDBG.log("pushing " + amt + " to " + inv.b.CustomName);
                            var remaining = transfer_item(origin, inv, type, amt, inputs, inputs);
                            amount -= amt;
                            amount += remaining;
                        }
                    }
                    if (amount <= 0) break;
                }
                if (amount > 0)
                {
                    StringBuilder err = new StringBuilder();
                    bapp(err, "Warning: failed to expel ", type.SubtypeId, " from \"", origin.lastN, "\": nowhere else to store?");
                    gInv.rerrlog(err.ToString());
                }
                return amount;
            }

            static public MyFixedPoint transfer_item(BlockInventory origin,
                BlockInventory dest,
                MyItemType type,
                MyFixedPoint amount,
                bool sendinputs = false,
                bool recieveinputs = false)
            {
                IDBG.set(origin, dest);
                IDBG.set(type.SubtypeId);
                if (amount == 0) return 0;
                IDBG.log("transfer_item " + type.SubtypeId + " " + amount + " " + origin.lastN + " > " + dest.lastN);
                //bool cerr = false;
                var sa = amount;
                foreach (var inva in origin.getSortedInventories(sendinputs))
                {
                    foreach (var invb in dest.getSortedInventories(recieveinputs))
                    {
                        amount = transfer_item(inva, invb, type, amount);
                        if (amount <= 0 || conveyor_error) break;
                    }
                    if (amount <= 0 || conveyor_error) break;
                }
                if (conveyor_error)
                {
                    conveyor_error = false;
                    StringBuilder err = new StringBuilder();
                    bapp(err, "Warning: xfer fail: no conveyor \"", origin.lastN, "\" > \"", dest.lastN + "\"");
                    gInv.rerrlog(err.ToString());
                }
                if (sa != amount)
                {
                    //IDBG.log("moved " + (sa - v) + " " + t.SubtypeId + " to " + b.b.CustomName + " from " + a.b.CustomName);
                    origin.updateM();
                    dest.updateM();
                }
                return amount;
            }

            static int transfer_count = 0;
            static bool conveyor_error = false;

            static int transTick = 0;
            static double transMS = 0;

            //transfers up to amount of type to dest from origin. returns how much of the amount couldn't be sent for whatever reason
            static public MyFixedPoint transfer_item(IMyInventory origin, IMyInventory dest, MyItemType type, MyFixedPoint amount)
            {
                if (transTick != tick)
                {
                    transTick = tick;
                    transMS = 0;
                }
                DateTime s = DateTime.Now;
                conveyor_error = false;
                List<MyInventoryItem> itms = new List<MyInventoryItem>();
                origin.GetItems(itms);
                foreach (MyInventoryItem item in itms)
                {
                    if (item.Type == type)
                    {
                        MyFixedPoint trns_amt = item.Amount > amount ? amount : item.Amount;
                        IDBG.log("_transfer_item " + type.SubtypeId + " " + trns_amt);
                        var nfo = type.GetItemInfo();
                        MyFixedPoint max_accept = ((dest.MaxVolume - dest.CurrentVolume) * (1f / nfo.Volume));
                        if (!nfo.UsesFractions) max_accept = MyFixedPoint.Floor(max_accept + (MyFixedPoint) 0.001);

                        if (trns_amt > max_accept)
                        {
                            IDBG.log("_capping amt to " + max_accept);
                            trns_amt = max_accept;
                        }
                        if (trns_amt > 0)
                        {
                            transfer_count++;
                            if (origin.TransferItemTo(dest, item, trns_amt))
                            {
                                amount -= trns_amt;
                                IDBG.log("_successfully moved " + trns_amt + " of " + item.Type.SubtypeId);
                            }
                            else
                            {
                                bool conveyed = origin.CanTransferItemTo(dest, type);
                                IDBG.log("_failed to move. checkset conveyor flag");
                                if (!conveyed) conveyor_error = true;
                            }
                        }
                    }
                    if (amount <= 0) break;
                }
                transMS += (DateTime.Now - s).TotalMilliseconds;
                return amount;
            }

            class IDebugger
            {
                bool DEBUG = false;
                string FIRST_CARGO = "Nascent.Cargo [1.7].[Barge].AllTypes.P70"; //2 CCTT Cargo Components Ammo P40";
                string SECOND_CARGO = ""; //"3 Nascent.Cargo [1.7].[Barge].AllTypes.P70";
                string ITEM = ""; // BelterComponent";// LidarComponent";// LithiumCell";
                //bool retrieve = true;
                //bool expel = false;

                BlockInventory a = null;
                BlockInventory b = null;
                string curitem = "";

                public void set(BlockInventory a, BlockInventory b)
                {
                    this.a = a;
                    this.b = b;
                }

                public void set(string item)
                {
                    this.curitem = item;
                }

                public void log(string msg)
                {
                    if (!DEBUG) return;
                    bool l = true;
                    if (FIRST_CARGO.Length > 0 && (a == null || a.lastN != FIRST_CARGO)) l = false;
                    if (SECOND_CARGO.Length > 0 && (b == null || b.lastN != SECOND_CARGO)) l = false;
                    if (ITEM.Length > 0 && curitem != ITEM) l = false;
                    if (l)
                    {
                        Program.log(msg);
                    }
                }
            }

            static IDebugger IDBG = new IDebugger();


            public void updateContainers(List<IMyTerminalBlock> c)
            {

                List<IMyTerminalBlock> del = new List<IMyTerminalBlock>();
                foreach (var b in containers)
                {
                    if (!c.Contains(b)) del.Add(b);
                }
                InventoryManifest dMan = new InventoryManifest();
                foreach (var b in del)
                {
                    BlockInventory bi = BlockInventory.getBI(b);
                    Inventory.globalManifest.sub(bi.manifest);
                }
                containers.Clear();


                foreach (var e in c)
                    if (e.CubeGrid.EntityId != gProgram.Me.EntityId)
                        containers.Add(e);
                foreach (var e in c)
                    if (e.CubeGrid.EntityId == gProgram.Me.EntityId)
                        containers.Add(e);

                upd();
            }

            List<IMyTerminalBlock> containers = new List<IMyTerminalBlock>();
            int nextC = 0;
            int nextCS = 0;

            bool itemsUpdating = true;
            public bool hasUpdatedOnce = false;
            public int updateInterval = 1; //60 * 10;

            public int lastUpdateTick = 0;
            public int ticksRun = 0;

            static Profiler invuP = new Profiler("invu");

            int updateCounter = 0;

            enum STATUS
            {
                PREINIT,
                INIT,
                MANIFESTS,
                IDLE
            }

            string[] statlbl =
            {
                "PREINIT",
                "INIT",
                "PROCESSING",
                "IDLE"
            };
            STATUS cstat = STATUS.PREINIT;
            Queue<string> errors = new Queue<string>();
            int rerrtick = 0;
            bool errd = false;

            public void rerrlog(string s)
            {
                errd = true;
                errors.Enqueue(s);
                if (errors.Count > 5) errors.Dequeue();
                rerrtick = tick;
            }

            static Profiler statP = new Profiler("stat");
            public string lastStatus = "";

            public void genstatus()
            {
                statP.s();
                if (tick % 5 == 0)
                {
                    if (errd && tick - rerrtick > 10 * 60)
                    {
                        errd = false;
                        errors.Clear();
                    }
                    StringBuilder status = new StringBuilder();
                    var lbl = statlbl[(int) cstat];
                    if (cstat >= STATUS.MANIFESTS && cstat != STATUS.IDLE)
                    {
                        bapp(status, "Working ", nextC + 1, "/", containers.Count, "\n");
                        if (nextC < containers.Count) bapp(status, containers[nextC].CustomName, "\n");
                    }
                    bapp(status, lbl, "\n");
                    bapp(status, transfer_count + " xfer ops this runtime\n\n");
                    foreach (var l in errors) bapp(status, l, "\n");
                    var s = status.ToString();
                    if (s != lastStatus)
                    {
                        lastStatus = s;
                        if (statusLog != null) statusLog.WriteText(s);
                    }
                }
                statP.e();
            }


            void clr()
            {
                itemsUpdating = false;
                ticksRun = tick - lastUpdateTick;
                lastUpdateTick = tick;
                nextC = 0;
                cstat = STATUS.IDLE;
            }

            void upd()
            {
                cstat = STATUS.INIT;

                itemsUpdating = true;
                lastUpdateTick = tick;
                nextC = 0;
            }

            bool movedItems = false;
            int lastBlockUpdate = 0;
            int blockUpdateStep = 0;
            int SANchk = 0;

            static Profiler cdbgP = new Profiler("cdbg");

            public void update()
            {
                cdbgP.s();
                if (!itemsUpdating && (tick - lastUpdateTick > updateInterval))
                {
                    upd();

                    if (CARGODBG && cargodbg != null)
                    {
                        //	genstatus();
                        //string o = "";
                        StringBuilder fml = new StringBuilder();
                        foreach (var e in BlockInventory.bPriorityList)
                        {
                            fml.Append(e.priority + "|||" + e.b.CustomName + "\n");
                        }
                        cargodbg.WriteText(fml.ToString());
                    }
                }
                cdbgP.e();
                invuP.s();
                if (itemsUpdating)
                {
                    if (nextC >= containers.Count)
                    {
                        clr();
                        log("full run (" + containers.Count + "):" + ticksRun + "t (" + (ticksRun / 60.0d).ToString("0.0") + "s");
                        hasUpdatedOnce = true;
                        updateCounter += 1;
                    }
                    else
                    {
                        var intrvl = blockInterval;
                        if (movedItems) intrvl = blockIntervalMove;

                        if (tick - lastBlockUpdate > intrvl)
                        {
                            IMyTerminalBlock t = containers[nextC];
                            BlockInventory bi = BlockInventory.getBI(t);
                            var bus = blockUpdateStep;
                            blockUpdateStep++;
                            if (bus == 0) bi.updateM();
                            if (bus == 1) bi.updateP();
                            if (bus == 2)
                            {
                                if (SORT)
                                {
                                    movedItems = bi.updateT();
                                    SANchk++;
                                }
                                if (!movedItems || SANchk > 10)
                                {
                                    SANchk = 0;
                                    lastBlockUpdate = tick;
                                    blockUpdateStep = 0;
                                    nextC++;
                                }
                                else
                                {
                                    lastBlockUpdate = tick;
                                    blockUpdateStep--;
                                }
                            }
                            cstat = STATUS.MANIFESTS;
                        }
                    }
                }
                invuP.e();
                genstatus();
            }
        }

        public enum LT
        {
            LOG_N = 0,
            LOG_D,
            LOG_DD
        }

        string[] logtype_labels = { "INFO", "_DBG", "DDBG" };

        public static LT LOG_LEVEL = LT.LOG_N;
        public static Logger logger = new Logger();

        public static void log(string s, LT level)
        {
            Logger.log(s, level);
        }

        public static void log(string s)
        {
            Logger.log(s, LT.LOG_N);
        }

        public class Logger
        {
            public class logmsg
            {
                public logmsg(string m, string m2, LT l)
                {
                    msg = m;
                    msg_raw = m2;
                    level = l;
                }

                public string msg = "";
                public string msg_raw = "";
                public int c = 1;
                public LT level = LT.LOG_N;
            }

            static List<logmsg> loggedMessages = new List<logmsg>();
            static int MAX_LOG = 50;
            static List<logmsg> superLoggedMessages = new List<logmsg>();
            static int MAX_SUPER_LOG = 1000;

            static public bool loggedMessagesDirty = true;

            public static void log(string s, LT level)
            {
                if (level > LOG_LEVEL) return;
                string s2 = s;
                if (s.Length > 50)
                {
                    List<string> tok = new List<string>();
                    while (s.Length > 50)
                    {
                        int c = 0;
                        if (tok.Count > 0) c = 2;
                        tok.Add(s.Substring(0, 50 - c));
                        s = s.Substring(50 - c);
                    }
                    tok.Add(s);
                    s = string.Join("\n ", tok);
                }
                var p = gProgram;
                logmsg l = null;
                if (loggedMessages.Count > 0)
                {
                    l = loggedMessages[loggedMessages.Count - 1];
                }
                if (l != null)
                {
                    if (l.msg == s) l.c += 1;
                    else loggedMessages.Add(new logmsg(s, s2, level));
                }
                else loggedMessages.Add(new logmsg(s, s2, level));
                if (loggedMessages.Count > MAX_LOG) loggedMessages.RemoveAt(0);

                l = null;
                if (superLoggedMessages.Count > 0)
                {
                    l = superLoggedMessages[superLoggedMessages.Count - 1];
                }
                if (l != null)
                {
                    if (l.msg == s) l.c += 1;
                    else superLoggedMessages.Add(new logmsg(s, s2, level));
                }
                else superLoggedMessages.Add(new logmsg(s, s2, level));
                if (superLoggedMessages.Count > MAX_SUPER_LOG) superLoggedMessages.RemoveAt(0);

                loggedMessagesDirty = true;
            }


            static public string loggedMessagesRender = "";

            static public void updateLoggedMessagesRender()
            {
                if (!loggedMessagesDirty) return;
                StringBuilder b = new StringBuilder();
                //if (!loggedMessagesDirty) return;// loggedMessagesRender;


                foreach (var m in loggedMessages)
                {
                    b.Append(m.msg);
                    if (m.c > 1) bapp(b, " (", m.c, ")");
                    b.Append("\n");
                }
                string o = b.ToString();
                loggedMessagesDirty = false;
                loggedMessagesRender = o;
            }

            static public void writeSuperlog()
            {
                StringBuilder b = new StringBuilder();
                //if (!loggedMessagesDirty) return;// loggedMessagesRender;


                foreach (var m in superLoggedMessages)
                {
                    b.Append(m.msg);
                    if (m.c > 1) bapp(b, " (", m.c, ")");
                    b.Append("\n");
                }
                string o = b.ToString();
                controllers[0].CustomData = o;
                log(controllers[0].CustomName, LT.LOG_N);
            }
        }

        static void bapp(StringBuilder b, params object[] args)
        {
            foreach (object a in args)
            {
                b.Append(a.ToString());
            }
        }

        public class Stopwatch
        {
            DateTime start;

            public Stopwatch()
            {
                start = DateTime.Now;
            }

            public double e()
            {
                return (DateTime.Now - start).TotalMilliseconds;
            }
        }


        public class Profiler
        {
            static bool PROFILING_ENABLED = true;
            static List<Profiler> profilers = new List<Profiler>();
            const int mstracklen = 60;
            double[] mstrack = new double[mstracklen];
            double msdiv = 1.0d / mstracklen;
            int mscursor = 0;
            DateTime start_time = DateTime.MinValue;
            string Name = "";
            string pre = "";
            string post = "";
            int _ticks_between_calls = 1;
            int ltick = int.MinValue;
            //..int callspertick = 1;

            static int base_sort_position_c = 0;
            int base_sort_position = 0;

            bool nevercalled = true;

            //bool closed = true;
            public int getSortPosition()
            {
                if (nevercalled) return int.MaxValue;
                int mult = (int) Math.Pow(10, 8 - (depth * 2));
                if (parent != null) return parent.getSortPosition() + (base_sort_position * mult);
                return base_sort_position * mult;
            }

            static int basep = (int) Math.Pow(10, 5);

            public Profiler(string name)
            {
                if (PROFILING_ENABLED)
                {
                    Name = name;
                    profilers.Add(this);
                    for (var i = 0; i < mstracklen; i++) mstrack[i] = 0;
                    base_sort_position = base_sort_position_c;
                    base_sort_position_c += 1;
                }
            }

            public void s()
            {
                start();
            }

            public void e()
            {
                stop();
            }

            static List<Profiler> stack = new List<Profiler>();
            Profiler parent = null;
            int depth = 0;
            bool adding = false;

            public void start()
            {
                if (PROFILING_ENABLED)
                {
                    //closed = false;
                    nevercalled = false;
                    if (tick != ltick)
                    {
                        if (_ticks_between_calls == 1 && ltick != int.MinValue)
                        {
                            _ticks_between_calls = tick - ltick;
                        }
                        else
                        {
                            var tbc = tick - ltick;
                            if (tbc != _ticks_between_calls)
                            {
                                _ticks_between_calls = 1;
                                for (var i = 0; i < mstracklen; i++) mstrack[i] = 0;
                            }
                        }

                        ltick = tick;
                        //callspertick = 1;
                        adding = false;
                    }
                    else
                    {
                        adding = true;
                    }
                    if (depth == 0) depth = stack.Count;
                    if (depth > 11) depth = 11;
                    if (stack.Count > 0 && parent == null) parent = stack[stack.Count - 1];
                    stack.Add(this);
                    start_time = DateTime.Now;
                }
            }

            double lastms = 0;
            double average = 0;


            /// <summary>
            /// records a fake ms consumption for this timeframe - for tests or demo
            /// </summary>
            public double FAKE_stop(double fakems)
            {
                return stop(fakems);
            }

            /// <summary>
            /// adds the elapsed time since start() to the records
            /// </summary>
            public double stop()
            {
                double time = 0;
                if (PROFILING_ENABLED)
                {
                    //closed = true;
                    time = (DateTime.Now - start_time).TotalMilliseconds;
                }
                return stop(time);
            }

            private double stop(double _ms)
            {
                double time = 0;
                if (PROFILING_ENABLED)
                {
                    time = _ms;

                    stack.Pop();
                    if (parent != null)
                    {
                        depth = parent.depth + 1;
                    }

                    //if(!adding)mscursor = (mscursor + 1) % mstracklen;


                    if (!adding) mstrack[mscursor] = 0;
                    mstrack[mscursor] += time;
                    if (!adding) mscursor = (mscursor + 1) % mstracklen;

                    average = 0d;
                    foreach (double ms in mstrack) average += ms;
                    average *= msdiv;
                    average /= _ticks_between_calls;
                    lastms = time;
                }
                return time;
            }

            /// <summary>
            /// generates a monospaced report text. If called every tick, every 120 ticks it will recalculate treeview data.
            /// </summary>
            //the treeview can be initially inaccurate as some profilers might not be called every tick, depending on program architecture
            public string getReport(StringBuilder bu)
            {
                if (PROFILING_ENABLED)
                {
                    if (tick % 120 == 25) //recalculate hacky treeview data, delayed by 25 ticks from program start
                    {
                        try
                        {
                            profilers.Sort(delegate(Profiler x, Profiler y)
                            {
                                return x.getSortPosition().CompareTo(y.getSortPosition());
                            });
                        }
                        catch (Exception) { }

                        for (int i = 0; i < profilers.Count; i++)
                        {
                            Profiler p = profilers[i];

                            p.pre = "";
                            if (p.depth > 0 && p.parent != null)
                            {
                                bool parent_has_future_siblings = false;
                                bool has_future_siblings_under_parent = false;
                                for (int b = i + 1; b < profilers.Count; b++)
                                {
                                    if (profilers[b].depth == p.parent.depth) parent_has_future_siblings = true;
                                    if (profilers[b].depth == p.depth) has_future_siblings_under_parent = true;
                                    if (profilers[b].depth < p.depth) break;

                                }
                                while (p.pre.Length < p.parent.depth)
                                {
                                    if (parent_has_future_siblings) p.pre += "│";
                                    else p.pre += " ";
                                }
                                bool last = false;

                                if (!has_future_siblings_under_parent)
                                {
                                    if (i < profilers.Count - 1)
                                    {
                                        if (profilers[i + 1].depth != p.depth) last = true;
                                    }
                                    else last = true;
                                }
                                if (last) p.pre += "└";
                                else p.pre += "├";
                                while (p.pre.Length < p.depth) p.pre += "─";
                            }
                        }
                        int mlen = 0;
                        foreach (Profiler p in profilers)
                            if (p.pre.Length + p.Name.Length > mlen)
                                mlen = p.pre.Length + p.Name.Length;
                        foreach (Profiler p in profilers)
                        {
                            p.post = "";
                            int l = p.pre.Length + p.Name.Length + p.post.Length;
                            if (l < mlen) p.post = new string('_', mlen - l);
                        }
                    }
                    if (nevercalled) bapp(bu, "!!!!", Name, "!!!!: NEVER CALLED!");
                    else bapp(bu, pre, Name, post, ": ", lastms.ToString("0.00"), ";", average.ToString("0.00"));
                }
                return "";
            }

            static public string getAllReports()
            {
                StringBuilder b = new StringBuilder();
                //string r = "";
                if (PROFILING_ENABLED)
                {
                    foreach (Profiler watch in profilers)
                    {
                        watch.getReport(b);
                        b.Append("\n");
                    }
                }
                if (stack.Count > 0)
                {
                    bapp(b, "profile stack error:\n", stack.Count, "\n");
                    foreach (var s in stack)
                    {
                        bapp(b, s.Name, ",");
                    }
                }
                return b.ToString();
            }
        }

        static IMyTextSurface consoleLog = null;
        static IMyTextSurface statusLog = null;
        static IMyTextSurface profileLog = null;
        static IMyTextSurface cargodbg = null;

        static IMyTextSurface autocraftingLCD = null;

        static List<IMyAssembler> assemblers = new List<IMyAssembler>();


//static List<IMyTerminalBlock> weaponCoreWeapons = new List<IMyTerminalBlock>();
        static List<IMyShipController> controllers = new List<IMyShipController>();
        static List<IMyShipConnector> connectors = new List<IMyShipConnector>();
        static List<IMyTerminalBlock> inventoryBlocks = new List<IMyTerminalBlock>();
        static public WcPbApi APIWC = null;
        static public ResourceLoader resourceLoader = null;

        public class ResourceLoader
        {
            public Program p;

            public bool neverFullyLoaded = true;

            public ResourceLoader()
            {
                mkBlockCheckMachine();
            }

            bool readConfig = false;

            public void update()
            {
                if (APIWC == null)
                {
                    APIWC = new WcPbApi();
                    try
                    {
                        APIWC.Activate(gProgram.Me);
                    }
                    catch (Exception) { }

                }
                if (!APIWC.isReady && tick % 30 == 0)
                {
                    try
                    {
                        APIWC.Activate(gProgram.Me);
                    }
                    catch (Exception) { }
                }
                if (!APIWC.isReady) return;

                if (!readConfig || tick % 60 == 0)
                {
                    readConfig = true;
                    /*if (p.Me.CustomData != lastCustomData)
                            {
                                //log("Loading CustomData.", LT.LOG_N);
                                //deserializeConfig(p.Me.CustomData);
                                //p.Me.CustomData = lastCustomData = serializeConfig();
                            }*/
                }

                if (blockCheckMachine != null)
                {
                    if (!blockCheckMachine.MoveNext())
                    {
                        blockCheckMachine.Dispose();
                        blockCheckMachine = null;
                    }
                }
                else if (readConfig && tick % (5 * 60 * 60) == 0) mkBlockCheckMachine();
            }

            public string lastCustomData = "-1";

            IEnumerator<bool> blockCheckMachine = null;

            void mkBlockCheckMachine()
            {
                if (blockCheckMachine != null) blockCheckMachine.Dispose();
                blockCheckMachine = blockLoader();
                step = 0;
            }

            public int step = 0;

            public bool isThis(IMyTerminalBlock b)
            {
                return b.OwnerId == p.Me.OwnerId && b.CubeGrid == p.Me.CubeGrid;
            }

            public IEnumerator<bool> blockLoader()
            {
                var gts = p.GridTerminalSystem;
                consoleLog = null;
                statusLog = null;
                profileLog = null;
                List<IMyTerminalBlock> LCDs = new List<IMyTerminalBlock>();
                gts.GetBlocksOfType(LCDs, b => (b is IMyTextSurface) && b.CubeGrid == p.Me.CubeGrid);
                foreach (var b in LCDs)
                {
                    IMyTextSurface s = b as IMyTextSurface;
                    if (b.CustomData.Contains("statusLog")) statusLog = s;
                    else if (b.CustomData.Contains("consoleLog")) consoleLog = s;
                    else if (b.CustomData.Contains("profileLog")) profileLog = s;
                    else if (b.CustomData.Contains("cargodbg")) cargodbg = s;
                    else if (b.CustomName.Contains("Autocrafting")) autocraftingLCD = s;
                }
                step++;
                yield return true;
                gts.GetBlocksOfType(controllers, isThis);
                step++;
                yield return true;
                step++;
                connectors.Clear();
                gts.GetBlocksOfType(connectors, isThis);
                yield return true;
                step++;

                if (USE_SKITS) gts.GetBlocksOfType(assemblers, isThis);
                else gts.GetBlocksOfType(assemblers, b => isThis(b) && b.DefinitionDisplayNameText != "Survival Kit");

                yield return true;
                step++;
                //gts.GetBlocksOfType(weaponCoreWeapons, b => b.CubeGrid == p.Me.CubeGrid && b.IsFunctional && APIWC.HasCoreWeapon(b));
                //yield return true;
                //step++;
                gts.GetBlocksOfType(inventoryBlocks, b => b.HasInventory && b.HasPlayerAccess(p.Me.OwnerId));
                yield return true;
                step++;
                if (neverFullyLoaded) log("BOOT DONE. " + tick + "t (" + (((float) tick) / 60).ToString("0.0") + "s)", LT.LOG_N);
                neverFullyLoaded = false;
                step++;
                yield return false;
            }
        }

        public class SpriteHUDLCD
        {
            static Dictionary<string, Color> ColorList = new Dictionary<string, Color> { { "aliceblue", Color.AliceBlue }, { "antiquewhite", Color.AntiqueWhite }, { "aqua", Color.Aqua }, { "aquamarine", Color.Aquamarine }, { "azure", Color.Azure }, { "beige", Color.Beige }, { "bisque", Color.Bisque }, { "black", Color.Black }, { "blanchedalmond", Color.BlanchedAlmond }, { "blue", Color.Blue }, { "blueviolet", Color.BlueViolet }, { "brown", Color.Brown }, { "burlywood", Color.BurlyWood }, { "badetblue", Color.CadetBlue }, { "chartreuse", Color.Chartreuse }, { "chocolate", Color.Chocolate }, { "coral", Color.Coral }, { "cornflowerblue", Color.CornflowerBlue }, { "cornsilk", Color.Cornsilk }, { "crimson", Color.Crimson }, { "cyan", Color.Cyan }, { "darkblue", Color.DarkBlue }, { "darkcyan", Color.DarkCyan }, { "darkgoldenrod", Color.DarkGoldenrod }, { "darkgray", Color.DarkGray }, { "darkgreen", Color.DarkGreen }, { "darkkhaki", Color.DarkKhaki }, { "darkmagenta", Color.DarkMagenta }, { "darkoliveGreen", Color.DarkOliveGreen }, { "darkorange", Color.DarkOrange }, { "darkorchid", Color.DarkOrchid }, { "darkred", Color.DarkRed }, { "darksalmon", Color.DarkSalmon }, { "darkseagreen", Color.DarkSeaGreen }, { "darkslateblue", Color.DarkSlateBlue }, { "darkslategray", Color.DarkSlateGray }, { "darkturquoise", Color.DarkTurquoise }, { "darkviolet", Color.DarkViolet }, { "deeppink", Color.DeepPink }, { "deepskyblue", Color.DeepSkyBlue }, { "dimgray", Color.DimGray }, { "dodgerblue", Color.DodgerBlue }, { "firebrick", Color.Firebrick }, { "floralwhite", Color.FloralWhite }, { "forestgreen", Color.ForestGreen }, { "fuchsia", Color.Fuchsia }, { "gainsboro", Color.Gainsboro }, { "ghostwhite", Color.GhostWhite }, { "gold", Color.Gold }, { "goldenrod", Color.Goldenrod }, { "gray", Color.Gray }, { "green", Color.Green }, { "greenyellow", Color.GreenYellow }, { "doneydew", Color.Honeydew }, { "hotpink", Color.HotPink }, { "indianred", Color.IndianRed }, { "indigo", Color.Indigo }, { "ivory", Color.Ivory }, { "khaki", Color.Khaki }, { "lavender", Color.Lavender }, { "lavenderblush", Color.LavenderBlush }, { "lawngreen", Color.LawnGreen }, { "lemonchiffon", Color.LemonChiffon }, { "lightblue", Color.LightBlue }, { "lightcoral", Color.LightCoral }, { "lightcyan", Color.LightCyan }, { "lightgoldenrodyellow", Color.LightGoldenrodYellow }, { "lightgray", Color.LightGray }, { "lightgreen", Color.LightGreen }, { "lightpink", Color.LightPink }, { "lightsalmon", Color.LightSalmon }, { "lightseagreen", Color.LightSeaGreen }, { "lightskyblue", Color.LightSkyBlue }, { "lightslategray", Color.LightSlateGray }, { "lightsteelblue", Color.LightSteelBlue }, { "lightyellow", Color.LightYellow }, { "lime", Color.Lime }, { "limegreen", Color.LimeGreen }, { "linen", Color.Linen }, { "magenta", Color.Magenta }, { "maroon", Color.Maroon }, { "mediumaquamarine", Color.MediumAquamarine }, { "mediumblue", Color.MediumBlue }, { "mediumorchid", Color.MediumOrchid }, { "mediumpurple", Color.MediumPurple }, { "mediumseagreen", Color.MediumSeaGreen }, { "mediumslateblue", Color.MediumSlateBlue }, { "mediumspringgreen", Color.MediumSpringGreen }, { "mediumturquoise", Color.MediumTurquoise }, { "mediumvioletred", Color.MediumVioletRed }, { "midnightblue", Color.MidnightBlue }, { "mintcream", Color.MintCream }, { "mistyrose", Color.MistyRose }, { "moccasin", Color.Moccasin }, { "navajowhite", Color.NavajoWhite }, { "navy", Color.Navy }, { "oldlace", Color.OldLace }, { "olive", Color.Olive }, { "olivedrab", Color.OliveDrab }, { "orange", Color.Orange }, { "orangered", Color.OrangeRed }, { "orchid", Color.Orchid }, { "palegoldenrod", Color.PaleGoldenrod }, { "palegreen", Color.PaleGreen }, { "paleturquoise", Color.PaleTurquoise }, { "palevioletred", Color.PaleVioletRed }, { "papayawhip", Color.PapayaWhip }, { "peachpuff", Color.PeachPuff }, { "peru", Color.Peru }, { "pink", Color.Pink }, { "plum", Color.Plum }, { "powderblue", Color.PowderBlue }, { "purple", Color.Purple }, { "red", Color.Red }, { "rosybrown", Color.RosyBrown }, { "royalblue", Color.RoyalBlue }, { "saddlebrown", Color.SaddleBrown }, { "salmon", Color.Salmon }, { "sandybrown", Color.SandyBrown }, { "seagreen", Color.SeaGreen }, { "seashell", Color.SeaShell }, { "sienna", Color.Sienna }, { "silver", Color.Silver }, { "skyblue", Color.SkyBlue }, { "slateblue", Color.SlateBlue }, { "slategray", Color.SlateGray }, { "snow", Color.Snow }, { "springgreen", Color.SpringGreen }, { "steelblue", Color.SteelBlue }, { "tan", Color.Tan }, { "teal", Color.Teal }, { "thistle", Color.Thistle }, { "tomato", Color.Tomato }, { "turquoise", Color.Turquoise }, { "violet", Color.Violet }, { "wheat", Color.Wheat }, { "white", Color.White }, { "whitesmoke", Color.WhiteSmoke }, { "yellow", Color.Yellow }, { "yellowgreen", Color.YellowGreen } };
            public IMyTextSurface s = null;

            public SpriteHUDLCD(IMyTextSurface s)
            {
                this.s = s;
            }

            int ltick = -1;
            string lasttext = "-1";

            public void setLCD(string text)
            {
                if (text != lasttext || tick - ltick > 120)
                {
                    ltick = tick;
                    lasttext = text;
                    s.WriteText(text);
                    List<object> tok = new List<object>();
                    string[] tokens = text.Split(new string[] { "<color=" }, StringSplitOptions.None);
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        var t = tokens[i];
                        foreach (var kvp in ColorList)
                        {
                            if (t.StartsWith(kvp.Key + ">"))
                            {
                                t = t.Substring(kvp.Key.Length + 1);
                                tok.Add(kvp.Value);
                                break;
                            }
                        }
                        tok.Add(t);
                    }

                    s.ContentType = ContentType.SCRIPT;
                    s.Script = "";
                    s.Font = "Monospace";
                    RectangleF _viewport;
                    _viewport = new RectangleF(
                        (s.TextureSize - s.SurfaceSize) / 2f,
                        s.SurfaceSize
                    );
                    using (var frame = s.DrawFrame())
                    {
                        var zpos = new Vector2(0, 0) + _viewport.Position + new Vector2(s.TextPadding / 100 * s.SurfaceSize.X, s.TextPadding / 100 * s.SurfaceSize.Y);
                        var position = zpos;
                        Color cColor = Color.White;
                        foreach (var t in tok)
                        {
                            if (t is Color) cColor = (Color) t;
                            else if (t is string) writeText((string) t, frame, ref position, zpos, s.FontSize, cColor);
                        }
                    }
                }
            }

            public void writeText(string text, MySpriteDrawFrame frame, ref Vector2 pos, Vector2 zpos, float textSize, Color color)
            {
                string[] lines = text.Split('\n');
                for (int l = 0; l < lines.Length; l++)
                {
                    var line = lines[l];
                    if (line.Length > 0)
                    {
                        MySprite sprite = MySprite.CreateText(line, "Monospace", color, textSize, TextAlignment.LEFT);
                        sprite.Position = pos;
                        frame.Add(sprite);
                    }
                    if (l < lines.Length - 1)
                    {
                        pos.X = zpos.X;
                        pos.Y += 28 * textSize;
                    }
                    else pos.X += 20 * textSize * line.Length;
                }
            }
        }
    }

    public class WcPbApi
    {
        public string[] WcBlockTypeLabels = new string[]
        {
            "Any",
            "Offense",
            "Utility",
            "Power",
            "Production",
            "Thrust",
            "Jumping",
            "Steering"
        };

        private Action<ICollection<MyDefinitionId>> a;
        private Func<IMyTerminalBlock, IDictionary<string, int>, bool> b;
        private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> c;
        private Func<long, bool> d;
        private Func<long, int, MyDetectedEntityInfo> e;
        private Func<IMyTerminalBlock, long, int, bool> f;
        private Action<IMyTerminalBlock, bool, bool, int> g;
        private Func<IMyTerminalBlock, bool> h;
        private Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> i;
        private Func<IMyTerminalBlock, ICollection<string>, int, bool> j;
        private Action<IMyTerminalBlock, ICollection<string>, int> k;
        private Func<IMyTerminalBlock, long, int, Vector3D?> l;

        private Func<IMyTerminalBlock, int, Matrix> m;
        private Func<IMyTerminalBlock, int, Matrix> n;
        private Func<IMyTerminalBlock, long, int, MyTuple<bool, Vector3D?>> o;
        private Func<IMyTerminalBlock, int, string> p;
        private Action<IMyTerminalBlock, int, string> q;
        private Func<long, float> r;
        private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> s;
        private Action<IMyTerminalBlock, long, int> t;
        private Func<long, MyTuple<bool, int, int>> u;

        private Action<IMyTerminalBlock, bool, int> v;
        private Func<IMyTerminalBlock, int, bool, bool, bool> w;
        private Func<IMyTerminalBlock, int, float> x;
        private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> y;
        private Func<IMyTerminalBlock, float> _getCurrentPower;
        public Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, float> _getHeatLevel;

        public bool isReady = false;
        IMyTerminalBlock pbBlock = null;

        public bool Activate(IMyTerminalBlock pbBlock)
        {
            this.pbBlock = pbBlock;
            var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception("WcPbAPI failed to activate");
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;
            AssignMethod(delegates, "GetCoreWeapons", ref a);
            AssignMethod(delegates, "GetBlockWeaponMap", ref b);
            AssignMethod(delegates, "GetSortedThreats", ref c);
            AssignMethod(delegates, "GetObstructions", ref i);
            AssignMethod(delegates, "HasGridAi", ref d);
            AssignMethod(delegates, "GetAiFocus", ref e);
            AssignMethod(delegates, "SetAiFocus", ref f);
            AssignMethod(delegates, "HasCoreWeapon", ref h);
            AssignMethod(delegates, "GetPredictedTargetPosition", ref l);
            AssignMethod(delegates, "GetTurretTargetTypes", ref j);
            AssignMethod(delegates, "SetTurretTargetTypes", ref k);
            AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref m);
            AssignMethod(delegates, "GetWeaponElevationMatrix", ref n);
            AssignMethod(delegates, "IsTargetAlignedExtended", ref o);
            AssignMethod(delegates, "GetActiveAmmo", ref p);
            AssignMethod(delegates, "SetActiveAmmo", ref q);
            AssignMethod(delegates, "GetConstructEffectiveDps", ref r);
            AssignMethod(delegates, "GetWeaponTarget", ref s);
            AssignMethod(delegates, "SetWeaponTarget", ref t);
            AssignMethod(delegates, "GetProjectilesLockedOn", ref u);

            AssignMethod(delegates, "FireWeaponOnce", ref v);
            AssignMethod(delegates, "ToggleWeaponFire", ref g);
            AssignMethod(delegates, "IsWeaponReadyToFire", ref w);
            AssignMethod(delegates, "GetMaxWeaponRange", ref x);
            AssignMethod(delegates, "GetWeaponScope", ref y);

            AssignMethod(delegates, "GetCurrentPower", ref _getCurrentPower);
            AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);

            //Delegate.CreateDelegate(null, null);

            isReady = true;
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }
            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => a?.Invoke(collection);

        public void GetSortedThreats(IDictionary<MyDetectedEntityInfo, float> collection) =>
            c?.Invoke(pbBlock, collection);

        public bool HasGridAi(long entity) => d?.Invoke(entity) ?? false;
        public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => e?.Invoke(shooter, priority);

        public bool SetAiFocus(IMyTerminalBlock pBlock, long target, int priority = 0) =>
            f?.Invoke(pBlock, target, priority) ?? false;

        public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
            g?.Invoke(weapon, on, allWeapons, weaponId);

        public bool HasCoreWeapon(IMyTerminalBlock weapon) => h?.Invoke(weapon) ?? false;

        public void GetObstructions(IMyTerminalBlock pBlock, ICollection<MyDetectedEntityInfo> collection) =>
            i?.Invoke(pBlock, collection);

        public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            l?.Invoke(weapon, targetEnt, weaponId) ?? null;

        public Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weapon, int weaponId) =>
            m?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public Matrix GetWeaponElevationMatrix(IMyTerminalBlock weapon, int weaponId) =>
            n?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public MyTuple<bool, Vector3D?> IsTargetAlignedExtended(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
            o?.Invoke(weapon, targetEnt, weaponId) ?? new MyTuple<bool, Vector3D?>();

        public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
            p?.Invoke(weapon, weaponId) ?? null;

        public void SetActiveAmmo(IMyTerminalBlock weapon, int weaponId, string ammoType) =>
            q?.Invoke(weapon, weaponId, ammoType);

        public float GetConstructEffectiveDps(long entity) => r?.Invoke(entity) ?? 0f;

        public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
            s?.Invoke(weapon, weaponId);

        public void SetWeaponTarget(IMyTerminalBlock weapon, long target, int weaponId = 0) =>
            t?.Invoke(weapon, target, weaponId);

        public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
            b?.Invoke(weaponBlock, collection) ?? false;

        public MyTuple<bool, int, int> GetProjectilesLockedOn(long victim) =>
            u?.Invoke(victim) ?? new MyTuple<bool, int, int>();

        public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
            v?.Invoke(weapon, allWeapons, weaponId);


        public bool IsWeaponReadyToFire(IMyTerminalBlock weapon,
            int weaponId = 0,
            bool anyWeaponReady = true,
            bool shootReady = false) =>
            w?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

        public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
            x?.Invoke(weapon, weaponId) ?? 0f;

        public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
            y?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();

        public float GetCurrentPower(IMyTerminalBlock weapon) => _getCurrentPower?.Invoke(weapon) ?? 0f;

        public float GetHeatLevel(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
    }
}