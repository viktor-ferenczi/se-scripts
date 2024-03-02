using System;
using System.IO;
using System.Collections.Generic;
using Sandbox.ModAPI;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace SDXMoveItems.Logging
{

    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate, priority: int.MaxValue)]
    public class Log : MySessionComponentBase
    {
        private static Handler LogHandler;
        private static bool unloaded = false;
        private static long dateStarted;

        public const string FILE = "DebugInfo.log";
        private const int DEFAULT_TIME_INFO = 3000;
        private const int DEFAULT_TIME_ERROR = 10000;

        public const string PRINT_GENERIC_ERROR = "<err>";

        public const string PRINT_MESSAGE = "<msg>";

        #region Handling of handler
        public override void LoadData()
        {
            EnsureHandlerCreated();
            LogHandler.Init(this);
        }

        protected override void UnloadData()
        {
            if (LogHandler != null && LogHandler.AutoClose)
            {
                Unload();
            }
        }

        private static void Unload()
        {
            if (!unloaded)
            {
                unloaded = true;
                LogHandler?.Close();
            }

            LogHandler = null;
        }

        private static void EnsureHandlerCreated()
        {
            if (unloaded)
                throw new Exception($"{typeof(Log).FullName} accessed after it was unloaded! Date started: {new DateTime(dateStarted).ToString()}");

            if (LogHandler == null)
            {
                LogHandler = new Handler();
                dateStarted = DateTime.Now.Ticks;
            }
        }
        #endregion LogHandler

        #region Publicly accessible properties and methods
        public static void Close()
        {
            Unload();
        }

        public static bool AutoClose
        {
            get
            {
                EnsureHandlerCreated();
                return LogHandler.AutoClose;
            }
            set
            {
                EnsureHandlerCreated();
                LogHandler.AutoClose = value;
            }
        }

        public static string ModName
        {
            get
            {
                EnsureHandlerCreated();
                return LogHandler.ModName;
            }
            set
            {
                EnsureHandlerCreated();
                LogHandler.ModName = value;
            }
        }

        public static ulong WorkshopId => LogHandler?.WorkshopId ?? 0;

        public static void IncreaseIndent()
        {
            EnsureHandlerCreated();
            LogHandler.IncreaseIndent();
        }

        public static void DecreaseIndent()
        {
            EnsureHandlerCreated();
            LogHandler.DecreaseIndent();
        }

        public static void ResetIndent()
        {
            EnsureHandlerCreated();
            LogHandler.ResetIndent();
        }

        public static void Error(Exception exception, string printText = PRINT_GENERIC_ERROR, int printTimeMs = DEFAULT_TIME_ERROR)
        {
            EnsureHandlerCreated();
            LogHandler.Error(exception.ToString(), printText, printTimeMs);
        }

        public static void Error(string message, string printText = PRINT_MESSAGE, int printTimeMs = DEFAULT_TIME_ERROR)
        {
            EnsureHandlerCreated();
            LogHandler.Error(message, printText, printTimeMs);
        }

        public static void Info(string message, string printText = null, int printTimeMs = DEFAULT_TIME_INFO)
        {

            EnsureHandlerCreated();
            LogHandler.Info(message, printText, printTimeMs);
        }

        public static bool TaskHasErrors(ParallelTasks.Task task, string taskName)
        {
            EnsureHandlerCreated();

            if (task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach (Exception e in task.Exceptions)
                {
                    Error($"Error in {taskName} thread!\n{e}");
                }

                return true;
            }

            return false;
        }
        #endregion Publicly accessible properties and methods

        private class Handler
        {
            private Log sessionComp;
            private string modName = string.Empty;

            private TextWriter writer;
            private int indent = 0;
            private string errorPrintText;
            private bool sessionReady = false;

            private double chatMessageCooldown;

            private IMyHudNotification notifyInfo;
            private IMyHudNotification notifyError;

            private StringBuilder sb = new StringBuilder(64);

            private List<string> preInitMessages;
            private bool preInitErrors = false;

            public bool AutoClose { get; set; } = true;

            public ulong WorkshopId { get; private set; } = 0;

            public string ModName
            {
                get
                {
                    return modName;
                }
                set
                {
                    modName = value;
                    ComputeErrorPrintText();
                }
            }
            public Handler()
            {
            }
            public void Init(Log sessionComp)
            {
                if (writer != null)
                    return; // already initialized
                if (MyAPIGateway.Utilities == null)
                    throw new Exception("MyAPIGateway.Utilities is NULL !");
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE, typeof(Log));
                MyAPIGateway.Session.OnSessionReady += OnSessionReady;
                this.sessionComp = sessionComp;
                if (string.IsNullOrWhiteSpace(ModName))
                    ModName = sessionComp.ModContext.ModName;
                WorkshopId = GetWorkshopID(sessionComp.ModContext.ModId);
                ShowPreInitMessages();
                InitMessage();
            }

            public void Close()
            {
                if (writer != null)
                {
                    Info("Unloaded.");

                    writer.Flush();
                    writer.Close();
                    writer = null;
                }
                sessionReady = false;
                MyAPIGateway.Session.OnSessionReady -= OnSessionReady;
            }

            private void OnSessionReady()
            {
                sessionReady = true;
            }

            private void ShowPreInitMessages()
            {
                if (preInitMessages == null)
                    return;

                if (preInitErrors)
                    Error($"Got errors occurred during loading:", PRINT_GENERIC_ERROR, 10000);
                else
                    Info($"Got log messages during loading:", PRINT_GENERIC_ERROR, 10000);

                Info($"--- pre-init messages ---");

                foreach (string msg in preInitMessages)
                {
                    Info(msg);
                }

                Info("--- end pre-init messages ---");

                preInitMessages = null;
            }

            private void InitMessage()
            {
                MyObjectBuilder_SessionSettings worldsettings = MyAPIGateway.Session.SessionSettings;

                sb.Clear();
                sb.Append("Initialized; ").Append(DateTime.Now.ToString("yyyy MMMM dd (dddd) HH:mm:ss")).Append("; GameVersion=").Append(MyAPIGateway.Session.Version.ToString());
                sb.Append("\nModWorkshopId=").Append(WorkshopId).Append("; ModName=").Append(modName).Append("; ModService=").Append(sessionComp.ModContext.ModServiceName);
                sb.Append("\nGameMode=").Append(worldsettings.GameMode.ToString()).Append("; OnlineMode=").Append(worldsettings.OnlineMode.ToString());
                sb.Append("\nServer=").Append(MyAPIGateway.Session.IsServer).Append("; DS=").Append(MyAPIGateway.Utilities.IsDedicated);
                sb.Append("\nDefined=");

#if STABLE
                sb.Append("STABLE, ");
#endif

#if UNOFFICIAL
                sb.Append("UNOFFICIAL, ");
#endif

#if DEBUG
                sb.Append("DEBUG, ");
#endif

#if BRANCH_STABLE
                sb.Append("BRANCH_STABLE, ");
#endif

#if BRANCH_DEVELOP
                sb.Append("BRANCH_DEVELOP, ");
#endif

#if BRANCH_UNKNOWN
                sb.Append("BRANCH_UNKNOWN, ");
#endif

                Info(sb.ToString());
                sb.Clear();
            }

            private void ComputeErrorPrintText()
            {
                errorPrintText = $"report contents of: %AppData%/SpaceEngineers/Storage/{MyAPIGateway.Utilities.GamePaths.ModScopeName}/{FILE}";
            }

            public void IncreaseIndent()
            {
                indent++;
            }

            public void DecreaseIndent()
            {
                if (indent > 0)
                    indent--;
            }

            public void ResetIndent()
            {
                indent = 0;
            }

            public void Error(string message, string printText = PRINT_GENERIC_ERROR, int printTime = DEFAULT_TIME_ERROR)
            {
                MyLog.Default.WriteLineAndConsole($"{modName} error/exception: {message}"); // write to game's log

                LogMessage(message, "ERROR: "); // write to custom log

                if (printText != null) // printing to HUD is optional
                    ShowHudMessage(ref notifyError, message, printText, printTime, MyFontEnum.Red);
            }

            public void Info(string message, string printText = null, int printTime = DEFAULT_TIME_INFO)
            {
                LogMessage(message); // write to custom log

                if (printText != null) // printing to HUD is optional
                    ShowHudMessage(ref notifyInfo, message, printText, printTime, MyFontEnum.Debug);
            }

            private void ShowHudMessage(ref IMyHudNotification notify, string message, string printText, int printTime, string font)
            {
                try
                {
                    if (!sessionReady || printText == null || MyAPIGateway.Utilities == null || MyAPIGateway.Utilities.IsDedicated)
                        return;

                    if (MyAPIGateway.Session?.Player != null)
                    {
                        double timeSec = TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds;
                        if (chatMessageCooldown <= timeSec)
                        {
                            chatMessageCooldown = timeSec + 60;

                            // HACK: SendChatMessageColored() no longer works if sent by MP clients (even to themselves)
                            if (printText == PRINT_GENERIC_ERROR)
                            {
                                MyAPIGateway.Utilities.ShowMessage($"{modName} ERROR", errorPrintText);
                                //MyVisualScriptLogicProvider.SendChatMessageColored(errorPrintText, Color.Red, $"{modName} ERROR", MyAPIGateway.Session.Player.IdentityId);
                            }
                            else if (printText == PRINT_MESSAGE)
                            {
                                if (font == MyFontEnum.Red)
                                {
                                    MyAPIGateway.Utilities.ShowMessage($"{modName} ERROR", message);
                                    //MyVisualScriptLogicProvider.SendChatMessageColored(message, Color.Red, $"{modName} ERROR", MyAPIGateway.Session.Player.IdentityId);
                                }
                                else
                                {
                                    MyAPIGateway.Utilities.ShowMessage($"{modName} WARNING", message);
                                    //MyVisualScriptLogicProvider.SendChatMessageColored(message, Color.Yellow, $"{modName} WARNING", MyAPIGateway.Session.Player.IdentityId);
                                }
                            }
                            else
                            {
                                if (font == MyFontEnum.Red)
                                {
                                    MyAPIGateway.Utilities.ShowMessage($"{modName} ERROR", printText);
                                    //MyVisualScriptLogicProvider.SendChatMessageColored(printText, Color.Red, $"{modName} ERROR", MyAPIGateway.Session.Player.IdentityId);
                                }
                                else
                                {
                                    MyAPIGateway.Utilities.ShowMessage($"{modName} WARNING", printText);
                                    //MyVisualScriptLogicProvider.SendChatMessageColored(printText, Color.Yellow, $"{modName} WARNING", MyAPIGateway.Session.Player.IdentityId);
                                }
                            }
                        }
                    }

                }
                catch (Exception e)
                {
                    Info("ERROR: Could not send notification to local client: " + e);
                    MyLog.Default.WriteLineAndConsole($"{modName} :: LOGGER error/exception: Could not send notification to local client: {e}");
                }
            }

            private void LogMessage(string message, string prefix = null)
            {
                try
                {
                    sb.Clear();
                    sb.Append(DateTime.Now.ToString("[HH:mm:ss/")).Append((MyAPIGateway.Session.GameplayFrameCounter % 60).ToString("00")).Append("] ");

                    if (writer == null)
                        sb.Append("(PRE-INIT) ");

                    for (int i = 0; i < indent; i++)
                        sb.Append(' ', 4);

                    if (prefix != null)
                        sb.Append(prefix);

                    sb.Append(message);

                    if (writer == null)
                    {
                        if (preInitMessages == null)
                            preInitMessages = new List<string>(2);

                        preInitMessages.Add(sb.ToString());

                        if (!preInitErrors && prefix != null && prefix.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) != -1)
                            preInitErrors = true;
                    }
                    else
                    {
                        writer.WriteLine(sb);
                        writer.Flush();
                    }

                    sb.Clear();
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole($"{modName} :: LOGGER error/exception while logging: '{message}'\nLogger error: {e.Message}\n{e.StackTrace}");
                }
            }

            private static ulong GetWorkshopID(string modId)
            {
                foreach (MyObjectBuilder_Checkpoint.ModItem mod in MyAPIGateway.Session.Mods)
                {
                    if (mod.Name == modId)
                        return mod.PublishedFileId;
                }

                return 0;
            }
        }
    }
}