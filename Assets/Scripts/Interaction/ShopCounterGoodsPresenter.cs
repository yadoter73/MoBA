using System.Collections.Generic;
using MoBA.Narrative;
using UnityEngine;
using Zenject;

namespace MoBA.Interaction
{
    /// <summary>
    /// Скрывает квестовые товары на прилавке до конца первого диалога с монстром и проявляет их на этапе CollectGoods.
    /// </summary>
    public class ShopCounterGoodsPresenter : MonoBehaviour
    {
        [SerializeField] private List<GroceryPickup> _counterGoods = new List<GroceryPickup>();

        [Inject(Optional = true)] private KeyQuestService _questService;

        private bool _revealed;

        private void Awake()
        {
            for (int i = 0; i < _counterGoods.Count; i++)
            {
                if (_counterGoods[i] != null)
                {
                    _counterGoods[i].HideImmediate();
                }
            }
        }

        private void OnEnable()
        {
            if (_questService != null)
            {
                _questService.OnCounterGoodsUnlocked += HandleGoodsUnlocked;
                if (_questService.AreCounterGoodsUnlocked && !_revealed)
                {
                    HandleGoodsUnlocked();
                }
            }
        }

        private void OnDisable()
        {
            if (_questService != null)
            {
                _questService.OnCounterGoodsUnlocked -= HandleGoodsUnlocked;
            }
        }

        private void HandleGoodsUnlocked()
        {
            if (_revealed) return;
            _revealed = true;

            for (int i = 0; i < _counterGoods.Count; i++)
            {
                if (_counterGoods[i] != null)
                {
                    _counterGoods[i].RevealAnimated(i * 0.08f);
                }
            }
        }
    }
}
