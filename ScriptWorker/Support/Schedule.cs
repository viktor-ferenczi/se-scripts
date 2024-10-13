namespace ScriptWorker.Support
{
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
}