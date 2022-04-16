using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;

namespace VoxelTerrain
{
    [CustomEditor(typeof(TerrainSettings))]
    [CanEditMultipleObjects]
    public class TerrainSettingsEditor : Editor
    {
        SerializedObject so;

        SerializedProperty propSeed;

        SerializedProperty propWorldName;
        SerializedProperty propGrid;
        SerializedProperty propBiomes;
        SerializedProperty propRenderDistance;

        SerializedProperty propMinTemperature;
        SerializedProperty propMaxTemperature;
        SerializedProperty propTemperatureLancunarity;
        SerializedProperty propTemperaturePersistance;
        SerializedProperty propTemperatureOctaves;
        SerializedProperty propTemperatureScale;
        SerializedProperty propTemperatureOffset;

        SerializedProperty propVoxelVertecies;
        SerializedProperty propVoxelTris;

        SerializedProperty propMinMoisture;
        SerializedProperty propMaxMoisture;
        SerializedProperty propMoistureLancunarity;
        SerializedProperty propMoisturePersistance;
        SerializedProperty propMoistureOctaves;
        SerializedProperty propMoistureScale;
        SerializedProperty propMoistureOffset;

        int previewChunkSize = 64;

        Texture2D previewTexture;

        private void OnEnable()
        {
            so = serializedObject;

            propSeed = so.FindProperty("seed");

            propWorldName = so.FindProperty("worldName");
            propGrid = so.FindProperty("grid");
            propBiomes = so.FindProperty("biomes");
            propRenderDistance = so.FindProperty("renderDistance");

            propMinTemperature = so.FindProperty("minTemperature");
            propMaxTemperature = so.FindProperty("maxTemperature");
            propTemperatureLancunarity = so.FindProperty("temperatureLancunarity");
            propTemperaturePersistance = so.FindProperty("temperaturePersistance");
            propTemperatureOctaves = so.FindProperty("temperatureOctaves");
            propTemperatureScale = so.FindProperty("temperatureScale");
            propTemperatureOffset = so.FindProperty("temperatureOffset");

            propVoxelVertecies = so.FindProperty("voxelVertecies");
            propVoxelTris = so.FindProperty("voxelTris");

            propMinMoisture = so.FindProperty("minMoisture");
            propMaxMoisture = so.FindProperty("maxMoisture");
            propMoistureLancunarity = so.FindProperty("moistureLancunarity");
            propMoisturePersistance = so.FindProperty("moisturePersistance");
            propMoistureOctaves = so.FindProperty("moistureOctaves");
            propMoistureScale = so.FindProperty("moistureScale");
            propMoistureOffset = so.FindProperty("moistureOffset");
        }
        public override void OnInspectorGUI()
        {
            so.Update();

            EditorGUILayout.LabelField("Generator Properties");
            EditorGUILayout.Space(25);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propSeed);
            EditorGUILayout.PropertyField(propWorldName);
            EditorGUILayout.PropertyField(propGrid);
            EditorGUILayout.PropertyField(propBiomes);
            EditorGUILayout.PropertyField(propRenderDistance);
            EditorGUILayout.PropertyField(propVoxelVertecies);
            EditorGUILayout.PropertyField(propVoxelTris);

            EditorGUILayout.LabelField("Climate Properties");
            EditorGUILayout.Space(25);

            EditorGUILayout.PropertyField(propMinTemperature);
            EditorGUILayout.PropertyField(propMaxTemperature);
            EditorGUILayout.PropertyField(propTemperatureLancunarity);
            EditorGUILayout.PropertyField(propTemperaturePersistance);
            EditorGUILayout.PropertyField(propTemperatureOctaves);
            EditorGUILayout.PropertyField(propTemperatureScale);
            EditorGUILayout.PropertyField(propTemperatureOffset);
            EditorGUILayout.PropertyField(propMinMoisture);
            EditorGUILayout.PropertyField(propMaxMoisture);
            EditorGUILayout.PropertyField(propMoistureLancunarity);
            EditorGUILayout.PropertyField(propMoisturePersistance);
            EditorGUILayout.PropertyField(propMoistureOctaves);
            EditorGUILayout.PropertyField(propMoistureScale);
            EditorGUILayout.PropertyField(propMoistureOffset);

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Preview");
            EditorGUILayout.Space(25);

            previewChunkSize = EditorGUILayout.IntField("Preview Chunk Width", previewChunkSize);

            if (EditorGUI.EndChangeCheck()) {
                float2[] climateMap = new float2[previewChunkSize * previewChunkSize];
                TerrainNoise.CreateClimateMap(
                    previewChunkSize,
                    ref climateMap,
                    0,
                    (TerrainSettings)target
                );

                previewTexture = TerrainNoise.CreateClimateTexture(climateMap, previewChunkSize);
            }

            GUIStyle previewStyle = new GUIStyle();
            previewStyle.fixedHeight = 0;
            previewStyle.fixedWidth = 0;
            previewStyle.stretchWidth = true;
            GUILayout.Label(previewTexture, previewStyle);

            so.ApplyModifiedProperties();
        }
    }    
}
