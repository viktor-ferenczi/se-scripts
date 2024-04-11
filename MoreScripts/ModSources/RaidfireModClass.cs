using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace OmniTanks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_GasTank), false, "OmniFlow5x5", "OmniFlow7x7")]
    public class TankPlacementController : MyGameLogicComponent
    {
        private bool IsEnabled { get; set; }
        private Action<IMyEntity> entityAddHandler;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            IsEnabled = true; // Enable the script
            SubscribeToEvents(); // Subscribe to relevant events
        }

        public override void OnAddedToContainer()
        {
            base.OnAddedToContainer();
            SubscribeToEvents(); // Subscribe to relevant events

        }

        private void SubscribeToEvents()
        {
            if (IsEnabled)
            {
                // Subscribe to the grid changed event
                entityAddHandler = OnEntityAdded; // Assign the event handler to the reference
                MyAPIGateway.Entities.OnEntityAdd += entityAddHandler;
            }
        }

        private void UnsubscribeFromEvents()
        {
            MyAPIGateway.Entities.OnEntityAdd -= entityAddHandler; // Unsubscribe from the event
        }

        public override void Close()
        {
            base.Close();
            UnsubscribeFromEvents(); // Unsubscribe from events when the component is removed
        }

        private void OnEntityAdded(IMyEntity entity)
        {
            // Check if the entity is a gas tank
            var tank = entity as IMyGasTank;
            if (tank != null)
            {
                // Check if the required thrusters are present on the rear
                if (!AreRequiredThrustersMounted(tank))
                {
                    // Disable or turn off the tank
                    DisableTank(tank);
                }
            }
        }

        private bool AreRequiredThrustersMounted(IMyGasTank tank)
        {
            // Get the forward direction of the tank
            var forwardDirection = tank.Orientation.Forward;

            // Calculate the rear direction based on the forward direction
            var rearDirection = Base6Directions.GetFlippedDirection(forwardDirection);
			var readDirectionRelativeLocation = Base6Directions.GetIntVector(rearDirection);
			
            // Get all the blocks adjacent to the tank
            var adjacentBlocks = new List<IMySlimBlock>();
            tank.CubeGrid.GetBlocks(adjacentBlocks);

            // Iterate over the adjacent blocks and check if any of them are on the rear side
            foreach (var block in adjacentBlocks)
            {
				var blockRelativeLocation = block.Position - tank.Position;
				var relativeDirection = blockRelativeLocation * readDirectionRelativeLocation;
				
                // Check if the block is on the rear side of the tank
                if (relativeDirection.X > 0 || relativeDirection.Y > 0 || relativeDirection.Z > 0)
                {
                    // Check if the block is a thruster with one of the specified subtype IDs
                    var thruster = block.FatBlock as IMyThrust;
                    if (thruster != null && IsSpecifiedThruster(thruster))
                    {
                        ShowMessage("Specified Thruster: One or more specified thrusters detected.");
                        return true; // Thruster is mounted on the rear
                    }
                }
            }
            return false; // None of the required thrusters are mounted on the rear
        }

        private bool IsSpecifiedThruster(IMyThrust thruster)
        {
            switch (thruster.BlockDefinition.SubtypeId)
            {
                case "ARYLNX_Epstein_Drive":
                case "ARYLNX_MUNR_Epstein_Drive":
                case "ARYLNX_PNDR_Epstein_Drive":
                case "ARYLNX_QUADRA_Epstein_Drive":
                case "ARYLNX_RAIDER_Epstein_Drive":
                case "ARYLNX_ROCI_Epstein_Drive":
                case "ARYLNX_Leo_Epstein_Drive":
                case "ARYLYNX_SILVERSMITH_Epstein_DRIVE":
                case "ARYLNX_DRUMMER_Epstein_Drive":
                    return true;
                default:
                    return false;
            }
        }

        private void DisableTank(IMyGasTank tank)
        {
            // Implement logic to disable or turn off the tank
            // You can set the tank's Enabled property to false or perform any other necessary actions
            tank.Enabled = false;

            ShowMessage("Tank Disabled: Required thrusters are not mounted on the rear.");
        }

        private void ShowMessage(string message)
        {
            MyAPIGateway.Utilities.SendMessage(message);
        }
    }
}
