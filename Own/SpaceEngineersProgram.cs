using System;
using Sandbox.ModAPI.Ingame;
using IMyGridTerminalSystem = Sandbox.ModAPI.Ingame.IMyGridTerminalSystem;

namespace Inventory
{
    public class SpaceEngineersProgram
    {
        protected IMyGridTerminalSystem GridTerminalSystem = null;

        protected class Runtime
        {
            public static UpdateFrequency UpdateFrequency { get; set; }
        }

        protected void Echo(string noPanelsFound)
        {
            throw new NotImplementedException();
        }

        protected String Storage { get; set; }
        protected IMyTerminalBlock Me { get; set; }

        public SpaceEngineersProgram()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script.
            //
            // The constructor is optional and can be removed if not
            // needed.
            //
            // It's recommended to set RuntimeInfo.UpdateFrequency
            // here, which will allow your script to run itself without a
            // timer block.
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

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from.
            //
            // The method itself is required, but the arguments above
            // can be removed if not needed.
        }
    }
}