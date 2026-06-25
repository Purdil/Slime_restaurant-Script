using System.Collections.Generic;
using Firebase.Firestore;

namespace _Project.Gameplay.BuildSystem.Scripts.SaveData
{
    [System.Serializable]
    [FirestoreData]
    public class BuildSaveWrapper
    {
        [FirestoreProperty] public int WallBuildingID { get; set; }
        [FirestoreProperty] public List<BuildingSaveData> Buildings { get; set; } = new();
        [FirestoreProperty] public List<FloorTileSaveData> FloorTiles { get; set; } = new();
        [FirestoreProperty] public List<BuildAreaSaveData> PurchasedAreas { get; set; } = new();
    }
}
