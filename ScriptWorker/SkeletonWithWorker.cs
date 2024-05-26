// This symbol is defined to support conditional compilation for backwards compatibility with the vanilla game

#define WORKER_V1

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
//       your script into this same namespace.
namespace ScriptWorker
{
    // TODO: See the README.md for more: Hints and ScriptDev client plugin
    // TODO: Add your supporting code in separate classes and source files

    // Where to schedule the next execution of the worker
    public enum Schedule
    {
        // Schedule on a background worker thread with read-only access to game objects
        Background,

        // Schedule on the game's main thread with full access to game objects
        Update100,
        Update10,
        Update,
    }

    // Enqueued parameters from a Main invocation
    public class Command
    {
        public string argument;
        public UpdateType updateSource;
    }

    // Worker control interfacte
    public interface IWorker
    {
        // Dequeues the next command if there is any
        bool TryDequeueCommand(out Command command);

        // Current schedule
        Schedule CurrentSchedule { get; }

        // The most frequent update allowed, requesting a more frequent one will kill the worker
        Schedule MostFrequentUpdate { get; }

        // Maximum time the worker can run on the game's main thread with full access at a time,
        // the worker will be killed if the time limit is exceeded
        TimeSpan MainTimeout { get; }

        // Maximum time the worker can run in a worker thread with read-only access at a time,
        // the worker will be killed if the time limit is exceeded
        TimeSpan BackgroundTimeout { get; }
    }

    // ReSharper disable once UnusedType.Global
    class Program : MyGridProgram
    {
#if WORKER_V1
        // The plugin looks for the presence of the Worker method with the signature below.
        // If it presents then it prepends the above types to the code, rewrites the code to
        // include accessibility checks on all setters and method calls to on game objects.
        // This is how the read-only access is enforced while running in the background.
        // The script must not have a Main method in this mode, it will be implemented
        // automatically to enqueue the commands received from the game, which can then
        // be processed by the Worker method.

        // No static constructor is allowed!

        public Program()
        {
            // TODO: One-time initialization executed when the worker is started.
            // This initialization runs on a background worker thread and has read-only access.
            // Use the constructor only to initialize data structures and don't access game objects.
            // Move any access to game objects into the Worker method, so it can be properly scheduled.
        }

        // ReSharper disable once UnusedMember.Global
        public IEnumerable<Schedule> Worker(IWorker worker)
        {
            // Started on a background worker thread right after the script is initialized (compiled).
            // It starts even if the Main of the script is not invoked yet.

            // Only read-only access is allowed to the grid while the worker runs in the background.
            // Dirty read may happen due to concurrent access, but it will never crash.

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
                Command command;
                if (worker.TryDequeueCommand(out command))
                {
                    // TODO: Handle command

                    // Suspend the program now, continue on a worker thread once CPU capacity is available
                    yield return Schedule.Background;

                    continue;
                }

                var started = DateTime.UtcNow;
                // TODO: Periodic background work
                var duration = DateTime.UtcNow - started;
                // TODO: Make sure the duration is less than worker.BackgroundTimeout

                // TODO: Yield this to split your task into periods shorter than the background execution time limit
                // Suspend the program now, continue on a worker thread once CPU capacity is available
                yield return Schedule.Background;

                // TODO: Use one of these to execute code on the main thread to apply changes to the grid
                // Suspend the program until an update, then run it on the main thread to have full grid access
                yield return Schedule.Update100;
                yield return Schedule.Update10;
                yield return Schedule.Update;

                started = DateTime.UtcNow;
                // TODO: Apply changes to the grid here
                duration = DateTime.UtcNow - started;
                // TODO: Make sure the duration is less than worker.MainTimeout

                // TODO: Use this to switch back to the worker thread once done with all modifications
                // Suspend the program now, continue on a worker thread once CPU capacity is available
                yield return Schedule.Background;

                // TODO: Break the loop to stop the worker
                break;
            }

            // This method is killed without raising an exception if it does not yield nor finish until
            // the configured amount of time. Use automatic throttling to stay under the limits.
        }
#else
        // TODO: Fallback implementation if there is no support for script workers

        public Program()
        {
            // TODO
        }

        // ReSharper disable once UnusedMember.Global
        public void Main(string argument, UpdateType updateSource)
        {
            // TODO
        }
#endif

        // ReSharper disable once UnusedMember.Global
        public void Save()
        {
            // TODO: You can run code here before the game is saved (optional method)
        }
    }
}