using UnityEngine;

public class NetworkWorld : MonoBehaviour
{
    private void OnDestroy()
    {
        NetworkMessageHandler.ClearAll();
    }
}
