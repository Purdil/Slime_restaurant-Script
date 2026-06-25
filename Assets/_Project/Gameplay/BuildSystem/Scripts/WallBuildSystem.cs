using _Project.Gameplay.AgentSystem.Astar;
using UnityEngine;
using UnityEngine.Tilemaps;

public class WallBuildSystem : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private FloorBuildSystem floorBuildSystem;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap previewWallTilemap;
    [SerializeField] private TileBase currentWallTile;
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private Vector2[] excludedWallPositions;
    [SerializeField] private float excludedWallPositionTolerance = 0.01f;

    private TileBase _defaultWallTile;
    private TileBase _previewWallTile;

    private void Awake()
    {
        _defaultWallTile = currentWallTile;
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            RebuildFloors();
            RebuildWalls();
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying == false)
        {
            return;
        }

        RebuildAreaTiles();
    }

    public void RebuildWalls()
    {
        RebuildWalls(wallTilemap, currentWallTile);
        RefreshAstarBlockedMap();
    }

    public void SetWallTile(TileBase wallTile)
    {
        if (wallTile == null)
        {
            return;
        }

        currentWallTile = wallTile;
        CancelPreview();
        RebuildWalls();
    }

    public void ResetWalls()
    {
        currentWallTile = _defaultWallTile;
        CancelPreview();
        RebuildWalls();
    }

    public void PreviewWallTile(TileBase wallTile)
    {
        if (wallTile == null)
        {
            return;
        }

        _previewWallTile = wallTile;
        RebuildWalls(previewWallTilemap, _previewWallTile);
    }

    public bool ApplyPreviewWallTile(TileBase fallbackWallTile)
    {
        TileBase wallTile = _previewWallTile != null ? _previewWallTile : fallbackWallTile;

        if (wallTile == null)
        {
            return false;
        }

        currentWallTile = wallTile;
        CancelPreview();
        RebuildWalls();
        return true;
    }

    public void CancelPreview()
    {
        _previewWallTile = null;
        ClearPreview();
    }

    public void ExpandArea(BuildAreaExpandDirection direction)
    {
        if (gridSystem == null)
        {
            return;
        }

        gridSystem.ExpandArea(direction);
        RebuildAreaTiles();
    }

    public bool PurchaseArea(Vector2Int areaCoordinate)
    {
        if (gridSystem == null)
        {
            return false;
        }

        if (gridSystem.PurchaseArea(areaCoordinate) == false)
        {
            return false;
        }

        RebuildAreaTiles();
        return true;
    }

    public void RebuildAreaTiles()
    {
        RebuildFloors();
        RebuildWalls();

        if (_previewWallTile != null)
        {
            RebuildWalls(previewWallTilemap, _previewWallTile);
        }
    }

    private void RefreshAstarBlockedMap()
    {
        if (AstarManager.IsNullInstance)
        {
            return;
        }

        AstarManager.Instance.RefreshBlockedMapAndNotifyChanges();
    }

    private void RebuildWalls(Tilemap targetTilemap, TileBase tile)
    {
        if (gridSystem == null || targetTilemap == null || tile == null)
        {
            return;
        }

        RectInt bounds = gridSystem.BuildBounds;

        targetTilemap.ClearAllTiles();
        BuildPerimeterWalls(targetTilemap, tile, bounds);
        targetTilemap.RefreshAllTiles();
    }

    private void ClearPreview()
    {
        if (previewWallTilemap == null)
        {
            return;
        }

        previewWallTilemap.ClearAllTiles();
        previewWallTilemap.RefreshAllTiles();
    }

    private void RebuildFloors()
    {
        if (floorBuildSystem == null)
        {
            return;
        }

        floorBuildSystem.RebuildFloors();
    }

    private void BuildPerimeterWalls(Tilemap targetTilemap, TileBase tile, RectInt bounds)
    {
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector2Int gridPosition = new Vector2Int(x, y);

                if (gridSystem.IsInsideGrid(gridPosition) == false)
                {
                    continue;
                }

                BuildCellWalls(targetTilemap, tile, gridPosition);
            }
        }
    }

    private void BuildCellWalls(Tilemap targetTilemap, TileBase tile, Vector2Int gridPosition)
    {
        Vector2Int left = gridPosition + Vector2Int.left;
        Vector2Int right = gridPosition + Vector2Int.right;
        Vector2Int up = gridPosition + Vector2Int.up;
        Vector2Int down = gridPosition + Vector2Int.down;
        bool isLeftOutside = gridSystem.IsInsideGrid(left) == false;
        bool isRightOutside = gridSystem.IsInsideGrid(right) == false;
        bool isUpOutside = gridSystem.IsInsideGrid(up) == false;
        bool isDownOutside = gridSystem.IsInsideGrid(down) == false;

        if (isLeftOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x - 1, gridPosition.y, 0));
        }

        if (isRightOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x + 1, gridPosition.y, 0));
        }

        if (isUpOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x, gridPosition.y + 1, 0));
        }

        if (isDownOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x, gridPosition.y - 1, 0));
        }

        if (isLeftOutside && isUpOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x - 1, gridPosition.y + 1, 0));
        }

        if (isRightOutside && isUpOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x + 1, gridPosition.y + 1, 0));
        }

        if (isLeftOutside && isDownOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x - 1, gridPosition.y - 1, 0));
        }

        if (isRightOutside && isDownOutside)
        {
            SetTile(targetTilemap, tile, new Vector3Int(gridPosition.x + 1, gridPosition.y - 1, 0));
        }
    }

    private void SetTile(Tilemap targetTilemap, TileBase tile, Vector3Int cellPosition)
    {
        if (IsExcludedWallPosition(targetTilemap, cellPosition))
        {
            return;
        }

        targetTilemap.SetTile(cellPosition, tile);
    }

    private bool IsExcludedWallPosition(Tilemap targetTilemap, Vector3Int cellPosition)
    {
        if (excludedWallPositions == null)
        {
            return false;
        }

        Vector2 wallCellPosition = new Vector2(cellPosition.x, cellPosition.y);
        Vector2 wallWorldPosition = targetTilemap.GetCellCenterWorld(cellPosition);

        for (int i = 0; i < excludedWallPositions.Length; i++)
        {
            if (targetTilemap.WorldToCell(excludedWallPositions[i]) == cellPosition)
            {
                return true;
            }

            if (IsSamePosition(excludedWallPositions[i], wallCellPosition))
            {
                return true;
            }

            if (IsSamePosition(excludedWallPositions[i], wallWorldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsSamePosition(Vector2 a, Vector2 b)
    {
        float tolerance = Mathf.Max(0f, excludedWallPositionTolerance);
        return Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;
    }
}
