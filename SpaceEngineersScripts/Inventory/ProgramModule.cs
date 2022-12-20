using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public abstract class ProgramModule
    {
        protected readonly Config Config;
        protected readonly Log Log;
        protected readonly IMyProgrammableBlock Me;
        protected readonly IMyGridTerminalSystem Gts;

        protected ProgramModule(Config config, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts)
        {
            Config = config;
            Log = log;
            Me = me;
            Gts = gts;
        }
    }
}