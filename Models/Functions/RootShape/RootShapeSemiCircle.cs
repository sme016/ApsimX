﻿using System;
using System.Collections.Generic;
using Models.Core;
using Models.Interfaces;
using APSIM.Shared.Utilities;
using Models.PMF.Organs;
using APSIM.Shared.Documentation;

namespace Models.Functions.RootShape
{
    /// <summary>
    /// This model calculates the proportion of each soil layer occupided by roots.
    /// </summary>
    [Serializable]
    [ValidParent(ParentType = typeof(Root))]
    public class RootShapeSemiCircle : Model, IRootShape
    {
        /// <summary>Calculates the root area for a layer of soil</summary>
        public void CalcRootProportionInLayers(ZoneState zone)
        {
            var physical = zone.Soil.FindChild<Soils.IPhysical>();

            zone.RootArea = 0;
            for (int layer = 0; layer < physical.Thickness.Length; layer++)
            {
                double prop;
                double top = layer == 0 ? 0 : MathUtilities.Sum(physical.Thickness, 0, layer - 1);
                double bottom = top + physical.Thickness[layer];
                double rootArea;

                if (zone.Depth < top)
                {
                    prop = 0;
                } 
                else
                {
                    rootArea = CalcRootAreaSemiCircleMaize(zone, top, bottom, zone.RightDist);    // Right side
                    rootArea += CalcRootAreaSemiCircleMaize(zone, top, bottom, zone.LeftDist);    // Left Side
                    zone.RootArea += rootArea / 1e6;

                    double soilArea = (zone.RightDist + zone.LeftDist) * (bottom - top);
                    prop = Math.Max(0.0, MathUtilities.Divide(rootArea, soilArea, 0.0));
                }
                zone.RootProportions[layer] = prop;
            }
        }

        /// <summary>Document the model.</summary>
        public override IEnumerable<ITag> Document()
        {
            // Write description of this class from summary and remarks XML documentation.
            foreach (var tag in GetModelDescription())
                yield return tag;

            foreach (var tag in DocumentChildren<IModel>())
                yield return tag;
        }

        private double CalcRootAreaSemiCircleMaize(ZoneState zone, double top, double bottom, double hDist)
        {
            if (zone.RootFront == 0.0)
            {
                return 0.0;
            }

            // get the area occupied by roots in a semi-circular section between top and bottom
            double SDepth, areaLayer;

            // intersection of roots and Section
            if (zone.RootFront <= hDist)
                SDepth = 0.0;
            else
                SDepth = Math.Sqrt(MathUtilities.Bound(Math.Pow(zone.RootFront, 2) - Math.Pow(hDist, 2), 0, 1000000));

            // Rectangle - SDepth past bottom of this area
            if (SDepth >= bottom)
                areaLayer = (bottom - top) * hDist;
            else               // roots Past top
            {
                double Theta = 2 * Math.Acos(MathUtilities.Divide(Math.Max(top, SDepth), zone.RootFront, 0));
                double topArea = (Math.Pow(zone.RootFront, 2) / 2.0 * (Theta - Math.Sin(Theta))) / 2.0;

                // bottom down
                double bottomArea = 0;
                if (zone.RootFront > bottom)
                {
                    Theta = 2 * Math.Acos(bottom / zone.RootFront);
                    bottomArea = (Math.Pow(zone.RootFront, 2) / 2.0 * (Theta - Math.Sin(Theta))) / 2.0;
                }
                // rectangle
                if (SDepth > top) topArea += (SDepth - top) * hDist;
                areaLayer = topArea - bottomArea;
            }
            return areaLayer;
        }
    }
}
