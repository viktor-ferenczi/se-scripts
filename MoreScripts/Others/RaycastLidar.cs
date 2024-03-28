/**************
 Raycast Lidar Script - simpla rangefinder by camera raycast
 https://steamcommunity.com/sharedfiles/filedetails/?id=2559196981

 Author: Survival Ready - steamcommunity.com/profiles/76561199069720721/myworkshopfiles/
  
 Programm Block (PB) paramaters (case insenitive):
  
 SCAN      - scan aim
 
 RANGE     - show current raycast range 
 
 RANGE NN  - set raycast range to NN in meters
 
 FIRE [PB] - missile launch (just run [PB] by prefix)
 
 ---
 
Simple script using raycast vanilla camera. The GPS of the found target is placed in the CustomData camera and can be used for further automation, for example, firing torpedoes.

Main features

1. measuring distance to object / target
2. setting the scanning range
3. transferring GPS coordinates of the found target to the autopilot (if installed)
4. calling a given program block with a parameter or GPS target

Installation and configuration
For the script to work, the following are required:

a) program block
b) ship controller (cockpit, remote control, pilot's seat)
c) camera (not behind glass)

The installation of the script code into the program unit is carried out by subscription in Steam. After saving the code, add the same "lidar" prefix (by default) to the name of the camera and LCD panel to display information. The output can be carried out on the screen of the cockpit or the pilot's seat; for this, the prefix must also be added to the cockpit name. The script works on demand and does not use the system timer, which makes it convenient for use on servers.

Usage

    If the variable autopilot is not equal to 0, then the script transfers the target coordinates to the autopilot installed on the same grid as the PB, the GPS target coordinates. Waypoint is put on autopilot under the name "Lidar". The autopilot is manually activated if necessary.

    You can make a sight out of a transparent LCD panel using a variable sight. If it is not equal to "", then its contents will be displayed when the script is initialized and the target is rescanned. This is convenient for scripts to control custom turrets like MART.

    Custom Data of camera lidar can be easily adapted to launch missiles of the same type using automatic guidance and control scripts type Easy Lidar Homing Script


An example of such use is published in the workshop.
 
*/


using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace RaycastLidar
{
    class Program : SpaceEngineersScripts.Skeleton.SpaceEngineersProgram
    {
        #region CodeEditor

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
        }

        int outseat = 0; // cockpit or pilot's seat screen = 0/NN
        int autopilot = 0; // send raycast lidar point to autopilot (if existst)
        double range = 15000; // default raycast range
        string prefix = "lidar"; // LCD & Camera name prefix
        string target = "hit"; // get hit raycast beam, otherwise aim center
        string sight = ""; // if not "" then out on LCD on rescanning aim

        string mispbl = ""; // programm block prefix by default
        string misrun = ""; // programm block parameters
        bool init = false;

        string head = "<< Raycast Lidar >>\n\n";

        IMyTextPanel lcd;
        IMyCameraBlock lidar;
        IMyRemoteControl remote;
        IMyCockpit cockpit;
        IMyTextSurface surface;
        IMyProgrammableBlock pbl;

        void Main(string arguments, UpdateType updateSource)
        {
            var hasprefix = false;
            var list = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocks(list);

            foreach (var block in list)
            {
                hasprefix = block.CustomName.ToLower().Contains(prefix);

                if (block is IMyCameraBlock && hasprefix)
                {
                    lidar = (IMyCameraBlock)block;
                    lidar.EnableRaycast = true;
                    lidar.ApplyAction("OnOff_On");
                }

                if (lcd == null)
                {
                    if (block is IMyTextPanel && hasprefix)
                    {
                        lcd = (IMyTextPanel)block;
                        lcd.ApplyAction("OnOff_On");
                        surface = lcd as IMyTextSurface;
                    }
                    else if (block is IMyCockpit && outseat > 0 && hasprefix)
                    {
                        cockpit = (IMyCockpit)block;
                        surface = cockpit.GetSurface(outseat - 1) as IMyTextSurface;
                    }
                }

                if (block is IMyRemoteControl && block.CubeGrid == Me.CubeGrid && remote == null && autopilot > 0)
                {
                    remote = (IMyRemoteControl)block;
                }
            }

            if (lidar == null)
            {
                outSur("No camera lidar");
                return;
            }

            string view = (sight == "" ? "Ready for scan" : sight);
            if (!init)
            {
                init = true;
                lidar.CustomData = "";
            }

            if (arguments.Length > 0)
            {
                string[] tokens = arguments.Trim().Split(' ');
                string token = (tokens.Length > 0 ? tokens[0].Trim().ToUpper() : "");

                switch (token)
                {
                    case "FIRE":

                        if (tokens.Length == 2) mispbl = tokens[1].ToLower();

                        if (mispbl == "")
                        {
                            view = "No Missile PB prefix";
                            break;
                        }

                        GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(list, b => b.CustomName.ToLower().Contains(mispbl));

                        if (list.Count > 0)
                        {
                            pbl = list[0] as IMyProgrammableBlock;
                            if (pbl.TryRun(misrun)) view = $"Missile launched\nMissile ready: {list.Count - 1}";
                        }
                        else
                        {
                            view = "NO ready missiles";
                        }

                        break;

                    case "RANGE":
                        if (tokens.Length == 2)
                        {
                            double r;
                            if (double.TryParse(tokens[1], out r)) range = r;
                        }

                        view = $"Range set {range}";

                        break;

                    case "SCAN":

                        MyDetectedEntityInfo info = lidar.Raycast(range);
                        string aim = "";

                        if (info.IsEmpty())
                        {
                            if (remote != null) remote.ClearWaypoints();
                            if (sight == "") view = $"NOT Found at {range}";
                            lidar.CustomData = "";
                            break;
                        }

                        string distance = $"{Math.Round(Vector3D.Distance(lidar.GetPosition(), (info.HitPosition == null ? info.Position : info.HitPosition.Value)), 0)}";

                        view = $"Distance: {distance} m\nType: {info.Type}\nSize: {Math.Round(Vector3D.Distance(info.BoundingBox.Min, info.BoundingBox.Max) / 2, 0)} m";

                        if (remote != null)
                        {
                            remote.ClearWaypoints();
                            remote.AddWaypoint(info.BoundingBox.Min, "Lidar");
                            view += "\nAutopilot: ready";
                        }

                        if (target == "hit" && info.HitPosition.HasValue)
                        {
                            aim = $"GPS:{distance}:{VectorToString(info.HitPosition.Value, 2)}:";
                        }
                        else
                        {
                            aim = $"GPS:{distance}:{VectorToString(info.Position, 2)}:";
                        }

                        if (sight != "" && lidar.CustomData.Contains($":{distance}:")) view = sight;

                        lidar.CustomData = aim;
                        break;
                }
            }

            outSur(view);
        }

        void outSur(string t = "")
        {
            if (surface != null)
            {
                surface.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                surface.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
                surface.WriteText((sight != "" && t == sight ? "" : head) + t);
            }
        }

        string VectorToString(Vector3D vector, int decimals)
        {
            return Math.Round(vector.GetDim(0), decimals) + ":" + Math.Round(vector.GetDim(1), decimals) + ":" + Math.Round(vector.GetDim(2), decimals);
        }

        #endregion
    }
}