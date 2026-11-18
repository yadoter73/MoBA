using UnityEngine;

namespace MoBA.Narrative
{
    /// <summary>
    /// Этапы квеста «Ключ от калитки».
    /// </summary>
    public enum KeyQuestStage
    {
        Inactive = 0,
        FindKey = 1,
        GoToGate = 2,
        GateOpened = 3,
        TalkToMonster = 4
    }

    /// <summary>
    /// Конфигурация ссылок сцены для квеста ключа (регистрируется в Zenject без строковых Id).
    /// </summary>
    public sealed class KeyQuestConfig
    {
        public Transform GateGuidanceTarget { get; }

        public KeyQuestConfig(Transform gateGuidanceTarget)
        {
            GateGuidanceTarget = gateGuidanceTarget;
        }
    }
}
