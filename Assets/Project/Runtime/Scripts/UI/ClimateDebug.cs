using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using TMPro;
using VoxelTerrain;

public class ClimateDebug : MonoBehaviour
{
    public TMP_Text tempText;
    public TMP_Text moisText;

    PlayerController player;

    // Start is called before the first frame update
    void Start()
    {
        player = FindObjectOfType<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {
        float2 localPosition = new float2(
            (player.transform.position.x * TerrainManager.instance.terrainSettings.grid.voxelSize) % TerrainManager.instance.terrainSettings.grid.chunkSize,
            (player.transform.position.z * TerrainManager.instance.terrainSettings.grid.voxelSize) % TerrainManager.instance.terrainSettings.grid.chunkSize
        );

        float2 climate = TerrainNoise.Climate(localPosition.x, localPosition.y, 
            TerrainManager.instance.terrainSettings, player.gridPosition, 
            TerrainManager.instance.terrainSettings.grid.chunkSize, TerrainManager.instance.terrainSettings.grid.voxelSize
        );

        tempText.text = climate.x.ToString();
        moisText.text = climate.y.ToString();
    }
}
