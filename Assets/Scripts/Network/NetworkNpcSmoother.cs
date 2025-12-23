using UnityEngine;

public class NetworkNpcSmoother : MonoBehaviour
{
    [SerializeField]
    private float followSpeed = 8f;

    [SerializeField]
    private float snapDistance = 2f;

    private Vector3 _targetPosition;
    private bool _hasTarget;

    public void SetTarget(Vector3 position, bool force = false)
    {
        _targetPosition = position;

        if (!_hasTarget || force)
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

        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            followSpeed * Time.deltaTime
        );
    }
}
