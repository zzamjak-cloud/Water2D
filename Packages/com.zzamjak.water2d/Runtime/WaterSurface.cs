using UnityEngine;

namespace CAT.Water2D
{
    /// <summary>
    /// 물 표면 시뮬레이션 파라미터. 호스트(Water2D / UIWater2D)가 매 프레임 값으로 전달한다.
    /// struct + in 전달이므로 할당이 발생하지 않는다.
    /// </summary>
    public struct WaterSimParams
    {
        // 기하 (파장·퍼짐 폭을 로컬 단위로 해석하기 위해 필요)
        public float Width;

        // 스프링
        public float SpringConstant;
        public float Damping;
        public float Spread;

        // 지속 출렁임 (진행 파형)
        public bool AmbientEnabled;
        public float AmbientIntensity;
        public float WaveAmplitude;
        public float WaveLength;
        public float WaveSpeed;
        public int WaveOctaves;
        public float WaveOctaveFalloff;
        public float WaveOctaveSpeedRatio;
        public float WaveRandomness;
        public float WaveNoiseScale;
        public float WaveNoiseSpeed;

        // 지속 출렁임 (랜덤 임펄스)
        public bool RandomImpulseEnabled;
        public float ImpulseIntervalMin;
        public float ImpulseIntervalMax;
        public float ImpulseForceMin;
        public float ImpulseForceMax;
        public float ImpulseSpread;
    }

    /// <summary>
    /// 물 표면 시뮬레이션 코어. 렌더링 방식(MeshRenderer / CanvasRenderer)과 무관하며
    /// 위치는 0~1 정규 좌표(t)로 다룬다.
    ///
    /// [구성]
    /// - 스프링 파동: Hooke's Law + damping + 좌·우 이웃 전파(2-pass). 고정 스텝 1/60s.
    ///   충돌·Splash·랜덤 임펄스로만 깨어나고, 정지하면 자동 슬립한다.
    /// - 진행 파형: 스프링과 별개로 표면 높이에 가산되는 해석적 변위 (감쇠에 먹히지 않음).
    ///
    /// [할당]
    /// - Tick 경로에서 new/LINQ 없음. 배열은 Configure(pointCount) 변경 시에만 재할당.
    /// </summary>
    public sealed class WaterSurface
    {
        #region 상태

        private WaterPoint[] _points = System.Array.Empty<WaterPoint>();
        private float[] _leftDeltas = System.Array.Empty<float>();
        private float[] _rightDeltas = System.Array.Empty<float>();

        // 고정 스텝 누적
        private float _simAccumulator;
        private const float SimStepSeconds = 1f / 60f;
        private const int MaxStepsPerFrame = 8;

        // 스프링 슬립
        private bool _springAwake;
        private const float SleepEpsilon = 0.0004f;

        // 진행 파형 (해석적: 캐시 배열 없이 임의 x 에서 평가)
        private float _ambientTime;
        private bool _ambientWasEnabled;

        // 랜덤 임펄스
        private float _impulseTimer;
        private uint _randomState = 1u;
        /// <summary>중첩 가능한 진행 파형 옥타브 상한.</summary>
        public const int MaxOctaves = 4;
        private readonly float[] _octavePhases = new float[MaxOctaves];

        // 렌더 갱신 요청
        private bool _flushPending;

        private const float Tau = Mathf.PI * 2f;

        #endregion

        #region 공개 상태

        /// <summary>표면 포인트 수.</summary>
        public int PointCount => _points.Length;

        /// <summary>스프링 시뮬 동작 여부. false 면 스텝 비용이 0.</summary>
        public bool SpringAwake => _springAwake;

        /// <summary>이번 프레임에 메시(정점)를 갱신해야 하는지. Tick 이 갱신한다.</summary>
        public bool NeedsMeshUpdate { get; private set; }

        #endregion

        #region 구성

        /// <summary>
        /// 포인트 수를 보장한다. 변경된 경우에만 재할당하며, 시드로 파형 위상·난수를 초기화한다.
        /// </summary>
        /// <returns>재할당이 일어났으면 true</returns>
        public bool Configure(int pointCount, int seed)
        {
            int n = Mathf.Max(2, pointCount);
            if (_points.Length == n) return false;

            _points = new WaterPoint[n];
            _leftDeltas = new float[n];
            _rightDeltas = new float[n];

            InitRandom(seed);
            _springAwake = false;
            _simAccumulator = 0f;
            _flushPending = true;
            return true;
        }

        /// <summary>시드 변경 시 파형 위상과 임펄스 타이머를 다시 만든다.</summary>
        public void ReseedRandom(int seed) => InitRandom(seed);

        /// <summary>다음 Tick 에서 메시를 1회 갱신하도록 요청 (설정 변경·재빌드 후).</summary>
        public void RequestMeshUpdate() => _flushPending = true;

        #endregion

        #region 프레임 갱신

        /// <summary>
        /// 1프레임 진행. 진행 파형은 해석적이라 프레임 dt 로, 스프링은 고정 스텝으로 처리한다.
        /// </summary>
        public void Tick(float deltaTime, in WaterSimParams p)
        {
            if (_points.Length < 2) { NeedsMeshUpdate = false; return; }

            float dt = Mathf.Min(Mathf.Max(0f, deltaTime), 0.1f);

            if (dt > 0f)
            {
                TickAmbient(dt, in p);

                if (_springAwake)
                {
                    _simAccumulator += dt;
                    int steps = 0;
                    while (_simAccumulator >= SimStepSeconds && steps < MaxStepsPerFrame)
                    {
                        _simAccumulator -= SimStepSeconds;
                        SingleStep(in p);
                        steps++;
                    }
                    if (steps >= MaxStepsPerFrame) _simAccumulator = 0f;

                    UpdateSleepState();
                }
                else
                {
                    _simAccumulator = 0f;
                }
            }

            bool moving = p.AmbientEnabled || _springAwake;
            NeedsMeshUpdate = moving || _flushPending;
            _flushPending = moving;
        }

        #endregion

        #region 파동 주입

        /// <summary>단일 포인트에 impulse 주입. t 는 0~1 정규 좌표.</summary>
        public void Splash(float t01, float force)
        {
            if (_points.Length == 0) return;
            _points[IndexOf(t01)].Velocity += force;
            WakeSpring();
        }

        /// <summary>
        /// 코사인 감쇠로 폭을 가진 impulse 주입.
        /// </summary>
        /// <param name="spread01">영향 범위를 0~1 정규 폭으로. 0 이면 단일 포인트.</param>
        public void SplashArea(float t01, float force, float spread01)
        {
            int n = _points.Length;
            if (n == 0) return;

            WakeSpring();

            if (spread01 <= 0f || n < 2)
            {
                _points[IndexOf(t01)].Velocity += force;
                return;
            }

            int radius = Mathf.Max(1, Mathf.RoundToInt(spread01 * (n - 1)));
            int center = IndexOf(t01);

            for (int offset = -radius; offset <= radius; offset++)
            {
                int idx = center + offset;
                if (idx < 0 || idx >= n) continue;

                // 0.5*(1+cos(pi*t)) : 중심 1, 경계 0
                float falloff = 0.5f * (1f + Mathf.Cos(Mathf.PI * (Mathf.Abs(offset) / (float)radius)));
                _points[idx].Velocity += force * falloff;
            }
        }

        /// <summary>표면을 평형·정지 상태로 리셋하고 슬립시킨다.</summary>
        /// <param name="clearAmbient">진행 파형 변위까지 0 으로 정리 (에디터 프리뷰 종료 등)</param>
        public void Reset(bool clearAmbient = false)
        {
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i].Height = _points[i].TargetHeight;
                _points[i].Velocity = 0f;
            }
            _springAwake = false;
            _simAccumulator = 0f;
            _flushPending = false;

            if (clearAmbient)
            {
                _ambientTime = 0f;
                _ambientWasEnabled = false;
            }

            NeedsMeshUpdate = true;
        }

        #endregion

        #region 샘플링

        /// <summary>인덱스의 스프링 높이 (진행 파형 제외).</summary>
        public float SpringHeightAt(int index)
        {
            if (index < 0 || index >= _points.Length) return 0f;
            return _points[index].Height;
        }

        /// <summary>
        /// 0~1 정규 좌표의 스프링 높이를 Catmull-Rom 스플라인으로 보간한다.
        /// 포인트를 모두 통과하는 C1 연속 곡선이므로, 적은 시뮬 포인트로도 표면이 각지지 않는다.
        /// </summary>
        public float SampleSpringHeight(float t01)
        {
            int n = _points.Length;
            if (n < 2) return 0f;
            if (n == 2) return Mathf.Lerp(_points[0].Height, _points[1].Height, Mathf.Clamp01(t01));

            float f = Mathf.Clamp01(t01) * (n - 1);
            int i1 = Mathf.Min(Mathf.FloorToInt(f), n - 2);
            float u = f - i1;

            // 양 끝은 자기 자신을 복제해 클램프 (끝단 튐 방지)
            float p0 = _points[Mathf.Max(i1 - 1, 0)].Height;
            float p1 = _points[i1].Height;
            float p2 = _points[i1 + 1].Height;
            float p3 = _points[Mathf.Min(i1 + 2, n - 1)].Height;

            float u2 = u * u;
            float u3 = u2 * u;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        /// <summary>
        /// 최종 표면 높이 = 스플라인 보간된 스프링 높이 + 해석적 진행 파형.
        /// </summary>
        /// <param name="t01">0~1 정규 좌표</param>
        /// <param name="x">진행 파형 평가용 로컬 X (중심 0 기준)</param>
        public float SampleHeight(float t01, float x, in WaterSimParams p)
        {
            return SampleSpringHeight(t01) + EvaluateAmbient(x, in p);
        }

        /// <summary>에디터 기즈모용 읽기 전용 노출.</summary>
        public WaterPoint[] GetPointsUnsafe() => _points;

        #endregion

        #region 내부 — 스프링

        /// <summary>한 번의 고정 스텝. Hooke's Law + damping + 이웃 전파(2-pass).</summary>
        private void SingleStep(in WaterSimParams p)
        {
            int n = _points.Length;

            for (int i = 0; i < n; i++)
            {
                float x = _points[i].Height - _points[i].TargetHeight;
                float force = -p.SpringConstant * x - p.Damping * _points[i].Velocity;
                _points[i].Velocity += force;
                _points[i].Height += _points[i].Velocity;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (i > 0)
                    {
                        _leftDeltas[i] = p.Spread * (_points[i].Height - _points[i - 1].Height);
                        _points[i - 1].Velocity += _leftDeltas[i];
                    }
                    if (i < n - 1)
                    {
                        _rightDeltas[i] = p.Spread * (_points[i].Height - _points[i + 1].Height);
                        _points[i + 1].Velocity += _rightDeltas[i];
                    }
                }
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) _points[i - 1].Height += _leftDeltas[i];
                    if (i < n - 1) _points[i + 1].Height += _rightDeltas[i];
                }
            }
        }

        private void WakeSpring()
        {
            _springAwake = true;
            _flushPending = true;
        }

        private void UpdateSleepState()
        {
            int n = _points.Length;
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(_points[i].Height - _points[i].TargetHeight) > SleepEpsilon) return;
                if (Mathf.Abs(_points[i].Velocity) > SleepEpsilon) return;
            }

            for (int i = 0; i < n; i++)
            {
                _points[i].Height = _points[i].TargetHeight;
                _points[i].Velocity = 0f;
            }
            _springAwake = false;
            _simAccumulator = 0f;
            _flushPending = true;
        }

        #endregion

        #region 내부 — 진행 파형 / 랜덤 임펄스

        private void TickAmbient(float dt, in WaterSimParams p)
        {
            if (!p.AmbientEnabled)
            {
                if (_ambientWasEnabled)
                {
                    _ambientWasEnabled = false;
                    _flushPending = true; // 파형 변위 제거를 1회 반영
                }
                return;
            }

            _ambientTime += dt;
            _ambientWasEnabled = true;

            if (!p.RandomImpulseEnabled) return;

            _impulseTimer -= dt;
            if (_impulseTimer > 0f) return;

            ScheduleNextImpulse(in p);

            float t01 = NextRandom01();
            float force = Mathf.Lerp(p.ImpulseForceMin, p.ImpulseForceMax, NextRandom01()) * p.AmbientIntensity;
            float width = Mathf.Max(0.0001f, p.Width);
            SplashArea(t01, force, Mathf.Clamp01(p.ImpulseSpread / width));
        }

        /// <summary>
        /// 임의의 로컬 X 에서 진행 파형 변위를 해석적으로 평가한다.
        /// 시뮬 포인트 해상도와 무관하므로, 포인트를 줄이고 렌더 정점을 늘려도 파형이 정확하다.
        /// </summary>
        public float EvaluateAmbient(float x, in WaterSimParams p)
        {
            if (!p.AmbientEnabled) return 0f;

            float baseAmp = p.WaveAmplitude * p.AmbientIntensity;
            int octaves = Mathf.Clamp(p.WaveOctaves, 1, MaxOctaves);

            float sum = 0f;
            float amp = baseAmp;
            float wl = Mathf.Max(0.01f, p.WaveLength);
            float spd = p.WaveSpeed;

            for (int o = 0; o < octaves; o++)
            {
                // 위상 = 공간항 + 시간항(진행) + 옥타브별 랜덤 오프셋
                float phase = Tau * (x / wl) + Tau * (spd / wl) * _ambientTime + _octavePhases[o];
                sum += amp * Mathf.Sin(phase);

                amp *= p.WaveOctaveFalloff;
                wl *= 0.5f;
                spd *= p.WaveOctaveSpeedRatio;
            }

            float noiseAmp = baseAmp * p.WaveRandomness;
            if (noiseAmp > 0f)
            {
                // Mathf.PerlinNoise 는 native interop 호출로 1회당 수 us 가 들어 정점 단위 평가에 부적합.
                // 동일한 특성(저주파 부드러운 난수장)을 관리 코드 value noise 로 대체해 interop 을 제거한다.
                float noise = SmoothNoise(x * p.WaveNoiseScale + _octavePhases[0],
                                          _ambientTime * p.WaveNoiseSpeed) * 2f - 1f;
                sum += noiseAmp * noise;
            }

            return sum;
        }

        /// <summary>정수 격자 해시 (0~1). 결정론적이며 할당·interop 없음.</summary>
        private static float Hash01(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000u;
            }
        }

        /// <summary>2D value noise (0~1). smoothstep 보간으로 C1 에 가까운 부드러운 난수장.</summary>
        private static float SmoothNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float xf = x - xi;
            float yf = y - yi;

            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash01(xi, yi);
            float b = Hash01(xi + 1, yi);
            float c = Hash01(xi, yi + 1);
            float d = Hash01(xi + 1, yi + 1);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private void ScheduleNextImpulse(in WaterSimParams p)
        {
            float min = Mathf.Max(0.02f, Mathf.Min(p.ImpulseIntervalMin, p.ImpulseIntervalMax));
            float max = Mathf.Max(min, p.ImpulseIntervalMax);
            _impulseTimer = Mathf.Lerp(min, max, NextRandom01());
        }

        private void InitRandom(int seed)
        {
            unchecked
            {
                uint s = (uint)(seed * 747796405 + 2891336453);
                _randomState = s == 0u ? 1u : s;
            }

            for (int o = 0; o < MaxOctaves; o++) _octavePhases[o] = NextRandom01() * Tau;

            // 기본 간격으로 초기 타이머 설정 (첫 Tick 에서 파라미터 기준으로 재계산됨)
            _impulseTimer = NextRandom01();
        }

        /// <summary>xorshift32 기반 0~1 난수. UnityEngine.Random 전역 상태를 건드리지 않는다.</summary>
        private float NextRandom01()
        {
            unchecked
            {
                _randomState ^= _randomState << 13;
                _randomState ^= _randomState >> 17;
                _randomState ^= _randomState << 5;
                return (_randomState & 0xFFFFFFu) / (float)0x1000000u;
            }
        }

        private int IndexOf(float t01)
        {
            int n = _points.Length;
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t01) * (n - 1)), 0, n - 1);
        }

        #endregion
    }
}
