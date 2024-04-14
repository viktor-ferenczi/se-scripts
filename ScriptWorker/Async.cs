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
using System.Threading.Tasks;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;


// TODO: Change the namespace name to something meaningful,
//       Put all the code which needs to be deployed as part of
//       your script into this same namespace!
namespace ScriptWorker.Async
{
    class MyAsyncGridProgram : MyGridProgram
    {
        // TODO: Return true while the program is running on the main thread
        protected bool IsScheduledOnMainThread { private set; get; } = true;

        protected async Task ScheduleOnWorkerThread()
        {
            // TODO: Implement scheduling the program on the main thread where it has full access to the grid
        }

        protected async Task ScheduleOnMainThread()
        {
            // TODO: Implement scheduling the program on a worker thread where it has read-only access to the grid
        }
    }

    // TODO: See the README.md for more: Hints and ScriptDev client plugin
    // TODO: Add your supporting code in separate classes and source files

    // ReSharper disable once UnusedType.Global
    class Program : MyAsyncGridProgram
    {
        public Program()
        {
            // TODO: Write your one-time initialization code here
        }

        // ReSharper disable once UnusedMember.Global
        public async Task Main(string argument, UpdateType updateSource)
        {
            // TODO: This code will run on the main thread with full access to the grid

            await ScheduleOnWorkerThread();

            await ScheduleOnMainThread();

            // TODO: This code will run on the main thread with full access to the grid
        }

        // ReSharper disable once UnusedMember.Global
        public void Save()
        {
            // TODO: You can run code here before the game is saved (optional method)
        }
    }
}