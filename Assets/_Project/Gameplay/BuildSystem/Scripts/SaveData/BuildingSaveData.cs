using Firebase.Firestore;

namespace _Project.Gameplay.BuildSystem.Scripts.SaveData
{
    [System.Serializable]
    [FirestoreData]
    public class BuildingSaveData
    {
        [FirestoreProperty] public int BuildingID { get; set; }
        [FirestoreProperty] public int ConstructorID { get; set; }
        // Old PlayerPrefs JSON used this misspelled field name.
        [FirestoreProperty] public int ConstuctorID { get; set; }
        [FirestoreProperty] public int X { get; set; }
        [FirestoreProperty] public int Y { get; set; }
        [FirestoreProperty] public int Rotation {get; set;}

        public int GetConstructorID()
        {
            if (ConstructorID != 0)
            {
                return ConstructorID;
            }

            return ConstuctorID;
        }

        public void SetConstructorID(int id)
        {
            ConstructorID = id;
            ConstuctorID = id;
        }
    }
}
