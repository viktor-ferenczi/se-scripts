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
using System.Security.Permissions;
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


namespace SignedDistanceFunction
{
    // ReSharper disable once UnusedType.Global
    class Program : MyGridProgram
    {
        // TODO: Replace these example instance variables according to your SDF logic
        Vector3 center;
        float radiusSquared;

        /*
        TODO: Implement any initialization code in the `PrepareGeneration` method.
        
        The `Vector3I boundingBoxSize` parameter provides the size of the bounding box.
        
        This size is independent of the grid size (1 means a single block) and the
        projection offset and rotation. 
        
        It is called only once for each generation.
        */
        void PrepareGeneration(Vector3I boundingBoxSize)
        {
            // Example: Fits a sphere in the bounding box
            center = 0.5f * boundingBoxSize;
            var radius = center.AbsMin();
            radiusSquared = radius * radius;
        }

        /*
        TODO: Implement the calculation in the `CalculateDistance` method.
        
        The `Vector3 point` parameter defines the spatial position to calculate
        the SDF for within the bounding box's frame of reference. The point is
        always inside the bounding box or on its surface, never outside it.
        
        Currently, this method is called for the block centers. Later it may
        be called for key points inside the blocks to select the best fitting
        one from the allowed set of blocks in that position.
         
        Return the signed distance from the shape's surface to the point.
        Points inside the shape have a negative distance, the surface of the
        shape has a zero distance and outside points have positive distance.
        
        Make this method fast, it may be called millions of times.
        */
        float CalculateDistance(Vector3 point)
        {
            // Example: Solid sphere
            return (point - center).LengthSquared() - radiusSquared;
        }

        // DO NOT CHANGE ANYTHING BELOW THIS LINE
        // Scaffolding to deliver SDF layers as bitmaps
        // from the SDF in-game script to the WFC mod
        
        // SDF program version, determines the protocol
        // to use between the WFC mod and this program 
        const string Version = "SDF1";
        const int MaxSize = 512;
        
        // MyTerminalBlock.SetCustomData_Internal method,
        // any longer value gets truncated by the game
        const int MaxCustomDataLength = 64000;

        // Variables used for the serialization of the SDF layer bitmaps
        StringBuilder buffer;
        Vector3I size;
        int strideY;

        // The Main is invoked whenever the program is run
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once UnusedParameter.Local
        void Main(string argument, UpdateType updateSource)
        {
            if (argument == null)
                return;
            
            var parts = argument.Split(' ');
            switch (parts[0])
            {
                case "version":
                    if (parts.Length != 1)
                        break;
                    
                    Me.CustomData = Version;
                    return;

                case "box":
                    if (parts.Length != 4 ||
                        !int.TryParse(parts[1], out size.X) ||
                        !int.TryParse(parts[2], out size.Y) ||
                        !int.TryParse(parts[3], out size.Z) ||
                        size.AbsMin() < 1 || size.AbsMax() > MaxSize)
                        break;
                    
                    PrepareGeneration(size);
                    
                    strideY = (size.X + 7) >> 3;
                    var capacity = strideY * size.Y;
                    if (capacity > MaxCustomDataLength)
                        break;
                        
                    buffer = new StringBuilder(capacity);
                    return;

                case "layer":
                    int z;
                    if (parts.Length != 2 ||
                        !int.TryParse(parts[1], out z) ||
                        z < 0 || z >= size.Z)
                        break;
                    
                    var data = CalculateLayer(z);
                    if (data.Length > MaxCustomDataLength)
                        break;
                    
                    Me.CustomData = data;
                    return;
                
                case "cleanup":
                    if (parts.Length != 1)
                        break;

                    buffer = null;
                    size = Vector3I.Zero;
                    strideY = 0;
                    return;
            }

            Me.CustomData = "";
            Echo($"Invalid arguments: {argument}");
        }

        string CalculateLayer(int z)
        {
            var data = buffer;
            data.Clear();

            var pos = new Vector3I(0, 0, z);
            var blockCenter = Vector3.Half;
            
            var xs = size.X;
            var ys = size.Y;

            var v = 0u;
            var b = 1u;
            for (var y = 0; y < ys; y++)
            {
                pos.Y = y;
                for (var x = 0; x < xs; x++)
                {
                    pos.X = x;

                    if (CalculateDistance(pos + blockCenter) <= 0)
                        v |= b;

                    b <<= 1;
                    if (b == 256u)
                    {
                        data.Append((char)v);
                        v = 0u;
                        b = 1u;
                    }
                }

                if (b != 1u)
                {
                    data.Append((char)v);
                }
            }

            var result = data.ToString();
            data.Clear();
            return result;
        }
    }
}