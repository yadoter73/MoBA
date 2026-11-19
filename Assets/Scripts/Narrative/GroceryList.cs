using System;
using System.Collections.Generic;

namespace MoBA.Narrative
{
    public enum GroceryItemType
    {
        Vodka = 0,
        Apple = 1,
        Bread = 2
    }

    /// <summary>
    /// Отдельный инвентарь и счётчик продуктов заказа Саи (водка ×1, яблоки ×3, хлеб ×1).
    /// </summary>
    public sealed class GroceryList
    {
        private readonly Dictionary<GroceryItemType, int> _required = new()
        {
            { GroceryItemType.Vodka, 1 },
            { GroceryItemType.Apple, 3 },
            { GroceryItemType.Bread, 1 }
        };

        private readonly Dictionary<GroceryItemType, int> _collected = new()
        {
            { GroceryItemType.Vodka, 0 },
            { GroceryItemType.Apple, 0 },
            { GroceryItemType.Bread, 0 }
        };

        public int TotalRequired => 5;
        public int CollectedTotal { get; private set; }
        public bool IsAllCollected => CollectedTotal >= TotalRequired;

        public event Action<int, int> OnProgressChanged;
        public event Action OnAllCollected;

        public int GetRequired(GroceryItemType type)
        {
            return _required.TryGetValue(type, out int req) ? req : 0;
        }

        public int GetCollected(GroceryItemType type)
        {
            return _collected.TryGetValue(type, out int count) ? count : 0;
        }

        public bool TryCollect(GroceryItemType type)
        {
            int req = GetRequired(type);
            int cur = GetCollected(type);
            if (cur >= req || IsAllCollected) return false;

            _collected[type] = cur + 1;
            CollectedTotal++;
            OnProgressChanged?.Invoke(CollectedTotal, TotalRequired);

            if (IsAllCollected)
            {
                OnAllCollected?.Invoke();
            }
            return true;
        }

        public void Reset()
        {
            _collected[GroceryItemType.Vodka] = 0;
            _collected[GroceryItemType.Apple] = 0;
            _collected[GroceryItemType.Bread] = 0;
            CollectedTotal = 0;
            OnProgressChanged?.Invoke(0, TotalRequired);
        }
    }
}
