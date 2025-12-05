using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class PlayerController : MonoBehaviour
{
    // ========= КЛАВИАТУРА =========

    private Vector2 GetKeyboardDirection()
    {
        // Явные диагонали только через Q/E/Z/C
        if (Input.GetKey(KeyCode.Q)) return new Vector2(-1f, 1f);
        if (Input.GetKey(KeyCode.E)) return new Vector2(1f, 1f);
        if (Input.GetKey(KeyCode.Z)) return new Vector2(-1f, -1f);
        if (Input.GetKey(KeyCode.C)) return new Vector2(1f, -1f);

        float x = 0f;
        float y = 0f;

        // Горизонталь (A/D или стрелки влево/вправо)
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            x = -1f;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            x = 1f;

        // Вертикаль (W/S или стрелки вверх/вниз)
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            y = 1f;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            y = -1f;

        // Если зажаты и горизонталь, и вертикаль одновременно (например, W+D),
        // убираем горизонталь, чтобы не было диагонали от двух клавиш.
        // Приоритет ВЕРТИКАЛИ.
        if (x != 0f && y != 0f)
        {
            x = 0f;
        }

        if (x == 0f && y == 0f)
            return Vector2.zero;

        return new Vector2(x, y);
    }

    private void HandleKeyboardMovement(Vector2 inputDirection)
    {
        // не рвём шаг на клаве, даём дойти до клетки

        if (!_isMoving)
        {
            if (inputDirection != Vector2.zero)
            {
                FaceByVector(inputDirection);

                if (!_keyHeld)
                {
                    _lastDirection = inputDirection;
                    StartCoroutine(MoveToDirection(inputDirection));
                    _keyHeld = true;
                    _holdTimer = 0f;
                }
                else
                {
                    _holdTimer += Time.deltaTime;
                    if (_holdTimer >= continuousMoveDelay)
                    {
                        _lastDirection = inputDirection;
                        StartCoroutine(MoveToDirection(inputDirection));
                        _holdTimer = 0f;
                    }
                }
            }
            else
            {
                _keyHeld = false;
                _holdTimer = 0f;
                _staminaSystem?.StopRunning();
            }
        }
        else
        {
            // пока идём — просто запоминаем последнее направление,
            // чтобы после окончания шага продолжить уже в нём
            if (inputDirection != Vector2.zero)
                _lastDirection = inputDirection;
        }
    }

    // ========= МЫШЬ =========

    private void HandleMouseClickTarget()
    {
        if (_camera == null) return;

        if (EventSystem.current != null)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            if (hits.Count > 0)
                return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = _camera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;

            _clickTargetCell = WorldToCell(mouseWorld);
            _movingByClick = true;

            // новый клик — прерываем текущий шаг, чтобы кот сразу сменил цель
            InterruptMovement();
        }
    }

    private void HandleClickMovement()
    {
        if (_isMoving) return;

        _currentCell = WorldToCell(transform.position);

        if (_currentCell == _clickTargetCell)
        {
            _movingByClick = false;
            return;
        }

        int dx = _clickTargetCell.x - _currentCell.x;
        int dy = _clickTargetCell.y - _currentCell.y;

        Vector2 stepDir = Vector2.zero;
        if (dx > 0) stepDir.x = 1f; else if (dx < 0) stepDir.x = -1f;
        if (dy > 0) stepDir.y = 1f; else if (dy < 0) stepDir.y = -1f;

        if (stepDir == Vector2.zero)
        {
            _movingByClick = false;
            return;
        }

        FaceByVector(stepDir);
        _lastDirection = stepDir;
        StartCoroutine(MoveToDirection(stepDir));
    }
}
