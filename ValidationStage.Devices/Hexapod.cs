using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices
{
    /// <summary>
    /// PI 헥사포드 (C-887 컨트롤러 + H-811, TCP/IP, PI GCS2 DLL). TX, TY, TZ 회전축만 다룬다 (헥사포드 X, Y, Z 는 항상 0 으로 둔다).
    /// 단위: arcmin (= 컨트롤러 deg × −60, 부호 반대). 좌표계/3D 조회 함수는 컨트롤러 단위 그대로 (mm, deg, 부호 변환 없음).
    /// 상태(위치/이동 중)는 연결 후 내부에서 100 ms 마다 갱신되며 <see cref="GetAxisStatuses"/> 로 조회한다.
    /// 실패한 명령은 false/null 을 돌려주고 원인은 <see cref="LastError"/>, 경과는 <see cref="GetLogs"/> 로 확인한다.
    /// 컨트롤러 전원을 켤 때마다 레퍼런스가 풀리며, 그 상태의 이동은 컨트롤러가 GCS 에러 5 로 거부한다 (<see cref="ReferenceAsync"/>).
    /// </summary>
    public class Hexapod
    {
        /// <summary>헥사포드가 담당하는 축 (TX, TY, TZ).</summary>
        public static readonly Axis[] Axes = { Axis.TX, Axis.TY, Axis.TZ };

        /// <summary>컨트롤러 TCP 포트 (고정).</summary>
        public const int Port = 50000;

        /// <summary><see cref="DefineCoordSystem"/> 로 만들 수 있는 좌표계 타입.</summary>
        public static readonly string[] DefinableCoordSystemTypes = { "KSD", "KST", "KSW" };

        /// <summary>속도 단계(저속/중속/고속)별 기준값. 실제 설정값은 이 값 × 1.5 (<see cref="SetSpeedLevel"/>).</summary>
        public double[] DefaultSpeedLevelValues = new double[3] { 0.2, 3.0, 10.0 };

        private const double SpeedLevelFactor = 1.5;
        private const int PI_RESULT_FAILURE = 0;
        private const int PI_TRUE = 1;

        // FRF/qPOS 등 6축 전체를 다루는 명령용. 헥사포드 축 이름 X,Y,Z,U,V,W 의 인덱스 = (int)Axis 순서 (TX=U, TY=V, TZ=W).
        private const string AllAxes = "X Y Z U V W";
        private static readonly string[] PiAxisNames = { "X", "Y", "Z", "U", "V", "W" };

        private const int ReferenceTimeoutMs = 120000;

        // 연속 조그: qTMN/qTMX 를 못 읽을 때 쓰는 시작 거리 (deg), 이동 가능 거리 이분 탐색 정밀도 (deg, 0.001° = 0.06 arcmin).
        private const double FallbackJogSpanDeg = 30.0;
        private const double MinJogDistanceDeg = 0.001;

        private int _deviceId = -1;
        private string[] _piAxes = new string[0];
        private AxisStatus[] _statuses = new AxisStatus[0];
        private readonly object _axisLock = new object();
        private CancellationTokenSource _cancelTokenSource;

        private readonly double[] _pivot = new double[3];
        private readonly double[] _coordinateSystem = new double[6];

        // qTMN/qTMX 가 이 컨트롤러 좌표계에서 미지원(GCS 551)이면 true - 다시 묻지 않는다.
        private bool _limitQueryUnsupported;

        private readonly LogBuffer _log = new LogBuffer("Hexapod");
        private readonly DeviceErrors _errors;

        /// <summary>헥사포드 객체를 만든다. 연결은 <see cref="Connect"/>.</summary>
        public Hexapod()
        {
            _errors = new DeviceErrors(_log);
        }

        /// <summary>연결 여부. 통신이 끊기면 false 가 된다.</summary>
        public bool IsConnected => PI_GCS2.IsConnected(_deviceId) == PI_TRUE;

        /// <summary>마지막으로 실패한 명령의 원인 (없으면 null). 성공해도 지워지지 않는다.</summary>
        public DeviceError LastError => _errors.Last;

        /// <summary>마지막 조회 이후 쌓인 로그를 꺼낸다 (꺼낸 로그는 버퍼에서 지워진다).</summary>
        public List<LogEntry> GetLogs()
        {
            return _log.Drain();
        }

        #region 연결

        /// <summary>
        /// 헥사포드 컨트롤러에 TCP/IP 로 연결한다 (포트 <see cref="Port"/>). TX, TY, TZ 상태 갱신을 시작한다.
        /// 연결 후에는 컨트롤러에 남아 있던 속도 그대로이므로, 필요하면 <see cref="SetSpeedLevel"/> 로 속도를 맞춘다.
        /// </summary>
        /// <param name="host">컨트롤러 IP (예: "169.254.3.106")</param>
        public bool Connect(string host)
        {
            try
            {
                if (IsConnected)
                {
                    throw new DeviceException(T("이미 연결되어 있습니다", "Already connected"));
                }
                _log.Add(T($"연결 시도 - {host}:{Port}", $"Connecting - {host}:{Port}"));
                _deviceId = PI_GCS2.ConnectTCPIP(host, Port);
                if (_deviceId < 0)
                {
                    _deviceId = -1;
                    throw new DeviceException(T("연결 실패", "Connection failed"));
                }

                _piAxes = Axes.Select(axis => PiAxisNames[(int)axis]).ToArray();
                lock (_axisLock)
                {
                    _statuses = Axes.Select(axis => new AxisStatus(axis)).ToArray();
                }

                _cancelTokenSource = new CancellationTokenSource();
                CancellationToken token = _cancelTokenSource.Token;
                Task.Run(() => PollingStatus(token));
                UpdateSpeedStatus();
                _log.Add(T("연결됨", "Connected"));
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(T($"연결 오류: {ex.Message}", $"Connect error: {ex.Message}"), (ex as DeviceException)?.Code ?? 0);
                return false;
            }
        }

        /// <summary>연결을 끊는다. 헥사포드는 현재 자세를 유지한다.</summary>
        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }
            _cancelTokenSource?.Cancel();
            Thread.Sleep(100);
            PI_GCS2.CloseConnection(_deviceId);
            _deviceId = -1;
            _log.Add(T("연결 해제", "Disconnected"));
        }

        /// <summary>
        /// 같은 네트워크의 PI 컨트롤러를 찾는다 (PIMikroMove 의 TCP/IP "Search" 와 같은 브로드캐스트 검색, 몇 초 걸림).
        /// 연결 여부와 무관하다. 반환: 컨트롤러 설명 문자열 목록 (IP 는 <see cref="ParseHostFromDescription"/> 로 꺼낸다). 실패하면 null.
        /// </summary>
        public Task<string[]> ScanControllersAsync()
        {
            return Task.Run(() =>
            {
                var buffer = new StringBuilder(10000);
                int count = PI_GCS2.EnumerateTCPIPDevices(buffer, buffer.Capacity, "");
                if (count < 0)
                {
                    _errors.Fail(T($"컨트롤러 검색 실패 (code {count})", $"Controller search failed (code {count})"), count);
                    return null;
                }
                return buffer.ToString()
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToArray();
            });
        }

        /// <summary>검색 결과 한 줄에서 IP 와 포트를 꺼낸다. IP 가 없으면 null. 포트가 없으면 50000.</summary>
        public static string ParseHostFromDescription(string description, out int port)
        {
            port = Port;
            Match match = Regex.Match(description ?? "", @"(\d{1,3}(?:\.\d{1,3}){3})(?::(\d+))?");
            if (!match.Success)
            {
                return null;
            }
            if (match.Groups[2].Success)
            {
                port = int.Parse(match.Groups[2].Value);
            }
            return match.Groups[1].Value;
        }

        #endregion

        #region 이동

        /// <summary>
        /// 이동하고 끝날 때까지 기다린다. positions 단위 arcmin (넘긴 배열은 바뀌지 않는다).
        /// 레퍼런스가 안 잡혀 있으면 실패하고 <see cref="LastError"/> 의 Code 가 5 다.
        /// </summary>
        /// <param name="axes">TX, TY, TZ 중 이동할 축</param>
        /// <param name="positions">axes 순서대로의 목표 (arcmin)</param>
        /// <param name="isAbsolute">true = 절대 위치, false = 현재 위치 기준 상대 이동</param>
        public async Task<bool> MoveAsync(Axis[] axes, double[] positions, bool isAbsolute)
        {
            try
            {
                CheckConnected();
                CheckAxes(axes);
                if (axes.Length != positions.Length)
                {
                    throw new DeviceException(T("이동 명령: 축 개수와 위치 개수가 다릅니다", "Move: number of axes and positions differ"));
                }
                if (IsMoving)
                {
                    throw new DeviceException(T("이동 중이라 이동 명령을 보낼 수 없습니다", "Move command is not ready (moving)"));
                }

                string sAxes = string.Join(" ", axes.Select(axis => PiAxisNames[(int)axis]));
                double[] degrees = positions.Select(arcmin => arcmin / -60).ToArray();   // arcmin -> deg

                int ret = isAbsolute
                    ? PI_GCS2.MOV(_deviceId, sAxes, degrees)
                    : PI_GCS2.MVR(_deviceId, sAxes, degrees);
                if (ret == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("이동 명령 실패", "Move command failed"));
                }

                await WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>
        /// 누르고 있는 동안 연속 이동을 시작한다. <see cref="JogStop"/> 으로 멈춘다 (멈추지 않으면 이동 한계까지 간다).
        /// dir = true 가 표시값(arcmin) 증가 방향. PI 헥사포드에는 연속 조그 명령이 없어서, 갈 수 있는 한계(qVMO 로 탐색)까지 MOV 를 보낸다.
        /// </summary>
        public bool JogRun(Axis axis, bool dir)
        {
            try
            {
                CheckConnected();
                CheckAxes(new[] { axis });
                if (IsMoving)
                {
                    throw new DeviceException(T("이동 중이라 조그를 시작할 수 없습니다", "Jog command is not ready (moving)"));
                }
                if (IsReferenced() == false)
                {
                    throw new DeviceException(T("레퍼런스가 잡혀 있지 않아 이동할 수 없습니다. 레퍼런스를 먼저 잡으세요.",
                        "Not referenced - run the reference first."));
                }

                string piAxis = PiAxisNames[(int)axis];
                double[] current = new double[1];
                if (PI_GCS2.qPOS(_deviceId, piAxis, current) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T($"조그: qPOS {piAxis} 실패", $"Jog: qPOS {piAxis} failed"));
                }

                // arcmin = deg × −60 → 표시값 + 방향은 deg − 방향 (TMN 쪽).
                double[] limit = new double[1];
                int limitRet = PI_RESULT_FAILURE;
                if (!_limitQueryUnsupported)
                {
                    limitRet = dir ? PI_GCS2.qTMN(_deviceId, piAxis, limit) : PI_GCS2.qTMX(_deviceId, piAxis, limit);
                    if (limitRet == PI_RESULT_FAILURE)
                    {
                        // 실제 장비(2026-10-01): 551 "This query is not supported for this coordinate system type".
                        // 매번 실패하므로 이후로는 조회 없이 고정 범위를 쓴다. 정상 경로라 로그는 남기지 않는다.
                        _limitQueryUnsupported = true;
                        PI_GCS2.GetError(_deviceId);   // 컨트롤러 에러 비우기
                    }
                }
                if (limitRet == PI_RESULT_FAILURE)
                {
                    limit[0] = current[0] + (dir ? -FallbackJogSpanDeg : FallbackJogSpanDeg);
                }

                double fullDistance = limit[0] - current[0];
                bool? fullReachable = CanReach(piAxis, current[0] + fullDistance);

                if (fullReachable == null)
                {
                    // qVMO 미지원: MOV 를 직접 시도하고, 거부되면 에러를 비우고 거리를 절반씩 줄인다.
                    for (double distance = fullDistance; Math.Abs(distance) >= MinJogDistanceDeg; distance /= 2)
                    {
                        if (PI_GCS2.MOV(_deviceId, piAxis, new[] { current[0] + distance }) != PI_RESULT_FAILURE)
                        {
                            return true;
                        }
                        PI_GCS2.GetError(_deviceId);
                    }
                    throw new DeviceException(T($"{axis}: 이동 한계 - 더 갈 수 없습니다", $"{axis}: travel limit - cannot move further"));
                }

                // 작업 영역 경계까지 최대한 가도록 이분 탐색한다 (절반씩 줄이면 경계보다 훨씬 앞에서 멈춘다 - 2026-10-01 실제 장비).
                double reachable = 0;
                double unreachable = fullDistance;
                if (fullReachable == true)
                {
                    reachable = fullDistance;
                }
                else
                {
                    while (Math.Abs(unreachable - reachable) > MinJogDistanceDeg)
                    {
                        double mid = (reachable + unreachable) / 2;
                        if (CanReach(piAxis, current[0] + mid) == true)
                        {
                            reachable = mid;
                        }
                        else
                        {
                            unreachable = mid;
                        }
                    }
                }

                if (Math.Abs(reachable) < MinJogDistanceDeg)
                {
                    throw new DeviceException(T($"{axis}: 이동 한계 - 더 갈 수 없습니다", $"{axis}: travel limit - cannot move further"));
                }
                if (PI_GCS2.MOV(_deviceId, piAxis, new[] { current[0] + reachable }) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T($"조그: MOV {piAxis} 실패", $"Jog: MOV {piAxis} failed"));
                }
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>조그를 멈춘다 (HLT, 감속 정지).</summary>
        public bool JogStop(Axis axis)
        {
            try
            {
                if (PI_GCS2.HLT(_deviceId, PiAxisNames[(int)axis]) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("정지 명령 실패", "Stop command failed"));
                }
                _ = WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>지정한 축을 감속 정지(HLT)하고 멈출 때까지 기다린다.</summary>
        public async Task<bool> StopAsync(Axis[] axes)
        {
            try
            {
                CheckAxes(axes);
                string sAxes = string.Join(" ", axes.Select(axis => PiAxisNames[(int)axis]));
                if (PI_GCS2.HLT(_deviceId, sAxes) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("정지 명령 실패", "Stop command failed"));
                }
                await WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>비상 정지 (STP, 모든 축 즉시 정지).</summary>
        public async Task<bool> StopEmergencyAsync()
        {
            try
            {
                if (PI_GCS2.STP(_deviceId) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("비상 정지 명령 실패", "Emergency stop command failed"));
                }
                await WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        #endregion

        #region 레퍼런스 (FRF)

        // 헥사포드 컨트롤러는 전원을 켤 때마다 레퍼런스가 풀린다. 그 상태에서 MOV 를 보내면
        // 에러 5 "Unallowable move attempted on unreferenced axis, or move attempted with servo off" (2026-10-01 실제 장비).
        // FRF 는 헥사포드 6축이 한 번에 잡히므로(개별 축 불가) X,Y,Z 까지 6축 전체로 묻고 잡는다.

        /// <summary>6축 모두 레퍼런스가 잡혀 있으면 true, 아니면 false. 조회 실패(미연결 등)면 null.</summary>
        public bool? IsReferenced()
        {
            if (!IsConnected)
            {
                return null;
            }
            int[] referenced = new int[6];
            if (PI_GCS2.qFRF(_deviceId, AllAxes, referenced) == PI_RESULT_FAILURE)
            {
                _errors.Fail(GcsFail(T("레퍼런스 상태 조회(qFRF) 실패", "Reference state query (qFRF) failed")));
                return null;
            }
            return referenced.All(r => r == 1);
        }

        /// <summary>
        /// 레퍼런스를 잡는다 (FRF). 헥사포드가 레퍼런스 위치(6축 0)로 실제 이동하므로 주변 간섭을 확인한 뒤 호출한다.
        /// 서보가 꺼져 있으면 먼저 켠다. 이동이 끝나고 레퍼런스가 확인될 때까지 기다린다 (최대 120초).
        /// </summary>
        public async Task<bool> ReferenceAsync()
        {
            try
            {
                CheckConnected();

                int[] servo = new int[6];
                if (PI_GCS2.qSVO(_deviceId, AllAxes, servo) != PI_RESULT_FAILURE && servo.Any(s => s == 0))
                {
                    if (PI_GCS2.SVO(_deviceId, AllAxes, new[] { 1, 1, 1, 1, 1, 1 }) == PI_RESULT_FAILURE)
                    {
                        throw GcsFail(T("서보 ON(SVO) 실패", "Servo ON (SVO) failed"));
                    }
                    _log.Add(T("서보 ON", "Servo ON"));
                }

                if (PI_GCS2.FRF(_deviceId, AllAxes) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("레퍼런스(FRF) 실패", "Reference (FRF) failed"));
                }

                // FRF 직후 잠깐은 아직 움직이기 전이라 이동 중 여부만으로는 끝을 알 수 없어서 qFRF 결과까지 기다린다.
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < ReferenceTimeoutMs)
                {
                    await Task.Delay(200);
                    if (!IsMoving && IsReferenced() == true)
                    {
                        return true;
                    }
                }
                throw new DeviceException(T($"레퍼런스 시간 초과 ({ReferenceTimeoutMs / 1000}초)", $"Reference timeout ({ReferenceTimeoutMs / 1000} s)"));
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        #endregion

        #region 조회

        /// <summary>TX, TY, TZ 상태 복사본 (위치 arcmin, 이동 중 여부, 속도). 미연결이면 null.</summary>
        public AxisStatus[] GetAxisStatuses()
        {
            if (!IsConnected)
            {
                return null;
            }
            lock (_axisLock)
            {
                return _statuses.CloneAll();
            }
        }

        /// <summary>TX, TY, TZ 현재 위치 (arcmin). 미연결이면 null.</summary>
        public double[] GetPositions()
        {
            if (!IsConnected)
            {
                _errors.Fail(T("연결되어 있지 않습니다", "Not connected"));
                return null;
            }
            lock (_axisLock)
            {
                return _statuses.Select(status => status.Position).ToArray();
            }
        }

        /// <summary>6축 현재 위치를 컨트롤러에서 바로 읽는다 (X, Y, Z: µm / TX, TY, TZ: arcmin). 실패하면 null.</summary>
        public double[] GetAllPositions()
        {
            try
            {
                CheckConnected();
                double[] positions = new double[6];
                if (PI_GCS2.qPOS(_deviceId, AllAxes, positions) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("위치 조회 실패", "Failed to read positions"));
                }
                for (int i = 0; i < 6; i++)
                {
                    positions[i] = Math.Round(i < 3 ? positions[i] * 1000 : positions[i] * -60, 4);   // mm -> µm, deg -> arcmin
                }
                return positions;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return null;
            }
        }

        /// <summary>TX, TY, TZ 의 설정 속도 (VLS 값). 미연결이면 null.</summary>
        public double[] GetSpeeds()
        {
            if (!IsConnected)
            {
                _errors.Fail(T("연결되어 있지 않습니다", "Not connected"));
                return null;
            }
            lock (_axisLock)
            {
                return _statuses.Select(status => status.Speed).ToArray();
            }
        }

        #endregion

        #region 설정 (속도 / 피벗 / 좌표계)

        /// <summary>헥사포드 시스템 속도(VLS)를 설정한다. 6축 공통 값이다.</summary>
        public bool SetSpeed(double velocity)
        {
            try
            {
                if (IsMoving)
                {
                    throw new DeviceException(T("이동 중이라 속도를 바꿀 수 없습니다", "SetSpeed command is not ready (moving)"));
                }
                if (PI_GCS2.VLS(_deviceId, velocity) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("속도 설정 실패", "SetSpeed command failed"));
                }
                UpdateSpeedStatus();
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>속도 단계를 적용한다 (<see cref="DefaultSpeedLevelValues"/>[단계] × 1.5 를 <see cref="SetSpeed"/>).</summary>
        public bool SetSpeedLevel(SpeedLevel level)
        {
            return SetSpeed(DefaultSpeedLevelValues[(int)level] * SpeedLevelFactor);
        }

        /// <summary>회전 중심(피벗, SPI)을 설정한다. 단위 mm (컨트롤러 값 그대로).</summary>
        public bool SetPivot(double x, double y, double z)
        {
            try
            {
                CheckConnected();
                if (IsMoving)
                {
                    throw new DeviceException(T("이동 중이라 피벗을 바꿀 수 없습니다", "SetPivot (SPI) command is not ready (moving)"));
                }
                if (PI_GCS2.SPI(_deviceId, "X Y Z", new double[] { x, y, z }) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("피벗 설정(SPI) 실패", "SetPivot (SPI) command failed"));
                }
                if (PI_GCS2.qSPI(_deviceId, "X Y Z", _pivot) == PI_RESULT_FAILURE)
                {
                    _errors.Fail(GcsFail(T("피벗 조회(qSPI) 실패", "GetPivot (qSPI) failed")));
                }
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        /// <summary>마지막으로 <see cref="SetPivot"/> 한 피벗 (mm). 설정 전에는 0, 0, 0.</summary>
        public double[] GetPivot()
        {
            return _pivot.ToArray();
        }

        /// <summary>마지막으로 <see cref="SetCoordinateSystem"/> 한 좌표계 위치 (X,Y,Z: µm / U,V,W: arcmin). 설정 전에는 모두 0.</summary>
        public double[] GetCoordinateSystem()
        {
            return _coordinateSystem.ToArray();
        }

        /// <summary>
        /// "TILTEDCS" 좌표계(KSD)를 주어진 위치로 다시 정의하고 활성화한다. X,Y,Z: µm / U,V,W: arcmin (A 프로젝트 방식).
        /// TILTEDCS 가 활성 상태면 잠시 ZERO 를 활성화한 뒤 재정의한다.
        /// </summary>
        public bool SetCoordinateSystem(double x, double y, double z, double u, double v, double w)
        {
            x *= 0.001;    // µm -> mm
            y *= 0.001;
            z *= 0.001;
            u /= 60;       // arcmin -> deg
            v /= 60;
            w /= 60;

            try
            {
                CheckConnected();
                if (IsMoving)
                {
                    throw new DeviceException(T("이동 중이라 좌표계를 바꿀 수 없습니다", "SetCoordinateSystem command is not ready (moving)"));
                }

                string csName = "TILTEDCS";
                StringBuilder buffer = new StringBuilder(1024);
                int ret = PI_GCS2.qKEN(_deviceId, csName, buffer, buffer.Capacity);

                // csName 이 활성 좌표계라면 비활성화 (ZERO 좌표계 활성화)
                if (ret != PI_RESULT_FAILURE && buffer.ToString().Contains(csName))
                {
                    if (PI_GCS2.KEN(_deviceId, "ZERO") == PI_RESULT_FAILURE)
                    {
                        throw GcsFail(T("KEN ZERO 실패", "KEN ZERO failed"));
                    }
                }
                if (PI_GCS2.KSD(_deviceId, csName, AllAxes, new double[] { x, y, z, -u, -v, -w }) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("KSD 실패", "KSD failed"));
                }
                if (PI_GCS2.KEN(_deviceId, csName) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("KEN 실패", "KEN failed"));
                }

                buffer.Clear();
                if (PI_GCS2.qKEN(_deviceId, csName, buffer, buffer.Capacity) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T($"좌표계 '{csName}' 이(가) 활성화되지 않았습니다", $"Coordinate system '{csName}' is not active."));
                }

                UpdateCoordinateSystem(csName);
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        #endregion

        #region 좌표계 관리 (PIMikroMove "Manage Coordinate Systems" 의 목록/생성/연결/삭제/활성화)

        // 값은 컨트롤러 그대로 X,Y,Z = mm, U,V,W = deg (PI 화면과 동일). 메인 축 값 arcmin(= deg × −60) 과 섞지 않는다.
        // 활성화(KEN)는 플랫폼을 움직이지 않고 좌표 기준만 바꾼다 - 이후 위치 값과 0 위치(원점)의 의미가 달라진다.

        /// <summary>
        /// 정의된 좌표계 목록 (KLS? + KEN?). PI 내부 좌표계(PI_BASE, PI_LEVELLING 등)도 포함되므로 화면에서는
        /// <see cref="HexapodCoordSystem.IsPiInternal"/> 로 거른다. 보이는 좌표계 중 활성인 게 없으면 ZERO 를 활성으로 표시한다. 실패하면 null.
        /// </summary>
        public List<HexapodCoordSystem> GetCoordSystems()
        {
            try
            {
                CheckConnected();
                var buffer = new StringBuilder(65536);
                if (PI_GCS2.qKLS(_deviceId, null, null, null, buffer, buffer.Capacity) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T("좌표계 목록 조회(KLS?) 실패", "Coordinate system list (KLS?) failed"));
                }

                // KLS? 응답은 XML (2026-10-02 실제 장비): <SingleCoordinateSystem><ZERO Name= Parent= Used= Type=><POS X=.. /><NLM/>...</ZERO>...
                var list = new List<HexapodCoordSystem>();
                XElement root;
                try
                {
                    root = XElement.Parse(buffer.ToString().Trim());
                }
                catch (Exception ex)
                {
                    throw new DeviceException(T("좌표계 목록(KLS?) 해석 실패 - ", "Failed to parse coordinate system list (KLS?) - ") + ex.Message);
                }
                foreach (var element in root.Elements())
                {
                    var cs = new HexapodCoordSystem
                    {
                        Name = (string)element.Attribute("Name") ?? element.Name.LocalName,
                        Parent = (string)element.Attribute("Parent"),
                        Type = (string)element.Attribute("Type"),
                    };
                    foreach (var item in element.Elements())
                    {
                        cs.Items[item.Name.LocalName] = item.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
                    }
                    list.Add(cs);
                }

                // KEN? 은 "PI_LEVELLING=KLD(PI)" 처럼 활성인 것만 나오고 ZERO 는 안 나온다.
                buffer.Clear();
                if (PI_GCS2.qKEN(_deviceId, "", buffer, buffer.Capacity) == PI_RESULT_FAILURE)
                {
                    _errors.Fail(GcsFail(T("활성 좌표계 조회(KEN?) 실패", "Active coordinate system query (KEN?) failed")));
                }
                var activeNames = new HashSet<string>(buffer.ToString()
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Split('=')[0].Trim())
                    .Where(name => name.Length > 0));
                foreach (var cs in list)
                {
                    cs.IsActive = activeNames.Contains(cs.Name);
                }
                if (!list.Any(cs => !cs.IsPiInternal && cs.IsActive))
                {
                    var zero = list.FirstOrDefault(cs => cs.Name == "ZERO");
                    if (zero != null)
                    {
                        zero.IsActive = true;
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return null;
            }
        }

        /// <summary>
        /// 좌표계를 정의한다 (같은 이름이 있으면 덮어씀). type = KSD / KST / KSW, 값: X,Y,Z mm / U,V,W deg.
        /// 정의만 하면 부모가 ZERO 가 되므로 다른 좌표계 밑에 두려면 이어서 <see cref="LinkCoordSystem"/> 을 호출한다.
        /// 활성 상태인 좌표계는 컨트롤러가 거부한다.
        /// </summary>
        public bool DefineCoordSystem(string type, string name, double[] xyzuvw)
        {
            Func<int, int> command;
            switch (type)
            {
                case "KSD": command = id => PI_GCS2.KSD(id, name, AllAxes, xyzuvw); break;
                case "KST": command = id => PI_GCS2.KST(id, name, AllAxes, xyzuvw); break;
                case "KSW": command = id => PI_GCS2.KSW(id, name, AllAxes, xyzuvw); break;
                default:
                    _errors.Fail(T($"지원하지 않는 좌표계 타입: {type}", $"Unsupported coordinate system type: {type}"));
                    return false;
            }
            return RunCoordSystemCommand(T($"좌표계 정의({type})", $"Define coordinate system ({type})"), command);
        }

        /// <summary>child 좌표계를 parent 좌표계의 하위로 연결한다 (KLN). 활성 상태인 좌표계는 컨트롤러가 거부할 수 있다.</summary>
        public bool LinkCoordSystem(string child, string parent)
        {
            return RunCoordSystemCommand(T($"좌표계 연결(KLN {child} → {parent})", $"Link coordinate system (KLN {child} → {parent})"),
                id => PI_GCS2.KLN(id, child, parent));
        }

        /// <summary>좌표계를 삭제한다 (KRM). 활성 상태이거나 하위 좌표계가 있으면 컨트롤러가 거부할 수 있다.</summary>
        public bool DeleteCoordSystem(string name)
        {
            return RunCoordSystemCommand(T($"좌표계 삭제(KRM {name})", $"Delete coordinate system (KRM {name})"), id => PI_GCS2.KRM(id, name));
        }

        /// <summary>좌표계를 활성화한다 (KEN). "ZERO" 를 활성화하면 기본 좌표계로 돌아간다. 플랫폼은 움직이지 않는다.</summary>
        public bool ActivateCoordSystem(string name)
        {
            return RunCoordSystemCommand(T("좌표계 활성화(KEN)", "Activate coordinate system (KEN)"), id => PI_GCS2.KEN(id, name));
        }

        private bool RunCoordSystemCommand(string what, Func<int, int> command)
        {
            try
            {
                CheckConnected();
                if (IsMoving)
                {
                    throw new DeviceException(T($"{what}: 이동 중에는 할 수 없습니다", $"{what}: not allowed while moving"));
                }
                if (command(_deviceId) == PI_RESULT_FAILURE)
                {
                    throw GcsFail(T($"{what} 실패", $"{what} failed"));
                }
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(ex);
                return false;
            }
        }

        #endregion

        #region 3D 보기용 조회 (주기적으로 불리므로 실패해도 로그/LastError 를 남기지 않고 null)

        /// <summary>헥사포드 모델 이름 (CST? "H-811.I2_AXIS_X" → "H-811.I2"). 실패하면 null.</summary>
        public string GetModelName()
        {
            if (!IsConnected)
            {
                return null;
            }
            var buffer = new StringBuilder(1024);
            if (PI_GCS2.qCST(_deviceId, "X", buffer, buffer.Capacity) == PI_RESULT_FAILURE)
            {
                PI_GCS2.GetError(_deviceId);
                return null;
            }
            string value = buffer.ToString().Split('=').Last().Trim();
            int axisTag = value.IndexOf("_AXIS", StringComparison.OrdinalIgnoreCase);
            return axisTag > 0 ? value.Substring(0, axisTag) : value;
        }

        /// <summary>6축 위치 (X,Y,Z mm / U,V,W deg), 활성 좌표계 기준 컨트롤러 값 그대로. 실패하면 null.</summary>
        public double[] GetRawPose()
        {
            if (!IsConnected)
            {
                return null;
            }
            var values = new double[6];
            if (PI_GCS2.qPOS(_deviceId, AllAxes, values) == PI_RESULT_FAILURE)
            {
                PI_GCS2.GetError(_deviceId);
                return null;
            }
            return values;
        }

        /// <summary>
        /// 활성 사용자 좌표계 이름 (KEN? 에서 PI 내부 "(PI)" 를 뺀 것). 없으면 "ZERO", 실패하면 null.
        /// 실제 장비(2026-10-02): "TILTEDCS=KSD", "PI_LEVELLING=KLD(PI)", "PI_BASE=KSB(PI)".
        /// </summary>
        public string GetActiveUserCoordSystem()
        {
            if (!IsConnected)
            {
                return null;
            }
            var buffer = new StringBuilder(4096);
            if (PI_GCS2.qKEN(_deviceId, "", buffer, buffer.Capacity) == PI_RESULT_FAILURE)
            {
                PI_GCS2.GetError(_deviceId);
                return null;
            }
            foreach (string line in buffer.ToString().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('=');
                if (parts.Length == 2 && !parts[1].Trim().EndsWith("(PI)"))
                {
                    return parts[0].Trim();
                }
            }
            return "ZERO";
        }

        /// <summary>
        /// 좌표계 cs → ZERO 변환 목록 (KLT? cs ZERO). 항목마다 X,Y,Z mm / U,V,W deg. cs 가 ZERO 면 빈 목록, 실패하면 null.
        /// 실제 장비(2026-10-02): "Name=TILTEDCS EndCoordinateSystem=ZERO X=0 Y=0 Z=0 U=180 V=0 W=0" (X축 180° 뒤집힘).
        /// </summary>
        public List<double[]> GetTransformToZero(string cs)
        {
            var result = new List<double[]>();
            if (!IsConnected)
            {
                return null;
            }
            if (cs == "ZERO")
            {
                return result;
            }
            var buffer = new StringBuilder(4096);
            if (PI_GCS2.qKLT(_deviceId, cs, "ZERO", buffer, buffer.Capacity) == PI_RESULT_FAILURE)
            {
                PI_GCS2.GetError(_deviceId);
                return null;
            }
            foreach (string line in buffer.ToString().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var fields = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Split('='))
                    .Where(kv => kv.Length == 2)
                    .ToDictionary(kv => kv[0], kv => kv[1]);
                if (fields.TryGetValue("Name", out string name) && name == "ZERO")
                {
                    continue;   // ZERO → ZERO 는 항등
                }
                var values = new double[6];
                bool ok = true;
                for (int i = 0; i < 6; i++)
                {
                    ok &= fields.TryGetValue(PiAxisNames[i], out string raw)
                        && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
                }
                if (ok)
                {
                    result.Add(values);
                }
            }
            return result;
        }

        #endregion

        #region private

        private bool IsMoving
        {
            get
            {
                lock (_axisLock)
                {
                    return _statuses.Any(status => status.IsMoving);
                }
            }
        }

        private void CheckConnected()
        {
            if (!IsConnected)
            {
                throw new DeviceException(T("연결되어 있지 않습니다", "Not connected"));
            }
        }

        private static void CheckAxes(Axis[] axes)
        {
            foreach (Axis axis in axes)
            {
                if (!Axes.Contains(axis))
                {
                    throw new DeviceException(T($"헥사포드 축은 TX, TY, TZ 만 쓸 수 있습니다 ({axis})", $"Hexapod axis must be TX, TY or TZ (got {axis})"));
                }
            }
        }

        /// <summary>GCS 에러 코드를 읽어(컨트롤러 에러도 같이 비워짐) "설명 - 코드: 내용" 예외로 만든다.</summary>
        private DeviceException GcsFail(string what)
        {
            int code = PI_GCS2.GetError(_deviceId);
            var text = new StringBuilder(256);
            PI_GCS2.TranslateError(code, text, text.Capacity);
            return new DeviceException($"{what} - {code}: {text}", code);
        }

        /// <summary>qVMO 로 목표 각도(deg)까지 갈 수 있는지 확인. qVMO 자체가 실패(미지원 등)하면 null.</summary>
        private bool? CanReach(string piAxis, double targetDeg)
        {
            int[] possible = new int[1];
            if (PI_GCS2.qVMO(_deviceId, piAxis, new[] { targetDeg }, possible) == PI_RESULT_FAILURE)
            {
                PI_GCS2.GetError(_deviceId);
                return null;
            }
            return possible[0] == 1;
        }

        private async Task WaitForReadyAsync()
        {
            while (true)
            {
                UpdateStatuses(ReadPositions(), ReadIsMovings());
                if (!IsMoving)
                {
                    return;
                }
                await Task.Delay(10);
            }
        }

        private async Task PollingStatus(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, token);
                    UpdateStatuses(ReadPositions(), ReadIsMovings());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _errors.Fail(T($"상태 갱신 오류: {ex.Message}", $"Status polling error: {ex.Message}"));
                    Disconnect();
                    break;   // 통신이 이미 끊겨 Disconnect 가 취소하지 못해도 같은 오류를 계속 쌓지 않게 끝낸다
                }
            }
        }

        private void UpdateStatuses(double[] positions, bool[] isMovings)
        {
            lock (_axisLock)
            {
                for (int i = 0; i < _statuses.Length; i++)
                {
                    _statuses[i].Position = positions[i];
                    _statuses[i].IsMoving = isMovings[i];
                }
            }
        }

        private void UpdateSpeedStatus()
        {
            CheckConnected();
            double velocity = 0.0;
            if (PI_GCS2.qVLS(_deviceId, ref velocity) == PI_RESULT_FAILURE)
            {
                throw GcsFail(T("속도 조회 실패", "Failed to read speed"));
            }
            lock (_axisLock)
            {
                foreach (var status in _statuses)
                {
                    if (velocity != 0)
                    {
                        status.Speed = velocity;
                    }
                }
            }
        }

        private double[] ReadPositions()
        {
            CheckConnected();
            double[] positions = new double[_piAxes.Length];
            if (PI_GCS2.qPOS(_deviceId, string.Join(" ", _piAxes), positions) == PI_RESULT_FAILURE)
            {
                throw new DeviceException(T("위치 조회 실패", "Failed to read positions"));
            }
            return positions.Select(deg => Math.Round(deg * -60, 3)).ToArray();   // deg -> arcmin
        }

        private bool[] ReadIsMovings()
        {
            CheckConnected();
            int[] isMovings = new int[_piAxes.Length];
            if (PI_GCS2.IsMoving(_deviceId, string.Join(" ", _piAxes), isMovings) == PI_RESULT_FAILURE)
            {
                throw new DeviceException(T("이동 상태 조회 실패", "Failed to read moving state"));
            }
            return isMovings.Select(isMoving => isMoving == PI_TRUE).ToArray();
        }

        private void UpdateCoordinateSystem(string csName)
        {
            StringBuilder buffer = new StringBuilder(1024);
            if (PI_GCS2.qKEN(_deviceId, csName, buffer, buffer.Capacity) == PI_RESULT_FAILURE)
            {
                throw GcsFail(T("좌표계 조회(qKEN) 실패", "GetCoordinateSystem (qKEN) failed"));
            }

            buffer.Clear();
            if (PI_GCS2.qKLS(_deviceId, csName, "POS", null, buffer, buffer.Capacity) == PI_RESULT_FAILURE)
            {
                throw GcsFail(T("좌표계 조회(qKLS) 실패", "GetCoordinateSystem (qKLS) failed"));
            }

            string[] rCmd = buffer.ToString().Split(' ');
            for (int i = 0; i < 6; i++)
            {
                double pos = double.Parse(rCmd[i + 1].Split('"')[1]);
                _coordinateSystem[i] = i >= 3 ? pos * -60 : pos * 1000;   // deg -> arcmin, mm -> µm
            }
        }

        #endregion
    }

    /// <summary>헥사포드 좌표계 하나 (<see cref="Hexapod.GetCoordSystems"/>). 값은 컨트롤러 단위 그대로 (mm, deg).</summary>
    public class HexapodCoordSystem
    {
        /// <summary>이름 (예: ZERO, TILTEDCS)</summary>
        public string Name;
        /// <summary>부모 좌표계 이름</summary>
        public string Parent;
        /// <summary>타입 (예: ZERO, KSD, KST, KSW, KSB(PI))</summary>
        public string Type;
        /// <summary>활성 여부</summary>
        public bool IsActive;
        /// <summary>하위 항목 (POS/NLM/PLM/SSL/SPI/SST 등) → (축 → 값 문자열)</summary>
        public Dictionary<string, Dictionary<string, string>> Items = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>PI 내부 좌표계 (Type 이 "KSB(PI)" 처럼 "(PI)" 로 끝남). PIMikroMove 화면도 숨긴다.</summary>
        public bool IsPiInternal => Type != null && Type.EndsWith("(PI)");
    }
}
