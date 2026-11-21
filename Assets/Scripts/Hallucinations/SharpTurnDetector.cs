using UnityEngine;

namespace MoBA.Hallucinations
{
    /// <summary>
    /// Отслеживает резкий разворот камеры игрока по горизонтали (yaw) на заданный угол
    /// в окне времени [minDuration, maxDuration] без аллокаций в Update.
    /// </summary>
    public sealed class SharpTurnDetector
    {
        private struct YawSample
        {
            public float Time;
            public float Yaw;
        }

        private const int Capacity = 64;
        private readonly YawSample[] _samples = new YawSample[Capacity];
        private int _head;
        private int _count;

        private readonly float _angleThreshold;
        private readonly float _minDuration;
        private readonly float _maxDuration;

        public SharpTurnDetector(float angleThresholdDegrees = 130f, float minDurationSeconds = 0.2f, float maxDurationSeconds = 0.5f)
        {
            _angleThreshold = Mathf.Clamp(angleThresholdDegrees, 45f, 179f);
            _minDuration = Mathf.Max(0.05f, minDurationSeconds);
            _maxDuration = Mathf.Max(_minDuration + 0.05f, maxDurationSeconds);
        }

        public void Reset()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Записывает текущий угол поворота головы и возвращает true, если зафиксирован резкий разворот.
        /// </summary>
        public bool SampleAndCheck(float currentYaw, float currentTime)
        {
            // Проверяем существующие сэмплы в окне [minDuration, maxDuration] до добавления нового
            bool triggered = false;
            float maxWindow = _maxDuration;
            float minWindow = _minDuration;

            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - 1 - i + Capacity) % Capacity;
                float dt = currentTime - _samples[idx].Time;

                if (dt > maxWindow)
                {
                    break;
                }

                // Учитываем и мгновенные очень быстрые рывки (< minWindow) только когда dt дошёл хотя бы до minWindow * 0.5,
                // чтобы не срабатывать от единичного кадрового телепорта камеры.
                if (dt < minWindow * 0.5f)
                {
                    continue;
                }

                float delta = Mathf.Abs(Mathf.DeltaAngle(_samples[idx].Yaw, currentYaw));
                if (delta >= _angleThreshold)
                {
                    triggered = true;
                    break;
                }
            }

            _samples[_head] = new YawSample { Time = currentTime, Yaw = currentYaw };
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;

            if (triggered)
            {
                Reset();
                return true;
            }

            return false;
        }
    }
}
