
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public class PathBaker : MonoBehaviour
    {
        [SerializeField] private Tilemap groundMap;
        [SerializeField] private Tilemap[] obstaclesMap = Array.Empty<Tilemap>();
        [SerializeField] private BakedDataSO bakedData;

        [SerializeField] private bool isDrawGizmo = true;
        [SerializeField] private Color nodeColor, edgeColor;

        [ContextMenu("Bake map data")]
        private void BakeMapData()
        {
            Debug.Assert(groundMap != null, "Target tilemap are null or empty");
            WritePointData();
            SaveIfUnityEditor();
        }

        private void SaveIfUnityEditor()
        {
            #if UNITY_EDITOR
            EditorUtility.SetDirty(bakedData);
            AssetDatabase.SaveAssets();
            #endif
        }

        private void WritePointData()
        {
            bakedData.ClearPoints();
            groundMap.CompressBounds();

            BoundsInt mapBound = groundMap.cellBounds;
            
            //Debug.Log($"xMin : {mapBound.xMin}, xMax : {mapBound.xMax}, yMin : {mapBound.yMin}, yMax : {mapBound.yMax}");

            for (int x = mapBound.xMin; x < mapBound.xMax; x++)
            {
                for (int y = mapBound.yMin; y < mapBound.yMax; y++)
                {
                    Vector2Int targetCell = new Vector2Int(x, y);
                    if (CanMovePosition(targetCell))
                    {
                        AddPoint(targetCell);
                    }
                }
            }
        }
        
        private bool CanMovePosition(Vector2Int targetCell)
        {
            bool hasGround = groundMap.HasTile((Vector3Int)targetCell);
            foreach (var obstacle in obstaclesMap)
            {
                if (obstacle != null && obstacle.HasTile((Vector3Int)targetCell))
                {
                    return false;
                }
            }
            return hasGround;
        }
        
        private void AddPoint(Vector2Int targetCell)
        {
            Vector3 worldPosition = groundMap.GetCellCenterWorld((Vector3Int)targetCell);
            bakedData.AddPoint(worldPosition, targetCell);
        }
        
        
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (isDrawGizmo == false)
            {
                return;
            }

            foreach (NodeData nodeData in bakedData.points)
            {
                Gizmos.color = nodeColor;
                Gizmos.DrawWireSphere(nodeData.worldPosition, 0.15f);
            }
        }

        private void DrawArrowGizmo(Vector3 start, Vector3 end)
        {
            Vector3 direction = (end - start).normalized;
            Vector3 arrowStart = end - direction * 0.25f;
            Vector3 arrowEnd = end - direction.normalized * 0.15f;
            const float arrowSize = 0.05f;

            Vector3 pointA = arrowStart + (Quaternion.Euler(0, 0, -90f) * direction) * arrowSize;
            Vector3 pointB = arrowStart + (Quaternion.Euler(0, 0, 90f) * direction) * arrowSize;
            
            Gizmos.DrawLine(start, arrowStart);
            Gizmos.DrawLine(pointA, arrowEnd);
            Gizmos.DrawLine(pointB, arrowEnd);
            Gizmos.DrawLine(pointA, pointB);
        }
#endif
    }
}
