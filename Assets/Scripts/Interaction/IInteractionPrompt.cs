namespace MoBA.Interaction
{
    /// <summary>
    /// Тип подсказки при наведении на интерактивный объект или NPC.
    /// </summary>
    public enum InteractionPromptType
    {
        Interact = 0,
        Talk = 1
    }

    /// <summary>
    /// Дополнительный контракт подсказки взаимодействия поверх IInteractable или диалоговых NPC.
    /// </summary>
    public interface IInteractionPrompt
    {
        InteractionPromptType PromptType { get; }
    }

    /// <summary>
    /// Подсказка с динамическим условием доступности (скрывает UI, пока объект/NPC недоступен).
    /// </summary>
    public interface IConditionalInteractionPrompt : IInteractionPrompt
    {
        bool CanInteractNow { get; }
    }
}
