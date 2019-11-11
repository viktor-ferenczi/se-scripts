/* Projector Inventory

Takes blueprint component requirements from a projector block.
Takes inventory information from a Central Inventory program.
Displays human readable summary on the missing resources.
Gives "GO" indication when all resources are ready for the build.

How to use:

Build a programmable block.
Copy-paste all code from the CodeEditor region below into the block.
Compile and run the code in the block.

The script will find the first active projector block if no block
name is configured explicitly.

Build three large text panels.

Assign your text panels to the "Projector Panels" group.

Set Content to Text and Images and Font to Monospaced on all panels.

Console blocks (hologram desks) are skipped by name. Any projector
with "console" in its name (case insensitive) is skipped.
Console blocks seem to have the exact same API as projector blocks.
Please let me know if you have a better solution to detect them.

Block definition data, component names and block definition parser
were taken from Projector2LCD by Juggernaut93:
https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551

*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Skeleton;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Profiler;
using VRageMath;
using ContentType = VRage.Game.GUI.TextPanel.ContentType;
using IMyBatteryBlock = Sandbox.ModAPI.Ingame.IMyBatteryBlock;
using IMyBlockGroup = Sandbox.ModAPI.Ingame.IMyBlockGroup;
using IMyCargoContainer = Sandbox.ModAPI.Ingame.IMyCargoContainer;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyGasTank = Sandbox.Game.Entities.Interfaces.IMyGasTank;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace ProjectorInventory
{
    public class Program: SpaceEngineersProgram
    {
        #region CodeEditor

        // Config

        private const string PROJECTOR_NAME = "";
        private const string PANEL_GROUP = "Projector Panels";
        private const string INVENTORY_PANEL_GROUP = "Inventory Panels";
        private const bool ASSUME_LIGHT_ARMOR = true;
        private const int PANEL_ROW_COUNT = 17;
        private const int PANEL_COLUMN_COUNT = 25;
        private const UpdateFrequency UPDATE_FREQUENCY = UpdateFrequency.Update100;

        // Debugging

        enum LogSeverity
        {
            Ok,
            Warning,
            Error,
        }

        private bool DEBUG = false;
        private LogSeverity highestLogLogSeverity = LogSeverity.Ok;
        private readonly StringBuilder log = new StringBuilder();

        private void Log(string formatString, params object[] args)
        {
            log.AppendFormat(formatString + "\n", args);
        }

        private void Debug(string formatString, params object[] args)
        {
            if (DEBUG)
            {
                Log("D: " + formatString, args);
            }
        }

        private void Warning(string formatString, params object[] args)
        {
            Log("W: " + formatString, args);
            IncreaseSeverity(LogSeverity.Warning);
        }

        private void Error(string formatString, params object[] args)
        {
            Log("E: " + formatString, args);
            IncreaseSeverity(LogSeverity.Error);
        }

        private void ClearLog()
        {
            highestLogLogSeverity = LogSeverity.Ok;
            log.Clear();
        }

        private void ShowLog()
        {
            Echo(log.ToString());
            Surface.WriteText(highestLogLogSeverity.ToString());
        }

        private void IncreaseSeverity(LogSeverity severity)
        {
            if (highestLogLogSeverity < severity)
            {
                highestLogLogSeverity = severity;
            }
        }

        private IMyTextSurface Surface
        {
            get
            {
                return Me.GetSurface(0);
            }
        }

        // Tables

        private const string blockDefinitionData = "SteelPlate*ConstructionComponent*MotorComponent*Display*ComputerComponent*GravityGeneratorComponent*ZoneChip*MetalGrid*InteriorPlate*LargeTube*RadioCommunicationComponent*DetectorComponent*SmallTube*Superconductor*BulletproofGlass*PowerCell*ReactorComponent*GirderComponent*SolarCell*MedicalComponent*ThrustComponent*ExplosivesComponent$StoreBlock*StoreBlock=0:30,1:20,2:6,3:4,4:10*AtmBlock=0:20,1:20,2:2,4:10,3:4$SafeZoneBlock*SafeZoneBlock=0:800,1:180,5:10,6:5,7:80,4:120$ContractBlock*ContractBlock=0:30,1:20,2:6,3:4,4:10$VendingMachine*VendingMachine=8:20,1:10,2:4,3:4,4:10$CubeBlock*LargeRailStraight=0:12,1:8,9:4*LargeBlockArmorBlock=0:25*LargeBlockArmorSlope=0:13*LargeBlockArmorCorner=0:4*LargeBlockArmorCornerInv=0:21*LargeRoundArmor_Slope=0:13*LargeRoundArmor_Corner=0:4*LargeRoundArmor_CornerInv=0:21*LargeHeavyBlockArmorBlock=0:150,7:50*LargeHeavyBlockArmorSlope=0:75,7:25*LargeHeavyBlockArmorCorner=0:25,7:10*LargeHeavyBlockArmorCornerInv=0:125,7:50*SmallBlockArmorBlock=0:1*SmallBlockArmorSlope=0:1*SmallBlockArmorCorner=0:1*SmallBlockArmorCornerInv=0:1*SmallHeavyBlockArmorBlock=0:5,7:2*SmallHeavyBlockArmorSlope=0:3,7:1*SmallHeavyBlockArmorCorner=0:2,7:1*SmallHeavyBlockArmorCornerInv=0:4,7:1*LargeHalfArmorBlock=0:12*LargeHeavyHalfArmorBlock=0:75,7:25*LargeHalfSlopeArmorBlock=0:7*LargeHeavyHalfSlopeArmorBlock=0:45,7:15*HalfArmorBlock=0:1*HeavyHalfArmorBlock=0:3,7:1*HalfSlopeArmorBlock=0:1*HeavyHalfSlopeArmorBlock=0:2,7:1*LargeBlockArmorRoundSlope=0:13*LargeBlockArmorRoundCorner=0:4*LargeBlockArmorRoundCornerInv=0:21*LargeHeavyBlockArmorRoundSlope=0:130,7:50*LargeHeavyBlockArmorRoundCorner=0:125,7:40*LargeHeavyBlockArmorRoundCornerInv=0:140,7:50*SmallBlockArmorRoundSlope=0:1*SmallBlockArmorRoundCorner=0:1*SmallBlockArmorRoundCornerInv=0:1*SmallHeavyBlockArmorRoundSlope=0:4,7:1*SmallHeavyBlockArmorRoundCorner=0:4,7:1*SmallHeavyBlockArmorRoundCornerInv=0:5,7:1*LargeBlockArmorSlope2Base=0:19*LargeBlockArmorSlope2Tip=0:7*LargeBlockArmorCorner2Base=0:10*LargeBlockArmorCorner2Tip=0:4*LargeBlockArmorInvCorner2Base=0:22*LargeBlockArmorInvCorner2Tip=0:16*LargeHeavyBlockArmorSlope2Base=0:112,7:45*LargeHeavyBlockArmorSlope2Tip=0:35,7:6*LargeHeavyBlockArmorCorner2Base=0:55,7:15*LargeHeavyBlockArmorCorner2Tip=0:19,7:6*LargeHeavyBlockArmorInvCorner2Base=0:133,7:45*LargeHeavyBlockArmorInvCorner2Tip=0:94,7:25*SmallBlockArmorSlope2Base=0:1*SmallBlockArmorSlope2Tip=0:1*SmallBlockArmorCorner2Base=0:1*SmallBlockArmorCorner2Tip=0:1*SmallBlockArmorInvCorner2Base=0:1*SmallBlockArmorInvCorner2Tip=0:1*SmallHeavyBlockArmorSlope2Base=0:4,7:1*SmallHeavyBlockArmorSlope2Tip=0:2,7:1*SmallHeavyBlockArmorCorner2Base=0:3,7:1*SmallHeavyBlockArmorCorner2Tip=0:2,7:1*SmallHeavyBlockArmorInvCorner2Base=0:5,7:1*SmallHeavyBlockArmorInvCorner2Tip=0:5,7:1*LargeBlockDeskChairless=8:30,1:30*LargeBlockDeskChairlessCorner=8:20,1:20*ArmorCenter=0:140*ArmorCorner=0:120*ArmorInvCorner=0:135*ArmorSide=0:130*SmallArmorCenter=0:5*SmallArmorCorner=0:5*SmallArmorInvCorner=0:5*SmallArmorSide=0:5*Monolith=0:130,13:130*Stereolith=0:130,13:130*DeadAstronaut=0:13,13:13*LargeDeadAstronaut=0:13,13:13*LargeStairs=8:50,1:30*LargeRamp=8:70,1:16*LargeSteelCatwalk=8:27,1:5,12:20*LargeSteelCatwalk2Sides=8:32,1:7,12:25*LargeSteelCatwalkCorner=8:32,1:7,12:25*LargeSteelCatwalkPlate=8:23,1:7,12:17*LargeCoverWall=0:4,1:10*LargeCoverWallHalf=0:2,1:6*LargeBlockInteriorWall=8:25,1:10*LargeInteriorPillar=8:25,1:10,12:4*LargeWindowSquare=8:12,1:8,12:4*LargeWindowEdge=8:16,1:12,12:6*Window1x2Slope=17:16,14:55*Window1x2Inv=17:15,14:40*Window1x2Face=17:15,14:40*Window1x2SideLeft=17:13,14:26*Window1x2SideLeftInv=17:13,14:26*Window1x2SideRight=17:13,14:26*Window1x2SideRightInv=17:13,14:26*Window1x1Slope=17:12,14:35*Window1x1Face=17:11,14:24*Window1x1Side=17:9,14:17*Window1x1SideInv=17:9,14:17*Window1x1Inv=17:11,14:24*Window1x2Flat=17:15,14:50*Window1x2FlatInv=17:15,14:50*Window1x1Flat=17:10,14:25*Window1x1FlatInv=17:10,14:25*Window3x3Flat=17:40,14:196*Window3x3FlatInv=17:40,14:196*Window2x3Flat=17:25,14:140*Window2x3FlatInv=17:25,14:140$DebugSphere1*DebugSphereLarge=0:10,4:20$DebugSphere2*DebugSphereLarge=0:10,4:20$DebugSphere3*DebugSphereLarge=0:10,4:20$MyProgrammableBlock*SmallProgrammableBlock=0:2,1:2,9:2,2:1,3:1,4:2*LargeProgrammableBlock=0:21,1:4,9:2,2:1,3:1,4:2$Projector*LargeProjector=0:21,1:4,9:2,2:1,4:2*SmallProjector=0:2,1:2,9:2,2:1,4:2*LargeBlockConsole=8:20,1:30,4:8,3:10$SensorBlock*SmallBlockSensor=8:5,1:5,4:6,10:4,11:6,0:2*LargeBlockSensor=8:5,1:5,4:6,10:4,11:6,0:2$SoundBlock*SmallBlockSoundBlock=8:4,1:6,4:3*LargeBlockSoundBlock=8:4,1:6,4:3$ButtonPanel*ButtonPanelLarge=8:10,1:20,4:20*ButtonPanelSmall=8:2,1:2,4:1$TimerBlock*TimerBlockLarge=8:6,1:30,4:5*TimerBlockSmall=8:2,1:3,4:1$RadioAntenna*LargeBlockRadioAntenna=0:80,9:40,12:60,1:30,4:8,10:40*SmallBlockRadioAntenna=0:1,12:1,1:2,4:1,10:4$Beacon*LargeBlockBeacon=0:80,1:30,9:20,4:10,10:40*SmallBlockBeacon=0:2,1:1,12:1,4:1,10:4$RemoteControl*LargeBlockRemoteControl=8:10,1:10,2:1,4:15*SmallBlockRemoteControl=8:2,1:1,2:1,4:1$LaserAntenna*LargeBlockLaserAntenna=0:50,1:40,2:16,11:30,10:20,13:100,4:50,14:4*SmallBlockLaserAntenna=0:10,12:10,1:10,2:5,10:5,13:10,4:30,14:2$TerminalBlock*ControlPanel=0:1,1:1,4:1,3:1*SmallControlPanel=0:1,1:1,4:1,3:1$Cockpit*LargeBlockCockpit=8:20,1:20,2:2,4:100,3:10*LargeBlockCockpitSeat=0:30,1:20,2:1,3:8,4:100,14:60*SmallBlockCockpit=0:10,1:10,2:1,3:5,4:15,14:30*DBSmallBlockFighterCockpit=1:20,2:1,0:20,7:10,8:15,3:4,4:20,14:40*CockpitOpen=8:20,1:20,2:2,4:100,3:4*LargeBlockDesk=8:30,1:30*LargeBlockDeskCorner=8:20,1:20*LargeBlockCouch=8:30,1:30*LargeBlockCouchCorner=8:35,1:35*LargeBlockBathroomOpen=8:30,1:30,12:8,2:4,9:2*LargeBlockBathroom=8:30,1:40,12:8,2:4,9:2*LargeBlockToilet=8:10,1:15,12:2,2:2,9:1*SmallBlockCockpitIndustrial=0:10,1:20,7:10,2:2,3:6,4:20,14:60,12:10*LargeBlockCockpitIndustrial=0:20,1:30,7:15,2:2,3:10,4:60,14:80,12:10*PassengerSeatLarge=8:20,1:20*PassengerSeatSmall=8:20,1:20$Gyro*LargeBlockGyro=0:600,1:40,9:4,7:50,2:4,4:5*SmallBlockGyro=0:25,1:5,9:1,2:2,4:3$Kitchen*LargeBlockKitchen=8:20,1:30,9:6,2:6,14:4$CryoChamber*LargeBlockBed=8:30,1:30,12:8,14:10*LargeBlockCryoChamber=8:40,1:20,2:8,3:8,19:3,4:30,14:10*SmallBlockCryoChamber=8:20,1:10,2:4,3:4,19:3,4:15,14:5$CargoContainer*LargeBlockLockerRoom=8:30,1:30,3:4,14:10*LargeBlockLockerRoomCorner=8:25,1:30,3:4,14:10*LargeBlockLockers=8:20,1:20,3:3,4:2*SmallBlockSmallContainer=8:3,1:1,4:1,2:1,3:1*SmallBlockMediumContainer=8:30,1:10,4:4,2:4,3:1*SmallBlockLargeContainer=8:75,1:25,4:6,2:8,3:1*LargeBlockSmallContainer=8:40,1:40,7:4,12:20,2:4,3:1,4:2*LargeBlockLargeContainer=8:360,1:80,7:24,12:60,2:20,3:1,4:8$Planter*LargeBlockPlanters=8:10,1:20,12:8,14:8$Door*(null)=8:10,1:40,12:4,2:2,3:1,4:2,0:8$AirtightHangarDoor*(null)=0:350,1:40,12:40,2:16,4:2$AirtightSlideDoor*LargeBlockSlideDoor=0:20,1:40,12:4,2:4,3:1,4:2,14:15$BatteryBlock*LargeBlockBatteryBlock=0:80,1:30,15:80,4:25*SmallBlockBatteryBlock=0:25,1:5,15:20,4:2*SmallBlockSmallBatteryBlock=0:4,1:2,15:2,4:2$Reactor*SmallBlockSmallGenerator=0:3,1:10,7:2,9:1,16:3,2:1,4:10*SmallBlockLargeGenerator=0:60,1:9,7:9,9:3,16:95,2:5,4:25*LargeBlockSmallGenerator=0:80,1:40,7:4,9:8,16:100,2:6,4:25*LargeBlockLargeGenerator=0:1000,1:70,7:40,9:40,13:100,16:2000,2:20,4:75$HydrogenEngine*LargeHydrogenEngine=0:100,1:70,9:12,12:20,2:12,4:4,15:1*SmallHydrogenEngine=0:30,1:20,9:4,12:6,2:4,4:1,15:1$WindTurbine*LargeBlockWindTurbine=8:40,2:8,1:20,17:24,4:2$SolarPanel*LargeBlockSolarPanel=0:4,1:14,17:12,4:4,18:32,14:4*SmallBlockSolarPanel=0:2,1:2,17:4,4:1,18:8,14:1$GravityGenerator*(null)=0:150,5:6,1:60,9:4,2:6,4:40$GravityGeneratorSphere*(null)=0:150,5:6,1:60,9:4,2:6,4:40$VirtualMass*VirtualMassLarge=0:90,13:20,1:30,4:20,5:9*VirtualMassSmall=0:3,13:2,1:2,4:2,5:1$SpaceBall*SpaceBallLarge=0:225,1:30,4:20,5:3*SpaceBallSmall=0:70,1:10,4:7,5:1$Passage*(null)=8:74,1:20,12:48$Ladder2*(null)=8:10,1:20,12:10$TextPanel*SmallTextPanel=8:1,1:4,4:4,3:3,14:1*SmallLCDPanelWide=8:1,1:8,4:8,3:6,14:2*SmallLCDPanel=8:1,1:4,4:4,3:3,14:2*LargeBlockCorner_LCD_1=1:5,4:3,3:1*LargeBlockCorner_LCD_2=1:5,4:3,3:1*LargeBlockCorner_LCD_Flat_1=1:5,4:3,3:1*LargeBlockCorner_LCD_Flat_2=1:5,4:3,3:1*SmallBlockCorner_LCD_1=1:3,4:2,3:1*SmallBlockCorner_LCD_2=1:3,4:2,3:1*SmallBlockCorner_LCD_Flat_1=1:3,4:2,3:1*SmallBlockCorner_LCD_Flat_2=1:3,4:2,3:1*LargeTextPanel=8:1,1:6,4:6,3:10,14:2*LargeLCDPanel=8:1,1:6,4:6,3:10,14:6*LargeLCDPanelWide=8:2,1:12,4:12,3:20,14:12$ReflectorLight*LargeBlockFrontLight=0:8,9:2,8:20,1:15,14:4*SmallBlockFrontLight=0:1,9:1,8:1,1:1,14:2$InteriorLight*SmallLight=1:2*SmallBlockSmallLight=1:2*LargeBlockLight_1corner=1:3*LargeBlockLight_2corner=1:6*SmallBlockLight_1corner=1:2*SmallBlockLight_2corner=1:4$OxygenTank*OxygenTankSmall=0:16,9:8,12:10,4:8,1:10*(null)=0:80,9:40,12:60,4:8,1:40*LargeHydrogenTank=0:280,9:80,12:60,4:8,1:40*SmallHydrogenTank=0:80,9:40,12:60,4:8,1:40$AirVent*(null)=0:45,1:20,2:10,4:5*SmallAirVent=0:8,1:10,2:2,4:5$Conveyor*SmallBlockConveyor=8:4,1:4,2:1*LargeBlockConveyor=8:20,1:30,12:20,2:6*SmallShipConveyorHub=8:25,1:45,12:25,2:2$Collector*Collector=0:45,1:50,12:12,2:8,3:4,4:10*CollectorSmall=0:35,1:35,12:12,2:8,3:2,4:8$ShipConnector*Connector=0:150,1:40,12:12,2:8,4:20*ConnectorSmall=0:7,1:4,12:2,2:1,4:4*ConnectorMedium=0:21,1:12,12:6,2:6,4:6$ConveyorConnector*ConveyorTube=8:14,1:20,12:12,2:6*ConveyorTubeSmall=8:1,2:1,1:1*ConveyorTubeMedium=8:10,1:20,12:10,2:6*ConveyorFrameMedium=8:5,1:12,12:5,2:2*ConveyorTubeCurved=8:14,1:20,12:12,2:6*ConveyorTubeSmallCurved=8:1,2:1,1:1*ConveyorTubeCurvedMedium=8:7,1:20,12:10,2:6$ConveyorSorter*LargeBlockConveyorSorter=8:50,1:120,12:50,4:20,2:2*MediumBlockConveyorSorter=8:5,1:12,12:5,4:5,2:2*SmallBlockConveyorSorter=8:5,1:12,12:5,4:5,2:2$PistonBase*LargePistonBase=0:15,1:10,9:4,2:4,4:2*SmallPistonBase=0:4,1:4,12:4,2:2,4:1$ExtendedPistonBase*LargePistonBase=0:15,1:10,9:4,2:4,4:2*SmallPistonBase=0:4,1:4,12:4,2:2,4:1$PistonTop*LargePistonTop=0:10,9:8*SmallPistonTop=0:4,9:2$MotorStator*LargeStator=0:15,1:10,9:4,2:4,4:2*SmallStator=0:5,1:5,12:1,2:1,4:1$MotorRotor*LargeRotor=0:30,9:6*SmallRotor=0:12,12:6$MotorAdvancedStator*LargeAdvancedStator=0:15,1:10,9:4,2:4,4:2*SmallAdvancedStator=0:5,1:5,12:1,2:1,4:1$MotorAdvancedRotor*LargeAdvancedRotor=0:30,9:10*SmallAdvancedRotor=0:30,9:10$MedicalRoom*LargeMedicalRoom=8:240,1:80,7:60,12:20,9:5,3:10,4:10,19:15$Refinery*LargeRefinery=0:1200,1:40,9:20,2:16,7:20,4:20*BlastFurnace=0:120,1:20,2:10,4:10$OxygenGenerator*(null)=0:120,1:5,9:2,2:4,4:5*OxygenGeneratorSmall=0:8,1:8,9:2,2:1,4:3$Assembler*LargeAssembler=0:140,1:80,2:20,3:10,7:10,4:160*BasicAssembler=0:80,1:40,2:10,3:4,4:80$SurvivalKit*SurvivalKitLarge=0:30,1:2,19:3,2:4,3:1,4:5*SurvivalKit=0:6,1:2,19:3,2:4,3:1,4:5$OxygenFarm*LargeBlockOxygenFarm=0:40,14:100,9:20,12:10,1:20,4:20$UpgradeModule*LargeProductivityModule=0:100,1:40,12:20,4:60,2:4*LargeEffectivenessModule=0:100,1:50,12:15,13:20,2:4*LargeEnergyModule=0:100,1:40,12:20,15:20,2:4$Thrust*SmallBlockSmallThrust=0:2,1:2,9:1,20:1*SmallBlockLargeThrust=0:5,1:2,9:5,20:12*LargeBlockSmallThrust=0:25,1:60,9:8,20:80*LargeBlockLargeThrust=0:150,1:100,9:40,20:960*LargeBlockLargeHydrogenThrust=0:150,1:180,7:250,9:40*LargeBlockSmallHydrogenThrust=0:25,1:60,7:40,9:8*SmallBlockLargeHydrogenThrust=0:30,1:30,7:22,9:10*SmallBlockSmallHydrogenThrust=0:7,1:15,7:4,9:2*LargeBlockLargeAtmosphericThrust=0:230,1:60,9:50,7:40,2:1100*LargeBlockSmallAtmosphericThrust=0:35,1:50,9:8,7:10,2:110*SmallBlockLargeAtmosphericThrust=0:20,1:30,9:4,7:8,2:90*SmallBlockSmallAtmosphericThrust=0:3,1:22,9:1,7:1,2:18$Drill*SmallBlockDrill=0:32,1:30,9:4,2:1,4:1*LargeBlockDrill=0:300,1:40,9:12,2:5,4:5$ShipGrinder*LargeShipGrinder=0:20,1:30,9:1,2:4,4:2*SmallShipGrinder=0:12,1:17,12:4,2:4,4:2$ShipWelder*LargeShipWelder=0:20,1:30,9:1,2:2,4:2*SmallShipWelder=0:12,1:17,12:6,2:2,4:2$OreDetector*LargeOreDetector=0:50,1:40,2:5,4:25,11:20*SmallBlockOreDetector=0:3,1:2,2:1,4:1,11:1$LandingGear*LargeBlockLandingGear=0:150,1:20,2:6*SmallBlockLandingGear=0:2,1:5,2:1$JumpDrive*LargeJumpDrive=0:60,7:50,5:20,11:20,15:120,13:1000,4:300,1:40$CameraBlock*SmallCameraBlock=0:2,4:3*LargeCameraBlock=0:2,4:3$MergeBlock*LargeShipMergeBlock=0:12,1:15,2:2,9:6,4:2*SmallShipMergeBlock=0:4,1:5,2:1,12:2,4:1$Parachute*LgParachute=0:9,1:25,12:5,2:3,4:2*SmParachute=0:2,1:2,12:1,2:1,4:1$Warhead*LargeWarhead=0:20,17:24,1:12,12:12,4:2,21:6*SmallWarhead=0:4,17:1,1:1,12:2,4:1,21:2$Decoy*LargeDecoy=0:30,1:10,4:10,10:1,9:2*SmallDecoy=0:2,1:1,4:1,10:1,12:2$LargeGatlingTurret*(null)=0:20,1:30,7:15,12:6,2:8,4:10*SmallGatlingTurret=0:10,1:30,7:5,12:6,2:4,4:10$LargeMissileTurret*(null)=0:20,1:40,7:5,9:6,2:16,4:12*SmallMissileTurret=0:10,1:40,7:2,9:2,2:8,4:12$InteriorTurret*LargeInteriorTurret=8:6,1:20,12:1,2:2,4:5,0:4$SmallMissileLauncher*(null)=0:4,1:2,7:1,9:4,2:1,4:1*LargeMissileLauncher=0:35,1:8,7:30,9:25,2:6,4:4$SmallMissileLauncherReload*SmallRocketLauncherReload=12:50,8:50,1:24,9:8,7:10,2:4,4:2,0:8$SmallGatlingGun*(null)=0:4,1:1,7:2,12:6,2:1,4:1$MotorSuspension*Suspension3x3=0:25,1:15,9:6,12:12,2:6*Suspension5x5=0:70,1:40,9:20,12:30,2:20*Suspension1x1=0:25,1:15,9:6,12:12,2:6*SmallSuspension3x3=0:8,1:7,12:2,2:1*SmallSuspension5x5=0:16,1:12,12:4,2:2*SmallSuspension1x1=0:8,1:7,12:2,2:1*Suspension3x3mirrored=0:25,1:15,9:6,12:12,2:6*Suspension5x5mirrored=0:70,1:40,9:20,12:30,2:20*Suspension1x1mirrored=0:25,1:15,9:6,12:12,2:6*SmallSuspension3x3mirrored=0:8,1:7,12:2,2:1*SmallSuspension5x5mirrored=0:16,1:12,12:4,2:2*SmallSuspension1x1mirrored=0:8,1:7,12:2,2:1$Wheel*SmallRealWheel1x1=0:2,1:5,9:1*SmallRealWheel=0:5,1:10,9:1*SmallRealWheel5x5=0:7,1:15,9:2*RealWheel1x1=0:8,1:20,9:4*RealWheel=0:12,1:25,9:6*RealWheel5x5=0:16,1:30,9:8*SmallRealWheel1x1mirrored=0:2,1:5,9:1*SmallRealWheelmirrored=0:5,1:10,9:1*SmallRealWheel5x5mirrored=0:7,1:15,9:2*RealWheel1x1mirrored=0:8,1:20,9:4*RealWheelmirrored=0:12,1:25,9:6*RealWheel5x5mirrored=0:16,1:30,9:8*Wheel1x1=0:8,1:20,9:4*SmallWheel1x1=0:2,1:5,9:1*Wheel3x3=0:12,1:25,9:6*SmallWheel3x3=0:5,1:10,9:1*Wheel5x5=0:16,1:30,9:8*SmallWheel5x5=0:7,1:15,9:2";

        private enum Component
        {
            BulletproofGlass,
            ComputerComponent,
            ConstructionComponent,
            DetectorComponent,
            Display,
            ExplosivesComponent,
            GirderComponent,
            GravityGeneratorComponent,
            InteriorPlate,
            LargeTube,
            MedicalComponent,
            MetalGrid,
            MotorComponent,
            PowerCell,
            RadioCommunicationComponent,
            ReactorComponent,
            SmallTube,
            SolarCell,
            SteelPlate,
            Superconductor,
            ThrustComponent,
            ZoneChip,
        }

        private readonly Dictionary<Component, string> componentNames = new Dictionary<Component, string>()
        {
            [Component.BulletproofGlass] = "Bulletproof Glass",
            [Component.ComputerComponent] = "Computer",
            [Component.ConstructionComponent] = "Construction Component",
            [Component.DetectorComponent] = "Detector Component",
            [Component.Display] = "Display",
            [Component.ExplosivesComponent] = "Explosive",
            [Component.GirderComponent] = "Girder",
            [Component.GravityGeneratorComponent] = "Gravity Gen Component",
            [Component.InteriorPlate] = "Interior Plate",
            [Component.LargeTube] = "Large Steel Tube",
            [Component.MedicalComponent] = "Medical Component",
            [Component.MetalGrid] = "Metal Grid",
            [Component.MotorComponent] = "Motor Component",
            [Component.PowerCell] = "Power Cell",
            [Component.RadioCommunicationComponent] = "Radio-Comm Component",
            [Component.ReactorComponent] = "Reactor Component",
            [Component.SmallTube] = "Small Steel Tube",
            [Component.SolarCell] = "Solar Cell",
            [Component.SteelPlate] = "Steel Plate",
            [Component.Superconductor] = "Superconductor Component",
            [Component.ThrustComponent] = "Thruster Component",
            [Component.ZoneChip] = "Zone Chip",
        };

        // Blocks

        private List<IMyTextPanel> textPanels = new List<IMyTextPanel>();
        private IMyTextPanel rawInventoryPanel = null;
        private IMyProjector projector = null;

        // State

        private enum State
        {
            GetInventory,
            GetBuildRequirements,
            Report,
            Failed,
        }

        private State state = State.GetInventory;

        private String rawInventory = "";
        private Dictionary<string, double> inventory = new Dictionary<string, double>();
        private Dictionary<string, double> requirements = new Dictionary<string, double>();
        private Dictionary<string, Dictionary<string, int>> blockComponents = new Dictionary<string, Dictionary<string, int>>();

        // Parameter parsing (commands)

        private enum Command
        {
            Default,
            Unknown,
        }

        private Command ParseCommand(string argument)
        {
            switch (argument)
            {
                case "":
                    return Command.Default;
                default:
                    return Command.Unknown;
            }
        }

        public Program()
        {
            Initialize();
            Load();
        }

        private void Initialize()
        {
            Surface.ContentType = ContentType.TEXT_AND_IMAGE;
            Surface.FontSize = 4f;

            CreateBlueprints();
            Reset();

            Runtime.UpdateFrequency = UPDATE_FREQUENCY;
        }

        private void CreateBlueprints()
        {
            // get data from blockDefinitionData. splitted[0] is component names
            string[] splitted = blockDefinitionData.Split(new char[] { '$' });
            string[] componentNames = splitted[0].Split(new char[] { '*' });
            for (var i = 0; i < componentNames.Length; i++)
                componentNames[i] = "MyObjectBuilder_BlueprintDefinition/" + componentNames[i];

            //$SmallMissileLauncher*(null)=0:4,2:2,5:1,7:4,8:1,4:1*LargeMissileLauncher=0:35,2:8,5:30,7:25,8:6,4:4$
            char[] asterisk = new char[] { '*' };
            char[] equalsign = new char[] { '=' };
            char[] comma = new char[] { ',' };
            char[] colon = new char[] { ':' };

            for (var i = 1; i < splitted.Length; i++)
            {
                // splitted[1 to n] are type names and all associated subtypes
                // blocks[0] is the type name, blocks[1 to n] are subtypes and component amounts
                string[] blocks = splitted[i].Split(asterisk);
                string typeName = "MyObjectBuilder_" + blocks[0];

                for (var j = 1; j < blocks.Length; j++)
                {
                    string[] compSplit = blocks[j].Split(equalsign);
                    string blockName = typeName + '/' + compSplit[0];

                    // add a new dict for the block
                    try
                    {
                        blockComponents.Add(blockName, new Dictionary<string, int>());
                    }
                    catch (Exception)
                    {
                        Echo("Error adding block: " + blockName);
                    }
                    var components = compSplit[1].Split(comma);
                    foreach (var component in components)
                    {
                        string[] amounts = component.Split(colon);
                        int idx = Convert.ToInt32(amounts[0]);
                        int amount = Convert.ToInt32(amounts[1]);
                        string compName = componentNames[idx];
                        blockComponents[blockName].Add(compName, amount);
                    }
                }
            }
        }

        private void Reset()
        {
            if (!DEBUG)
            {
                ClearLog();
            }

            state = State.GetInventory;
            rawInventoryPanel = null;
            rawInventory = "";
            inventory.Clear();
            requirements.Clear();
            projector = null;

            FindTextPanels();
            FindRawInventoryPanel();
            FindProjector();
        }

        private void FindTextPanels()
        {
            var panels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlockGroupWithName(PANEL_GROUP).GetBlocksOfType<IMyTextPanel>(panels);
            textPanels = panels.OrderBy(panel => panel.CustomName).ToList();

            foreach (var panel in textPanels)
            {
                panel.ContentType = ContentType.TEXT_AND_IMAGE;
                panel.Font = "Monospace";
                panel.FontSize = 1f;
            }

            ClearPanels();
        }

        private void ClearPanels()
        {
            foreach (var panel in textPanels)
            {
                panel.WriteText("");
            }
        }

        private void FindRawInventoryPanel()
        {
            var panels = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlockGroupWithName(INVENTORY_PANEL_GROUP)?.GetBlocksOfType<IMyTextPanel>(panels);
            rawInventoryPanel = panels.FirstOrDefault(panel => panel.CustomName.ToLower().Contains("raw"));
        }

        private void FindProjector()
        {
            var projectors = FindAllProjectors();
            projector = projectors.FirstOrDefault(p => p.CustomName == PROJECTOR_NAME) ??
                        projectors.FirstOrDefault(p => p.IsProjecting);
        }

        private List<IMyProjector> FindAllProjectors()
        {
            var projectors = new List<IMyProjector>();
            GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors);
            return projectors.Where(block => block.IsSameConstructAs(Me) && !block.CustomName.ToLower().Contains("console")).ToList();
        }

        private void Load()
        {
            // Load state from Storage here
        }

        public void Save()
        {
            // Save state to Storage here
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Debug("Main {0} {1}", updateSource, argument);

            switch (updateSource)
            {
                case UpdateType.None:
                case UpdateType.Terminal:
                case UpdateType.Trigger:
                case UpdateType.Antenna:
                case UpdateType.Mod:
                case UpdateType.Script:
                case UpdateType.Once:
                case UpdateType.IGC:
                    ClearLog();

                    try {
                        ProcessCommand(argument);
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                    }

                    break;

                case UpdateType.Update1:
                case UpdateType.Update10:
                case UpdateType.Update100:
                    try {
                        PeriodicProcessing();
                    }
                    catch (Exception e)
                    {
                        Error(e.ToString());
                        StopPeriodicProcessing();
                    }

                    break;
            }

            ShowLog();
        }

        private void StopPeriodicProcessing()
        {
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        private void ProcessCommand(string argument)
        {
            var command = ParseCommand(argument);
            switch (command)
            {
                case Command.Default:
                    Reset();
                    break;

                default:
                    Error("Unknown command");
                    break;
            }
        }

        private void PeriodicProcessing()
        {
            switch (state)
            {
                case State.GetInventory:
                    ClearLog();
                    GetInventory();
                    break;
                case State.GetBuildRequirements:
                    GetBuildRequirements();
                    break;
                case State.Report:
                    ReportDifference();
                    break;
                default:
                    Reset();
                    break;
            }

            if (textPanels.Count > 0 && highestLogLogSeverity == LogSeverity.Error)
            {
                var panel = textPanels[0];
                panel.WriteText(Wrap(log.ToString(), PANEL_COLUMN_COUNT));
            }
        }

        private void GetInventory()
        {
            if (rawInventoryPanel == null)
            {
                Error("Raw inventory panel not found");
                state = State.Failed;
                return;
            }

            if (rawInventoryPanel.GetText() == rawInventory)
            {
                return;
            }

            rawInventory = rawInventoryPanel.GetText();

            inventory.Clear();
            ParseRawInventory();

            state = State.GetBuildRequirements;
        }

        private void ParseRawInventory()
        {
            foreach (var line in rawInventory.Split('\n'))
            {
                var colonPosition = line.IndexOf(':');
                if (colonPosition < 0)
                {
                    if (line.Trim() != "")
                    {
                        Warning("Invalid raw inventory line: " + line);
                    }
                    continue;
                }

                var valuePosition = colonPosition + 1;
                while (valuePosition < line.Length && line[valuePosition] == ' ')
                {
                    valuePosition++;
                }

                if (valuePosition == line.Length)
                {
                    Warning("Invalid raw inventory line: " + line);
                    continue;
                }

                var name = line.Substring(0, colonPosition);
                var text = line.Substring(valuePosition);

                if (text[0] == '"')
                {
                    // Ignore string values for now
                    continue;
                }

                double value;
                if (double.TryParse(text, out value))
                {
                    inventory[name] = value;
                }
                else
                {
                    Warning("Error parsing double value from raw inventory line: " + line);
                }
            }
        }

        private void GetBuildRequirements()
        {
            if (projector == null)
            {
                Error("No active projector found");
                state = State.Failed;
                return;
            }

            if (!projector.IsFunctional)
            {
                Error("Broken projector: {0}", projector.CustomName);
                state = State.Failed;
                return;
            }

            if (!projector.IsWorking)
            {
                Error("Disabled projector: {0}", projector.CustomName);
                state = State.Failed;
                return;
            }

            if (!projector.IsProjecting)
            {
                Error("Projector is not active: {0}", projector.CustomName);
                state = State.Failed;
                return;
            }

            requirements.Clear();
            StoreBuildRequirements();

            if (requirements.Count > 0)
            {
                state = State.Report;
            }
        }

        private void StoreBuildRequirements()
        {
            var blocks = projector.RemainingBlocksPerType;
            var delimiters = new char[] { ',' };
            var remove = new char[] { '[', ']' };
            var totalComponents = new Dictionary<string, int>();
            var largeGrid = true;
            foreach (var item in blocks)
            {
                var blockInfo = item.ToString().Trim(remove).Split(delimiters, StringSplitOptions.None);
                var blockName = blockInfo[0].Replace(" ", "");
                var amount = Convert.ToInt32(blockInfo[1]);

                if (blockName.StartsWith("SmallBlock"))
                {
                    largeGrid = false;
                }

                AddComponentsForPart(blockComponents[blockName], amount);
            }

            var armorType = "MyObjectBuilder_CubeBlock/";
            if (largeGrid)
                if (ASSUME_LIGHT_ARMOR)
                    armorType += "LargeBlockArmorBlock";
                else
                    armorType += "LargeHeavyBlockArmorBlock";
            else
            if (ASSUME_LIGHT_ARMOR)
                armorType += "SmallBlockArmorBlock";
            else
                armorType += "SmallHeavyBlockArmorBlock";

            var armors = projector.RemainingArmorBlocks;
            AddComponentsForPart(blockComponents[armorType], armors);
        }

        public void AddComponentsForPart(Dictionary<string, int> partDescriptor, int amount = 1)
        {
            foreach (KeyValuePair<string, int> component in partDescriptor)
            {
                var key = component.Key.Replace("MyObjectBuilder_BlueprintDefinition/", "");
                var cost = component.Value;
                var totalAmount = cost * amount;
                if (requirements.ContainsKey(key))
                    requirements[key] += totalAmount;
                else
                    requirements[key] = totalAmount;
            }
        }

        private void ReportDifference()
        {
            if (textPanels.Count == 0)
            {
                Error("No text panels found");
                state = State.Failed;
                return;
            }

            var panelRowCount = (int)Math.Floor(PANEL_ROW_COUNT / textPanels[0].FontSize);
            var panelIndex = 0;

            foreach (var page in FormatSummary(panelRowCount))
            {
                if (panelIndex >= textPanels.Count)
                {
                    Warning("Not enough panels to display full information");
                    break;
                }

                textPanels[panelIndex++].WriteText(page);
            }

            while (panelIndex < textPanels.Count)
            {
                textPanels[panelIndex++].WriteText("");
            }

            state = State.GetInventory;
        }

        private IEnumerable<StringBuilder> FormatSummary(int panelRowCount)
        {
            var page = new StringBuilder();

            page.AppendLine(projector.CustomName);

            var missing = new Dictionary<Component, double>();
            foreach (var pair in requirements)
            {
                Component component;
                if (!Component.TryParse(pair.Key, out component))
                {
                    Warning("Blueprint requires {0} of unknown component {1}", pair.Value, pair.Key);
                    continue;
                }

                double stock;
                if (inventory.TryGetValue("component" + pair.Key, out stock))
                {
                    var needed = pair.Value - stock;
                    if (needed > 0)
                    {
                        missing[component] = needed;
                    }
                }
            }

            if (missing.Count == 0)
            {
                if (projector.DetailedInfo == "")
                {
                    page.AppendLine("");
                    page.AppendLine("Ready to print");
                }
                else
                {
                    var pageLineCount = 1;
                    var lines = projector.DetailedInfo.Split('\n').Skip(3);
                    foreach (var line in lines)
                    {
                        if (pageLineCount == PANEL_ROW_COUNT)
                        {
                            yield return page;
                            page.Clear();
                            pageLineCount = 0;
                        }
                        page.AppendLine(line);
                        pageLineCount++;
                    }
                }
                yield return page;
                yield break;
            }

            var title = "Missing components";
            page.AppendLine(title);
            page.AppendLine(new String('-', title.Length));
            var lineCount = 2;

            var maxValue = missing.Values.Max();
            var maxWidth = maxValue >= 10
                ? string.Format("{0:n0}", Math.Round(maxValue)).Length
                : 1;

            var sortedSummary = missing.ToList().OrderBy(pair => componentNames[pair.Key]);
            foreach (KeyValuePair<Component, double> item in sortedSummary)
            {
                var formattedAmount = string.Format("{0:n0}", Math.Round(item.Value));
                var line = formattedAmount.PadLeft(maxWidth) + " " + componentNames[item.Key];

                page.AppendLine(line);
                lineCount++;

                if (lineCount >= panelRowCount)
                {
                    yield return page;
                    page = new StringBuilder();
                    lineCount = 0;
                }
            }

            if (page.Length > 0)
            {
                yield return page;
            }
        }

        // Utility functions

        private static string Wrap(string text, int width)
        {
            var output = new StringBuilder();
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.TrimEnd();
                int position = 0;
                while (trimmed.Length > position + width)
                {
                    output.AppendLine(trimmed.Substring(position, width));
                    position += width;
                }
                if (position < trimmed.Length)
                {
                    output.AppendLine(trimmed.Substring(position));
                }
            }
            return output.ToString();
        }

        #endregion
    }
}