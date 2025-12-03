using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 20f; // Скорость траты в секунду
    public float staminaRegenRate = 15f; // Скорость восстановления в секунду
    public float regenDelay = 1f; // Задержка перед восстановлением
    
    private float _currentStamina;
    private float _regenTimer;
    private bool _isRunning;

    public float CurrentStamina => _currentStamina;
    public bool CanRun => _currentStamina > 0;

    void Start()
    {
        _currentStamina = maxStamina;
    }

    void Update()
    {
        if (_isRunning)
        {
            // Тратим выносливость при беге
            _currentStamina -= staminaDrainRate * Time.deltaTime;
            _currentStamina = Mathf.Max(_currentStamina, 0);
            _regenTimer = 0f;
        }
        else
        {
            // Восстанавливаем выносливость
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= regenDelay && _currentStamina < maxStamina)
            {
                _currentStamina += staminaRegenRate * Time.deltaTime;
                _currentStamina = Mathf.Min(_currentStamina, maxStamina);
            }
        }
    }

    public void StartRunning()
    {
        if (_currentStamina > 0)
            _isRunning = true;
    }

    public void StopRunning()
    {
        _isRunning = false;
    }
}