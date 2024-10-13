using System;

namespace ScriptWorker.Support
{
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
}