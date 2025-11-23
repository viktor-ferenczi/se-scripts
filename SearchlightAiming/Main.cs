using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;

namespace SearchlightAiming
{
    class Program : MyGridProgram
    {
        // Configuration constants
        private const string SEARCHLIGHT_GROUP_SUBSTRING = "[Searchlights]";
        private const string TURRET_CONTROLLER_GROUP_SUBSTRING = "[Turret Controllers]";
        private const int UPDATE_PERIOD = 10; // Simulation steps (1 or 10, default 10 for performance)

        // Block references
        private List<IMySearchlight> searchlights = new List<IMySearchlight>();
        private IMyTurretControlBlock turretController = null;

        // Runtime state
        private int updateCounter = 0;
        private bool initialized = false;
        private string errorMessage = "";

        public Program()
        {
            // Initialize the script
            Initialize();
            
            // Set up the update frequency based on UPDATE_PERIOD
            if (UPDATE_PERIOD >= 10)
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update10;
            }
            else
            {
                Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // Handle initialization on first run or if forced
            if (!initialized || argument == "reset")
            {
                Initialize();
                return;
            }

            // Only process updates, not commands
            UpdateType requiredUpdateType = (UPDATE_PERIOD >= 10) ? UpdateType.Update10 : UpdateType.Update1;
            if ((updateSource & requiredUpdateType) == 0)
                return;

            // Increment update counter
            updateCounter++;

            // Check if it's time to update (based on UPDATE_PERIOD)
            if (updateCounter >= UPDATE_PERIOD)
            {
                updateCounter = 0;
                UpdateSearchlights();
            }
        }

        private void Initialize()
        {
            initialized = false;
            errorMessage = "";
            searchlights.Clear();
            turretController = null;

            try
            {
                // Find searchlight group
                IMyBlockGroup searchlightGroup = FindGroupBySubstring(SEARCHLIGHT_GROUP_SUBSTRING);
                if (searchlightGroup == null)
                {
                    errorMessage = $"Error: No group found containing '{SEARCHLIGHT_GROUP_SUBSTRING}'";
                    Echo(errorMessage);
                    return;
                }

                // Get searchlights from group
                searchlightGroup.GetBlocksOfType(searchlights);
                if (searchlights.Count == 0)
                {
                    errorMessage = $"Error: No searchlights found in group containing '{SEARCHLIGHT_GROUP_SUBSTRING}'";
                    Echo(errorMessage);
                    return;
                }

                // Find turret controller group
                IMyBlockGroup turretControllerGroup = FindGroupBySubstring(TURRET_CONTROLLER_GROUP_SUBSTRING);
                if (turretControllerGroup == null)
                {
                    errorMessage = $"Error: No group found containing '{TURRET_CONTROLLER_GROUP_SUBSTRING}'";
                    Echo(errorMessage);
                    return;
                }

                // Get turret controller from group (only the first one)
                List<IMyTurretControlBlock> turretControllers = new List<IMyTurretControlBlock>();
                turretControllerGroup.GetBlocksOfType(turretControllers);
                if (turretControllers.Count == 0)
                {
                    errorMessage = $"Error: No turret controller found in group containing '{TURRET_CONTROLLER_GROUP_SUBSTRING}'";
                    Echo(errorMessage);
                    return;
                }

                turretController = turretControllers[0];

                // Success!
                initialized = true;
                Echo($"Initialized: {searchlights.Count} searchlights, 1 turret controller");
                Echo($"Update period: {UPDATE_PERIOD} simulation steps");
            }
            catch (Exception ex)
            {
                errorMessage = $"Initialization error: {ex.Message}";
                Echo(errorMessage);
            }
        }

        private IMyBlockGroup FindGroupBySubstring(string substring)
        {
            // Get all block groups and search for the substring (case-insensitive)
            List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(allGroups);
            
            return allGroups.FirstOrDefault(group => 
                group.Name.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void UpdateSearchlights()
        {
            if (!initialized || turretController == null || searchlights.Count == 0)
                return;

            try
            {
                // Get the turret's shooting direction in world space
                Vector3 worldShootDirection = turretController.GetShootDirection();
                
                // Apply the direction to all searchlights
                foreach (IMySearchlight searchlight in searchlights)
                {
                    if (searchlight.IsFunctional)
                    {
                        // Get the searchlight's world matrix
                        MatrixD searchlightWorldMatrix = searchlight.WorldMatrix;
                        
                        // Convert world direction to searchlight's local coordinate system
                        // We need to transform the world direction to be relative to the searchlight's orientation
                        MatrixD searchlightInverseMatrix = MatrixD.Invert(searchlightWorldMatrix);
                        Vector3 localDirection = Vector3.TransformNormal(worldShootDirection, searchlightInverseMatrix);
                        
                        // Convert direction to azimuth and elevation angles for this searchlight
                        float azimuth, elevation;
                        Vector3.GetAzimuthAndElevation(localDirection, out azimuth, out elevation);
                        
                        searchlight.SetManualAzimuthAndElevation(azimuth, elevation);
                    }
                }
            }
            catch (Exception ex)
            {
                Echo($"Update error: {ex.Message}");
            }
        }
    }
}
