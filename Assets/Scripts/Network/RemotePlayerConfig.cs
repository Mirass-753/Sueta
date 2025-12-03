using UnityEngine;

public class RemotePlayerConfig : MonoBehaviour
{
    public GameObject remotePlayerPrefab;
    public Sprite idleSprite;
    public Sprite movingSprite;

    private void Awake()
    {
        RemotePlayer.prefab = remotePlayerPrefab;
        RemotePlayer.idleSprite = idleSprite;
        RemotePlayer.movingSprite = movingSprite;
    }
}
