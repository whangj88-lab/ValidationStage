using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MotorizedStage_SK_PI
{
    /// <summary>
    /// 제어 컨셉: X, Y, Z 는 Motorized Stage(MMT_Motion, 2026-09-30 이전에는 SK_Motion)로, TX, TY, TZ 는 Hexapod(PI_Motion)로 나눠서 제어한다.
    /// PI_Motion 은 원래 6축 전체를 다룰 수 있지만, Connect 시 넘기는 Axis[] 구독으로
    /// 각 장비가 맡을 축만 제한해서 사용한다.
    /// </summary>
    public class MotionController
    {
        public static readonly Axis[] StageAxes = { Axis.X, Axis.Y, Axis.Z };
        public static readonly Axis[] HexapodAxes = { Axis.TX, Axis.TY, Axis.TZ };

        public MMT_Motion Stage { get; } = new MMT_Motion();
        public PI_Motion Hexapod { get; } = new PI_Motion();

        // 연결에 성공한 포트/주소만 저장해서 다음 실행 때 기본값으로 쓴다.
        // 응답 없는 주소면 MMT_Motion.Connect 가 연결 타임아웃(2초)만큼 블록되므로 UI 스레드 밖에서 실행한다.
        public async Task<bool> ConnectStageAsync(string host)
        {
            if (!await Task.Run(() => Stage.Connect(StageAxes, host)))
            {
                return false;
            }
            StageHost = host;
            SaveConnectionSetting(StageHostFile, host);
            return true;
        }

        public bool ConnectHexapod(string hostName)
        {
            if (!Hexapod.Connect(HexapodAxes, hostName))
            {
                return false;
            }
            HexapodHost = hostName;
            SaveConnectionSetting(HexapodHostFile, hostName);
            return true;
        }

        /// <summary>
        /// 같은 네트워크의 PI 컨트롤러를 찾는다 (PIMikroMove 의 TCP/IP "Search" 와 같은 PI_EnumerateTCPIPDevices).
        /// 어댑터 전체로 브로드캐스트해서 찾으므로 호스트 입력칸 값과 무관하다. 몇 초 걸려서 백그라운드로 실행한다.
        /// 반환: 컨트롤러 설명 문자열 목록 (예: "C-887 SN 123456789 (169.254.3.106:50000)" 형식, IP 는 ParseHostFromDescription 로 추출).
        /// </summary>
        public Task<string[]> ScanHexapodControllersAsync()
        {
            return Task.Run(() =>
            {
                var buffer = new System.Text.StringBuilder(10000);
                int count = PI.PI_GCS2.EnumerateTCPIPDevices(buffer, buffer.Capacity, "");
                if (count < 0)
                {
                    throw new System.Exception($"컨트롤러 검색 실패 (code {count})");
                }
                return buffer.ToString()
                    .Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .ToArray();
            });
        }

        /// <summary>검색 결과 한 줄에서 IP 와 포트를 꺼낸다. IP 가 없으면 null.</summary>
        public static string ParseHostFromDescription(string description, out int port)
        {
            port = 50000;
            var match = System.Text.RegularExpressions.Regex.Match(description, @"(\d{1,3}(?:\.\d{1,3}){3})(?::(\d+))?");
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

        // 설정 파일은 모두 exe 폴더에 둔다 (실행 위치/바로가기 시작 위치와 무관하게 같은 파일을 쓰도록).
        private static readonly string StageHostFile = SettingPath("StageHost.txt");
        private static readonly string HexapodHostFile = SettingPath("HexapodHost.txt");

        private static string SettingPath(string fileName)
        {
            return Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        /// <summary>마지막으로 연결에 성공한 스테이지(MMT 컨트롤러) 주소. 저장된 적 없으면 컨트롤러 기본 IP.</summary>
        public string StageHost { get; private set; } = LoadConnectionSetting(StageHostFile, "192.168.0.123");

        /// <summary>마지막으로 연결에 성공한 헥사포드 주소. 저장된 적 없으면 127.0.0.1.</summary>
        public string HexapodHost { get; private set; } = LoadConnectionSetting(HexapodHostFile, "127.0.0.1");

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

        public void DisconnectStage()
        {
            Stage.Disconnect();
        }

        public void DisconnectHexapod()
        {
            Hexapod.Disconnect();
        }

        public void DisconnectAll()
        {
            DisconnectStage();
            DisconnectHexapod();
        }

        /// <summary>
        /// 속도 단계(0:저속, 1:중속, 2:고속)를 두 장비에 함께 적용한다.
        /// 스테이지: MMT_Motion.SpeedLevelValues (µsteps/s), 가/감속도는 속도 x10 (약 0.1초에 목표 속도 도달).
        /// 헥사포드: A 프로젝트(F_Motion_SK_PI.cs)의 실사용 환산식을 그대로 따른다.
        /// </summary>
        /// <returns>장비별 적용 결과 (로그용). 예: "스테이지 적용, 헥사포드 미연결"</returns>
        public string ApplySpeedLevel(int level)
        {
            // 연결 안 된 장비는 건너뛴다 (안 그러면 "Not connected" 로그만 쌓인다).
            string stageResult = "스테이지 미연결";
            if (Stage.IsConnected)
            {
                bool ok = true;
                foreach (var axis in StageAxes)
                {
                    int velocity = Stage.SpeedLevelValues[(int)axis][level];
                    ok &= Stage.SetSpeed(axis, velocity, velocity * 10, velocity * 10);
                }
                stageResult = ok ? "스테이지 적용" : "스테이지 실패";
            }

            string hexapodResult = "헥사포드 미연결";
            if (Hexapod.IsConnected)
            {
                double hexapodSpeed = Hexapod.DefaultSpeedLevelValues[level];
                hexapodResult = Hexapod.SetSpeed(hexapodSpeed * 1.5) ? "헥사포드 적용" : "헥사포드 실패";
            }

            return stageResult + ", " + hexapodResult;
        }

        /// <summary>
        /// 한 축만 감속 정지. 스테이지: MMT "S". 헥사포드: PI HLT (감속 정지, 비상정지 STP 와 다름).
        /// </summary>
        public Task<bool> StopAxisAsync(Axis axis)
        {
            if (StageAxes.Contains(axis))
            {
                return Stage.StopAsync(new[] { axis });
            }
            if (!Hexapod.IsConnected)
            {
                return Task.FromResult(false);
            }
            return Hexapod.StopAsync(new[] { axis }, new[] { true });
        }

        /// <summary>
        /// 일반 전체 정지 (감속 정지). 스테이지: 세 축 "S", 헥사포드: 세 축 HLT.
        /// 비상정지(EmergencyStopAllAsync)와의 차이는 헥사포드뿐이다 - MMT 는 정지 명령이 "S" 하나뿐.
        /// </summary>
        public async Task StopAllAsync()
        {
            if (Stage.IsConnected)
            {
                await Stage.StopAsync(StageAxes);
            }
            if (Hexapod.IsConnected)
            {
                await Hexapod.StopAsync(HexapodAxes, HexapodAxes.Select(axis => true).ToArray());
            }
        }

        public async Task EmergencyStopAllAsync()
        {
            if (Stage.IsConnected)
            {
                await Stage.StopEmergencyAsync();
            }
            if (Hexapod.IsConnected)
            {
                await Hexapod.StopEmergencyAsync();
            }
        }

        #region 헥사포드 좌표계 (PIMikroMove "Manage Coordinate Systems" 의 생성/활성화)

        // 값은 컨트롤러 그대로 X,Y,Z = mm, U,V,W = deg (PI 화면과 동일). 화면의 arcmin(= deg * -60) 과 섞지 않는다.
        // 활성화(KEN)는 플랫폼을 움직이지 않고 좌표 기준만 바꾼다 - 이후 표시 위치/저장된 헥사포드 원점의 의미가 달라진다.

        /// <summary>KLS? 의 좌표계 하나. Items 는 POS/NLM/PLM/SSL/SPI/SST 등 하위 항목 → (축 → 값 문자열).</summary>
        public class HexapodCoordSystem
        {
            public string Name;
            public string Parent;
            public string Type;
            public bool IsActive;
            public System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> Items
                = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();

            /// <summary>PI 내부 좌표계 (Type 이 "KSB(PI)" 처럼 "(PI)" 로 끝남). PIMikroMove 화면도 숨긴다.</summary>
            public bool IsPiInternal => Type != null && Type.EndsWith("(PI)");
        }

        /// <summary>
        /// 정의된 좌표계 목록 (KLS? + KEN?). PI 내부 좌표계(PI_BASE, PI_LEVELLING 등)도 포함되므로 화면에서는 IsPiInternal 로 거른다.
        /// KLS? 응답은 XML (2026-10-02 실제 장비):
        ///   &lt;SingleCoordinateSystem&gt; &lt;ZERO Name="ZERO" Parent="PI_BASE" Used="True" Type="ZERO"&gt; &lt;POS X="0.000000" .../&gt; &lt;NLM .../&gt; ... &lt;/ZERO&gt; ...
        /// KEN? 은 "PI_LEVELLING=KLD(PI)", "PI_BASE=KSB(PI)" 처럼 활성인 것만 나오고 ZERO 는 안 나온다. 그래서
        /// PIMikroMove 처럼 "보이는(내부용이 아닌) 좌표계 중 활성인 게 없으면 ZERO 가 활성" 으로 판단한다.
        /// 실패하면 null.
        /// </summary>
        public System.Collections.Generic.List<HexapodCoordSystem> GetHexapodCoordSystems()
        {
            int id = GetHexapodDeviceId();
            var buffer = new System.Text.StringBuilder(65536);
            if (PI.PI_GCS2.qKLS(id, null, null, null, buffer, buffer.Capacity) == 0)
            {
                HexapodLogged?.Invoke(this, $"좌표계 목록 조회(KLS?) 실패 - {GcsError(id)}");
                return null;
            }

            var list = new System.Collections.Generic.List<HexapodCoordSystem>();
            try
            {
                var root = System.Xml.Linq.XElement.Parse(buffer.ToString().Trim());
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
            }
            catch (System.Exception ex)
            {
                HexapodLogged?.Invoke(this, "좌표계 목록(KLS?) 해석 실패 - " + ex.Message);
                return null;
            }

            buffer.Clear();
            if (PI.PI_GCS2.qKEN(id, "", buffer, buffer.Capacity) == 0)
            {
                HexapodLogged?.Invoke(this, $"활성 좌표계 조회(KEN?) 실패 - {GcsError(id)}");
            }
            var activeNames = new System.Collections.Generic.HashSet<string>(buffer.ToString()
                .Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries)
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

        /// <summary>좌표계 생성 화면에서 고를 수 있는 타입. 모두 (이름, 축, 값) 형식이라 같은 방식으로 정의한다.</summary>
        public static readonly string[] DefinableCoordSystemTypes = { "KSD", "KST", "KSW" };

        /// <summary>
        /// 좌표계를 정의(같은 이름이 있으면 덮어씀)한다. type = KSD / KST / KSW. 값: X,Y,Z mm, U,V,W deg.
        /// 활성 상태인 좌표계는 컨트롤러가 거부한다.
        /// </summary>
        public bool DefineHexapodCoordSystem(string type, string name, double[] xyzuvw)
        {
            System.Func<int, int> command;
            switch (type)
            {
                case "KSD": command = id => PI.PI_GCS2.KSD(id, name, AllHexapodAxes, xyzuvw); break;
                case "KST": command = id => PI.PI_GCS2.KST(id, name, AllHexapodAxes, xyzuvw); break;
                case "KSW": command = id => PI.PI_GCS2.KSW(id, name, AllHexapodAxes, xyzuvw); break;
                default:
                    HexapodLogged?.Invoke(this, $"지원하지 않는 좌표계 타입: {type}");
                    return false;
            }
            return RunCoordSystemCommand($"좌표계 정의({type})", command);
        }

        /// <summary>
        /// child 좌표계를 parent 좌표계의 하위로 연결(KLN)한다. KSD 등으로 정의만 하면 부모가 ZERO 가 되므로(2026-10-02 실제 장비),
        /// 트리에서 고른 좌표계 밑에 만들려면 정의 후 이걸 호출한다. 활성 상태인 좌표계는 컨트롤러가 거부할 수 있다.
        /// </summary>
        public bool LinkHexapodCoordSystem(string child, string parent)
        {
            return RunCoordSystemCommand($"좌표계 연결(KLN {child} → {parent})", id => PI.PI_GCS2.KLN(id, child, parent));
        }

        /// <summary>좌표계를 삭제(KRM)한다. 활성 상태이거나 하위 좌표계가 있으면 컨트롤러가 거부할 수 있다.</summary>
        public bool DeleteHexapodCoordSystem(string name)
        {
            return RunCoordSystemCommand($"좌표계 삭제(KRM {name})", id => PI.PI_GCS2.KRM(id, name));
        }

        /// <summary>좌표계를 활성화(KEN)한다. "ZERO" 를 활성화하면 기본 좌표계로 돌아간다.</summary>
        public bool ActivateHexapodCoordSystem(string name)
        {
            return RunCoordSystemCommand("좌표계 활성화(KEN)", id => PI.PI_GCS2.KEN(id, name));
        }

        private bool RunCoordSystemCommand(string what, System.Func<int, int> command)
        {
            try
            {
                if (!Hexapod.IsConnected)
                {
                    throw new System.Exception("Not Connected");
                }
                if (Hexapod.GetAxisStatuses().Any(status => status.IsMoving))
                {
                    throw new System.Exception($"{what}: 이동 중에는 할 수 없습니다");
                }
                int id = GetHexapodDeviceId();
                if (command(id) == 0)
                {
                    throw new System.Exception($"{what} 실패 - {GcsError(id)}");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                HexapodLogged?.Invoke(this, ex.Message);
                return false;
            }
        }

        #endregion

        #region 헥사포드 레퍼런스 (FRF)

        // 헥사포드 컨트롤러는 전원을 켤 때마다 레퍼런스가 풀린다. 그 상태에서 MOV 를 보내면
        // 에러 5 "Unallowable move attempted on unreferenced axis, or move attempted with servo off" (2026-10-01 실제 장비).
        // FRF 는 헥사포드 6축이 한 번에 잡히므로(개별 축 불가) 구독하지 않는 X,Y,Z 까지 6축 전체로 묻고 잡는다.
        private const string AllHexapodAxes = "X Y Z U V W";

        /// <summary>6축 모두 레퍼런스가 잡혀 있으면 true. 조회 실패(미연결 등)면 null.</summary>
        public bool? IsHexapodReferenced()
        {
            if (!Hexapod.IsConnected)
            {
                return null;
            }
            int id = GetHexapodDeviceId();
            int[] referenced = new int[6];
            if (PI.PI_GCS2.qFRF(id, AllHexapodAxes, referenced) == 0)
            {
                HexapodLogged?.Invoke(this, $"레퍼런스 상태 조회(qFRF) 실패 - {GcsError(id)}");
                return null;
            }
            return referenced.All(r => r == 1);
        }

        /// <summary>
        /// 레퍼런스 동작(FRF). 헥사포드가 레퍼런스 위치(6축 0)로 실제 이동한다. 서보가 꺼져 있으면 먼저 켠다.
        /// 끝나면 qFRF 로 레퍼런스가 잡혔는지 확인한다 (최대 ReferenceTimeoutMs).
        /// </summary>
        public async Task<bool> ReferenceHexapodAsync()
        {
            try
            {
                if (!Hexapod.IsConnected)
                {
                    throw new System.Exception("Not Connected");
                }
                int id = GetHexapodDeviceId();

                int[] servo = new int[6];
                if (PI.PI_GCS2.qSVO(id, AllHexapodAxes, servo) != 0 && servo.Any(s => s == 0))
                {
                    if (PI.PI_GCS2.SVO(id, AllHexapodAxes, new[] { 1, 1, 1, 1, 1, 1 }) == 0)
                    {
                        throw new System.Exception($"서보 ON(SVO) 실패 - {GcsError(id)}");
                    }
                    HexapodLogged?.Invoke(this, "서보 ON");
                }

                if (PI.PI_GCS2.FRF(id, AllHexapodAxes) == 0)
                {
                    throw new System.Exception($"레퍼런스(FRF) 실패 - {GcsError(id)}");
                }

                // FRF 직후 잠깐은 아직 움직이기 전이라 IsMoving 만으로는 끝을 알 수 없어서 qFRF 결과까지 기다린다.
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < ReferenceTimeoutMs)
                {
                    await Task.Delay(200);
                    bool moving = Hexapod.GetAxisStatuses().Any(status => status.IsMoving);
                    if (!moving && IsHexapodReferenced() == true)
                    {
                        return true;
                    }
                }
                throw new System.Exception($"레퍼런스 시간 초과 ({ReferenceTimeoutMs / 1000}초)");
            }
            catch (System.Exception ex)
            {
                HexapodLogged?.Invoke(this, ex.Message);
                return false;
            }
        }

        private const int ReferenceTimeoutMs = 120000;

        #endregion

        #region 헥사포드 연속 Jog

        // PI_Motion.JogRun 은 누를 때마다 0.1° MVR 한 번만 보내서(A 원본 그대로), 누르고 있어도 6 arcmin 가고 멈춘다.
        // PI 헥사포드에는 MMT 의 j+/j- 같은 연속 조그 명령이 없으므로, 누르는 순간 이동 한계(qTMN/qTMX)로 MOV 를 보내고
        // 손을 떼면 HLT 로 세운다. PI_Motion 은 A 와 동일하게 유지하기 위해 수정하지 않고 여기서 GCS2 를 직접 호출한다.
        private static readonly string[] PiAxisNames = { "X", "Y", "Z", "U", "V", "W" };

        // 목표가 작업 영역 밖이면(다른 축 기울기에 따라 달라짐) qVMO 이분 탐색으로 경계를 찾는 정밀도.
        // 갈 수 있는 거리가 이보다 작으면 한계로 본다 (deg, 0.001° = 0.06 arcmin, 30° 기준 qVMO 약 15회).
        private const double MinHexapodJogDistanceDeg = 0.001;

        /// <summary>누르고 있는 동안 연속 이동. dir=true 가 표시값(arcmin) 증가 방향. HexapodJogStop 으로 멈춘다.</summary>
        public bool HexapodJogRun(Axis axis, bool dir)
        {
            try
            {
                if (!Hexapod.IsConnected)
                {
                    throw new System.Exception("Not Connected");
                }
                if (Hexapod.GetAxisStatuses().Any(status => status.IsMoving))
                {
                    throw new System.Exception("JogMove Command is not ready");
                }

                if (IsHexapodReferenced() == false)
                {
                    throw new System.Exception("레퍼런스가 잡혀 있지 않아 이동할 수 없습니다. [레퍼런스] 버튼을 먼저 누르세요.");
                }

                int id = GetHexapodDeviceId();
                string piAxis = PiAxisNames[(int)axis];
                double[] current = new double[1];
                if (PI.PI_GCS2.qPOS(id, piAxis, current) == 0)
                {
                    throw new System.Exception($"Jog: qPOS {piAxis} 실패 - {GcsError(id)}");
                }

                // PI_Motion 의 단위 변환과 맞춘다: arcmin = deg * -60 → 표시값 + 방향은 deg - 방향 (TMN 쪽).
                double[] limit = new double[1];
                int limitRet = 0;
                if (!_hexapodLimitQueryUnsupported)
                {
                    limitRet = dir ? PI.PI_GCS2.qTMN(id, piAxis, limit) : PI.PI_GCS2.qTMX(id, piAxis, limit);
                    if (limitRet == 0)
                    {
                        // 실제 장비(2026-10-01): 551 "This query is not supported for this coordinate system type".
                        // 매번 실패하므로 이후로는 조회 없이 고정 범위를 쓴다. 정상 경로라 로그는 남기지 않는다 (사용자 요청).
                        _hexapodLimitQueryUnsupported = true;
                        PI.PI_GCS2.GetError(id);   // 컨트롤러 에러 비우기
                    }
                }
                if (limitRet == 0)
                {
                    // 한계 조회가 안 되면 넉넉한 고정 범위에서 시작해 갈 수 있는 거리까지 줄여 나간다.
                    limit[0] = current[0] + (dir ? -FallbackHexapodJogSpanDeg : FallbackHexapodJogSpanDeg);
                }

                double fullDistance = limit[0] - current[0];
                bool? fullReachable = CanReach(id, piAxis, current[0] + fullDistance);

                if (fullReachable == null)
                {
                    // qVMO 미지원: 판단을 못 하니 MOV 를 직접 시도하고, 거부되면 에러를 비우고 거리를 절반씩 줄인다.
                    for (double distance = fullDistance; System.Math.Abs(distance) >= MinHexapodJogDistanceDeg; distance /= 2)
                    {
                        if (PI.PI_GCS2.MOV(id, piAxis, new[] { current[0] + distance }) != 0)
                        {
                            return true;
                        }
                        PI.PI_GCS2.GetError(id);
                    }
                    throw new System.Exception($"{axis}: 이동 한계 - 더 갈 수 없습니다");
                }

                // 작업 영역 경계까지 최대한 가도록 이분 탐색한다 (절반씩 줄여 처음 되는 값을 쓰면 경계보다 훨씬 앞에서
                // 멈춰서, 한계 근처에서 "조금 가고 멈춤" 이 반복됐다 - 2026-10-01 실제 장비).
                double reachable = 0;               // 갈 수 있는 거리 (부호 포함)
                double unreachable = fullDistance;  // 못 가는 거리
                if (fullReachable == true)
                {
                    reachable = fullDistance;
                }
                else
                {
                    while (System.Math.Abs(unreachable - reachable) > MinHexapodJogDistanceDeg)
                    {
                        double mid = (reachable + unreachable) / 2;
                        if (CanReach(id, piAxis, current[0] + mid) == true)
                        {
                            reachable = mid;
                        }
                        else
                        {
                            unreachable = mid;
                        }
                    }
                }

                if (System.Math.Abs(reachable) < MinHexapodJogDistanceDeg)
                {
                    throw new System.Exception($"{axis}: 이동 한계 - 더 갈 수 없습니다");
                }
                if (PI.PI_GCS2.MOV(id, piAxis, new[] { current[0] + reachable }) == 0)
                {
                    throw new System.Exception($"Jog: MOV {piAxis} 실패 - {GcsError(id)}");
                }
                return true;
            }
            catch (System.Exception ex)
            {
                HexapodLogged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public bool HexapodJogStop(Axis axis)
        {
            return Hexapod.JogStop(axis);   // HLT (감속 정지)
        }

        /// <summary>PI_Motion 이 바깥에서 쓰는 로그 대신 쓰는 헥사포드 Jog 로그.</summary>
        public event System.EventHandler<string> HexapodLogged;

        // qTMN/qTMX 를 못 읽을 때 쓰는 시작 거리 (deg). 실제 가능 거리는 qVMO/MOV 결과로 절반씩 줄여 찾는다.
        private const double FallbackHexapodJogSpanDeg = 30.0;

        // qTMN/qTMX 가 이 컨트롤러 좌표계에서 미지원(551)이면 true - 다시 묻지 않는다.
        private bool _hexapodLimitQueryUnsupported;

        /// <summary>qVMO 로 목표 각도(deg)까지 갈 수 있는지 확인. qVMO 자체가 실패(미지원 등)하면 null.</summary>
        private static bool? CanReach(int id, string piAxis, double targetDeg)
        {
            int[] possible = new int[1];
            if (PI.PI_GCS2.qVMO(id, piAxis, new[] { targetDeg }, possible) == 0)
            {
                PI.PI_GCS2.GetError(id);
                return null;
            }
            return possible[0] == 1;
        }

        /// <summary>GCS 에러 코드를 읽어(컨트롤러 에러도 같이 비워짐) "코드: 설명" 문자열로 만든다.</summary>
        private static string GcsError(int id)
        {
            int code = PI.PI_GCS2.GetError(id);
            var text = new System.Text.StringBuilder(256);
            PI.PI_GCS2.TranslateError(code, text, text.Capacity);
            return $"{code}: {text}";
        }

        private int GetHexapodDeviceId()
        {
            var field = typeof(PI_Motion).GetField("_deviceId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null)
            {
                throw new System.Exception("PI_Motion._deviceId not found");
            }
            return (int)field.GetValue(Hexapod);
        }

        #endregion

        /// <summary>
        /// 한 축을 기계 좌표 기준으로 절대 이동한다 (X,Y,Z: µm / TX,TY,TZ: arcmin).
        /// </summary>
        public Task<bool> MoveAbsAsync(Axis axis, double position)
        {
            if (StageAxes.Contains(axis))
            {
                return Stage.MoveAsync(new[] { axis }, new[] { position }, true);
            }
            return Hexapod.MoveAsync(new[] { axis }, new[] { position }, true);
        }

        #region 원점(Home)

        // 스테이지(MMT): 컨트롤러 자체 원점을 쓴다 (2026-09-30 사용자 요청으로 파일 저장 방식에서 변경).
        // - 기계 원점 찾기: 리밋 방향 홈 서칭 (HM0)
        // - 현재 위치를 원점으로: 컨트롤러 위치를 0 으로 설정 (Set Zero, p0)
        // - 원점으로 이동: 컨트롤러 좌표 0 으로 절대 이동
        public Task<bool> HomeStageAsync()
        {
            return Stage.HomeAsync(StageAxes);
        }

        public bool SetStageZero()
        {
            return Stage.SetZero(StageAxes);
        }

        public Task<bool> MoveStageToZeroAsync()
        {
            return Stage.MoveAsync(StageAxes, new double[StageAxes.Length], true);
        }

        // 헥사포드: A 프로젝트의 SetHome6DFormCurPos/MoveHome6D 와 같은 개념 - 컨트롤러 좌표는 건드리지 않고
        // 현재 기계 좌표를 파일에 저장해 두었다가 그 위치로 절대 이동한다.
        // A 와 달리 TX,TY,TZ 도 0 이 아니라 저장 시점의 실제 각도를 저장한다 (B 에는 비전 기울기 보정이 없음).
        // 헥사포드 자체 X,Y,Z 는 구독하지 않으므로 저장/이동 대상이 아니다.
        private static readonly string HexapodHomeFile = SettingPath("HexapodHome.txt");

        /// <summary>TX,TY,TZ 원점 (arcmin). 저장된 적 없으면 null.</summary>
        public double[] HexapodHome { get; private set; } = LoadHome(HexapodHomeFile);

        public bool SetHexapodHomeFromCurrent()
        {
            double[] positions = Hexapod.GetPositions();
            if (positions == null)
            {
                return false;
            }
            HexapodHome = positions;
            return SaveHome(HexapodHomeFile, positions);
        }

        // MoveAsync 가 넘겨받은 배열을 단위 변환하며 덮어쓰므로(PI_Motion) 복사본을 넘긴다.
        public Task<bool> MoveHexapodHomeAsync()
        {
            if (HexapodHome == null)
            {
                return Task.FromResult(false);
            }
            return Hexapod.MoveAsync(HexapodAxes, (double[])HexapodHome.Clone(), true);
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
