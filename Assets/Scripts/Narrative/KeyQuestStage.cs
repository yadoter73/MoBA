using UnityEngine;

namespace MoBA.Narrative
{
    /// <summary>
    /// Этапы сюжетного квеста: TalkToSaya -> FindKey -> GoToGate -> GateOpened -> TalkToMonster -> CollectGoods -> BringToSaya -> Completed.
    /// </summary>
    public enum KeyQuestStage
    {
        Inactive = 0,
        FindKey = 1,
        GoToGate = 2,
        GateOpened = 3,
        TalkToMonster = 4,
        TalkToSaya = 5,
        CollectGoods = 6,
        BringToSaya = 7,
        Completed = 8,
        DinnerWithSaya = 9
    }

    /// <summary>
    /// Конфигурация ориентиров сцены для квеста (регистрируется в Zenject без строковых Id).
    /// </summary>
    public sealed class KeyQuestConfig
    {
        public Transform GateGuidanceTarget { get; }
        public Transform SayaTarget { get; }

        public KeyQuestConfig(Transform gateGuidanceTarget, Transform sayaTarget)
        {
            GateGuidanceTarget = gateGuidanceTarget;
            SayaTarget = sayaTarget;
        }
    }
}
