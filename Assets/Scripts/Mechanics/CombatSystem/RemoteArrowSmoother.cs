using UnityEngine;

/// <summary>
/// Вешается на стрелку (или объект игрока с ссылкой на стрелку) и плавно доводит угол до цели.
/// Использовать только для удалённых игроков.
/// </summary>
public class RemoteArrowSmoother : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ArrowController arrow;

    [Header("Smoothing")]
    [Tooltip("Чем меньше, тем резче. 0.05–0.15 обычно норм.")]
    [SerializeField] private float smoothTime = 0.08f;

    [Tooltip("Если true — при больших скачках быстрее догоняет")]
    [SerializeField] private float maxSpeedDegPerSec = 9999f;

    private float _targetAngle;
    private float _velocity;

    private void Awake()
    {
        if (arrow == null)
            arrow = GetComponentInChildren<ArrowController>(true);

        if (arrow != null)
        {
            // Удалённому игроку ввод стрелки не нужен
            arrow.allowPlayerInput = false;
        }
    }

    public void SetTargetAngle(float angleDeg)
    {
        _targetAngle = Mathf.Repeat(angleDeg, 360f);
    }

    private void Update()
    {
        if (arrow == null)
            return;

        float current = arrow.Angle;
        float next = Mathf.SmoothDampAngle(
            current,
            _targetAngle,
            ref _velocity,
            smoothTime,
            maxSpeedDegPerSec,
            Time.deltaTime
        );

        arrow.SetAngle(next);
    }
}
