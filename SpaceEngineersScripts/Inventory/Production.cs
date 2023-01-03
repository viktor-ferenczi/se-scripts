using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game;

namespace SpaceEngineersScripts.Inventory
{
    public class Production : ProgramModule
    {
        private int enqueueCount;
        private readonly List<IMyAssembler> assemblerBlocks = new List<IMyAssembler>();
        private readonly Dictionary<string, int> queuedComponents = new Dictionary<string, int>();
        private readonly Dictionary<Component, MyDefinitionId> componentDefinitions = new Dictionary<Component, MyDefinitionId>();
        private IReadOnlyDictionary<Component, int> restockTargetAmounts;

        public int EnqueueCount => enqueueCount;
        public int AssemblerCount => assemblerBlocks.Count;
        private IEnumerable<IMyAssembler> WorkingAssemblers => Config.EnableComponentRestocking ? assemblerBlocks.Where(a => !a.Closed && a.IsWorking && a.Mode == MyAssemblerMode.Assembly) : Enumerable.Empty<IMyAssembler>();
        private IMyAssembler FirstWorkingAssembler => WorkingAssemblers.FirstOrDefault(); 
        public bool IsRestockingPossible => WorkingAssemblers.Any();
        private IMyAssembler mainAssembler;
        private int assemblerIndex;

        public Production(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            enqueueCount = 0;
            
            assemblerBlocks.Clear();
            queuedComponents.Clear();
            componentDefinitions.Clear();

            restockTargetAmounts = Config.GetRestockTargetAmounts();

            Gts.GetBlockGroupWithName(Config.RestockAssemblersGroup)?.GetBlocksOfType(assemblerBlocks, block => block.IsSameConstructAs(Me));
            Util.SortBlocksByName(assemblerBlocks);

            mainAssembler = WorkingAssemblers.Any(a => a.CooperativeMode) ? WorkingAssemblers.FirstOrDefault(a => !a.CooperativeMode) : null;

            FillComponentDefinitions();
        }

        private void FillComponentDefinitions()
        {
            var assembler = FirstWorkingAssembler;
            if (assembler == null)
            {
                return;
            }
            
            // See https://forum.keenswh.com/threads/how-to-add-an-individual-component-to-the-assembler-queue.7393616/
            // See https://steamcommunity.com/app/244850/discussions/0/527273452877873614/
            foreach (var component in Naming.ComponentNames.Keys)
            {
                var definitionString = $"MyObjectBuilder_BlueprintDefinition/{component.ToString()}";
                var definitionId = MyDefinitionId.Parse(definitionString);
                if (!assembler.CanUseBlueprint(definitionId))
                {
                    definitionId = MyDefinitionId.Parse($"{definitionString}Component");
                }

                if (assembler.CanUseBlueprint(definitionId))
                {
                    componentDefinitions[component] = definitionId;
                }
            }
        }

        public void ScanAssemblerQueues()
        {
            if (!IsRestockingPossible)
            {
                return;
            }
            
            foreach (var assembler in assemblerBlocks)
            {
                AggregateAssemblerQueue(assembler);
            }
        }

        private void AggregateAssemblerQueue(IMyAssembler assembler)
        {
            if (assembler.Closed || !assembler.IsWorking || assembler.Mode != MyAssemblerMode.Assembly)
            {
                return;
            }

            var queue = new List<MyProductionItem>();

            assembler.GetQueue(queue);

            foreach (var item in queue)
            {
                var subtypeName = item.BlueprintId.SubtypeName;
                var amount = queuedComponents.GetValueOrDefault(subtypeName, 0);
                queuedComponents[subtypeName] = amount + (int)item.Amount;
            }
        }

        public void ProduceMissing(Inventory inventory)
        {
            if (!IsRestockingPossible || restockTargetAmounts.Count == 0)
            {
                return;
            }
            
            var stock = inventory.ComponentStock;
            
            if (Config.Debug)
            {
                foreach (var kv in stock)
                {
                    Log.Info("CC S:{1} C:{0}", kv.Key, kv.Value);
                }

                Log.Info("---");

                foreach (var kv in queuedComponents)
                {
                    Log.Info("QC Q:{1} C:{0}", kv.Key, kv.Value);
                }

                Log.Info("---");
            }

            // Prefer using the main assembler,
            // otherwise round-robin on all working ones if no cooperative mode is set
            var assemblers = new List<IMyAssembler>();
            if (mainAssembler == null)
            {
                assemblers.AddRange(WorkingAssemblers);
            }
            else
            {
                assemblers.Add(mainAssembler);
            }
            
            foreach (var p in restockTargetAmounts)
            {
                MyDefinitionId definitionId;
                if (!componentDefinitions.TryGetValue(p.Key, out definitionId))
                {
                    Log.Warning("No definition: {0}", p.Key);
                    continue;
                }
                
                int target;
                if (!restockTargetAmounts.TryGetValue(p.Key, out target) || target < 1)
                {
                    continue;
                }

                var subtypeName = p.Key.ToString();
                var subtypeNameComponent = $"{subtypeName}Component";

                int queued;
                if (!queuedComponents.TryGetValue(subtypeName, out queued))
                {
                    queuedComponents.TryGetValue(subtypeNameComponent, out queued);
                }

                var inStock = 0;
                double d;
                if (stock.TryGetValue(subtypeName, out d) || stock.TryGetValue(subtypeNameComponent, out d)) {
                    inStock = (int)d;
                }
                
                var missing = target - inStock - queued;
                if (missing > 0 && (queued == 0 || missing >= target / 2))
                {
                    if (Config.Debug)
                    {
                        Log.Info("RF S:{0} Q:{1} M:{2} C:{3}", inStock, queued, missing, definitionId.SubtypeName);
                    }

                    assemblerIndex = (assemblerIndex + 1) % assemblers.Count;
                    var assembler = assemblers[assemblerIndex];
                    assembler.AddQueueItem(definitionId, (decimal)missing);
                    enqueueCount++;
                }
            }
        }
    }
}