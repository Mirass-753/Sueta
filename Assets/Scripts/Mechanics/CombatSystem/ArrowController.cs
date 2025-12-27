using UnityEngine;

/// Управляет стрелкой: может крутиться либо от ввода, либо от внешнего кода (RotateTowards/SetAngle).
public class ArrowController : MonoBehaviour
{
    public const float DefaultRotationSpeedDegPerSec = 72f;

    [Header("Rotation")]
    [Tooltip("Градусов в секунду. 72 = полный оборот за 5 секунд.")]
    public float rotationSpeedDegPerSec = DefaultRotationSpeedDegPerSec;

    [Tooltip("Прятать объект стрелки вне боевого режима.")]
    public bool hideWhenNotCombat = true;

    [Tooltip("Разрешить чтение ввода J/L. Для AI-стрелок выключите.")]
    public bool allowPlayerInput = true;

    private float _currentAngle;
    private bool _inputEnabled;
    private bool _isRotating;

    public Vector2 Direction => new Vector2(Mathf.Cos(_currentAngle * Mathf.Deg2Rad), Mathf.Sin(_currentAngle * Mathf.Deg2Rad)).normalized;
    public float Angle => _currentAngle;
    public bool IsRotating => _isRotating;

    public void SetCombatActive(bool active)
    {
        _inputEnabled = active;
        if (hideWhenNotCombat)
        {
            // Если hideWhenNotCombat=true, показываем/скрываем стрелку в зависимости от боевого режима
            gameObject.SetActive(active);
        }
        else
        {
            // Если hideWhenNotCombat=false, стрелка всегда должна быть видна
            // Но если active=true, убеждаемся, что она активирована (на случай, если была деактивирована ранее)
            if (active && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            // Если active=false и hideWhenNotCombat=false, не скрываем стрелку (она остается видимой)
        }
    }

    /// Жёстко ставит угол.
    public void SetAngle(float angleDeg)
    {
        _currentAngle = Mathf.Repeat(angleDeg, 360f);
        transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);
    }

    /// Плавно поворачивает к направлению с ограничением по скорости.
    public void RotateTowards(Vector2 dir, float maxDeltaDeg)
    {
        if (dir == Vector2.zero) return;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(_currentAngle, targetAngle, maxDeltaDeg);
        _isRotating = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, targetAngle)) > 0.01f;
        SetAngle(newAngle);
    }

    void Update()
    {
        if (!_inputEnabled || !allowPlayerInput) return;

        float input = 0f;
        bool j = Input.GetKey(KeyCode.J);
        bool l = Input.GetKey(KeyCode.L);
        if (j) input += 1f; // по часовой
        if (l) input -= 1f; // против часовой

        _isRotating = input != 0f;
        if (_isRotating)
        {
            _currentAngle += input * rotationSpeedDegPerSec * Time.deltaTime;
            _currentAngle = Mathf.Repeat(_currentAngle, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }
    }
}
