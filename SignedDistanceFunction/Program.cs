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

        The `Vector3I boundingBox` parameter provides the size of the bounding box.

        This size is independent of the grid size and the projection offset and rotation. 

        This method is called only once before each generation.
        */
        void PrepareGeneration(Vector3I boundingBox)
        {
            // Example: Fits a sphere in the bounding box
            center = 0.5f * boundingBox;
            var radius = center.AbsMin();
            radiusSquared = radius * radius;
        }

        /*
        TODO: Implement the point-wise calculation in the `CalculateDistance` method.

        The `Vector3 point` parameter defines the spatial position to calculate
        the SDF for within the bounding box's frame of reference. The point is
        always inside the bounding box or on its surface, never outside it.

        Currently, this method is called for the block centers. Later it may
        be called for key points inside the blocks to select the best fitting
        one from the allowed set of blocks in that position.

        The return value must be the signed distance of the point from the shape:
        - Negative: Inside
        - Zero: On the surface
        - Positive: Outside

        It does not have to be an exact distance, but the value returned may be
        used later to define a notion of "thickness". Currently only the sign of
        the value is used, considering positive return values as outside,
        otherwise inside.

        Make this method fast, it may be called millions of times.
        */
        float CalculateDistance(Vector3 point)
        {
            // Example: Solid sphere
            return (point - center).LengthSquared() - radiusSquared;
        }

        // !!!!!!!! DO NOT CHANGE ANYTHING BELOW THIS LINE !!!!!!!!
        // Scaffolding to deliver SDF layers as bitmaps to the Shape Designer mod

        // SDF program version, determines the communication protocol
        const string ProtocolVersion = "SDF1";
        const int MaxSize = 512;

        // MyTerminalBlock.SetCustomData_Internal method,
        // any longer value gets truncated by the game
        const int MaxCustomDataLength = 64000;

        // Variables used for the serialization of the SDF layer bitmaps
        Vector3I size;
        int strideY;
        int layerSize;
        StringBuilder buffer;

        // The Main is invoked whenever the program is run
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once UnusedParameter.Local
        void Main(string argument, UpdateType updateSource)
        {
            if (string.IsNullOrEmpty(argument))
            {
                Echo("This script implements a signed distance function (SDF) for use with the Shape Designer mod.");
                Echo($"Protocol version: {ProtocolVersion}");
                Echo($"Maximum box size: {MaxSize}");
                return;
            }

            Me.CustomData = "";

            var parts = argument.Split(' ');
            switch (parts[0])
            {
                case "version":
                    if (parts.Length != 1)
                        break;

                    Me.CustomData = ProtocolVersion;
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
                    layerSize = strideY * size.Y;
                    if (layerSize > MaxCustomDataLength)
                    {
                        Echo($"Layer is too large {layerSize}, maximum is {MaxCustomDataLength}");
                        return;
                    }

                    buffer = new StringBuilder(layerSize);
                    return;

                case "layer":
                    Vector3 offset;
                    if (parts.Length != 4 ||
                        !float.TryParse(parts[1], out offset.X) ||
                        !float.TryParse(parts[2], out offset.Y) ||
                        !float.TryParse(parts[3], out offset.Z) ||
                        offset.AbsMin() < 0 ||
                        offset.X > size.X ||
                        offset.Y > size.Y ||
                        offset.Z > size.Z)
                        break;

                    var data = CalculateLayer(offset);
                    if (data.Length != layerSize)
                    {
                        Echo($"Invalid layer size {data.Length}, expected {layerSize}");
                        return;
                    }

                    Me.CustomData = data;
                    return;

                case "cleanup":
                    if (parts.Length != 1)
                        break;

                    size = Vector3I.Zero;
                    strideY = 0;
                    layerSize = 0;
                    buffer = null;
                    return;
            }

            Echo($"Invalid arguments: {argument}");
        }

        string CalculateLayer(Vector3 offset)
        {
            var buf = buffer;

            var xs = size.X;
            var ys = size.Y;

            var position = Vector3I.Zero;
            for (var y = 0; y < ys; y++)
            {
                position.Y = y;

                var v = 0;
                var b = 1;
                for (var x = 0; x < xs; x++)
                {
                    position.X = x;

                    if (CalculateDistance(position + offset) <= 0)
                        v |= b;

                    b <<= 1;
                    if (b == 256)
                    {
                        buf.Append((char)v);
                        v = 0;
                        b = 1;
                    }
                }

                if (b != 1)
                {
                    buf.Append((char)v);
                }
            }

            var layer = buf.ToString();
            buf.Clear();

            return layer;
        }
    }
}