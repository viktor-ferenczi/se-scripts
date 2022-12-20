using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;

namespace SpaceEngineersScripts.Inventory
{
    public class Production : ProgramModule
    {
        private IMyAssembler mainAssembler;
        
        private readonly List<IMyAssembler> assemblerBlocks = new List<IMyAssembler>();
        private readonly Dictionary<string, int> queuedComponents = new Dictionary<string, int>();
        private readonly Dictionary<string, MyDefinitionId> restockComponents = new Dictionary<string, MyDefinitionId>();
        
        public Production(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts) : base(config, log, me, gts)
        {
        }

        public void Reset()
        {
            assemblerBlocks.Clear();
            queuedComponents.Clear();

            Gts.GetBlockGroupWithName(Config.RestockAssemblersGroup)?.GetBlocksOfType(assemblerBlocks, block => block.IsSameConstructAs(Me));
            mainAssembler = assemblerBlocks.Where(assembler => assembler.CooperativeMode).FirstOrDefault() ?? assemblerBlocks.FirstOrDefault();
        }

        private void ScanAssemblerQueues()
        {
            foreach (var assembler in assemblerBlocks)
            {
                if (assembler.Closed || !assembler.IsFunctional)
                {
                    continue;
                }

                AggregateAssemblerQueue(assembler);
            }
        }
        
        private void AggregateAssemblerQueue(IMyAssembler assembler)
        {
            if (assembler.Mode != MyAssemblerMode.Assembly)
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

        private void ProduceMissing()
        {
            if (mainAssembler == null || restockComponents.Count == 0 || mainAssembler.Closed || !mainAssembler.IsFunctional)
            {
                return;
            }

            !!!Enqueue only if the assembler is in assembly mode.Don't touch the queue if it is in disassembly mode!

            if (cfg.Debug)
            {
                foreach (var kv in component)
                {
                    log.Info(string.Format("CC S:{1} C:{0}", kv.Key, kv.Value));
                }

                log.Info("---");

                foreach (var kv in queuedComponents)
                {
                    log.Info(string.Format("QC Q:{1} C:{0}", kv.Key, kv.Value));
                }

                log.Info("---");
            }

            foreach (var kv in restockComponents)
            {
                var subtypeName = kv.Key;
                var subtypeNameComponent = subtypeName + "Component";

                var stock = (int)component.GetValueOrDefault(subtypeName);
                if (stock == 0)
                {
                    stock = (int)component.GetValueOrDefault(subtypeNameComponent);
                }

                var queued = queuedComponents.GetValueOrDefault(subtypeName);
                if (queued == 0)
                {
                    queued = queuedComponents.GetValueOrDefault(subtypeNameComponent);
                }

                var missing = cfg.RestockMinimum - queued - stock;
                if (missing > queued)
                {
                    var definitionId = kv.Value;
                    if (cfg.Debug)
                    {
                        log.Info(string.Format("RF S:{0} Q:{1} M:{2} C:{3}", stock, queued, missing, definitionId.SubtypeName));
                    }

                    mainAssembler.AddQueueItem(definitionId, (MyFixedPoint)(missing + cfg.RestockOverhead));
                }
            }
        }
    }
}