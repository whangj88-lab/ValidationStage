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
    /// 연결 주소와 헥사포드 원점은 exe 폴더의 StageHost.txt, HexapodHost.txt, HexapodHome.txt 에 저장된다.
    /// </summary>
    public class ValidationSystem
    {
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
        private static readonly string HexapodHomeFile = SettingPath("HexapodHome.txt");

        /// <summary>마지막으로 연결에 성공한 스테이지 주소 (StageHost.txt). 저장된 적 없으면 컨트롤러 기본 IP "192.168.0.123".</summary>
        public string StageHost { get; private set; } = LoadConnectionSetting(StageHostFile, "192.168.0.123");

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
            SaveConnectionSetting(StageHostFile, host);
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
        // - 기계 원점 찾기: 리밋 방향 홈 서칭 (HM0)
        // - 현재 위치를 원점으로: 컨트롤러 위치를 0 으로 설정 (p0)
        // - 원점으로 이동: 컨트롤러 좌표 0 으로 절대 이동

        /// <summary>스테이지 X, Y, Z 기계 원점 찾기 ((−)리밋 방향으로 이동). 주변 간섭을 확인한 뒤 호출한다.</summary>
        public async Task<bool> HomeStageAsync()
        {
            return Track(await Stage.HomeAsync(MotorizedStage.Axes), Stage.LastError);
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

        // 헥사포드: 컨트롤러 좌표는 건드리지 않고 현재 TX,TY,TZ 를 파일에 저장해 두었다가 그 위치로 절대 이동한다.

        /// <summary>저장된 헥사포드 원점 TX, TY, TZ (arcmin, HexapodHome.txt). 저장된 적 없으면 null.</summary>
        public double[] HexapodHome { get; private set; } = LoadHome(HexapodHomeFile);

        /// <summary>헥사포드 현재 TX, TY, TZ 를 원점으로 저장한다 (HexapodHome.txt).</summary>
        public bool SetHexapodHomeFromCurrent()
        {
            double[] positions = Hexapod.GetPositions();
            if (positions == null)
            {
                LastError = Hexapod.LastError;
                return false;
            }
            HexapodHome = positions;
            if (!SaveHome(HexapodHomeFile, positions))
            {
                LastError = new DeviceError(0, T("원점 파일 저장 실패: ", "Failed to save home file: ") + HexapodHomeFile);
                return false;
            }
            return true;
        }

        /// <summary>헥사포드를 저장된 원점으로 이동한다. 저장된 원점이 없으면 false.</summary>
        public async Task<bool> MoveHexapodHomeAsync()
        {
            if (HexapodHome == null)
            {
                LastError = new DeviceError(0, T("저장된 헥사포드 원점이 없습니다", "No hexapod home saved"));
                return false;
            }
            return Track(await Hexapod.MoveAsync(Hexapod.Axes, HexapodHome, true), Hexapod.LastError);
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

        private static double[] LoadHome(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }
                double[] values = File.ReadAllLines(filePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => double.Parse(line, CultureInfo.InvariantCulture))
                    .ToArray();
                return values.Length == 3 ? values : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool SaveHome(string filePath, double[] positions)
        {
            try
            {
                File.WriteAllLines(filePath, positions.Select(p => p.ToString("R", CultureInfo.InvariantCulture)));
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
