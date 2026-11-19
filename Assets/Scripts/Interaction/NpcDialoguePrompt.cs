using UnityEngine;

namespace MoBA.Interaction
{
    /// <summary>
    /// Помечает живого персонажа/NPC с диалогом типом подсказки Talk («Press E to talk»).
    /// </summary>
    public class NpcDialoguePrompt : MonoBehaviour, IInteractionPrompt
    {
        public InteractionPromptType PromptType => InteractionPromptType.Talk;
    }
}
