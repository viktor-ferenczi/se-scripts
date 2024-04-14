using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KTZHunt3
{
    partial class Program : MyGridProgram
    {
        IMyTextSurface consoleLog = null;
        IMyTextSurface statusLog = null;
        IMyTextSurface profileLog = null;
        IMyTextSurface PDCLog = null;

        List<IMyTerminalBlock> weaponCoreWeapons = new List<IMyTerminalBlock>();

        List<IMyPowerProducer> powergens = new List<IMyPowerProducer>();

        List<IMyShipController> controllers = new List<IMyShipController>();

        List<IMyGyro> gyros = new List<IMyGyro>();

        List<IMyTerminalBlock> grinders = new List<IMyTerminalBlock>();

        List<IMyGasGenerator> extractors = new List<IMyGasGenerator>();
        List<IMyGasTank> tanks_hydrogen = new List<IMyGasTank>();
        List<IMyThrust> thrusters = new List<IMyThrust>();

        List<IMyAssembler> assemblers = new List<IMyAssembler>();

        List<IMyTerminalBlock> inventoryBlocks = new List<IMyTerminalBlock>();

        List<IMyCargoContainer> loot_cargos = new List<IMyCargoContainer>();
        List<IMyShipConnector> connectors = new List<IMyShipConnector>();

        List<IMyFunctionalBlock> torpedoGroup = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> railGroup = new List<IMyFunctionalBlock>();
        List<IMyFunctionalBlock> PDCGroup = new List<IMyFunctionalBlock>();

        static bool addIfT<T>(IMyTerminalBlock b, List<T> l) where T : class
        {
            T t = b as T;
            if (t != null)
            {
                l.Add(t);
                return true;
            }
            return false;
        }

        bool isThis(IMyTerminalBlock b)
        {
            return b.OwnerId == Me.OwnerId && b.CubeGrid == Me.CubeGrid;
        }

        int loadstep = 0;
        IEnumerator<bool> blockLoad = null;
        public bool norun = true;
        public bool loaded = false;
        public double lastMS = 0;

        public bool load(double maxMS)
        {
            if (norun)
            {
                norun = false;
                blockLoad = blockLoader();
            }
            else if (!loaded)
            {
                DateTime s = DateTime.Now;
                double r = 0;
                do
                {
                    loaded = !blockLoad.MoveNext();
                    loadstep++;
                    r = (DateTime.Now - s).TotalMilliseconds;
                } while (!loaded && r < maxMS);
                lastMS = r;
                if (loaded)
                {
                    blockLoad.Dispose();
                    blockLoad = null;
                    log("Blockload completed in " + loadstep + " steps, " + tick + "t", LT.LOG_N);
                }
            }
            return loaded;
        }

        public IEnumerator<bool> blockLoader()
        {
            yield return true;
            if (APIWC == null)
            {
                APIWC = new WcPbApi();
                try
                {
                    APIWC.Activate(Me);
                }
                catch (Exception) { }
                if (!APIWC.isReady) log("Unable to load WeaponCore PBAPI?", LT.LOG_N);
            }
            yield return true;
            var gts = GridTerminalSystem;
            List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
            yield return true;
            gts.GetBlocks(allBlocks);
            int allBlocksLength = allBlocks.Count;
            for (int i = 0; i < allBlocksLength; i++)
            {
                yield return true;
                var b = allBlocks[i];
                //bool m = false;
                if (isThis(b))
                {
                    if (APIWC.HasCoreWeapon(b)) weaponCoreWeapons.Add(b);
                    var lcd = b as IMyTextSurface;
                    if (lcd != null)
                    {
                        if (statusLog == null && b.CustomData.Contains("radarLog"))
                        {
                            statusLog = lcd;
                            continue;
                        }
                        else if (consoleLog == null && b.CustomData.Contains("consoleLog"))
                        {
                            consoleLog = lcd;
                            continue;
                        }
                        else if (profileLog == null && b.CustomData.Contains("profileLog"))
                        {
                            profileLog = lcd;
                            continue;
                        }
                        else if (PDCLog == null && b.CustomData.Contains("PDCLog"))
                        {
                            PDCLog = lcd;
                            continue;
                        }
                    }
                }
                yield return true;
                if (b.InventoryCount > 0)
                {
                    inventoryBlocks.Add(b);
                    yield return true;
                }
                if (addIfT(b, controllers)) continue;
                yield return true;
                if (addIfT(b, gyros)) continue;
                yield return true;
                if (addIfT(b, connectors)) continue;
                yield return true;
                if (addIfT(b, powergens)) continue;
                yield return true;
                var deftxt = b.DefinitionDisplayNameText;

                if (deftxt.Contains("Grinder"))
                {
                    grinders.Add(b);
                    continue;
                }
                yield return true;
                if (b is IMyGasGenerator && (deftxt == "Extractor" || deftxt == "Small Fuel Extractor"))
                {
                    extractors.Add((IMyGasGenerator) b);
                    continue;
                }
                yield return true;
                if (b is IMyGasTank && deftxt.Contains("Hydrogen"))
                {
                    tanks_hydrogen.Add((IMyGasTank) b);
                    continue;
                }
            }

            List<IMyBlockGroup> bgs = new List<IMyBlockGroup>();
            gts.GetBlockGroups(bgs);
            int l = bgs.Count;
            for (int gi = 0; gi < bgs.Count; gi++)
            {
                yield return true;
                var bg = bgs[gi];
                var n = bg.Name;
                if (n == "PDCs") bg.GetBlocksOfType(PDCGroup);
                else if (n == "Railguns") bg.GetBlocksOfType(railGroup);
                else if (n == "Torps") bg.GetBlocksOfType(torpedoGroup);
            }
            yield return false;
        }




        /*public class Hyperloader
        {
            public Program p = null;
            public bool isThis(IMyTerminalBlock b)
            {
                return b.OwnerId == p.Me.OwnerId && b.CubeGrid == p.Me.CubeGrid;
            }


            public int steps = 0;
        }*/
    }
}