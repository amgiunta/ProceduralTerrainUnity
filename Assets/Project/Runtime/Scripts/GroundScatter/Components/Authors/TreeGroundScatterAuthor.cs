using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    public class TreeGroundScatterAuthor : GroundScatterAuthor
    {
        public override Type GetScatterType()
        {
            return typeof(TreeGroundScatter);
        }

        public new TreeGroundScatter GetComponentData()
        {
            TreeGroundScatter scatter = new TreeGroundScatter();
            scatter.maxRenderDistance = this.maxRenderDistance;
            scatter.scatterDensity = this.scatterDensity;
            scatter.minTemperature = this.minTemperature;
            scatter.maxTemperature = this.maxTemperature;
            scatter.minMoisture = this.minMoisture;
            scatter.maxMoisture = this.maxMoisture;
            scatter.minHeight = this.minHeight;
            scatter.maxHeight = this.maxHeight;
            scatter.uniformScale = this.uniformScale;
            scatter.offset = this.offset;
            scatter.jitterFactor = this.jitterFactor;

            return scatter;
        }
    }
}
