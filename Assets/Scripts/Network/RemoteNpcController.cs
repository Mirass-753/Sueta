using UnityEngine;

public class RemoteNpcController : MonoBehaviour
{
    [SerializeField]
    private float moveDuration = 0.2f;

    [SerializeField]
    private float snapDistance = 2f;

    private string _npcId;
    private string _state;
    private float _hp;

    private Vector3 _targetPosition;
    private Vector3 _startPosition;
    private float _moveStartTime;
    private bool _hasTarget;

    private EnemyAttack _attack;
    private ArrowController _arrow;

    public string NpcId => _npcId;
    public string State => _state;
    public float Hp => _hp;

    private void Awake()
    {
        _attack = GetComponent<EnemyAttack>();
        _arrow = GetComponent<ArrowController>();

        if (_attack != null && _attack.attackHitbox != null)
        {
            _attack.attackHitbox.enabled = false;
            _attack.attackHitbox = null;
        }

        if (_arrow != null)
        {
            _arrow.allowPlayerInput = false;
            _arrow.SetCombatActive(true);
        }
    }

    public void Initialize(string npcId, float hp)
    {
        _npcId = npcId;
        _hp = hp;
    }

    public void ApplyState(Vector3 position, float hp, string state, Vector2 direction, bool moving)
    {
        _hp = hp;
        if (!string.IsNullOrEmpty(state))
            _state = state;
        SetTargetPosition(position, force: !_hasTarget);

        if (_arrow != null && direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _arrow.SetAngle(angle);
        }
    }

    public void PlayAttack(Vector2 direction)
    {
        if (_arrow != null && direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _arrow.SetAngle(angle);
        }

        if (_attack != null)
        {
            _attack.TryAttack(direction);
        }
    }

    private void SetTargetPosition(Vector3 position, bool force)
    {
        _targetPosition = position;
        _startPosition = transform.position;
        _moveStartTime = Time.time;

        if (force)
        {
            transform.position = position;
        }

        _hasTarget = true;
    }

    private void Update()
    {
        if (!_hasTarget)
            return;

        float distance = Vector3.Distance(transform.position, _targetPosition);
        if (distance > snapDistance)
        {
            transform.position = _targetPosition;
            return;
        }

        if (moveDuration <= 0f)
        {
            transform.position = _targetPosition;
            return;
        }

        float t = Mathf.Clamp01((Time.time - _moveStartTime) / moveDuration);
        transform.position = Vector3.Lerp(_startPosition, _targetPosition, t);
    }
}
