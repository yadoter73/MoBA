# MoBA (Unity 6 Horror) — Жёсткие архитектурные правила

## 0. Запрет на догадки и работа с Unity-ассетами
- НИКОГДА не опирайся на старые пересказы проекта. Перед изменением кода всегда прочитай целевой `.cs` файл и связанные зависимости.
- Запрещено переименовывать или удалять `[SerializeField]` поля и менять публичные имена классов без крайней необходимости. Если переименование неизбежно — ОБЯЗАТЕЛЬНО добавляй `[FormerlySerializedAs("oldName")]`.
- Никогда не редактируй `.unity`, `.prefab`, `.asset` вручную (YAML), если задача не требует явного анализа сцены — и даже тогда сначала опиши изменение для Unity Inspector вместо правки YAML наугад.
- Запрещено импортировать `UnityEditor` / `UnityEditor.*` в рантайм-скриптах без `#if UNITY_EDITOR`.

## 1. Структура проекта и Namespaces
- Весь новый игровой код писать в пространстве имён `MoBA.<Module>` (например: `MoBA.Player`, `MoBA.Interaction`, `MoBA.AI`, `MoBA.Narrative`, `MoBA.UI`, `MoBA.VFX`).
- Запрещено добавлять новую игровую логику в `Assets/Scripts/ExampleCharacterScripts` (это слой адаптации KCC). Нельзя расширять `namespace KinematicCharacterController.Examples` игровыми механиками.
- Всякий интерактивный объект обязан реализовывать `IInteractable` (держать его в модуле `Interaction`, не плодить дубликатов интерфейса).

## 2. Dependency Injection (Zenject) — никаких Find* и Service Locator
- На уровне `Level1` (и новых геймплейных сцен) зависимости передаются через Zenject (`MonoInstaller`), а не через `FindAnyObjectByType`, `FindObjectOfType`, `GameObject.Find*` или `static Instance`.
- Запрещено писать пустые блоки `catch (NullReferenceException) { return; }` в инсталлерах и менеджерах: отсутствующая зависимость должна падать явно с понятным `Debug.LogError(..., this)` или проверяться через Zenject Validation.
- Строковые `Id` в `[Inject(Id = "...")]` не плодить. Для новых систем регистрировать конкретные типы/интерфейсы (`IPlayerControlLock`, `IGameStateService`, `IDialogueRunner`), а не сырые `Transform` или `LayerMask` по строкам.
- Для префабов, спавнящихся в рантайме (враги, эффекты, маркеры), использовать исключительно `_diContainer.InstantiatePrefab(...)`, никогда `Object.Instantiate(...)`, если префабу нужны зависимости сцены.

## 3. Асинхронность, таймеры и анимации
- **Единый стандарт:**
  - Анимации UI, дверей, шкафчиков, фейдов, тряски и простых задержек -> **только `PrimeTween`** (`Tween.*`, `Sequence`).
  - Асинхронные сценарии, ожидание событий/условий, жизненный цикл ИИ -> **только `UniTask`** с обязательной передачей `CancellationToken` (`this.GetCancellationTokenOnDestroy()` или связанного `CancellationTokenSource`, который отменяется в `OnDisable`/`OnDestroy` и при смене состояния FSM).
- **ЗАПРЕЩЕНО** писать новые `Coroutine` (`IEnumerator` / `StartCoroutine`) и завязывать новую логику на `UniRx` (если существующий код не требует точечной правки).
- Любой `Tween` или `UniTask` должен учитывать `Time.timeScale == 0` (пауза): для UI/смерти/меню явно указывать `useUnscaledTime: true`, для геймплея — scaled time.

## 4. Игровое состояние: Пауза, Диалог, Кат-сцена, Смерть
- Игрок не должен двигаться, вращать камерой или взаимодействовать с миром во время `Dialogue`, `Cutscene`, `Pause` и `Death`.
- Запрещено из случайных скриптов (врагов, триггеров, кнопок) напрямую отключать `CinemachineBrain`, `PlayerInput`, `ExampleCharacterController` через `Find*`. Любая блокировка управления должна идти через единую точку/сервис состояния игрока (включая `Cursor.lockState` и `Cursor.visible`).
- Все `static` поля и кэши в игровых скриптах (если они неизбежны) обязаны сбрасываться через `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` (поддержка Enter Play Mode без Domain Reload).

## 5. ИИ врагов (FSM)
- `EnemyAI` — только фасад над `NavMeshAgent`, сенсорами и аниматором; вся логика поведения — строго в классах-наследниках `EnemyState`.
- У каждого `EnemyState` строгое соблюдение контракта: `EnterState()` (если есть), `UpdateState()` (вызывается в `FixedUpdate`), `ExitState()` (обязательно отменяет `CancellationTokenSource` и стопает активные `Tween` этого состояния).
- Проверка видимости/дистанции — без аллокаций в `FixedUpdate` (использовать `sqrMagnitude` или `NonAlloc`-физику). Выбор точек патруля — только через `ArenaBounds` + валидацию `NavMesh.SamplePosition` + проверку пути (`NavMeshPath`).
- Кэшировать все `Animator.StringToHash(...)` в `static readonly int` (никаких строк в `Animator.SetBool`/`SetTrigger`).

## 6. Рендеринг, Post-Process и UI
- Перед правкой шейдеров, материалов, света или пост-процессинга ОБЯЗАТЕЛЬНО проверить, какой Render Pipeline активен в текущей сцене/`GraphicsSettings` (в проекте присутствуют пакеты и HDRP, и URP). Не смешивать HDRP Volume API с URP.
- UI-эффекты экрана (страх, фейды, моргание, смерть) делать через выделенные контроллеры поверх `CanvasGroup` / `RawImage` (как `HorrorShaderController`), не засовывая рендер-логику внутрь геймплейных скриптов.
- Новые скрипты не должны превращаться в God-классы (> 250–300 строк): разделяй View (UI/анимации), Trigger/Input и State/Logic.

## 7. Сцена и ввод (Input System)
- Использовать **только новую Unity Input System** (`InputSystem_Actions`). Использование старого `UnityEngine.Input` (`Input.GetKeyDown` и т.п.) запрещено.
- Загрузки сцен выносить в единый механизм с очисткой/фейдом, не хардкодить магические числа индексов сцен в десятках скриптов.
