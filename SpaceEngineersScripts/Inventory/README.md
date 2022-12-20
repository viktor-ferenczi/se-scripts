# Inventory

Periodically builds a summary on cargo and power status.
Displays human readable summary on text panels.
Produces machine readable information to be used by other programs.
Optionally sorts items into specific containers.

## How to set up inventory display

Build a programmable block.
Copy-paste all code from the CodeEditor region below into the block.
Compile and run the code in the block.

Build large text panels with the following words in their name (case insensitive):
- Resource
- Ore
- Ingot
- Component
- Ammo
- Other
- Status
- Log
- Raw

Assign your text panels to the "Inventory Panels" group.

Run the programmable block again after making changes to the group.

Text panel content mode, font and character size will be overridden
by the script in order to nicely fit the contents. Colors are free
to configure to match your environment.

You may need two Component panels and two Other panels to fit all items.
All text panels inside each resource type must have the same size.
Panels of the same type are concatenated in ascending name order.

The Status panel displays top level summaries.

The Error panel displays warnings and errors.

The Raw panel displays raw inventory information in a YAML like format.
It can be used by compatible programs to quickly acquire inventory
information without walking on all the blocks again.

## How to set up inventory sorting

Sorting uses the same programmable block as the display functionality,
do not add a separate one. You can use only sorting by setting up the
programmable block like above, but without the displays configured.

Include the following words in the name of the cargo blocks you expect
the items to be placed into (case insensitive):
- Ore
- Ingot
- Component
- Weapon
- Ammo
- Tool
- Food

Make sure your have the following words in the name of your gas tanks or
generator in order to automatically pull bottles for refilling:
- Hydrogen or H2
- Oxygen or O2
- Generator

Sorting is enabled by assigning your target containers (where you expect
the sorted items to be placed) to the "Sorted Containers" group.

Run the programmable block again after making changes to the group.

For safety reasons sorting functionality will NOT pull
- ammo out of any turrets or interior guns
- ore out of refineries
- ice out of gas generators
- ingots out of assemblers
- ingots (Uranium) out of reactors
- components out of enabled welders
- zone chips out from safe zone generators

Sorting collects items from the programmable block's own grid.
This is to prevent pulling out cargo from all docked ships.

## How to set up restocking of components

It is useful to restock the components automatically if the inventory
drops below a certain level, so welding can continue uninterrupted
without manually managing the assembler queues.

Restocking of components can be enabled by assigning your designated
assemblers to the "Restock Assemblers" group. Only the components seen
in the inventory are restocked. Build one of any new component to
trigger restocking.

The minimum stock amount is set in the configuration in `CustomData`.

In order to disable restocking of a specific component set the target
amount to zero in the configuration.

Restocking is supported only for the basic vanilla components, since
they are relatively inexpensive and most commonly used. Some expensive
components are set to zero restock amount, disabling the automatic
stock refilling for them.

## Configuration

On first run the PB fill fill the `CustomData` with a template.
All options are disabled (#) and showing their defaults. The PB
needs to be re-run in order to pick up modified configuration.
Clear the `CustomData` and re-run the PB to recover the original
configuration template.

## Remarks

Display updates and sorting will be slower if you have more blocks.
It is normal and the script is designed this way to prevent the PB
from burning down on multiplayer servers.

You can adjust the batch size options to change the scanning speed,
higher value will result in faster processing at the cost of more 
computation  and higher risk of your PB ending up in smoke and fire. 
If your  PB burns down, then try to decrease BATCH_SIZE. It may need 
to be tuned for the server you are playing on.

Mod compatibility is entirely untested. Works well with vanialla Space Engineers
in Experimental mode and scripts enabled as of 2019-11-07. Also works
on Alehouse PvP Two multiplayer server.

## Credits

Resource name tables were taken from Projector2LCD by Juggernaut93:
https://steamcommunity.com/sharedfiles/filedetails/?id=1500259551