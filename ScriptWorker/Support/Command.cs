using Sandbox.ModAPI.Ingame;

namespace ScriptWorker.Support
{
    // Enqueued parameters from a Main invocation
    public class Command
    {
        public string Argument;
        public UpdateType UpdateSource;
    }
}