namespace MoBA.Player
{
    /// <summary>
    /// Контракт сервиса стамины и бега игрока (без UI).
    /// </summary>
    public interface IStaminaService
    {
        float NormalizedStamina { get; }
        bool IsSprinting { get; }
        bool IsExhausted { get; }
        float MaxSprintDuration { get; }
        float SprintSpeedMultiplier { get; }
    }
}
