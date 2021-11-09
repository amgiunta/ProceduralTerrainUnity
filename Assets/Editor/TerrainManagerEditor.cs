using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VoxelTerrain;

[CustomEditor(typeof(TerrainManager))]
public class TerrainManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        TerrainManager terrainManager = (TerrainManager)target;
        if (GUILayout.Button("Regenerate"))
        {
            terrainManager.Generate();
        }
    }
}
