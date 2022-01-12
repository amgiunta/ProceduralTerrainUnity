using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Entities;

namespace VoxelTerrain
{
    [CustomEditor(typeof(BiomeObject))]
    [CanEditMultipleObjects]
    public class BiomeEditor : Editor
    {
        SerializedObject so;
        SerializedProperty propColor;
        SerializedProperty propMaxTemperature;
        SerializedProperty propminTemperature;
        SerializedProperty propMaxMoisture;
        SerializedProperty propMinMoisture;
        SerializedProperty propHeightNormalIntensity;
        SerializedProperty propMinTerrainHeight;
        SerializedProperty propMaxTerrainHeight;
        SerializedProperty propGeneratorNoiseScale;
        SerializedProperty propHeightNormalNoiseScale;
        SerializedProperty propPersistance;
        SerializedProperty propLancunarity;
        SerializedProperty propOctaves;

        int previewSeed;
        int previewChunkSize = 64;

        Texture2D previewTexture;

        private void OnEnable()
        {
            so = serializedObject;

            propColor = so.FindProperty("color");
            propMaxTemperature = so.FindProperty("maxTemperature");
            propminTemperature = so.FindProperty("minTemperature");
            propMaxMoisture = so.FindProperty("maxMoisture");
            propMinMoisture = so.FindProperty("minMoisture");
            propHeightNormalIntensity = so.FindProperty("heightNormalIntensity");
            propMinTerrainHeight = so.FindProperty("minTerrainHeight");
            propMaxTerrainHeight = so.FindProperty("maxTerrainHeight");
            propGeneratorNoiseScale = so.FindProperty("generatorNoiseScale");
            propHeightNormalNoiseScale = so.FindProperty("heightNormalNoiseScale");
            propPersistance = so.FindProperty("persistance");
            propLancunarity = so.FindProperty("lancunarity");
            propOctaves = so.FindProperty("octaves");
        }

        public override void OnInspectorGUI() {
            so.Update();

            EditorGUILayout.LabelField("Biome Properties");
            EditorGUILayout.Space(25);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propColor);
            EditorGUILayout.PropertyField(propMaxTemperature);
            EditorGUILayout.PropertyField(propminTemperature);
            EditorGUILayout.PropertyField(propMaxMoisture);
            EditorGUILayout.PropertyField(propMinMoisture);
            EditorGUILayout.PropertyField(propHeightNormalIntensity);
            EditorGUILayout.PropertyField(propMinTerrainHeight);
            EditorGUILayout.PropertyField(propMaxTerrainHeight);
            EditorGUILayout.PropertyField(propGeneratorNoiseScale);
            EditorGUILayout.PropertyField(propHeightNormalNoiseScale);
            EditorGUILayout.PropertyField(propPersistance);
            EditorGUILayout.PropertyField(propLancunarity);
            EditorGUILayout.PropertyField(propOctaves);
            EditorGUILayout.Separator();

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Preview");
            EditorGUILayout.Space(25);

            previewSeed = EditorGUILayout.IntField("Preview Seed", previewSeed);
            previewChunkSize = EditorGUILayout.IntField("Preview Chunk Width", previewChunkSize);

            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();

                float[] noiseMap = new float[previewChunkSize * previewChunkSize];
                TerrainNoise.CreateNoiseMap(
                    previewChunkSize,
                    (Biome) target,
                    previewSeed,
                    ref noiseMap
                );

                previewTexture = TerrainNoise.CreateNoiseTexture(noiseMap, previewChunkSize);
            }

            GUIStyle previewStyle = new GUIStyle();
            previewStyle.fixedHeight = 0;
            previewStyle.fixedWidth = 0;
            previewStyle.stretchWidth = true;
            GUILayout.Label(previewTexture, previewStyle);

            
        }
    }
}
