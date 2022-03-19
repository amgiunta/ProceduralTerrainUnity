using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using Unity.Entities;

using VoxelTerrain.ECS.Systems;

namespace VoxelTerrain
{
    public class TerrainEditorWindow : EditorWindow
    {
        EditorSpawnVoxelTerrainChunkSystem systemTest;

        Vector2 terrainScrollPos;
        Vector2 biomeScrollPos;

        SerializedObject settingsObject;
        SerializedObject biomeObject;

        public TerrainSettings terrainSettings;
        public BiomeObject biomeSettings;

        enum PreviewType { Mesh, Map};
        PreviewType previewType;

        MapPreview.MapLense previewLense;

        #region terrainFields
        SerializedProperty propTerrainSeed;

        SerializedProperty propTerrainWorldName;
        SerializedProperty propTerrainGrid;
        SerializedProperty propTerrainBiomes;
        SerializedProperty propTerrainRenderDistance;

        SerializedProperty propTerrainMinTemperature;
        SerializedProperty propTerrainMaxTemperature;
        SerializedProperty propTerrainTemperatureLancunarity;
        SerializedProperty propTerrainTemperaturePersistance;
        SerializedProperty propTerrainTemperatureOctaves;
        SerializedProperty propTerrainTemperatureScale;
        SerializedProperty propTerrainTemperatureOffset;

        SerializedProperty propTerrainMinMoisture;
        SerializedProperty propTerrainMaxMoisture;
        SerializedProperty propTerrainMoistureLancunarity;
        SerializedProperty propTerrainMoisturePersistance;
        SerializedProperty propTerrainMoistureOctaves;
        SerializedProperty propTerrainMoistureScale;
        SerializedProperty propTerrainMoistureOffset;
        #endregion

        #region biomeFields
        SerializedProperty propBiomeColor;
        SerializedProperty propBiomeMaxTemperature;
        SerializedProperty propBiomeminTemperature;
        SerializedProperty propBiomeMaxMoisture;
        SerializedProperty propBiomeMinMoisture;
        SerializedProperty propBiomeHeightNormalIntensity;
        SerializedProperty propBiomeMinTerrainHeight;
        SerializedProperty propBiomeMaxTerrainHeight;
        SerializedProperty propBiomeGeneratorNoiseScale;
        SerializedProperty propBiomeHeightNormalNoiseScale;
        SerializedProperty propBiomeNoiseRotation;
        SerializedProperty propBiomePersistance;
        SerializedProperty propBiomeLancunarity;
        SerializedProperty propBiomeOctaves;
        #endregion

        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/Voxel Terrain")]
        public static void ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            GetWindow(typeof(TerrainEditorWindow));
        }

        public void OnEnable()
        {
            Debug.Log("---------------------------------------------------");//helps see wich logs are from the last compile
            if (EditorApplication.isPlaying || systemTest != null) return;

            DefaultWorldInitialization.DefaultLazyEditModeInitialize();//boots the ECS EditorWorld

            systemTest = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<EditorSpawnVoxelTerrainChunkSystem>();
            var conversionSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<GameObjectConversionSystem>();

            EditorApplication.update += () => systemTest.Update();//makes the system tick properly, not every 2 seconds !

            TerrainChunkConversionManager chunkConverter = GameObject.FindObjectOfType<TerrainChunkConversionManager>();
            chunkConverter.Convert(new Entity(), World.DefaultGameObjectInjectionWorld.EntityManager, conversionSystem);
        }

        public void OnDestroy()
        {
            //extra safety against post-compilation problems (typeLoadException) and spamming the console with failed updates
            if (!EditorApplication.isPlaying)
                EditorApplication.update -= () => systemTest.Update();
        }

        void InitializeTerrainFields() {
            propTerrainSeed = settingsObject.FindProperty("seed");

            propTerrainWorldName = settingsObject.FindProperty("worldName");
            propTerrainGrid = settingsObject.FindProperty("grid");
            propTerrainBiomes = settingsObject.FindProperty("biomes");
            propTerrainRenderDistance = settingsObject.FindProperty("renderDistance");

            propTerrainMinTemperature = settingsObject.FindProperty("minTemperature");
            propTerrainMaxTemperature = settingsObject.FindProperty("maxTemperature");
            propTerrainTemperatureLancunarity = settingsObject.FindProperty("temperatureLancunarity");
            propTerrainTemperaturePersistance = settingsObject.FindProperty("temperaturePersistance");
            propTerrainTemperatureOctaves = settingsObject.FindProperty("temperatureOctaves");
            propTerrainTemperatureScale = settingsObject.FindProperty("temperatureScale");
            propTerrainTemperatureOffset = settingsObject.FindProperty("temperatureOffset");

            propTerrainMinMoisture = settingsObject.FindProperty("minMoisture");
            propTerrainMaxMoisture = settingsObject.FindProperty("maxMoisture");
            propTerrainMoistureLancunarity = settingsObject.FindProperty("moistureLancunarity");
            propTerrainMoisturePersistance = settingsObject.FindProperty("moisturePersistance");
            propTerrainMoistureOctaves = settingsObject.FindProperty("moistureOctaves");
            propTerrainMoistureScale = settingsObject.FindProperty("moistureScale");
            propTerrainMoistureOffset = settingsObject.FindProperty("moistureOffset");
        }

        void InitializeBiomeFields() {
            propBiomeColor = biomeObject.FindProperty("color");
            propBiomeMaxTemperature = biomeObject.FindProperty("maxTemperature");
            propBiomeminTemperature = biomeObject.FindProperty("minTemperature");
            propBiomeMaxMoisture = biomeObject.FindProperty("maxMoisture");
            propBiomeMinMoisture = biomeObject.FindProperty("minMoisture");
            propBiomeHeightNormalIntensity = biomeObject.FindProperty("heightNormalIntensity");
            propBiomeMinTerrainHeight = biomeObject.FindProperty("minTerrainHeight");
            propBiomeMaxTerrainHeight = biomeObject.FindProperty("maxTerrainHeight");
            propBiomeGeneratorNoiseScale = biomeObject.FindProperty("generatorNoiseScale");
            propBiomeHeightNormalNoiseScale = biomeObject.FindProperty("heightNormalNoiseScale");
            propBiomeNoiseRotation = biomeObject.FindProperty("noiseRotation");
            propBiomePersistance = biomeObject.FindProperty("persistance");
            propBiomeLancunarity = biomeObject.FindProperty("lancunarity");
            propBiomeOctaves = biomeObject.FindProperty("octaves");
        }

        void RenderTerrainHelpBox() {
            GUILayout.Label("No Terrain Settings object selected.", EditorStyles.helpBox);
            GUILayout.Label("Right-click in the project pane and click 'Terrain Settings' to create a new one.", EditorStyles.helpBox);
        }

        void RenderBiomeHelpBox()
        {
            GUILayout.Label("No Biome Settings object selected.", EditorStyles.helpBox);
            GUILayout.Label("Right-click in the project pane and click 'Biome Object' to create a new one.", EditorStyles.helpBox);
        }

        void RenderTerrainFields() {
            settingsObject.Update();

            terrainScrollPos = EditorGUILayout.BeginScrollView(terrainScrollPos);
            EditorGUILayout.LabelField("Generator Properties");
            EditorGUILayout.Space(25);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propTerrainSeed);
            EditorGUILayout.PropertyField(propTerrainWorldName);
            EditorGUILayout.PropertyField(propTerrainGrid);
            EditorGUILayout.PropertyField(propTerrainBiomes);
            EditorGUILayout.PropertyField(propTerrainRenderDistance);

            EditorGUILayout.Space(25);
            EditorGUILayout.LabelField("Climate Properties", EditorStyles.boldLabel);
            EditorGUILayout.Space(25);

            EditorGUILayout.PropertyField(propTerrainMinTemperature);
            EditorGUILayout.PropertyField(propTerrainMaxTemperature);
            EditorGUILayout.PropertyField(propTerrainTemperatureLancunarity);
            EditorGUILayout.PropertyField(propTerrainTemperaturePersistance);
            EditorGUILayout.PropertyField(propTerrainTemperatureOctaves);
            EditorGUILayout.PropertyField(propTerrainTemperatureScale);
            EditorGUILayout.PropertyField(propTerrainTemperatureOffset);
            EditorGUILayout.PropertyField(propTerrainMinMoisture);
            EditorGUILayout.PropertyField(propTerrainMaxMoisture);
            EditorGUILayout.PropertyField(propTerrainMoistureLancunarity);
            EditorGUILayout.PropertyField(propTerrainMoisturePersistance);
            EditorGUILayout.PropertyField(propTerrainMoistureOctaves);
            EditorGUILayout.PropertyField(propTerrainMoistureScale);
            EditorGUILayout.PropertyField(propTerrainMoistureOffset);
            EditorGUILayout.EndScrollView();
        }

        void RenderBiomeFields() {

            biomeScrollPos = EditorGUILayout.BeginScrollView(biomeScrollPos);

            EditorGUILayout.PropertyField(propBiomeColor);
            EditorGUILayout.PropertyField(propBiomeMaxTemperature);
            EditorGUILayout.PropertyField(propBiomeminTemperature);
            EditorGUILayout.PropertyField(propBiomeMaxMoisture);
            EditorGUILayout.PropertyField(propBiomeMinMoisture);
            EditorGUILayout.PropertyField(propBiomeHeightNormalIntensity);
            EditorGUILayout.PropertyField(propBiomeMinTerrainHeight);
            EditorGUILayout.PropertyField(propBiomeMaxTerrainHeight);
            EditorGUILayout.PropertyField(propBiomeGeneratorNoiseScale);
            EditorGUILayout.PropertyField(propBiomeHeightNormalNoiseScale);
            EditorGUILayout.PropertyField(propBiomeNoiseRotation);
            EditorGUILayout.PropertyField(propBiomePersistance);
            EditorGUILayout.PropertyField(propBiomeLancunarity);
            EditorGUILayout.PropertyField(propBiomeOctaves);

            EditorGUILayout.EndScrollView();
        }

        void OnGUI()
        {
            // Split window in two vertical sections
            EditorGUILayout.BeginVertical();

            // Split top section in two
            EditorGUILayout.BeginHorizontal(GUILayout.Height(position.height / 2));
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width/2));
            GUILayout.Label("Terrain Settings", EditorStyles.boldLabel);
            terrainSettings = EditorGUILayout.ObjectField(terrainSettings, typeof(TerrainSettings), allowSceneObjects: true) as TerrainSettings;

            if (terrainSettings != null)
            {
                settingsObject = new SerializedObject(terrainSettings);
                InitializeTerrainFields();
            }
            else {
                settingsObject = null;
            }

            if (settingsObject == null)
            {
                RenderTerrainHelpBox();
            }
            else {
                RenderTerrainFields();
            }


            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2));
            GUILayout.Label("Biome Settings", EditorStyles.boldLabel);

            biomeSettings = EditorGUILayout.ObjectField(biomeSettings, typeof(BiomeObject), allowSceneObjects: true) as BiomeObject;

            if (biomeSettings != null)
            {
                biomeObject = new SerializedObject(biomeSettings);
                InitializeBiomeFields();
            }
            else
            {
                biomeObject = null;
            }

            if (biomeObject == null)
            {
                RenderBiomeHelpBox();
            }
            else
            {
                RenderBiomeFields();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal(GUILayout.Height(position.height/2));
            EditorGUILayout.BeginVertical();
            GUILayout.Label("Preview", EditorStyles.boldLabel);

            previewType = (PreviewType) EditorGUILayout.EnumPopup("Preview Type", previewType);
            previewLense = (MapPreview.MapLense)EditorGUILayout.EnumPopup("Preview Lense", previewLense);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}
