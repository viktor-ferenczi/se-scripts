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


namespace DebugMergedScript
{
    class Program : MyGridProgram
    {
        #region MergedScript

const string SEARCHLIGHT_GROUP_SUBSTRING = "[Searchlights]";

const string TURRET_GROUP_SUBSTRING = "[Turret Controllers]";

const int UPDATE_FREQUENCY_TICKS = 10;


IMyTurretControlBlock turretController;

List<IMySearchlight> searchlights = new List<IMySearchlight>();


public Program()
{
    Initialize();
}


void Initialize()
{
    searchlights.Clear();
    turretController = null;

    var turretGroup = FindGroup(TURRET_GROUP_SUBSTRING);
    if (turretGroup == null)
    {
        Echo($"ERROR: No group found containing '{TURRET_GROUP_SUBSTRING}'");
        return;
    }

    var turretControllers = new List<IMyTurretControlBlock>();
    turretGroup.GetBlocksOfType(turretControllers);
    if (turretControllers.Count == 0)
    {
        Echo($"ERROR: No Turret Controller found in group '{turretGroup.Name}'");
        return;
    }

    turretController = turretControllers[0];

    var searchlightGroup = FindGroup(SEARCHLIGHT_GROUP_SUBSTRING);
    if (searchlightGroup == null)
    {
        Echo($"ERROR: No group found containing '{SEARCHLIGHT_GROUP_SUBSTRING}'");
        turretController = null;
        return;
    }

    searchlightGroup.GetBlocksOfType(searchlights);
    if (searchlights.Count == 0)
    {
        Echo($"ERROR: No searchlights found in group '{searchlightGroup.Name}'");
        turretController = null;
        return;
    }

    Runtime.UpdateFrequency = UPDATE_FREQUENCY_TICKS == 1 ? UpdateFrequency.Update1 : UpdateFrequency.Update10;
    Echo($"Initialized successfully");
    Echo($"Turret Controller: {turretController.CustomName}");
    Echo($"Searchlights: {searchlights.Count}");
}


IMyBlockGroup FindGroup(string substring)
{
    var groups = new List<IMyBlockGroup>();
    GridTerminalSystem.GetBlockGroups(groups);

    foreach (var group in groups)
    {
        if (group.Name.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return group;
        }
    }

    return null;
}


public void Main(string argument, UpdateType updateSource)
{
    if (turretController == null || searchlights.Count == 0)
    {
        Echo("Script is not functional. Check errors above.");
        return;
    }

    var shootDirection = turretController.GetShootDirection();

    foreach (var searchlight in searchlights)
    {
        var searchlightMatrix = searchlight.WorldMatrix;
        var localDirection = Vector3D.TransformNormal(shootDirection, MatrixD.Transpose(searchlightMatrix));

        var azimuth = (Math.Atan2(localDirection.X, localDirection.Z) + Math.PI) % (2.0 * Math.PI);
        var elevation = Math.Asin(Math.Max(0.0, Math.Min(1.0, localDirection.Y)));

        searchlight.SetManualAzimuthAndElevation((float)azimuth, (float)elevation);
    }

    Echo($"Aiming {searchlights.Count} searchlights");
}


        #endregion
    }
}