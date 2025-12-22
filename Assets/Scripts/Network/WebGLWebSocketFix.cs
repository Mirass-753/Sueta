using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class WebGLWebSocketFix : MonoBehaviour
{
    #if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void webSocketSafeCall(IntPtr cb, IntPtr res);

    void Awake() {
        Debug.Log("Linker check: webSocketSafeCall exists");
    }
    #endif
}