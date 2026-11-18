using System;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Состояние и переходы квеста «Ключ от калитки»: FindKey -> GoToGate -> GateOpened -> TalkToMonster.
    /// </summary>
    public sealed class KeyQuestService : IInitializable, IDisposable
    {
        private const string FindKeyHint = "Найти ключ";
        private const string EnterHouseHint = "Зайти в дом";
        private const string OpenGateHint = "Открыть калитку";

        private readonly Stone _stone;
        private readonly TextRenaming _textRenaming;
        private readonly LineToSaya _lineToSaya;
        private readonly PointerInmpulse _pointerImpulse;
        private readonly KeyQuestConfig _config;

        private bool _isPlayerInsideHouse;

        public KeyQuestStage Stage { get; private set; } = KeyQuestStage.Inactive;
        public bool HasGateKey { get; private set; }
        public bool IsQuestStarted => Stage != KeyQuestStage.Inactive;
        public bool CanTalkToMonster => Stage >= KeyQuestStage.GateOpened;

        public event Action<KeyQuestStage> OnStageChanged;

        public KeyQuestService(
            Stone stone,
            TextRenaming textRenaming,
            LineToSaya lineToSaya,
            PointerInmpulse pointerImpulse,
            KeyQuestConfig config)
        {
            _stone = stone;
            _textRenaming = textRenaming;
            _lineToSaya = lineToSaya;
            _pointerImpulse = pointerImpulse;
            _config = config;
        }

        public void Initialize()
        {
            if (_stone == null)
            {
                Debug.LogError("[KeyQuestService] Зависимость Stone не назначена в RegObj.");
                return;
            }

            _stone.OnSayaSeated += HandleSayaSeated;
            if (_stone.IsSayaSeated)
            {
                HandleSayaSeated();
            }
        }

        public void Dispose()
        {
            if (_stone != null)
            {
                _stone.OnSayaSeated -= HandleSayaSeated;
            }
        }

        public void SetPlayerInsideHouse(bool isInside)
        {
            if (_isPlayerInsideHouse == isInside && Stage != KeyQuestStage.FindKey) return;
            _isPlayerInsideHouse = isInside;

            if (Stage == KeyQuestStage.FindKey)
            {
                UpdateHouseObjectiveText();
            }
        }

        public void CollectGateKey()
        {
            if (HasGateKey) return;
            HasGateKey = true;

            if (Stage < KeyQuestStage.GoToGate)
            {
                TransitionToGoToGate();
            }
        }

        public void NotifyGateOpened()
        {
            if (!HasGateKey || Stage >= KeyQuestStage.GateOpened) return;

            SetStage(KeyQuestStage.GateOpened);
            ClearGateGuidance();
            _textRenaming?.ClearQuestObjective(0.6f);
        }

        public void NotifyMonsterTalkStarted()
        {
            if (!CanTalkToMonster || Stage == KeyQuestStage.TalkToMonster) return;
            SetStage(KeyQuestStage.TalkToMonster);
        }

        private void HandleSayaSeated()
        {
            if (IsQuestStarted) return;

            if (HasGateKey)
            {
                TransitionToGoToGate();
                return;
            }

            SetStage(KeyQuestStage.FindKey);
            UpdateHouseObjectiveText();
        }

        private void TransitionToGoToGate()
        {
            SetStage(KeyQuestStage.GoToGate);
            _textRenaming?.ShowQuestObjective(OpenGateHint);
            ActivateGateGuidance();
        }

        private void UpdateHouseObjectiveText()
        {
            if (_textRenaming == null || Stage != KeyQuestStage.FindKey) return;
            _textRenaming.ShowQuestObjective(_isPlayerInsideHouse ? FindKeyHint : EnterHouseHint);
        }

        private void ActivateGateGuidance()
        {
            Transform gateTarget = _config != null ? _config.GateGuidanceTarget : null;
            if (gateTarget == null)
            {
                Debug.LogError("[KeyQuestService] Не задан GateGuidanceTarget в RegObj.");
                return;
            }

            if (_pointerImpulse != null)
            {
                _pointerImpulse.SetQuestGuidanceTarget(gateTarget, enableWorldPointers: false);
            }

            if (_lineToSaya != null)
            {
                _lineToSaya.SetQuestTarget(gateTarget.gameObject, deactivateDist: 2.5f);
            }
        }

        private void ClearGateGuidance()
        {
            _pointerImpulse?.ClearQuestGuidance();
            _lineToSaya?.ClearQuestTarget(0.5f);
        }

        private void SetStage(KeyQuestStage nextStage)
        {
            if (Stage == nextStage) return;
            Stage = nextStage;
            OnStageChanged?.Invoke(nextStage);
        }
    }
}
