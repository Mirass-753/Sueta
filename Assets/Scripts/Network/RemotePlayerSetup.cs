using UnityEngine;

public class RemotePlayerSetup : MonoBehaviour
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
