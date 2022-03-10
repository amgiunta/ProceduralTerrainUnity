using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using Unity.Entities;

namespace VoxelTerrain
{
    //[CustomEditor(typeof(TerrainManager))]
    [CanEditMultipleObjects]
    public class TerrainManagerEditor : Editor
    {
        SerializedObject so;
        SerializedProperty propWorldName;
        SerializedProperty propChunkQueueLimit;
        SerializedProperty propGeneratorFrequency;
        SerializedProperty propBiomes;
        SerializedProperty propRenderDistance;
        SerializedProperty propLodRanges;
        SerializedProperty propOnStartGeneration;
        SerializedProperty propTerrainSettings;

        SerializedProperty propGrid;
        SerializedProperty propChunkWidth;
        SerializedProperty propVoxelSize;

        TerrainSettings settings;
        List<Biome> biomes;

        Vector2Int mapPreviewSize;
        MapPreview preview;
        Texture2D previewTexture;

        MapPreview.MapLense previewLense;
        TerrainManager manager;

        private double scheduled = 0f;
        private double rendered = 0f;
        private double minWaitTime = 10f;
        private double maxWaitTime = 60f;
        private double elapsedTime = 0f;

        private void OnEnable()
        {
            so = serializedObject;
            manager = (TerrainManager)target;
            settings = manager.terrainSettings;
            biomes = new List<Biome>();
            previewTexture = new Texture2D(0,0);

            foreach (BiomeObject bo in settings.biomes) {
                biomes.Add(bo);
            }

            propGrid = so.FindProperty("grid");
            propWorldName = so.FindProperty("worldName");
            propChunkQueueLimit = so.FindProperty("chunkQueueLimit");
            propChunkWidth = propGrid.FindPropertyRelative("chunkSize");
            propVoxelSize = propGrid.FindPropertyRelative("voxelSize");
            propGeneratorFrequency = so.FindProperty("generatorFrequency");
            propRenderDistance = so.FindProperty("renderDistance");
            propTerrainSettings = so.FindProperty("terrainSettings");
            propBiomes = so.FindProperty("biomes");
            propLodRanges = so.FindProperty("lodRanges");
            propOnStartGeneration = so.FindProperty("OnStartGeneration");

            preview = new MapPreview(new int2(mapPreviewSize.x, mapPreviewSize.y), propChunkWidth.intValue, settings, biomes.ToArray());
        }
        public override void OnInspectorGUI()
        {
            elapsedTime = EditorApplication.timeSinceStartup;

            so.Update();
            EditorGUILayout.Space(25);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propWorldName);
            EditorGUILayout.PropertyField(propChunkQueueLimit);
            EditorGUILayout.PropertyField(propChunkWidth);
            EditorGUILayout.PropertyField(propVoxelSize);
            EditorGUILayout.PropertyField(propGeneratorFrequency);
            EditorGUILayout.PropertyField(propRenderDistance);
            EditorGUILayout.PropertyField(propTerrainSettings);
            EditorGUILayout.PropertyField(propBiomes);
            EditorGUILayout.PropertyField(propLodRanges);
            EditorGUILayout.PropertyField(propOnStartGeneration);

            mapPreviewSize = EditorGUILayout.Vector2IntField("Map Preview Chunk Size", mapPreviewSize);
            mapPreviewSize = mapPreviewSize == Vector2Int.zero ? Vector2Int.one : mapPreviewSize;

            if (EditorGUI.EndChangeCheck()) {
                biomes = new List<Biome>();
                foreach (BiomeObject bo in settings.biomes)
                {
                    biomes.Add(bo);
                }
                preview = new MapPreview(new int2(mapPreviewSize.x, mapPreviewSize.y), propChunkWidth.intValue, settings, biomes.ToArray());

            }

            so.ApplyModifiedProperties();

            previewLense = (MapPreview.MapLense) EditorGUILayout.EnumPopup("Map Preview Lense", previewLense);

            if (preview.isRunning)
            {
                if (elapsedTime - rendered > minWaitTime)
                {
                    if (GUILayout.Button("Force Complete"))
                    {
                        preview.CompleteRunningJobs();
                        rendered = elapsedTime;
                    }
                }
            }
            else
            {
                if (GUILayout.Button("Generate Preview"))
                {
                    biomes = new List<Biome>();
                    foreach (BiomeObject bo in settings.biomes)
                    {
                        biomes.Add(bo);
                    }
                    preview = new MapPreview(new int2(mapPreviewSize.x, mapPreviewSize.y), propChunkWidth.intValue, settings, biomes.ToArray());
                    preview.GenerateAsync();
                    scheduled = elapsedTime;
                }
            }

            if (elapsedTime - rendered > maxWaitTime)
            {
                preview.CompleteRunningJobs();
                rendered = elapsedTime;
            }

            float aspect = (float) mapPreviewSize.x / mapPreviewSize.y;
            float width = EditorGUIUtility.currentViewWidth /2;
            float height = width / aspect;

            previewTexture = preview.GetLenseTexture(previewLense);            

            EditorGUILayout.ObjectField(previewTexture, typeof(Texture2D), false , GUILayout.Width(width), GUILayout.Height(height));
            
            so.ApplyModifiedProperties();
        }
    }    
}
