using Sandbox.ModAPI.Ingame;

namespace SpaceEngineersScripts.Inventory
{
    public abstract class ProgramModule
    {
        protected readonly Cfg Cfg;
        protected readonly Log Log;
        protected readonly IMyProgrammableBlock Me;
        protected readonly IMyGridTerminalSystem Gts;

        protected ProgramModule(Cfg cfg, Log log, IMyProgrammableBlock me, IMyGridTerminalSystem gts)
        {
            Cfg = cfg;
            Log = log;
            Me = me;
            Gts = gts;
        }
    }
}