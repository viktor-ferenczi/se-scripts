// This symbol is defined to support conditional compilation for backwards compatibility with the vanilla game

#define SCRIPT_WORKER_V1

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

// TODO: Change the namespace name to something meaningful,
//       Put all the code which needs to be deployed as part of
//       your script into this same namespace!
namespace ScriptWorker
{
    // TODO: See the README.md for more: Hints and ScriptDev client plugin
    // TODO: Add your supporting code in separate classes and source files

    // Where to schedule the next execution of the worker
    public enum WorkerSchedule
    {
        // Schedule on the main thread, can apply changes to the grid while running here
        Update,
        Update10,
        Update100,

        // Schedule on a background worker thread, have only read-only access to the grid while running here
        Background,
    }

    // Exceeding any of these limits causes the worker to be killed
    public interface IWorkerLimits
    {
        // Maximum time the worker can run with full access, like if it would run on the main thread
        TimeSpan MaxMain { get; }

        // Maximum time the worker can run with read-only access in a worker thread in parallel with main
        TimeSpan MaxBackground { get; }
    }

    // ReSharper disable once UnusedType.Global
    class Program : MyGridProgram
    {
        public Program()
        {
            // TODO: One-time initialization executed when the PB program is loaded
            // Ideally don't put anything here, move all initialization into the Worker
        }

        // ReSharper disable once UnusedMember.Global
        public void Main(string argument, UpdateType updateSource)
        {
            // TODO: This is executed when the PB is run
            // Use this method to receive commands and events from the grid,
            // but delegate all actual processing to the Worker.
        }

#if SCRIPT_WORKER_V1
        // The plugin looks for the presence of the Worker method with this signature.
        // If it presents then it prepends WorkerSchedule and IWorkerLimits to the code
        // and rewrites the code to include accessibility checks on all setters and
        // method calls to all accessible game objects. This is how the read-only access
        // is enforced while running on a background thread. Dirty read may happen.

        // ReSharper disable once UnusedMember.Global
        public IEnumerable<WorkerSchedule> Worker(IWorkerLimits limits)
        {
            // Started in a worker thread after the PB is run with any arguments unless it is already running

            // Only read-only access is allowed to the grid while the program runs in a worker thread.
            // Dirty read may happen due to the concurrent access, but it will never crash.

            // The read-only access is enforced by wrapping all setter and methods with side effect in a guard
            // condition which checks a flag the program cannot change, the flag indicates main thread execution.

            // Depending on the server's configuration (number of worker threads for PB execution) multiple
            // PBs may execute this method at the same time even on the same grid, but they cannot interact
            // due to the read-only access, so that's not an issue (no race conditions are possible).

            // Scheduling on the configured number of background worker threads happens in a round-robin manner
            // for each player and inside that for each PB. It means fully fair scheduling of players, then for
            // each player the script workers they run at the time.

            // TODO: Background worker initialization

            for (;;)
            {
                var started = DateTime.UtcNow;
                // TODO: Periodic background work
                var duration = DateTime.UtcNow - started;

                // TODO: Yield this to split your task into periods shorter than the background execution time limit
                // Suspend the program now, continue on a worker thread once CPU capacity is available
                yield return WorkerSchedule.Background;

                // TODO: Use one of these to execute code on the main thread to apply changes to the grid
                // Suspend the program until an update, then run it on the main thread to have full grid access
                yield return WorkerSchedule.Update;
                yield return WorkerSchedule.Update10;
                yield return WorkerSchedule.Update100;

                started = DateTime.UtcNow;
                // TODO: Apply changes to the grid here
                duration = DateTime.UtcNow - started;
                // TODO: Make sure the duration is less than the maximum in the limits

                // TODO: Use this to switch back to the worker thread once done with all modifications
                // Suspend the program now, continue on a worker thread once CPU capacity is available
                yield return WorkerSchedule.Background;

                // TODO: Break the loop to stop the worker
                break;
            }

            // This method is killed without raising an exception if it does not yield nor finish until
            // the configured amount of time. Use automatic throttling to stay under the limits.
        }
#endif

        // ReSharper disable once UnusedMember.Global
        public void Save()
        {
            // TODO: You can run code here before the game is saved (optional method)
        }
    }
}