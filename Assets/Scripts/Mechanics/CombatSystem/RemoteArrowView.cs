using UnityEngine;

public class RemoteArrowView : MonoBehaviour
{
    /// <summary>
    /// Повернуть стрелку в нужный угол (в градусах, как в ArrowController.Angle).
    /// </summary>
    public void SetAngle(float angleDeg)
    {
        transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }
}
