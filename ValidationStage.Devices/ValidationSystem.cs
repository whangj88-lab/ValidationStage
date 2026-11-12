using System;
using System.Collections.Generic;
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
    /// 연결 주소는 exe 폴더의 StageHost.txt, HexapodHost.txt 에 저장된다 (StageHost.txt 에는 스테이지 원점 찾는 방향도 함께 저장).
    /// </summary>
    public class ValidationSystem
    {
        /// <summary>장비 객체를 만들고 exe 폴더의 설정 파일(StageHost.txt)을 읽는다. 장비 연결은 하지 않는다.</summary>
        public ValidationSystem()
        {
            LoadStageSettings();
        }

        /// <summary>Motorized Stage (X, Y, Z)</summary>
        public MotorizedStage Stage { get; } = new MotorizedStage();

        /// <summary>Hexapod (TX, TY, TZ)</summary>
        public Hexapod Hexapod { get; } = new Hexapod();

        /// <summary>Solartron 프로브</summary>
        public Probe Probe { get; } = new Probe();

        /// <summary>이 클래스를 통해 호출한 명령 중 마지막으로 실패한 것의 원인 (없으면 null). 성공해도 지워지지 않는다.</summary>
        public DeviceError LastError { get; private set; }

        #region 연결

        // 설정 파일은 모두 exe 폴더에 둔다 (실행 위치/바로가기 시작 위치와 무관하게 같은 파일을 쓰도록).
        private static readonly string StageHostFile = SettingPath("StageHost.txt");
        private static readonly string HexapodHostFile = SettingPath("HexapodHost.txt");

        /// <summary>마지막으로 연결에 성공한 스테이지 주소 (StageHost.txt). 저장된 적 없으면 컨트롤러 기본 IP "192.168.0.123".</summary>
        public string StageHost { get; private set; } = "192.168.0.123";

        // 스테이지 축별 원점 찾는 방향 (StageHost.txt 의 HomeDirX/Y/Z). 기본은 (−)방향.
        private readonly Dictionary<Axis, HomeDirection> _stageHomeDirections = MotorizedStage.Axes
            .ToDictionary(axis => axis, axis => HomeDirection.Negative);

        /// <summary>마지막으로 연결에 성공한 헥사포드 주소 (HexapodHost.txt). 저장된 적 없으면 "127.0.0.1".</summary>
        public string HexapodHost { get; private set; } = LoadConnectionSetting(HexapodHostFile, "127.0.0.1");

        /// <summary>
        /// 스테이지에 연결한다 (백그라운드 실행 - 응답 없는 주소면 2초 걸림). 성공하면 주소를 StageHost.txt 에 저장한다.
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
            SaveConnectionSetting(HexapodHostFile, host);
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
            return Stage.GetLogs()
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
        /// </summary>
        public async Task StopAllAsync()
        {
            if (Stage.IsConnected)
            {
                Track(await Stage.StopAsync(MotorizedStage.Axes), Stage.LastError);
            }
            if (Hexapod.IsConnected)
            {
                Track(await Hexapod.StopAsync(Hexapod.Axes), Hexapod.LastError);
            }
        }

        /// <summary>전체 비상 정지 (연결된 장비만). 스테이지: "@s", 헥사포드: STP.</summary>
        public async Task EmergencyStopAllAsync()
        {
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

        // 스테이지(MMT): 컨트롤러 자체 원점을 쓴다.
        // - 기계 원점 찾기: 리밋 방향 홈 서칭 (HM0 = (−)방향, HM1 = (+)방향)
        // - 현재 위치를 원점으로: 컨트롤러 위치를 0 으로 설정 (p0)
        // - 원점으로 이동: 컨트롤러 좌표 0 으로 절대 이동

        /// <summary>스테이지 X, Y, Z 기계 원점 찾기 (축별로 지정한 리밋 방향으로 이동, 기본은 (−)방향). 주변 간섭을 확인한 뒤 호출한다.</summary>
        public async Task<bool> HomeStageAsync(HomeDirection x = HomeDirection.Negative,
            HomeDirection y = HomeDirection.Negative, HomeDirection z = HomeDirection.Negative)
        {
            return Track(await Stage.HomeAsync(MotorizedStage.Axes, new[] { x, y, z }), Stage.LastError);
        }

        /// <summary>스테이지 한 축(X, Y, Z 중 하나)만 기계 원점 찾기. 주변 간섭을 확인한 뒤 호출한다.</summary>
        public async Task<bool> HomeStageAxisAsync(Axis axis, HomeDirection direction = HomeDirection.Negative)
        {
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

        /// <summary>스테이지 현재 위치를 원점(0)으로 설정한다.</summary>
        public bool SetStageZero()
        {
            return Track(Stage.SetZero(MotorizedStage.Axes), Stage.LastError);
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

        private static string SettingPath(string fileName)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        private static string LoadConnectionSetting(string filePath, string defaultValue)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string value = File.ReadAllText(filePath).Trim();
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        private static void SaveConnectionSetting(string filePath, string value)
        {
            try { File.WriteAllText(filePath, value); } catch { }
        }

        // StageHost.txt 형식 (한 줄에 "키=값"):
        //   Host=192.168.0.123
        //   HomeDirX=Negative   (HomeDirY, HomeDirZ 도 같은 형식, Negative / Positive)
        // 예전 형식(주소 한 줄만, '=' 없음)도 주소로 읽는다 - 다음 저장 때 새 형식으로 바뀐다.
        private void LoadStageSettings()
        {
            try
            {
                if (!File.Exists(StageHostFile))
                {
                    return;
                }
                foreach (string rawLine in File.ReadAllLines(StageHostFile))
                {
                    string line = rawLine.Trim();
                    int eq = line.IndexOf('=');
                    if (eq < 0)
                    {
                        if (line.Length > 0)
                        {
                            StageHost = line;
                        }
                        continue;
                    }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (key == "Host" && value.Length > 0)
                    {
                        StageHost = value;
                    }
                    else if (key.StartsWith("HomeDir") && Enum.TryParse(key.Substring("HomeDir".Length), out Axis axis)
                        && _stageHomeDirections.ContainsKey(axis)
                        && Enum.TryParse(value, out HomeDirection direction) && Enum.IsDefined(typeof(HomeDirection), direction))
                    {
                        _stageHomeDirections[axis] = direction;
                    }
                }
            }
            catch { }
        }

        private void SaveStageSettings()
        {
            var lines = new List<string> { "Host=" + StageHost };
            lines.AddRange(_stageHomeDirections.Select(kv => $"HomeDir{kv.Key}={kv.Value}"));
            try { File.WriteAllLines(StageHostFile, lines); } catch { }
        }

        #endregion
    }
}
