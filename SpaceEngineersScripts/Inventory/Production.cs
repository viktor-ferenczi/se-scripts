using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game;

namespace SpaceEngineersScripts.Inventory
{
    public class Production : ProgramModule
    {
        private IMyAssembler mainAssembler;
        private readonly List<IMyAssembler> assemblerBlocks = new List<IMyAssembler>();
        private readonly Dictionary<string, int> queuedComponents = new Dictionary<string, int>();
        private readonly Dictionary<Component, MyDefinitionId> componentDefinitions = new Dictionary<Component, MyDefinitionId>();
        private IReadOnlyDictionary<Component, int> restockTargetAmounts;

        public int AssemblerCount => assemblerBlocks.Count;
        public bool IsMainAssemblerAvailable => Config.EnableComponentRestocking && mainAssembler != null && !mainAssembler.Closed && mainAssembler.IsWorking && mainAssembler.Mode == MyAssemblerMode.Assembly;

        public Production(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            assemblerBlocks.Clear();
            queuedComponents.Clear();
            componentDefinitions.Clear();

            restockTargetAmounts = Config.GetRestockTargetAmounts();

            Gts.GetBlockGroupWithName(Config.RestockAssemblersGroup)?.GetBlocksOfType(assemblerBlocks, block => block.IsSameConstructAs(Me));
            mainAssembler = assemblerBlocks.FirstOrDefault(assembler => assembler.CooperativeMode) ?? assemblerBlocks.FirstOrDefault();

            FillComponentDefinitions();
        }

        private void FillComponentDefinitions()
        {
            // See https://forum.keenswh.com/threads/how-to-add-an-individual-component-to-the-assembler-queue.7393616/
            // See https://steamcommunity.com/app/244850/discussions/0/527273452877873614/
            foreach (var component in Naming.ComponentNames.Keys)
            {
                var definitionString = $"MyObjectBuilder_BlueprintDefinition/{component.ToString()}";
                var definitionId = MyDefinitionId.Parse(definitionString);
                if (!mainAssembler.CanUseBlueprint(definitionId))
                {
                    definitionId = MyDefinitionId.Parse($"{definitionString}Component");
                }

                if (mainAssembler.CanUseBlueprint(definitionId))
                {
                    componentDefinitions[component] = definitionId;
                }
            }
        }

        public void ScanAssemblerQueues()
        {
            if (!IsMainAssemblerAvailable)
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
            if (!IsMainAssemblerAvailable || restockTargetAmounts.Count == 0)
            {
                return;
            }

            if (Config.Debug)
            {
                foreach (var kv in inventory.ComponentStock)
                {
                    Log.Info(string.Format("CC S:{1} C:{0}", kv.Key, kv.Value));
                }

                Log.Info("---");

                foreach (var kv in queuedComponents)
                {
                    Log.Info(string.Format("QC Q:{1} C:{0}", kv.Key, kv.Value));
                }

                Log.Info("---");
            }

            var stock = inventory.ComponentStock;

            foreach (var p in restockTargetAmounts)
            {
                MyDefinitionId definitionId;
                if (!componentDefinitions.TryGetValue(p.Key, out definitionId))
                {
                    Log.Warning($"No definition: {p.Key}");
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
                        Log.Info(string.Format("RF S:{0} Q:{1} M:{2} C:{3}", inStock, queued, missing, definitionId.SubtypeName));
                    }

                    mainAssembler.AddQueueItem(definitionId, (decimal)missing);
                }
            }
        }
    }
}