using System;
using MoBA.SaveSystem;
using UnityEngine;
using Zenject;

namespace MoBA.Narrative
{
    /// <summary>
    /// Конечный автомат и события сюжетного квеста:
    /// TalkToSaya -> FindKey -> GoToGate -> GateOpened -> TalkToMonster -> CollectGoods -> BringToSaya -> Completed.
    /// </summary>
    public sealed class KeyQuestService : IInitializable, IDisposable
    {
        private const string TalkToSayaHint = "Поговорить с Саей";
        private const string FindKeyHint = "Найти ключ";
        private const string EnterHouseHint = "Зайти в дом";
        private const string GoToStoreHint = "Дойти до продуктового";
        private const string CollectGoodsHint = "Взять у продавца: водка, 3 яблока, хлеб";
        private const string BringToSayaHint = "Вернуться к Сае";
        private const string SitAtTableHint = "Сесть за стол";

        private readonly Stone _stone;
        private readonly TextRenaming _textRenaming;
        private readonly LineToSaya _lineToSaya;
        private readonly PointerInmpulse _pointerImpulse;
        private readonly GroceryList _groceryList;
        private readonly KeyQuestConfig _config;

        private bool _isPlayerInsideHouse;
        private bool _monsterOrderCompleted;

        public KeyQuestStage Stage { get; private set; } = KeyQuestStage.Inactive;
        public bool HasGateKey { get; private set; }
        public bool IsQuestStarted => Stage != KeyQuestStage.Inactive;
        public bool CanCollectKey => Stage == KeyQuestStage.FindKey;
        public bool CanTalkToSayaIntro => Stage == KeyQuestStage.TalkToSaya;
        public bool CanTalkToSayaFinal => Stage == KeyQuestStage.BringToSaya;
        public bool CanTalkToMonster => Stage == KeyQuestStage.GateOpened
            || Stage == KeyQuestStage.TalkToMonster
            || Stage == KeyQuestStage.CollectGoods
            || Stage == KeyQuestStage.BringToSaya;
        public bool IsMonsterOrderCompleted => _monsterOrderCompleted;
        public bool AreCounterGoodsUnlocked => Stage == KeyQuestStage.CollectGoods;
        public bool CanSitForDinner => Stage == KeyQuestStage.DinnerWithSaya;

        public event Action<KeyQuestStage> OnStageChanged;
        public event Action OnCounterGoodsUnlocked;

        public KeyQuestService(
            Stone stone,
            TextRenaming textRenaming,
            LineToSaya lineToSaya,
            PointerInmpulse pointerImpulse,
            GroceryList groceryList,
            KeyQuestConfig config)
        {
            _stone = stone;
            _textRenaming = textRenaming;
            _lineToSaya = lineToSaya;
            _pointerImpulse = pointerImpulse;
            _groceryList = groceryList;
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
            if (_groceryList != null)
            {
                _groceryList.OnAllCollected += HandleAllGoodsCollected;
            }

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
            if (_groceryList != null)
            {
                _groceryList.OnAllCollected -= HandleAllGoodsCollected;
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

        public void NotifySayaIntroDialogueFinished()
        {
            if (Stage != KeyQuestStage.TalkToSaya) return;

            if (HasGateKey)
            {
                TransitionToGoToGate();
                return;
            }

            SetStage(KeyQuestStage.FindKey);
            UpdateHouseObjectiveText();
        }

        public void CollectGateKey()
        {
            if (HasGateKey || Stage != KeyQuestStage.FindKey) return;
            HasGateKey = true;
            TransitionToGoToGate();
        }

        public void NotifyGateOpened()
        {
            if (!HasGateKey || Stage != KeyQuestStage.GoToGate) return;

            SetStage(KeyQuestStage.GateOpened);
            ClearGuidance();
            _textRenaming?.ClearQuestObjective(0.6f);
        }

        public void NotifyMonsterTalkStarted()
        {
            if (Stage == KeyQuestStage.GateOpened)
            {
                SetStage(KeyQuestStage.TalkToMonster);
            }
        }

        public void NotifyMonsterOrderDialogueFinished()
        {
            if (_monsterOrderCompleted) return;
            if (Stage != KeyQuestStage.GateOpened && Stage != KeyQuestStage.TalkToMonster) return;

            _monsterOrderCompleted = true;
            _groceryList?.Reset();
            SetStage(KeyQuestStage.CollectGoods);
            _textRenaming?.ShowQuestObjective(CollectGoodsHint);
            OnCounterGoodsUnlocked?.Invoke();
        }

        public bool CollectGroceryItem(GroceryItemType itemType)
        {
            if (Stage != KeyQuestStage.CollectGoods || _groceryList == null) return false;
            return _groceryList.TryCollect(itemType);
        }

        public void NotifySayaFinalDialogueFinished()
        {
            if (Stage != KeyQuestStage.BringToSaya) return;

            ClearGuidance();
            SetStage(KeyQuestStage.DinnerWithSaya);
            _textRenaming?.ShowQuestObjective(SitAtTableHint);
            GameSaveService.SaveCurrentGame(showNotification: true);
        }

        public void NotifyDinnerFinished()
        {
            if (Stage != KeyQuestStage.DinnerWithSaya) return;

            ClearGuidance();
            _textRenaming?.ClearQuestObjective(0.6f);
            SetStage(KeyQuestStage.Completed);
            GameSaveService.SaveCurrentGame(showNotification: false);
        }

        public void RestoreState(KeyQuestStage stage, bool hasGateKey, bool monsterOrderCompleted)
        {
            HasGateKey = hasGateKey;
            _monsterOrderCompleted = monsterOrderCompleted;
            ClearGuidance();
            SetStage(stage);

            switch (stage)
            {
                case KeyQuestStage.TalkToSaya:
                    _textRenaming?.ShowQuestObjective(TalkToSayaHint);
                    break;
                case KeyQuestStage.FindKey:
                    UpdateHouseObjectiveText();
                    break;
                case KeyQuestStage.GoToGate:
                    TransitionToGoToGate();
                    break;
                case KeyQuestStage.CollectGoods:
                    _textRenaming?.ShowQuestObjective(CollectGoodsHint);
                    OnCounterGoodsUnlocked?.Invoke();
                    break;
                case KeyQuestStage.BringToSaya:
                    _textRenaming?.ShowQuestObjective(BringToSayaHint);
                    ActivateGuidance(_config != null ? _config.SayaTarget : null, deactivateDist: 3.0f);
                    break;
                case KeyQuestStage.DinnerWithSaya:
                    _textRenaming?.ShowQuestObjective(SitAtTableHint);
                    break;
                default:
                    _textRenaming?.ClearQuestObjective(0.1f);
                    break;
            }
        }

        private void HandleSayaSeated()
        {
            if (IsQuestStarted) return;

            ClearGuidance();
            SetStage(KeyQuestStage.TalkToSaya);
            _textRenaming?.ShowQuestObjective(TalkToSayaHint);
        }

        private void HandleAllGoodsCollected()
        {
            if (Stage != KeyQuestStage.CollectGoods) return;

            SetStage(KeyQuestStage.BringToSaya);
            _textRenaming?.ShowQuestObjective(BringToSayaHint);
            ActivateGuidance(_config != null ? _config.SayaTarget : null, deactivateDist: 3.0f);
        }

        private void TransitionToGoToGate()
        {
            SetStage(KeyQuestStage.GoToGate);
            _textRenaming?.ShowQuestObjective(GoToStoreHint);
            ActivateGuidance(_config != null ? _config.GateGuidanceTarget : null, deactivateDist: 2.5f);
        }

        private void UpdateHouseObjectiveText()
        {
            if (_textRenaming == null || Stage != KeyQuestStage.FindKey) return;
            _textRenaming.ShowQuestObjective(_isPlayerInsideHouse ? FindKeyHint : EnterHouseHint);
        }

        private void ActivateGuidance(Transform target, float deactivateDist)
        {
            if (target == null)
            {
                Debug.LogError("[KeyQuestService] Не задан целевой Transform для стрелок квеста в RegObj.");
                return;
            }

            if (_pointerImpulse != null)
            {
                _pointerImpulse.SetQuestGuidanceTarget(target, enableWorldPointers: false);
            }

            // Жёлтая стрелка на земле разрешена исключительно в начальной сцене при первом поиске повешенной Саи.
            // Во всех остальных заданиях и фазах квеста она строго выключена.
            if (_lineToSaya != null)
            {
                _lineToSaya.DeactivateImmediate();
            }
        }

        private void ClearGuidance()
        {
            _pointerImpulse?.ClearQuestGuidance();
            _lineToSaya?.DeactivateImmediate();
        }

        private void SetStage(KeyQuestStage nextStage)
        {
            if (Stage == nextStage) return;
            Stage = nextStage;
            OnStageChanged?.Invoke(nextStage);
        }
    }
}
