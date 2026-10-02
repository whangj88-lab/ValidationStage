using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MotorizedStage_SK_PI
{
    /// <summary>
    /// MMT MMDC-ST466 4축 스테핑 드라이버/컨트롤러 - 선형 스테이지 X, Y, Z (Ethernet TCP, ASCII 프로토콜).
    /// 2026-09-30 스테이지 제조사 변경으로 SK_Motion 을 대체한다. MotionController/F_Main 변경을 줄이려고
    /// 공개 API 모양(이벤트, MoveAsync/JogRun/JogStop, µm 단위 등)은 SK_Motion 과 맞춘다.
    ///
    /// 프로토콜 (MMT "4axis driver controller manual" 7~9장 + 실측):
    /// - 요청: "[축번호]명령[데이터]\r" (대소문자 무관). 응답: "*..." (예: "*#1POS0", "*ok", "*okAtten")
    /// - 응답 끝에 종료문자가 일정하지 않다 (실측: "#" 응답만 CR 로 끝남). 그래서 짧은 무신호 구간으로 응답을 끊는다.
    /// - 응답 후 다음 요청까지 최소 2ms (매뉴얼 7.1).
    /// - 위치/거리/속도는 µstep 단위, 51200 µstep/rev.
    /// - 컨트롤러 전원 투입 시 모터 전원(ST)은 기본 1(차단)이므로 연결 시 "st0" 을 보낸다.
    /// - 위치 카운터는 컨트롤러 전원 투입 시 0 부터 시작한다 (절대 원점 아님).
    /// </summary>
    public class MMT_Motion
    {
        public const int DefaultPort = 5001;
        private const int MicrostepsPerRev = 51200;

        // 1 µstep 당 이동량 (µm). 인덱스 = (int)Axis (X=0, Y=1, Z=2), 컨트롤러 축 번호 = 인덱스 + 1.
        // X, Y: AM1-0602-3DY - 볼나사 리드 1mm (카탈로그 표준 리드) -> 1000µm / 51200.
        // Z   : AZ-0803-3DY  - 쐐기형 Z. 1회전 상승량 = 리드 1mm x tan(경사각).
        //       경사각은 카탈로그/모델명 규칙에 없어 AZ-0808 과 같은 20도로 잡았고, 2026-09-30 프로브 실측으로 맞는 것을 확인했다.
        private const double ZSlopeDegree = 20.0;
        private static readonly double[] UmPerMicrostep =
        {
            1000.0 / MicrostepsPerRev,
            1000.0 / MicrostepsPerRev,
            1000.0 * Math.Tan(ZSlopeDegree * Math.PI / 180.0) / MicrostepsPerRev
        };

        /// <summary>
        /// 속도 단계(0:저속, 1:중속, 2:고속)별 속도 (µsteps/s). 인덱스 = (int)Axis.
        /// 보수적으로 카탈로그 최대속도(X,Y 20mm/s, Z 3.6mm/s)의 25~50% 이하로 잡았다.
        /// 세 축 모두 모터 회전 속도 0.5 / 2 / 5 rev/s 로 같다 (2026-09-30 사용자 선택).
        /// X, Y: 0.5 / 2 / 5 mm/s.  Z: 쐐기(경사 20도)라 약 0.18 / 0.73 / 1.8 mm/s.
        /// </summary>
        public readonly int[][] SpeedLevelValues =
        {
            new int[] { 25600, 102400, 256000 },
            new int[] { 25600, 102400, 256000 },
            new int[] { 25600, 102400, 256000 }
        };

        private const int ConnectTimeoutMs = 2000;
        private const int ReadTimeoutMs = 1000;
        private const int ReplyQuietMs = 10;
        private const int PollIntervalMs = 100;

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly object _commLock = new object();
        private readonly object _statusLock = new object();
        private DateTime _lastReplyTime = DateTime.MinValue;

        private Axis[] _axes = new Axis[0];
        private AxisStatus[] _statuses = new AxisStatus[0];
        private CancellationTokenSource _pollCts;

        public event EventHandler<string> Logged;
        public event EventHandler<bool> OnConnectionChange;
        public event EventHandler<AxisStatus[]> OnStatusChanged;

        public bool IsConnected
        {
            get
            {
                TcpClient client = _client;
                return client != null && client.Connected;
            }
        }

        /// <param name="host">"192.168.0.123" 또는 "192.168.0.123:5001"</param>
        public bool Connect(Axis[] axes, string host)
        {
            if (IsConnected)
            {
                Logged?.Invoke(this, "Already connected");
                return false;
            }

            try
            {
                ParseHost(host, out string ip, out int port);
                Logged?.Invoke(this, $"-- Try to Connect -- {ip}:{port}");

                var client = new TcpClient();
                IAsyncResult ar = client.BeginConnect(ip, port, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeoutMs))
                {
                    client.Close();
                    throw new Exception($"Connection timeout ({ip}:{port})");
                }
                client.EndConnect(ar);
                client.NoDelay = true;

                lock (_commLock)
                {
                    _client = client;
                    _stream = client.GetStream();
                    _stream.ReadTimeout = ReadTimeoutMs;
                    _stream.WriteTimeout = ReadTimeoutMs;
                }

                // MMT 컨트롤러가 맞는지 확인 ("#" -> "*#1")
                string reply = Query("#");
                if (!reply.StartsWith("*"))
                {
                    throw new Exception("Not an MMT controller reply: " + reply);
                }

                _axes = axes.ToArray();
                lock (_statusLock)
                {
                    _statuses = _axes.Select(axis => new AxisStatus(axis)).ToArray();
                }

                // 모터 전원 투입 (컨트롤러 전원 투입 시 기본값이 ST1 = 차단)
                foreach (Axis axis in _axes)
                {
                    ExpectOk(Query($"{AxisNo(axis)}st0"));
                }

                UpdateStatuses();
                _pollCts = new CancellationTokenSource();
                CancellationToken token = _pollCts.Token;
                Task.Run(() => PollingStatus(token));

                OnConnectionChange?.Invoke(this, true);
                Logged?.Invoke(this, ": Successfully connected.");
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, $"Error : Connect \n\t{ex.Message}");
                lock (_commLock)
                {
                    CloseClient();
                }
                return false;
            }
        }

        /// <summary>
        /// 통신만 끊는다. 모터 전원(ST)은 그대로 두어 유지 토크로 위치를 잡고 있게 한다.
        /// </summary>
        public void Disconnect()
        {
            if (_client == null)
            {
                return;
            }
            _pollCts?.Cancel();
            lock (_commLock)
            {
                CloseClient();
            }
            OnConnectionChange?.Invoke(this, false);
            Logged?.Invoke(this, ": Disconnected");
        }

        /// <param name="positions">X,Y,Z: µm</param>
        public async Task<bool> MoveAsync(Axis[] axes, double[] positions, bool isAbsolute)
        {
            try
            {
                CheckReady(axes);
                if (axes.Length != positions.Length)
                {
                    throw new Exception("Move Command: The length of 'axes' must match the length of 'positions'.");
                }

                // MA: 0 상대 / 1 절대 이동 (매뉴얼 9.1.14). 모든 축 설정 후 한꺼번에 G.
                for (int i = 0; i < axes.Length; i++)
                {
                    int axisNo = AxisNo(axes[i]);
                    ExpectOk(Query($"{axisNo}ma{(isAbsolute ? 1 : 0)}"));
                    ExpectOk(Query($"{axisNo}d{ToMicrosteps(axes[i], positions[i])}"));
                }
                foreach (Axis axis in axes)
                {
                    ExpectOk(Query($"{AxisNo(axis)}g"));
                }

                await WaitForStopAsync(axes);
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>누르고 있는 동안 등속 이동 (J+/J-). JogStop 으로 멈춘다.</summary>
        public bool JogRun(Axis axis, bool dir)
        {
            try
            {
                CheckReady(new[] { axis });
                ExpectOk(Query($"{AxisNo(axis)}j{(dir ? "+" : "-")}"));
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public bool JogStop(Axis axis)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                ExpectOk(Query($"{AxisNo(axis)}s"));
                // 감속 정지를 기다려야 하므로 UI 스레드를 막지 않게 백그라운드에서 busy 를 해제한다.
                Task.Run(() => ClearBusyAfterStopSafe(axis));
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 지정한 축만 감속 정지 ("S"). 이동/원점찾기 중이어도 보낼 수 있다.
        /// 정지 후 Busy 가 남는 컨트롤러 특성 때문에 JogStop 과 같이 busy 를 정리한다.
        /// (MMT 는 정지 명령이 "S" 하나뿐이라 비상정지와 일반 정지가 같은 명령이다.)
        /// </summary>
        public async Task<bool> StopAsync(Axis[] axes)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                foreach (Axis axis in axes)
                {
                    ExpectOk(Query($"{AxisNo(axis)}s"));
                }
                Logged?.Invoke(this, "Stop " + string.Join(",", axes));
                await Task.Run(() =>
                {
                    foreach (Axis axis in axes)
                    {
                        ClearBusyAfterStopSafe(axis);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>모든 축 정지 ("@s" - 매뉴얼 9.1.2 모든 축에 명령).</summary>
        public async Task<bool> StopEmergencyAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                ExpectOk(Query("@s"));
                Logged?.Invoke(this, "StopEmergency");
                await Task.Run(() =>
                {
                    foreach (Axis axis in _axes)
                    {
                        ClearBusyAfterStopSafe(axis);
                    }
                });
                await WaitForStopAsync(_axes);
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 기계 원점 찾기 ("HM0" - (-)리밋 방향으로 홈 서칭, 매뉴얼 9.4.3). 모든 축을 동시에 시작하고 끝날 때까지 기다린다.
        /// 홈 서칭 속도(HMV 등)는 컨트롤러 저장값이 10mm/s 로 빨라서, 시작 전에 중속 수준으로 맞춘다 (다를 때만 쓴다).
        /// </summary>
        public async Task<bool> HomeAsync(Axis[] axes)
        {
            try
            {
                CheckReady(axes);
                foreach (Axis axis in axes)
                {
                    int axisNo = AxisNo(axis);
                    int speed = SpeedLevelValues[(int)axis][1];
                    WriteIfDifferent(axisNo, "hmv", speed);
                    // 매뉴얼: HMA/HMAD 는 HMV ~ HMV x10 범위, HMVF 는 특별한 경우가 아니면 HMV 의 1/10
                    WriteIfDifferent(axisNo, "hma", speed * 10);
                    WriteIfDifferent(axisNo, "hmad", speed * 10);
                    WriteIfDifferent(axisNo, "hmvf", speed / 10);
                }
                foreach (Axis axis in axes)
                {
                    ExpectOk(Query($"{AxisNo(axis)}hm0"));
                }
                Logged?.Invoke(this, "Homing...");

                await WaitForStopAsync(axes);
                Logged?.Invoke(this, "Homing done");
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>현재 위치를 컨트롤러의 0 위치로 설정 ("p0", 매뉴얼 9.3.3).</summary>
        public bool SetZero(Axis[] axes)
        {
            try
            {
                CheckReady(axes);
                foreach (Axis axis in axes)
                {
                    ExpectOk(Query($"{AxisNo(axis)}p0"));
                }
                UpdateStatuses();
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 속도/가속도/감속도 설정 (µsteps/s, µsteps/s²).
        /// V/A/AD 는 컨트롤러 비휘발성 메모리에 저장되는 값이라, 쓰기 횟수를 줄이려고 현재 값과 다를 때만 쓴다.
        /// </summary>
        public bool SetSpeed(Axis axis, int velocity, int accel, int decel)
        {
            try
            {
                CheckReady(new[] { axis });
                int axisNo = AxisNo(axis);
                WriteIfDifferent(axisNo, "v", velocity);
                WriteIfDifferent(axisNo, "a", accel);
                WriteIfDifferent(axisNo, "ad", decel);

                lock (_statusLock)
                {
                    AxisStatus status = _statuses.FirstOrDefault(s => s.Axis == axis);
                    if (status != null)
                    {
                        status.Speed = velocity * UmPerMicrostep[(int)axis];
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        /// <returns>Connect 에 넘긴 축 순서대로의 현재 위치 (µm). 연결 안 됐으면 null.</returns>
        public double[] GetPositions()
        {
            if (!IsConnected)
            {
                Logged?.Invoke(this, "Not connected");
                return null;
            }
            lock (_statusLock)
            {
                return _statuses.Select(status => status.Position).ToArray();
            }
        }

        #region private 함수

        private bool IsMoving
        {
            get
            {
                lock (_statusLock)
                {
                    return _statuses.Any(status => status.IsMoving);
                }
            }
        }

        private void CheckReady(Axis[] axes)
        {
            if (!IsConnected)
            {
                throw new Exception("Not connected");
            }
            foreach (Axis axis in axes)
            {
                if (!_axes.Contains(axis))
                {
                    throw new Exception($"The axis '{axis}' is not in the allowed list of axes.");
                }
            }
            if (IsMoving)
            {
                throw new Exception("Command is not ready (moving)");
            }
        }

        private static int AxisNo(Axis axis)
        {
            if (axis > Axis.Z)
            {
                throw new ArgumentException($"MMT stage axis must be X, Y or Z (got {axis}).");
            }
            return (int)axis + 1;
        }

        private static int ToMicrosteps(Axis axis, double um)
        {
            return (int)Math.Round(um / UmPerMicrostep[(int)axis]);
        }

        private static void ParseHost(string host, out string ip, out int port)
        {
            string text = (host ?? "").Trim();
            int colon = text.LastIndexOf(':');
            if (colon > 0)
            {
                ip = text.Substring(0, colon);
                port = int.Parse(text.Substring(colon + 1));
            }
            else
            {
                ip = text;
                port = DefaultPort;
            }
        }

        private void ClearBusyAfterStopSafe(Axis axis)
        {
            try
            {
                ClearBusyAfterStop(axis);
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, $"{axis} busy 해제 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 컨트롤러 동작 특성(실측 2026-09-30): 조그 이동을 "S" 로 멈추면 모터는 서지만 축 상태가 계속 Busy
        /// ("r" -> "B", STATUS bit3 = 0) 로 남는다. "S" 를 다시 보내도 풀리지 않고, 위치결정 이동이 한 번 끝나야 풀린다.
        /// 그래서 감속 정지가 끝난 뒤(위치가 더 이상 안 바뀔 때) 거리 0 상대 이동(ma0, d0, g)으로 상태를 정리한다.
        /// 실측으로 위치 변화 없이 Ready 로 돌아오는 것을 확인했다.
        /// </summary>
        private void ClearBusyAfterStop(Axis axis)
        {
            int axisNo = AxisNo(axis);
            int last = ReadMicrosteps(axisNo);
            for (int i = 0; i < 40; i++)
            {
                Thread.Sleep(50);
                int now = ReadMicrosteps(axisNo);
                if (now == last)
                {
                    break;
                }
                last = now;
            }

            if (!Query($"{axisNo}r").Contains("B"))
            {
                return;
            }
            ExpectOk(Query($"{axisNo}ma0"));
            ExpectOk(Query($"{axisNo}d0"));
            ExpectOk(Query($"{axisNo}g"));
        }

        private int ReadMicrosteps(int axisNo)
        {
            string reply = Query($"{axisNo}p");
            Match match = Regex.Match(reply, @"POS(-?\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                throw new Exception("Unexpected position reply: " + reply);
            }
            return int.Parse(match.Groups[1].Value);
        }

        private void WriteIfDifferent(int axisNo, string command, int value)
        {
            // 조회 응답 예: "*#1V512000", "*#1AD5120000"
            string reply = Query($"{axisNo}{command}");
            Match match = Regex.Match(reply, command + @"(-?\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.Parse(match.Groups[1].Value) == value)
            {
                return;
            }
            ExpectOk(Query($"{axisNo}{command}{value}"));
        }

        private static void ExpectOk(string reply)
        {
            // "*ok" (모터 전원 투입 상태) 또는 "*okAtten" (모터 전원 차단 상태) - 매뉴얼 8.2 Case3
            if (!reply.StartsWith("*ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Unexpected reply: " + reply);
            }
        }

        /// <summary>요청 하나를 보내고 응답 하나를 받는다. 모든 통신은 이 함수로만 한다 (반이중, 요청-응답).</summary>
        private string Query(string command)
        {
            lock (_commLock)
            {
                NetworkStream stream = _stream;
                if (stream == null)
                {
                    throw new InvalidOperationException("Not connected");
                }

                // 매뉴얼 7.1: 응답 완료 후 다음 요청까지 최소 2ms
                if ((DateTime.UtcNow - _lastReplyTime).TotalMilliseconds < 2)
                {
                    Thread.Sleep(2);
                }

                // 이전 요청의 늦게 온 응답이 남아 있으면 버린다 (응답이 섞이지 않게)
                var buffer = new byte[1024];
                while (stream.DataAvailable)
                {
                    stream.Read(buffer, 0, buffer.Length);
                }

                byte[] data = Encoding.ASCII.GetBytes(command + "\r");
                stream.Write(data, 0, data.Length);

                string reply = ReadReply(stream, buffer);
                _lastReplyTime = DateTime.UtcNow;
                return reply;
            }
        }

        /// <summary>
        /// 첫 바이트는 ReadTimeout 까지 기다리고(무응답이면 IOException), 이후 ReplyQuietMs 동안 추가 데이터가 없으면 응답 끝으로 본다.
        /// </summary>
        private static string ReadReply(NetworkStream stream, byte[] buffer)
        {
            var sb = new StringBuilder();
            int n = stream.Read(buffer, 0, buffer.Length);
            if (n == 0)
            {
                throw new IOException("Connection closed by controller");
            }
            sb.Append(Encoding.ASCII.GetString(buffer, 0, n));

            Stopwatch quiet = Stopwatch.StartNew();
            while (quiet.ElapsedMilliseconds < ReplyQuietMs)
            {
                if (stream.DataAvailable)
                {
                    n = stream.Read(buffer, 0, buffer.Length);
                    sb.Append(Encoding.ASCII.GetString(buffer, 0, n));
                    quiet.Restart();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            return sb.ToString().Trim('\r', '\n', '\0', ' ');
        }

        private void CloseClient()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
            }
            catch
            {
                // 닫는 중 예외는 무시한다.
            }
            _stream = null;
            _client = null;
        }

        private async Task WaitForStopAsync(Axis[] axes)
        {
            // G 직후에는 아직 '정지' 로 읽힐 수 있으므로 조금 기다린 뒤부터 본다.
            await Task.Delay(150);
            double[] lastPositions = null;
            Stopwatch unchanged = Stopwatch.StartNew();
            while (true)
            {
                UpdateStatuses();
                bool anyMoving;
                double[] positions;
                lock (_statusLock)
                {
                    AxisStatus[] targets = _statuses.Where(status => axes.Contains(status.Axis)).ToArray();
                    anyMoving = targets.Any(status => status.IsMoving);
                    positions = targets.Select(status => status.Position).ToArray();
                }
                if (!anyMoving)
                {
                    return;
                }

                // 위치가 3초 동안 전혀 안 바뀌는데 Busy 면 (조그 정지 후와 같은) 멈춘 채 Busy 로 남은 상태로 보고 정리한다.
                if (lastPositions == null || !positions.SequenceEqual(lastPositions))
                {
                    lastPositions = positions;
                    unchanged.Restart();
                }
                else if (unchanged.ElapsedMilliseconds > 3000)
                {
                    Logged?.Invoke(this, "Stopped but still busy - clearing busy state");
                    await Task.Run(() =>
                    {
                        foreach (Axis axis in axes)
                        {
                            ClearBusyAfterStopSafe(axis);
                        }
                    });
                    UpdateStatuses();
                    return;
                }
                await Task.Delay(50);
            }
        }

        private async Task PollingStatus(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PollIntervalMs, token);
                    UpdateStatuses();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }
                    Logged?.Invoke(this, $"Error : PollingStatus {ex.Message}");
                    Disconnect();
                    break;
                }
            }
        }

        /// <summary>
        /// 축마다 "IPS" (입력 + 위치 + 상태 일괄, 매뉴얼 9.3.5) 로 위치와 상태를 읽는다.
        /// 응답 예: "*IN000000*#1POS0*#1STATUS0001_0100_0000_0011_1101"
        /// </summary>
        private void UpdateStatuses()
        {
            bool isChanged = false;
            foreach (Axis axis in _axes)
            {
                string reply = Query($"{AxisNo(axis)}ips");
                Match pos = Regex.Match(reply, @"POS(-?\d+)", RegexOptions.IgnoreCase);
                Match stat = Regex.Match(reply, @"STATUS([01_]+)", RegexOptions.IgnoreCase);
                if (!pos.Success || !stat.Success)
                {
                    // 응답이 깨진 경우 이번 주기만 건너뛴다.
                    continue;
                }

                string bits = stat.Groups[1].Value.Replace("_", "");
                double position = int.Parse(pos.Groups[1].Value) * UmPerMicrostep[(int)axis];
                // STATUS 비트 (매뉴얼 9.3.2, 문자열 오른쪽 끝이 bit0):
                // bit1 드라이버 에러, bit3 0=동작 중/1=정지, bit9 과열 에러, bit12 CW 리밋, bit13 CCW 리밋
                bool isMoving = !Bit(bits, 3);
                int alarm = Bit(bits, 1) ? 1 : (Bit(bits, 9) ? 2 : 0);
                // 실장비 확인(2026-09-30): CCW 리밋(bit13)이 + 방향, CW 리밋(bit12)이 - 방향 (REV=1 설정)
                bool isPosLimit = Bit(bits, 13);
                bool isNegLimit = Bit(bits, 12);

                lock (_statusLock)
                {
                    AxisStatus status = _statuses.FirstOrDefault(s => s.Axis == axis);
                    if (status == null)
                    {
                        continue;
                    }
                    if (status.Position != position || status.IsMoving != isMoving || status.Alarm != alarm
                        || status.IsPosLimit != isPosLimit || status.IsNegLimit != isNegLimit)
                    {
                        status.Position = position;
                        status.IsMoving = isMoving;
                        status.Alarm = alarm;
                        status.IsPosLimit = isPosLimit;
                        status.IsNegLimit = isNegLimit;
                        isChanged = true;
                    }
                }
            }

            if (isChanged)
            {
                AxisStatus[] snapshot;
                lock (_statusLock)
                {
                    snapshot = (AxisStatus[])_statuses.Clone();
                }
                OnStatusChanged?.Invoke(this, snapshot);
            }
        }

        private static bool Bit(string bits, int index)
        {
            int pos = bits.Length - 1 - index;
            return pos >= 0 && bits[pos] == '1';
        }

        #endregion
    }
}
