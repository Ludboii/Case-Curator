using UnityEngine;

public enum CollectionType
{
    Collection,
    SouvenirCollection,
    Case
}

[CreateAssetMenu(
    fileName = "NewCollection",
    menuName = "Case Catcher/Collection Data")]
public class CollectionData : ScriptableObject
{
    public string collectionName;
    public string apiId;
    public CollectionType type;
    public Sprite icon;
}