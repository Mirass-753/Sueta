using System.Collections;
using UnityEngine;

public partial class PlayerController : MonoBehaviour
{
    // ========= ДВИЖЕНИЕ / АНИМАЦИЯ =========

    public void ResetMovementState()
    {
        StopAllCoroutines();
        _isMoving = false;
        _keyHeld = false;
        _holdTimer = 0f;
        _movingByClick = false;
        SetIdleSprite();
        _staminaSystem?.StopRunning();
        SyncPositionToGrid();
    }

    public void FaceByVector(Vector2 dir)
    {
        if (_spriteRenderer == null) return;
        if (dir.x > 0.01f) _spriteRenderer.flipX = true;
        else if (dir.x < -0.01f) _spriteRenderer.flipX = false;
    }

    private void SetIdleSprite()
    {
        if (_spriteRenderer != null && idleSprite != null)
            _spriteRenderer.sprite = idleSprite;
    }

    private void SetMovingSprite()
    {
        if (_spriteRenderer != null && movingSprite != null)
            _spriteRenderer.sprite = movingSprite;
    }

    /// <summary>
    /// Прерывает текущее движение — используем только для клика мышкой.
    /// Для клавы мы теперь НЕ рвём шаг, чтобы дойти до клетки.
    /// </summary>
    private void InterruptMovement()
    {
        if (!_isMoving) return;

        StopAllCoroutines();
        _isMoving = false;
        _staminaSystem?.StopRunning();

        // чтобы не было диагональных скачков — привязываем к ближайшей клетке
        SyncPositionToGrid();
    }

    private bool IsBlockedByOtherPlayer(Vector2 targetWorldPos)
    {
        if (bodyCollider == null)
            return false;

        // берём размеры коллайдера в мире, чуть уменьшаем, чтобы не было ложных срабатываний
        Vector2 size = bodyCollider.bounds.size * 0.9f;

        Collider2D hit = Physics2D.OverlapBox(
            targetWorldPos,
            size,
            0f,
            playerLayer
        );

        // hit может быть мы сами
        if (hit == null) return false;

        return hit.gameObject != gameObject;
    }

    private IEnumerator MoveToDirection(Vector2 direction)
    {
        _isMoving = true;

        SetMovingSprite();

        if (_wantsToRun && _staminaSystem != null)
            _staminaSystem.StartRunning();

        FaceByVector(direction);

        Vector2Int cellStep = new Vector2Int(
            direction.x > 0 ? 1 : (direction.x < 0 ? -1 : 0),
            direction.y > 0 ? 1 : (direction.y < 0 ? -1 : 0)
        );

        Vector2Int nextCell = _currentCell + cellStep;
        Vector2 targetPos = CellToWorld(nextCell);

        // 1) проверка на других игроков
        if (IsBlockedByOtherPlayer(targetPos))
        {
            Debug.Log("[MOVE] blocked by other player at " + targetPos);
            _isMoving = false;
            _staminaSystem?.StopRunning();
            SetIdleSprite();
            yield break;
        }

        // 2) проверка по гриду (стены, препятствия и т.п.)
        bool canMove = occupancyManager == null || occupancyManager.TryMove(_currentCell, nextCell);
        if (!canMove)
        {
            _isMoving = false;
            _staminaSystem?.StopRunning();
            SetIdleSprite();
            yield break;
        }

        Vector2 startPosition = transform.position;
        float elapsedTime = 0f;
        float baseMoveDuration = _wantsToRun ? moveDuration / 1.5f : moveDuration;
        float currentMoveDuration = baseMoveDuration * direction.magnitude;

        while (elapsedTime < currentMoveDuration)
        {
            float t = elapsedTime / currentMoveDuration;
            transform.position = Vector2.Lerp(startPosition, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        _currentCell = nextCell;

        // это последний шаг для клика мышкой?
        bool isFinalClickStep = _movingByClick && (_currentCell == _clickTargetCell);

        _isMoving = false;

        // если не держим клавишу, то:
        // - при обычном движении по клавиатуре всегда возвращаем idle;
        // - при движении по клику — только на последнем шаге.
        if (!_keyHeld && (!_movingByClick || isFinalClickStep))
        {
            SetIdleSprite();
        }

        _staminaSystem?.StopRunning();

        // автоповтор для зажатой клавиши
        if (_keyHeld && _lastDirection == direction)
        {
            yield return new WaitForSeconds(0.05f);
            if (!_isMoving && _keyHeld)
                StartCoroutine(MoveToDirection(direction));
        }
    }
}
