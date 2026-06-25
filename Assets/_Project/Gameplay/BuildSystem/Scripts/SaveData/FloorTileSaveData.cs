using Firebase.Firestore;

namespace _Project.Gameplay.BuildSystem.Scripts.SaveData
{
    [System.Serializable]
    [FirestoreData]
    public class FloorTileSaveData
    {
        [FirestoreProperty] public int BuildingID {get; set;}
        [FirestoreProperty] public int X {get; set;}
        [FirestoreProperty] public int Y  {get; set;}
    }
}
