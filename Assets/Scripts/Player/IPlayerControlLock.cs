using System;

namespace MoBA.Player
{
    /// <summary>
    /// Единый контракт временной блокировки управления игроком (движение, камера, взаимодействие, курсор).
    /// </summary>
    public interface IPlayerControlLock
    {
        bool IsLocked { get; }
        bool IsCursorUnlocked { get; }
        IDisposable AcquireLock(bool unlockCursor = true);
    }
}
