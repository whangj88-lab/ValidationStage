using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices
{
    /// <summary>
    /// Validation Stage 장비 전체 (Motorized Stage + Hexapod + Probe). 보통 이 클래스 하나만 만들어 쓴다.
    /// 제어 컨셉: X, Y, Z 는 <see cref="Stage"/>, TX, TY, TZ 는 <see cref="Hexapod"/> 가 담당하며, 축을 받는 함수는 자동으로 나눠 보낸다.
    /// 장비 전용 기능(좌표계, 프로브 스캔/읽기 등)은 <see cref="Stage"/>, <see cref="Hexapod"/>, <see cref="Probe"/> 를 직접 호출한다.
    /// 연결 주소는 <see cref="SettingsDirectory"/> 의 StageHost.txt, HexapodHost.txt 에 저장된다 (StageHost.txt 에는 스테이지 원점 찾는 방향/모터 방향, HexapodHost.txt 에는 헥사포드 소프트웨어 리밋도 함께 저장).
    /// </summary>
    public partial class ValidationSystem
    {
        /// <summary>장비 객체를 만들고 <see cref="SettingsDirectory"/> 의 설정 파일(StageHost.txt, HexapodHost.txt)을 읽는다. 장비 연결은 하지 않는다.</summary>
        public ValidationSystem()
        {
            LoadStageSettings();
            LoadHexapodSettings();
        }

        /// <summary>Motorized Stage (X, Y, Z)</summary>
        public MotorizedStage Stage { get; } = new MotorizedStage();

        /// <summary>Hexapod (TX, TY, TZ)</summary>
        public Hexapod Hexapod { get; } = new Hexapod();

        /// <summary>Solartron 프로브</summary>
        public Probe Probe { get; } = new Probe();

        /// <summary>이 클래스를 통해 호출한 명령 중 마지막으로 실패한 것의 원인 (없으면 null). 성공해도 지워지지 않는다.</summary>
        public DeviceError LastError { get; private set; }

        // 전체 시스템 초기화 등 이 클래스 자체의 로그 (GetLogs 에 합쳐진다)
        private readonly LogBuffer _log = new LogBuffer("System");

        // 전체 시스템 초기화에서 스테이지 영점을 잡은 뒤 프로브 영점까지 기다리는 시간 (진동 안정)
        private const int ProbeSettleMs = 1500;
        private const int StageHomeSettleMs = 1000;

        /// <summary>
        /// 설정 파일 폴더 (StageHost.txt, HexapodHost.txt, ProbeLabels.txt, ProbeTiltCorrection.xml 등).
        /// exe 위치와 무관한 고정 경로라 Debug/Release 등 어느 폴더에서 실행해도 같은 설정을 쓴다. 없으면 저장할 때 만든다.
        /// </summary>
        public const string SettingsDirectory = @"C:\ValidationStage\DoNotTouch";

        #region 연결

        private static readonly string StageHostFile = SettingPath("StageHost.txt");
        private static readonly string HexapodHostFile = SettingPath("HexapodHost.txt");

        /// <summary>마지막으로 연결에 성공한 스테이지 주소 (StageHost.txt). 저장된 적 없으면 컨트롤러 기본 IP "192.168.0.123".</summary>
        public string StageHost { get; private set; } = "192.168.0.123";

        // 스테이지 축별 원점 찾는 방향 (StageHost.txt 의 HomeDirX/Y/Z). 기본은 (−)방향.
        private readonly Dictionary<Axis, HomeDirection> _stageHomeDirections = MotorizedStage.Axes
            .ToDictionary(axis => axis, axis => HomeDirection.Negative);

        // 스테이지 축별 모터 방향 설정값 (StageHost.txt 의 MotorDirX/Y/Z). 파일에 적힌 축만 들어 있고, 없는 축은 검사하지 않는다.
        // 파일을 직접 고쳐서만 정한다 (방향이 실수로 바뀌면 충돌 위험).
        private readonly Dictionary<Axis, MotorDirection> _stageMotorDirections = new Dictionary<Axis, MotorDirection>();

        /// <summary>마지막으로 연결에 성공한 헥사포드 주소 (HexapodHost.txt). 저장된 적 없으면 "127.0.0.1".</summary>
        public string HexapodHost { get; private set; } = "127.0.0.1";

        /// <summary>
        /// 스테이지에 연결한다 (백그라운드 실행 - 응답 없는 주소면 2초 걸림). 성공하면 주소를 StageHost.txt 에 저장한다.
        /// 컨트롤러의 모터 방향이 StageHost.txt 의 설정값과 다르면 경고 로그를 남긴다 (연결은 성공, <see cref="GetStageMotorDirectionMismatches"/> 참고).
        /// </summary>
        /// <param name="host">"192.168.0.123" 또는 "192.168.0.123:5001"</param>
        public async Task<bool> ConnectStageAsync(string host)
        {
            if (!Track(await Task.Run(() => Stage.Connect(host)), Stage.LastError))
            {
                return false;
            }
            StageHost = host;
            SaveStageSettings();
            string problem = await Task.Run(() => MotorDirectionProblem(MotorizedStage.Axes));
            if (problem != null)
            {
                _log.Add(T("경고: ", "Warning: ") + problem);
            }
            return true;
        }

        /// <summary>헥사포드에 연결한다. 성공하면 주소를 HexapodHost.txt 에 저장한다.</summary>
        public bool ConnectHexapod(string host)
        {
            if (!Track(Hexapod.Connect(host), Hexapod.LastError))
            {
                return false;
            }
            HexapodHost = host;
            SaveHexapodSettings();
            return true;
        }

        /// <summary>
        /// 헥사포드 좌표계 하나의 소프트웨어 리밋을 정하고 HexapodHost.txt 에 저장한다 (연결 안 돼 있어도 된다).
        /// 이후 그 좌표계가 활성일 때의 이동/조그가 이 값으로 검사된다. 읽기는 <see cref="Hexapod.GetSoftLimit"/>.
        /// </summary>
        /// <param name="coordSystem">좌표계 이름 (예: "ZERO", "TILTEDCS")</param>
        /// <param name="limit">리밋 (arcmin, Min &lt; Max)</param>
        public bool SetHexapodSoftLimit(string coordSystem, HexapodSoftLimit limit)
        {
            if (!Track(Hexapod.SetSoftLimit(coordSystem, limit), Hexapod.LastError))
            {
                return false;
            }
            SaveHexapodSettings();
            return true;
        }

        /// <summary>스테이지 연결 해제 (모터 전원은 유지).</summary>
        public void DisconnectStage()
        {
            Stage.Disconnect();
        }

        /// <summary>헥사포드 연결 해제.</summary>
        public void DisconnectHexapod()
        {
            Hexapod.Disconnect();
        }

        /// <summary>세 장비 모두 연결 해제 (프로그램 종료 시).</summary>
        public void DisconnectAll()
        {
            DisconnectStage();
            DisconnectHexapod();
            if (Probe.IsConnected)
            {
                Probe.Disconnect();
            }
        }

        #endregion

        #region 조회

        /// <summary>연결된 장비의 축 상태 (스테이지 X,Y,Z 다음 헥사포드 TX,TY,TZ). 미연결 장비의 축은 빠진다.</summary>
        public AxisStatus[] GetAxisStatuses()
        {
            var statuses = new List<AxisStatus>();
            statuses.AddRange(Stage.GetAxisStatuses() ?? new AxisStatus[0]);
            statuses.AddRange(Hexapod.GetAxisStatuses() ?? new AxisStatus[0]);
            return statuses.ToArray();
        }

        /// <summary>
        /// 세 장비의 로그를 시간순으로 한 번에 꺼낸다 (꺼낸 로그는 지워진다).
        /// 이 함수와 장비별 GetLogs 를 함께 쓰면 서로 로그를 나눠 가져가므로 한쪽만 쓴다.
        /// </summary>
        public List<LogEntry> GetLogs()
        {
            return _log.Drain()
                .Concat(Stage.GetLogs())
                .Concat(Hexapod.GetLogs())
                .Concat(Probe.GetLogs())
                .OrderBy(entry => entry.Time)
                .ToList();
        }

        #endregion

        #region 이동 / 정지 / 속도

        /// <summary>
        /// 속도 단계를 연결된 장비에 함께 적용한다 (연결 안 된 장비는 건너뜀).
        /// 스테이지: <see cref="MotorizedStage.SetSpeedLevel"/>, 헥사포드: <see cref="Hexapod.SetSpeedLevel"/>.
        /// </summary>
        /// <returns>장비별 적용 결과 (로그용). 예: "스테이지 적용, 헥사포드 미연결"</returns>
        public string SetSpeedLevel(SpeedLevel level)
        {
            string stageResult = T("스테이지 미연결", "stage not connected");
            if (Stage.IsConnected)
            {
                stageResult = Track(Stage.SetSpeedLevel(level), Stage.LastError)
                    ? T("스테이지 적용", "stage applied") : T("스테이지 실패", "stage failed");
            }

            string hexapodResult = T("헥사포드 미연결", "hexapod not connected");
            if (Hexapod.IsConnected)
            {
                hexapodResult = Track(Hexapod.SetSpeedLevel(level), Hexapod.LastError)
                    ? T("헥사포드 적용", "hexapod applied") : T("헥사포드 실패", "hexapod failed");
            }

            return stageResult + ", " + hexapodResult;
        }

        /// <summary>한 축을 절대 위치로 이동하고 끝날 때까지 기다린다 (X,Y,Z: µm / TX,TY,TZ: arcmin).</summary>
        public async Task<bool> MoveAbsAsync(Axis axis, double position)
        {
            if (IsStageAxis(axis))
            {
                return Track(await Stage.MoveAsync(new[] { axis }, new[] { position }, true), Stage.LastError);
            }
            return Track(await Hexapod.MoveAsync(new[] { axis }, new[] { position }, true), Hexapod.LastError);
        }

        /// <summary>조그 시작 (누르고 있는 동안). dir = true 가 표시값 증가 방향. <see cref="JogStop"/> 으로 멈춘다.</summary>
        public bool JogRun(Axis axis, bool dir)
        {
            return IsStageAxis(axis)
                ? Track(Stage.JogRun(axis, dir), Stage.LastError)
                : Track(Hexapod.JogRun(axis, dir), Hexapod.LastError);
        }

        /// <summary>조그 정지 (감속 정지).</summary>
        public bool JogStop(Axis axis)
        {
            return IsStageAxis(axis)
                ? Track(Stage.JogStop(axis), Stage.LastError)
                : Track(Hexapod.JogStop(axis), Hexapod.LastError);
        }

        /// <summary>한 축만 감속 정지. 스테이지: MMT "S". 헥사포드: HLT (비상 정지 STP 와 다름).</summary>
        public async Task<bool> StopAxisAsync(Axis axis)
        {
            RequestTiltStop();
            if (IsStageAxis(axis))
            {
                return Track(await Stage.StopAsync(new[] { axis }), Stage.LastError);
            }
            if (!Hexapod.IsConnected)
            {
                return false;
            }
            return Track(await Hexapod.StopAsync(new[] { axis }), Hexapod.LastError);
        }

        /// <summary>
        /// 전체 감속 정지 (연결된 장비만). 스테이지: 세 축 "S", 헥사포드: 세 축 HLT.
        /// 비상 정지(<see cref="EmergencyStopAllAsync"/>)와의 차이는 헥사포드뿐이다 - MMT 는 정지 명령이 "S" 하나뿐.
        /// 전체 시스템 초기화 중이면 초기화도 중단된다.
        /// </summary>
        public async Task StopAllAsync()
        {
            RequestSystemInitStop();
            if (Stage.IsConnected)
            {
                Track(await Stage.StopAsync(MotorizedStage.Axes), Stage.LastError);
            }
            if (Hexapod.IsConnected)
            {
                Track(await Hexapod.StopAsync(Hexapod.Axes), Hexapod.LastError);
            }
        }

        /// <summary>전체 비상 정지 (연결된 장비만). 스테이지: "@s", 헥사포드: STP. 전체 시스템 초기화 중이면 초기화도 중단된다.</summary>
        public async Task EmergencyStopAllAsync()
        {
            RequestSystemInitStop();
            if (Stage.IsConnected)
            {
                Track(await Stage.StopEmergencyAsync(), Stage.LastError);
            }
            if (Hexapod.IsConnected)
            {
                Track(await Hexapod.StopEmergencyAsync(), Hexapod.LastError);
            }
        }

        #endregion

        #region 원점(Home)

        // 전체 시스템 초기화 (HomeAndZeroProbesAsync, 참고 프로젝트의 "비전으로 마크 0 위치 → 프로브 영점" 대신):
        //   헥사포드 TX,TY,TZ = 0 → 스테이지 기계 원점 찾기 → 저장된 측정 원점(기계 좌표)으로 이동 → 그 위치를 스테이지 0 (p0) → 프로브 영점
        // 스테이지(MMT): 컨트롤러 자체 원점을 쓴다.
        // - 기계 원점 찾기: 리밋 방향 홈 서칭 (HM0 = (−)방향, HM1 = (+)방향)
        // - 측정 원점: 기계 원점 기준 좌표를 StageHost.txt 에 저장 (SaveStageMeasureOriginFromCurrent)
        // - 원점으로 이동: 컨트롤러 좌표 0 으로 절대 이동 (전체 시스템 초기화 이후에는 측정 원점)
        // 헥사포드는 측정 원점을 저장하지 않고 항상 TX, TY, TZ = 0 (활성 좌표계) 이다.

        // 측정 원점 (스테이지 기계 좌표 µm, X,Y,Z). 저장된 적 없으면 null.
        private double[] _stageMeasureOrigin;

        /// <summary>
        /// 전체 시스템 초기화: 헥사포드 TX, TY, TZ 를 0 (활성 좌표계)으로 → 스테이지 기계 원점 찾기 → 저장된 측정 원점으로 이동 →
        /// 그 위치를 스테이지 0 으로 설정 → 잠깐 기다린 뒤 프로브 전체 영점. 끝나면 스테이지/헥사포드 0 = 프로브 0 위치가 된다.
        /// 측정 원점이 저장된 적 없으면 스테이지 기계 원점 찾기까지만 하고 true 를 돌려준다 (그 뒤 측정 원점으로 쓸 위치로 옮겨
        /// <see cref="SaveStageMeasureOriginFromCurrent"/>). 축이 크게 움직이므로 주변 간섭(프로브 접촉 포함)을 확인한 뒤 호출한다.
        /// 시작 전 확인: 세 장비 연결, 스테이지 모터 방향(<see cref="GetStageMotorDirectionMismatches"/>), 헥사포드 레퍼런스 - 하나라도 아니면 아무것도 움직이지 않는다.
        /// 실행 중 진행 단계는 <see cref="CurrentSystemInitStep"/> 으로 조회한다. 실행 중에 <see cref="EmergencyStopAllAsync"/> 나
        /// <see cref="StopAllAsync"/> 를 부르면 그 자리에서 중단하고(다음 단계로 넘어가지 않음) false 를 돌려주며,
        /// 이때 <see cref="SystemInitAborted"/> 가 true 다. 이미 실행 중이면 바로 false.
        /// </summary>
        public async Task<bool> HomeAndZeroProbesAsync(HomeDirection x = HomeDirection.Negative,
            HomeDirection y = HomeDirection.Negative, HomeDirection z = HomeDirection.Negative)
        {
            lock (_sequenceLock)
            {
                if (_systemInitRunning)
                {
                    return Fail(T("전체 시스템 초기화가 이미 실행 중입니다", "System init is already running"));
                }
                if (_tiltRunning)
                {
                    return Fail(T("프로브 기울기 측정 중에는 전체 시스템 초기화를 할 수 없습니다", "System init is not allowed during probe tilt measurement"));
                }
                _systemInitRunning = true;
            }
            _systemInitVersions = null;
            _systemInitStopRequested = false;
            SystemInitAborted = false;
            try
            {
                return await RunSystemInitAsync(x, y, z);
            }
            finally
            {
                _systemInitStep = (int)SystemInitStep.None;
                _systemInitRunning = false;
            }
        }

        /// <summary>전체 시스템 초기화(<see cref="HomeAndZeroProbesAsync"/>) 실행 중 여부.</summary>
        public bool IsSystemInitRunning => _systemInitRunning;

        /// <summary>전체 시스템 초기화의 현재 단계 (실행 중이 아니면 <see cref="SystemInitStep.None"/>).</summary>
        public SystemInitStep CurrentSystemInitStep => (SystemInitStep)_systemInitStep;

        /// <summary>마지막 전체 시스템 초기화가 정지/비상 정지 명령으로 중단됐으면 true (다음 실행을 시작하면 false 로 돌아간다).</summary>
        public bool SystemInitAborted { get; private set; }

        // 전체 시스템 초기화와 프로브 기울기 측정은 동시에 실행하지 않는다.
        private readonly object _sequenceLock = new object();

        private volatile bool _systemInitRunning;
        private volatile bool _systemInitStopRequested;
        private volatile int _systemInitStep;

        // 정지/비상 정지가 눌리면 초기화가 다음 단계로 넘어가지 않게 표시한다 (이동 중이던 단계는 장비 쪽에서 실패로 끝난다).
        private void RequestSystemInitStop()
        {
            RequestTiltStop();
            if (_systemInitRunning)
            {
                _systemInitStopRequested = true;
            }
        }

        // 정지 요청이 들어왔으면 중단 처리 (LastError + 로그) 후 true.
        private bool CheckSystemInitAborted()
        {
            if (!_systemInitStopRequested)
            {
                return false;
            }
            SystemInitAborted = true;
            Fail(T("전체 시스템 초기화가 정지 명령으로 중단되었습니다", "System init was interrupted by a stop command"));
            return true;
        }

        // 단계 하나의 결과: 정지 요청으로 실패했으면 그 원인을 "중단"으로 바꿔 남긴다.
        private bool StepOk(bool ok)
        {
            if (CheckSystemInitAborted())
            {
                return false;
            }
            return ok;
        }

        // 단계 사이 대기. 기다리는 동안 정지 요청이 들어오면 바로 중단한다 (false).
        private async Task<bool> SystemInitDelayAsync(int milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds)
            {
                if (CheckSystemInitAborted())
                {
                    return false;
                }
                await Task.Delay(Math.Min(50, Math.Max(1, milliseconds - (int)watch.ElapsedMilliseconds)));
            }
            return !CheckSystemInitAborted();
        }

        private void EnterStep(SystemInitStep step, string message)
        {
            _systemInitStep = (int)step;
            _log.Add(message);
        }

        private async Task<bool> RunSystemInitAsync(HomeDirection x, HomeDirection y, HomeDirection z)
        {
            if (!Stage.IsConnected)
            {
                return Fail(T("스테이지가 연결되어 있지 않습니다", "Stage not connected"));
            }
            if (!CheckMotorDirections(MotorizedStage.Axes))
            {
                return false;
            }
            double[] origin = _stageMeasureOrigin;
            if (origin == null)
            {
                EnterStep(SystemInitStep.StageHoming,
                    T("측정 원점이 저장되지 않아 스테이지 기계 원점 찾기만 합니다", "No measurement origin saved - stage homing only"));
                if (!StepOk(await HomeStageAsync(x, y, z)))
                {
                    return false;
                }
                _log.Add(T("기계 원점 찾기 완료. 측정 원점으로 쓸 위치로 옮긴 뒤 측정 원점을 저장하세요",
                    "Homing done. Move to the position to use as the measurement origin, then save it"));
                return true;
            }

            if (!Hexapod.IsConnected)
            {
                return Fail(T("헥사포드가 연결되어 있지 않습니다", "Hexapod not connected"));
            }
            if (!Probe.IsConnected)
            {
                return Fail(T("프로브가 연결되어 있지 않습니다", "Probe not connected"));
            }
            if (Hexapod.IsReferenced() != true)
            {
                return Fail(T("헥사포드 레퍼런스가 잡혀 있지 않습니다. 레퍼런스를 먼저 하세요", "Hexapod is not referenced. Reference it first"));
            }

            EnterStep(SystemInitStep.HexapodToZero, T("전체 시스템 초기화 1/4: 헥사포드 TX, TY, TZ → 0", "System init 1/4: hexapod TX, TY, TZ -> 0"));
            if (!StepOk(await MoveHexapodToZeroAsync()))
            {
                return false;
            }
            EnterStep(SystemInitStep.StageHoming, T("전체 시스템 초기화 2/4: 스테이지 기계 원점 찾기", "System init 2/4: stage homing"));
            if (!StepOk(await HomeStageAsync(x, y, z)) || !await SystemInitDelayAsync(StageHomeSettleMs))
            {
                return false;
            }

            EnterStep(SystemInitStep.StageToOrigin, string.Format(CultureInfo.InvariantCulture,
                T("전체 시스템 초기화 3/4: 스테이지 측정 원점으로 이동 (X {0:0.000}, Y {1:0.000}, Z {2:0.000} µm) 후 0 으로 설정",
                  "System init 3/4: stage to measurement origin (X {0:0.000}, Y {1:0.000}, Z {2:0.000} um), then set as 0"),
                origin[0], origin[1], origin[2]));
            if (!StepOk(Track(await Stage.MoveAsync(MotorizedStage.Axes, origin, true), Stage.LastError))
                || !await SystemInitDelayAsync(StageHomeSettleMs))
            {
                return false;
            }
            if (!StepOk(Track(Stage.SetZero(MotorizedStage.Axes), Stage.LastError)))
            {
                return false;
            }

            EnterStep(SystemInitStep.ProbeZero, T("전체 시스템 초기화 4/4: 프로브 전체 영점", "System init 4/4: zero all probes"));
            if (!await SystemInitDelayAsync(ProbeSettleMs) || !StepOk(Track(Probe.ZeroAll(), Probe.LastError)))
            {
                return false;
            }
            _systemInitVersions = CurrentReferenceVersions();
            _log.Add(T("전체 시스템 초기화 완료", "System init done"));
            return true;
        }

        /// <summary>
        /// 스테이지 현재 위치를 측정 원점으로 StageHost.txt 에 저장한다 (기계 원점 기준 좌표, 스테이지 X, Y, Z 만 - 헥사포드는 항상 0).
        /// 연결 후 기계 원점 찾기(<see cref="HomeAndZeroProbesAsync"/>, <see cref="HomeStageAsync"/>)를 한 뒤에만 된다.
        /// 스테이지가 멈춰 있어야 한다. 스테이지 좌표는 바꾸지 않는다.
        /// </summary>
        public bool SaveStageMeasureOriginFromCurrent()
        {
            if (!Stage.IsConnected)
            {
                return Fail(T("스테이지가 연결되어 있지 않습니다", "Stage not connected"));
            }
            if ((Stage.GetAxisStatuses() ?? new AxisStatus[0]).Any(status => status.IsMoving))
            {
                return Fail(T("스테이지가 이동 중입니다", "Stage is moving"));
            }
            double[] machine = Stage.GetMachinePositions();
            if (machine == null)
            {
                return Fail(T("연결 후 기계 원점 찾기를 하지 않은 축이 있어 저장할 수 없습니다. 원점 찾기를 먼저 하세요",
                    "Some axes have not been homed since connecting. Run homing first"));
            }
            _stageMeasureOrigin = machine;
            SaveStageSettings();
            _log.Add(string.Format(CultureInfo.InvariantCulture,
                T("측정 원점 저장: X {0:0.000}, Y {1:0.000}, Z {2:0.000} µm (기계 원점 기준)",
                  "Measurement origin saved: X {0:0.000}, Y {1:0.000}, Z {2:0.000} um (from machine home)"),
                machine[0], machine[1], machine[2]));
            return true;
        }

        /// <summary>저장된 측정 원점 (기계 원점 기준 µm, X,Y,Z). 저장된 적 없으면 null.</summary>
        public double[] GetStageMeasureOrigin()
        {
            return (double[])_stageMeasureOrigin?.Clone();
        }

        /// <summary>스테이지 X, Y, Z 기계 원점 찾기 (축별로 지정한 리밋 방향으로 이동, 기본은 (−)방향). 주변 간섭을 확인한 뒤 호출한다. 모터 방향이 설정 파일과 다른 축이 있으면(<see cref="GetStageMotorDirectionMismatches"/>) 움직이지 않고 false.</summary>
        public async Task<bool> HomeStageAsync(HomeDirection x = HomeDirection.Negative,
            HomeDirection y = HomeDirection.Negative, HomeDirection z = HomeDirection.Negative)
        {
            if (!CheckMotorDirections(MotorizedStage.Axes))
            {
                return false;
            }
            return Track(await Stage.HomeAsync(MotorizedStage.Axes, new[] { x, y, z }), Stage.LastError);
        }

        /// <summary>
        /// 스테이지 한 축(X, Y, Z 중 하나)만 기계 원점 찾기. 주변 간섭을 확인한 뒤 호출한다.
        /// 그 축의 모터 방향이 설정 파일과 다르면(<see cref="GetStageMotorDirectionMismatches"/>) 움직이지 않고 false.
        /// </summary>
        public async Task<bool> HomeStageAxisAsync(Axis axis, HomeDirection direction = HomeDirection.Negative)
        {
            if (!CheckMotorDirections(new[] { axis }))
            {
                return false;
            }
            return Track(await Stage.HomeAsync(new[] { axis }, new[] { direction }), Stage.LastError);
        }

        /// <summary>
        /// 저장된 스테이지 원점 찾는 방향 (StageHost.txt). 저장된 적 없으면 (−)방향.
        /// 원점 찾기에 자동으로 쓰이지는 않으므로 <see cref="HomeStageAsync"/> 등에 넘겨서 쓴다.
        /// </summary>
        /// <param name="axis">X, Y, Z 중 하나 (그 외 축은 (−)방향을 돌려준다)</param>
        public HomeDirection GetStageHomeDirection(Axis axis)
        {
            return _stageHomeDirections.TryGetValue(axis, out HomeDirection direction) ? direction : HomeDirection.Negative;
        }

        /// <summary>스테이지 원점 찾는 방향을 정하고 StageHost.txt 에 저장한다 (장비로는 보내지 않는다).</summary>
        /// <param name="axis">X, Y, Z 중 하나</param>
        /// <param name="direction">찾는 방향</param>
        public bool SetStageHomeDirection(Axis axis, HomeDirection direction)
        {
            if (!_stageHomeDirections.ContainsKey(axis))
            {
                LastError = new DeviceError(0, T($"{axis} 축은 스테이지 축이 아닙니다", $"{axis} is not a stage axis"));
                return false;
            }
            _stageHomeDirections[axis] = direction;
            SaveStageSettings();
            return true;
        }

        /// <summary>
        /// StageHost.txt 에 적힌 축의 모터 방향 설정값 (MotorDirX/Y/Z). 적혀 있지 않으면 null.
        /// 컨트롤러의 실제 값은 <see cref="MotorizedStage.GetMotorDirection"/>.
        /// </summary>
        /// <param name="axis">X, Y, Z 중 하나 (그 외 축은 null)</param>
        public MotorDirection? GetStageMotorDirection(Axis axis)
        {
            return _stageMotorDirections.TryGetValue(axis, out MotorDirection direction) ? direction : (MotorDirection?)null;
        }

        /// <summary>
        /// 컨트롤러의 모터 방향(REV)이 StageHost.txt 의 설정값(MotorDirX/Y/Z)과 다른 축 (예: 새 컨트롤러로 바꾸고 방향을 적용하지 않은 경우).
        /// 설정값이 없는 축은 보지 않는다. 모두 같으면 빈 배열, 미연결이거나 읽지 못하면 null.
        /// 다른 축이 있으면 기계 원점 찾기와 전체 시스템 초기화가 거부되므로 <see cref="MotorizedStage.SetMotorDirections"/> 로 설정값을 적용한다.
        /// </summary>
        public Axis[] GetStageMotorDirectionMismatches()
        {
            return FindMotorDirectionMismatches(MotorizedStage.Axes);
        }

        // 설정값이 있는 축만 컨트롤러에서 읽어 비교한다. 하나라도 읽지 못하면 null (원인은 Stage.LastError).
        private Axis[] FindMotorDirectionMismatches(Axis[] axes)
        {
            if (!Stage.IsConnected)
            {
                return null;
            }
            var mismatches = new List<Axis>();
            foreach (Axis axis in axes.Where(a => _stageMotorDirections.ContainsKey(a)))
            {
                MotorDirection? actual = Stage.GetMotorDirection(axis);
                if (actual == null)
                {
                    return null;
                }
                if (actual != _stageMotorDirections[axis])
                {
                    mismatches.Add(axis);
                }
            }
            return mismatches.ToArray();
        }

        // 모터 방향이 설정과 다르거나 확인하지 못하면 그 내용, 문제없으면 null.
        private string MotorDirectionProblem(Axis[] axes)
        {
            Axis[] mismatches = FindMotorDirectionMismatches(axes);
            if (mismatches == null)
            {
                return T("스테이지 모터 방향을 확인하지 못했습니다", "Could not check stage motor direction");
            }
            if (mismatches.Length == 0)
            {
                return null;
            }
            string list = string.Join(", ", mismatches.Select(axis => $"{axis}={_stageMotorDirections[axis]}"));
            return T($"컨트롤러의 모터 방향이 설정(StageHost.txt: {list})과 다릅니다. 반대로 움직이므로 원점 찾기를 할 수 없습니다 - 모터 방향을 먼저 적용하십시오",
                $"Motor direction in the controller differs from the settings (StageHost.txt: {list}). The axis moves the opposite way, so homing is blocked - apply the motor direction first");
        }

        // 원점 찾기 전 확인. 확인하지 못해도 막는다 (반대 방향으로 홈 서칭하면 충돌 위험). 미연결은 장비 쪽에서 실패 처리한다.
        private bool CheckMotorDirections(Axis[] axes)
        {
            if (!Stage.IsConnected)
            {
                return true;
            }
            string problem = MotorDirectionProblem(axes);
            return problem == null || Fail(problem);
        }

        /// <summary>스테이지 X, Y, Z 를 원점(0)으로 이동한다.</summary>
        public async Task<bool> MoveStageToZeroAsync()
        {
            return Track(await Stage.MoveAsync(MotorizedStage.Axes, new double[MotorizedStage.Axes.Length], true), Stage.LastError);
        }

        // 헥사포드(PI): 원점은 컨트롤러가 정한다.
        // - 기계 원점: 레퍼런스 (Hexapod.ReferenceAsync, FRF)
        // - 좌표 원점: 활성 좌표계의 0 (옮기려면 Hexapod.DefineCoordSystem + ActivateCoordSystem)
        // - 원점으로 이동: 활성 좌표계 기준 TX, TY, TZ = 0 으로 절대 이동

        /// <summary>헥사포드 TX, TY, TZ 를 0 (활성 좌표계 기준)으로 이동한다. 헥사포드의 X, Y, Z 는 그대로 둔다.</summary>
        public async Task<bool> MoveHexapodToZeroAsync()
        {
            return Track(await Hexapod.MoveAsync(Hexapod.Axes, new double[Hexapod.Axes.Length], true), Hexapod.LastError);
        }

        #endregion

        #region private

        private static bool IsStageAxis(Axis axis)
        {
            return MotorizedStage.Axes.Contains(axis);
        }

        /// <summary>장비 명령 결과를 그대로 돌려주고, 실패면 그 장비의 LastError 를 이 클래스의 LastError 로도 남긴다.</summary>
        private bool Track(bool ok, DeviceError deviceError)
        {
            if (!ok)
            {
                LastError = deviceError;
            }
            return ok;
        }

        /// <summary>이 클래스에서 난 실패를 LastError + 로그로 남기고 false 를 돌려준다.</summary>
        private bool Fail(string message)
        {
            LastError = new DeviceError(0, message);
            _log.Add(message);
            return false;
        }

        private static string SettingPath(string fileName)
        {
            return Path.Combine(SettingsDirectory, fileName);
        }

        // 설정 파일 형식: 한 줄에 "키=값". 예전 형식(주소 한 줄만, '=' 없음)은 키 "Host" 로 읽는다 - 다음 저장 때 새 형식으로 바뀐다.
        // 파일이 없거나 읽지 못하면 빈 목록 (기본값으로 시작).
        private static List<KeyValuePair<string, string>> ReadSettings(string filePath)
        {
            var settings = new List<KeyValuePair<string, string>>();
            try
            {
                if (!File.Exists(filePath))
                {
                    return settings;
                }
                foreach (string rawLine in File.ReadAllLines(filePath))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    settings.Add(eq < 0
                        ? new KeyValuePair<string, string>("Host", line)
                        : new KeyValuePair<string, string>(line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
                }
            }
            catch { }
            return settings;
        }

        private static void WriteSettings(string filePath, IEnumerable<string> lines)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.WriteAllLines(filePath, lines);
            }
            catch { }
        }

        // StageHost.txt:
        //   Host=192.168.0.123
        //   HomeDirX=Negative   (HomeDirY, HomeDirZ 도 같은 형식, Negative / Positive)
        //   MotorDirX=Ccw       (MotorDirY, MotorDirZ 도 같은 형식, Ccw = REV0 / Cw = REV1, 적은 축만 검사)
        //   MeasureOrigin=x,y,z (µm, 기계 원점 기준, 저장했을 때만)
        private void LoadStageSettings()
        {
            string[] origin = null;
            foreach (var kv in ReadSettings(StageHostFile))
            {
                if (kv.Key == "Host" && kv.Value.Length > 0)
                {
                    StageHost = kv.Value;
                }
                else if (kv.Key == "MeasureOrigin")
                {
                    origin = kv.Value.Split(',');
                }
                else if (kv.Key.StartsWith("HomeDir") && Enum.TryParse(kv.Key.Substring("HomeDir".Length), out Axis axis)
                    && _stageHomeDirections.ContainsKey(axis)
                    && Enum.TryParse(kv.Value, out HomeDirection direction) && Enum.IsDefined(typeof(HomeDirection), direction))
                {
                    _stageHomeDirections[axis] = direction;
                }
                else if (kv.Key.StartsWith("MotorDir"))
                {
                    if (Enum.TryParse(kv.Key.Substring("MotorDir".Length), out Axis motorAxis) && IsStageAxis(motorAxis)
                        && Enum.TryParse(kv.Value, true, out MotorDirection motorDirection) && Enum.IsDefined(typeof(MotorDirection), motorDirection))
                    {
                        _stageMotorDirections[motorAxis] = motorDirection;
                    }
                    else
                    {
                        _log.Add(T($"경고: StageHost.txt 의 모터 방향 설정을 읽지 못해 무시합니다 ({kv.Key}={kv.Value}) - Ccw 또는 Cw 로 적으십시오",
                            $"Warning: ignoring invalid motor direction in StageHost.txt ({kv.Key}={kv.Value}) - use Ccw or Cw"));
                    }
                }
            }

            // 값이 잘못되면 저장 안 된 것으로 본다.
            var values = new double[MotorizedStage.Axes.Length];
            if (origin != null && origin.Length == values.Length
                && origin.Select((text, i) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])).All(ok => ok))
            {
                _stageMeasureOrigin = values;
            }
        }

        private void SaveStageSettings()
        {
            var lines = new List<string> { "Host=" + StageHost };
            lines.AddRange(_stageHomeDirections.Select(kv => $"HomeDir{kv.Key}={kv.Value}"));
            lines.AddRange(MotorizedStage.Axes.Where(axis => _stageMotorDirections.ContainsKey(axis))
                .Select(axis => $"MotorDir{axis}={_stageMotorDirections[axis]}"));
            if (_stageMeasureOrigin != null)
            {
                lines.Add("MeasureOrigin=" + string.Join(",", _stageMeasureOrigin.Select(v => v.ToString("R", CultureInfo.InvariantCulture))));            }
            WriteSettings(StageHostFile, lines);
        }

        // HexapodHost.txt (소프트웨어 리밋은 값을 정한 좌표계만, 단위 arcmin):
        //   Host=169.254.3.106
        //   SoftLimit.TILTEDCS.Enabled=1
        //   SoftLimit.TILTEDCS.TX=-300,300   (TY, TZ 도 같은 형식, 하한,상한)
        // 항목이 빠지거나 잘못된 좌표계는 그 항목만 기본값 (사용, ±300 arcmin).
        private void LoadHexapodSettings()
        {
            var limits = new Dictionary<string, HexapodSoftLimit>();
            foreach (var kv in ReadSettings(HexapodHostFile))
            {
                string[] keyParts = kv.Key.Split('.');
                if (kv.Key == "Host" && kv.Value.Length > 0)
                {
                    HexapodHost = kv.Value;
                }
                else if (keyParts.Length == 3 && keyParts[0] == "SoftLimit")
                {
                    if (!limits.TryGetValue(keyParts[1], out HexapodSoftLimit limit))
                    {
                        limit = limits[keyParts[1]] = new HexapodSoftLimit();
                    }
                    int index = Enum.TryParse(keyParts[2], out Axis axis) ? Array.IndexOf(Hexapod.Axes, axis) : -1;
                    string[] range = kv.Value.Split(',');
                    if (keyParts[2] == "Enabled")
                    {
                        limit.Enabled = kv.Value != "0";
                    }
                    else if (index >= 0 && range.Length == 2
                        && double.TryParse(range[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double min)
                        && double.TryParse(range[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double max)
                        && min < max)
                    {
                        limit.Min[index] = min;
                        limit.Max[index] = max;
                    }
                }
            }
            foreach (var kv in limits)
            {
                Hexapod.SetSoftLimit(kv.Key, kv.Value);   // 이름이 잘못된 항목은 버린다
            }
        }

        private void SaveHexapodSettings()
        {
            var lines = new List<string> { "Host=" + HexapodHost };
            foreach (var kv in Hexapod.GetStoredSoftLimits().OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                lines.Add($"SoftLimit.{kv.Key}.Enabled={(kv.Value.Enabled ? 1 : 0)}");
                for (int i = 0; i < Hexapod.Axes.Length; i++)
                {
                    lines.Add(string.Format(CultureInfo.InvariantCulture, "SoftLimit.{0}.{1}={2},{3}",
                        kv.Key, Hexapod.Axes[i], kv.Value.Min[i], kv.Value.Max[i]));
                }
            }
            WriteSettings(HexapodHostFile, lines);
        }

        #endregion
    }
}
