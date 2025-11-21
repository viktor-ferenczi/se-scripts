Write a PB script to let Searchlight blocks follow the angle of a custom turret controlled by a Turret Controller.

The searchlights must be in a group with `"[Searchlights]"` in its name. 

The Turret Controller must be in a group with `"[Turret Controllers]"` in its name.
The script must handle only the first Turret Controller bock in that group.

Do case-insensitive sub-string search to find the above groups.
Pick the first matching group if there are multiple matching ones.

Make the above sub-strings configurable at the top of the script in a constant.

Schedule the following code to run periodically:
- Read the effective (shooting) direction of the turret controller.
- Point the search light into the same direction.

Use the relevant APIs:
- Use `IMyTurretControlBlock.GetShootDirection` to retrieve the effective (shooting) direction of the custom turret.
- Use `IMySearchlight.SetManualAzimuthAndElevation` to explicitly set the direction of each searchlight.

The period can be either 1 simulation step (default) or 10 simulation steps. 
Make this configurable at the top of the script with a constant. 
Default to 10 simulation steps for less performance impact.
