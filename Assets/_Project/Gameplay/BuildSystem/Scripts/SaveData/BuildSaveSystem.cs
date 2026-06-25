using System;
using System.Collections.Generic;
using _Project.Core.CustomLogging;
using _Project.Gameplay.AgentSystem.AgentModules.CommonModule;
using _Project.Gameplay.AgentSystem.Astar;
using _Project.Gameplay.BuildSystem.Scriptable_Script;
using _Project.Gameplay.BuildSystem.Scripts;
using _Project.Gameplay.BuildSystem.Scripts.SaveData;
using UnityEngine;

public class BuildSaveSystem : MonoBehaviour
{
    private const string SAVE_KEY = "BUILD_DATA";
    private const float PLACED_BUILDING_Y_OFFSET = 0.2f;
    
    public event Action<BuildSaveWrapper> OnBuildSave; 
    
    [SerializeField] private GridUpdatedChannel gridChannel;
    [SerializeField] private BuildingDataBase buildingDatabase;
    [SerializeField] private BuildingPool buildingPool;
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private WallBuildSystem wallBuildSystem;
    [SerializeField] private FloorBuildSystem floorBuildSystem;

    private readonly BuildingSaveCollection _buildings = new();
    private readonly FloorTileSaveCollection _floorTiles = new();
    private readonly List<Vector2Int> _purchasedAreas = new();
    private int _wallBuildingID;
    private bool _hasUnsavedFloorChanges;

    private void OnEnable()
    {
        if (gridChannel != null)
        {
            gridChannel.OnEvent += OnBuildingPlaced;
        }
    }

    private void OnDisable()
    {
        if (gridChannel != null)
        {
            gridChannel.OnEvent -= OnBuildingPlaced;
        }
    }

    /*private void Start()
    {
        Load();
    }*/

    public void Save()
    {
        BuildSaveWrapper wrapper = new BuildSaveWrapper
        {
            WallBuildingID = _wallBuildingID,
            Buildings = new List<BuildingSaveData>(_buildings.Items),
            FloorTiles = new List<FloorTileSaveData>(_floorTiles.Items),
            PurchasedAreas = CreateAreaSaveData()
        };
        
        OnBuildSave?.Invoke(wrapper);
        
        _hasUnsavedFloorChanges = false;
        CLog.Log($"Build save completed. Count: {_buildings.Count}");
    }

    public void Load(BuildSaveWrapper wrapper)
    {
        if (buildingDatabase == null)
        {
            CLog.LogError("BuildSaveSystem load failed. BuildingDataBase is null.");
            return;
        }

        if (buildingPool == null)
        {
            CLog.LogError("BuildSaveSystem load failed. BuildingPool is null.");
            return;
        }

        if (gridSystem == null)
        {
            CLog.LogError("BuildSaveSystem load failed. GridSystem is null.");
            return;
        }

        ClearRuntimeState();
        RefreshAstarBlockedMap();

        if (wrapper == null)
        {
            CLog.LogError("BuildSaveSystem load failed. Save JSON wrapper is null.");
            return;
        }

        if (wrapper.Buildings == null)
        {
            CLog.LogError("BuildSaveSystem load failed. Saved building list is null.");
            return;
        }

        _buildings.Clear();
        _wallBuildingID = wrapper.WallBuildingID;
        ApplySavedAreas(wrapper.PurchasedAreas);
        ApplySavedWall();
        RebuildAreaTiles();
        ApplySavedFloors(wrapper.FloorTiles);

        int loadedCount = 0;

        foreach (BuildingSaveData data in wrapper.Buildings)
        {
            BuildingData buildingData =
                buildingDatabase.GetBuilding(data.BuildingID);

            if (buildingData == null)
            {
                CLog.LogError($"BuildSaveSystem skipped saved building. Missing BuildingData ID: {data.BuildingID}");
                continue;
            }

            Vector2Int gridPos =
                new Vector2Int(data.X, data.Y);
            int rotationIndex = buildingData.GetRotationIndex(data.Rotation);

            GameObject building = buildingPool.Get(
                buildingData,
                GetPlacedBuildingWorldPosition(gridPos),
                buildingData.GetWorldRotation(rotationIndex)
            );
            buildingData.ApplyRotationSprite(building, rotationIndex);

            PlaceBuilding placeBuilding =
                building.GetComponent<PlaceBuilding>();

            if (placeBuilding != null)
            {
                placeBuilding.Init(
                    data.BuildingID,
                    data.GetConstructorID(),
                    gridPos,
                    data.Rotation
                );
            }

            foreach (Vector2Int cell in buildingData.GetOccupiedCells(rotationIndex))
            {
                gridSystem.SetOccupied(gridPos + cell, true);
            }

            loadedCount++;
        }

        RefreshAstarBlockedMap();
        _buildings.LoadFrom(wrapper.Buildings);
        CLog.Log($"Build load completed. Count: {loadedCount}");
    }

    public void ClearSave()
    {
        CLog.Log("세이브 데이터 삭제");
        _buildings.Clear();
        _floorTiles.Clear();
        _purchasedAreas.Clear();
        _wallBuildingID = 0;
        _hasUnsavedFloorChanges = false;
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
    }

    public void SaveWall(int wallBuildingID)
    {
        _wallBuildingID = wallBuildingID;
        Save();
    }

    public void SaveFloor(int floorBuildingID, Vector2Int gridPos)
    {
        if (_floorTiles.Save(floorBuildingID, gridPos))
        {
            _hasUnsavedFloorChanges = true;
        }
    }

    public void FlushFloors()
    {
        if (_hasUnsavedFloorChanges == false)
        {
            return;
        }

        Save();
    }

    private void OnBuildingPlaced(BuildingPlacedInfo info)
    {
        if (info.HasGridPos == false)
        {
            return;
        }

        if (info.ChangeType == BuildingGridChangeType.Removed)
        {
            _buildings.Remove(info.BuildingId, info.GridPos);
            Save();
            return;
        }

        _buildings.Add(info);
        RefreshAstarBlockedMap();
        AgentMovementModule.RelocateAgentsFromBlockedCells(info.Points);
        Save();
    }

    private void ClearRuntimeState()
    {
        _buildings.Clear();
        _floorTiles.Clear();
        _purchasedAreas.Clear();
        _wallBuildingID = 0;
        _hasUnsavedFloorChanges = false;

        if (buildingPool != null)
        {
            buildingPool.ClearAll();
        }

        if (gridSystem != null)
        {
            gridSystem.ResetBuildArea();
        }

        if (floorBuildSystem != null)
        {
            floorBuildSystem.ResetFloors();
        }

        if (wallBuildSystem != null)
        {
            wallBuildSystem.ResetWalls();
        }
    }

    private void ApplySavedFloors(List<FloorTileSaveData> floorTiles)
    {
        if (floorTiles == null)
        {
            return;
        }

        _floorTiles.Clear();

        if (floorBuildSystem == null)
        {
            CLog.LogError("BuildSaveSystem failed to apply saved floors. FloorBuildSystem is null.");
            return;
        }

        foreach (FloorTileSaveData data in floorTiles)
        {
            BuildingData floorData = buildingDatabase.GetBuilding(data.BuildingID);

            if (floorData == null || floorData.TileBase == null)
            {
                CLog.LogError($"BuildSaveSystem skipped saved floor. Missing BuildingData ID: {data.BuildingID}");
                continue;
            }

            Vector2Int gridPos = new Vector2Int(data.X, data.Y);

            if (floorBuildSystem.SetFloorTile(gridPos, floorData.TileBase) == false)
            {
                continue;
            }

            _floorTiles.Save(data.BuildingID, gridPos);
        }
    }

    private List<BuildAreaSaveData> CreateAreaSaveData()
    {
        List<BuildAreaSaveData> saveData = new List<BuildAreaSaveData>();

        if (gridSystem == null)
        {
            return saveData;
        }

        gridSystem.GetPurchasedAreas(_purchasedAreas);

        for (int i = 0; i < _purchasedAreas.Count; i++)
        {
            saveData.Add(new BuildAreaSaveData
            {
                X = _purchasedAreas[i].x,
                Y = _purchasedAreas[i].y
            });
        }

        return saveData;
    }

    private void ApplySavedAreas(List<BuildAreaSaveData> purchasedAreas)
    {
        if (gridSystem == null || purchasedAreas == null)
        {
            return;
        }

        _purchasedAreas.Clear();

        for (int i = 0; i < purchasedAreas.Count; i++)
        {
            _purchasedAreas.Add(new Vector2Int(purchasedAreas[i].X, purchasedAreas[i].Y));
        }

        gridSystem.LoadPurchasedAreas(_purchasedAreas);
    }

    private void ApplySavedWall()
    {
        if (_wallBuildingID <= 0)
        {
            return;
        }

        if (buildingDatabase == null || wallBuildSystem == null)
        {
            return;
        }

        BuildingData wallData = buildingDatabase.GetBuilding(_wallBuildingID);

        if (wallData == null || wallData.TileBase == null)
        {
            return;
        }

        wallBuildSystem.SetWallTile(wallData.TileBase);
    }

    private void RebuildAreaTiles()
    {
        if (wallBuildSystem == null)
        {
            return;
        }

        wallBuildSystem.RebuildAreaTiles();
    }

    private Vector3 GetPlacedBuildingWorldPosition(Vector2Int gridPos)
    {
        Vector3 worldPosition = gridSystem.GridToWorld(gridPos);
        worldPosition.y += PLACED_BUILDING_Y_OFFSET;
        return worldPosition;
    }

    private void RefreshAstarBlockedMap()
    {
        if (AstarManager.IsNullInstance)
        {
            return;
        }

        AstarManager.Instance.RefreshBlockedMapAndNotifyChanges();
    }
}
