using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Solartron.Orbit3;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices
{
    /// <summary>
    /// Solartron Orbit3 프로브 (RS232IM / USBIM / ETHIM 컨트롤러 한 대에 DP10, DP20 등 Digital Probe 모듈이
    /// 데이지체인으로 여러 개 연결된 구성). 연결된 모듈 하나 = 채널 하나이며 개수는 고정하지 않는다.
    /// 값 단위: µm. 모듈ID → 라벨(예: "TZ1") 매핑은 exe 폴더의 ProbeLabels.txt 에 저장된다.
    /// </summary>
    /// <remarks>
    /// 읽기(<see cref="ReadAll"/>, <see cref="Read"/>)는 호출할 때마다 실제로 읽는다. 상태를 기억하지 않는다.
    /// 케이블이 빠진 모듈은 Receive Timeout 까지 기다린 뒤 오류를 돌려주므로 백그라운드 스레드에서 호출한다.
    /// 통신 오류(<see cref="ProbeReadStatus.CommError"/>)가 나면 반복 호출을 멈추고, 케이블을 확인한 뒤 다시 호출한다.
    /// 끊긴 모듈을 계속 읽으면 케이블을 다시 꽂아도 컨트롤러 전원을 다시 켜기 전까지 복구되지 않을 수 있다.
    /// </remarks>
    public class Probe
    {
        private readonly OrbitServer _server = new OrbitServer();
        private readonly Dictionary<string, double> _zeroOffsets = new Dictionary<string, double>();
        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>();
        private readonly string _labelFilePath;

        // SDK(모듈 목록/읽기) 접근을 직렬화한다. ReadAll 은 다른 작업 중이면 기다리지 않고 Busy 를 돌려주고,
        // 개별 읽기/영점/해제는 잠깐 기다린 뒤 포기한다.
        private readonly object _busLock = new object();

        private readonly LogBuffer _log = new LogBuffer("Probe");
        private readonly DeviceErrors _errors;

        /// <param name="labelFilePath">라벨 파일. 상대 경로면 exe 폴더 기준.</param>
        public Probe(string labelFilePath = "ProbeLabels.txt")
        {
            _errors = new DeviceErrors(_log);
            _labelFilePath = Path.IsPathRooted(labelFilePath)
                ? labelFilePath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, labelFilePath);
            LoadLabels();
        }

        /// <summary>Orbit 컨트롤러 연결 여부.</summary>
        public bool IsConnected => _server.Connected;

        /// <summary>마지막으로 실패한 명령의 원인 (없으면 null). 성공해도 지워지지 않는다. 읽기 결과의 오류는 여기 남지 않는다 (결과에 담김).</summary>
        public DeviceError LastError => _errors.Last;

        /// <summary>마지막 조회 이후 쌓인 로그를 꺼낸다 (꺼낸 로그는 버퍼에서 지워진다).</summary>
        public List<LogEntry> GetLogs()
        {
            return _log.Drain();
        }

        #region 연결 / 모듈 찾기

        /// <summary>Orbit 컨트롤러에 연결한다 (컨트롤러 종류는 자동 인식). 연결된 모듈은 이전에 등록된 것만 보이며, 새 모듈은 <see cref="ScanAsync"/>.</summary>
        public Task<bool> ConnectAsync()
        {
            return Task.Run(() =>
            {
                lock (_busLock)
                {
                    try
                    {
                        _server.Connect();
                        if (!_server.Connected)
                        {
                            _errors.Fail(T("Orbit 연결 실패 (컨트롤러가 연결되어 있는지 확인하세요)", "Orbit connection failed (check the controller)"));
                            return false;
                        }
                        _log.Add(T($"Orbit 연결됨 - 네트워크 {_server.Networks.Count}개 발견", $"Orbit connected - {_server.Networks.Count} network(s) found"));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _errors.Fail(T("오류: ", "ERROR: ") + ex.Message);
                        return _server.Connected;
                    }
                }
            });
        }

        /// <summary>
        /// 연결을 해제한다. <see cref="NotifyAddModuleAsync"/> 대기 중이면 대기를 취소한다.
        /// 스캔 등 다른 작업이 3초 안에 끝나지 않으면 false 를 돌려주고 아무것도 하지 않는다.
        /// </summary>
        public bool Disconnect()
        {
            if (!Monitor.TryEnter(_busLock))
            {
                // NotifyAddModule 은 프로브를 움직일 때까지 끝나지 않으므로 StopNotify 로 끊어준다.
                if (HasNetwork() && _server.Networks[0].Modules.Notifying)
                {
                    _server.Networks[0].Modules.StopNotify();
                }
                if (!Monitor.TryEnter(_busLock, 3000))
                {
                    _errors.Fail(T("스캔 등 작업 중에는 해제할 수 없습니다. 끝난 뒤 다시 시도하세요.", "Cannot disconnect while scanning etc. Try again when it finishes."));
                    return false;
                }
            }
            try
            {
                if (_server.Connected)
                {
                    _server.Disconnect();
                }
                _log.Add(T("Orbit 연결 해제", "Orbit disconnected"));
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(T("오류: ", "ERROR: ") + ex.Message);
                return false;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>네트워크의 모듈을 핑으로 찾는다 (약 12초). 반환: 전체 모듈 수 (실패하면 0).</summary>
        public Task<int> ScanAsync()
        {
            if (!HasNetwork())
            {
                _errors.Fail(T("연결되어 있지 않습니다", "Not connected"));
                return Task.FromResult(0);
            }

            _log.Add(T("프로브 스캔 중... (약 12초 소요)", "Scanning probes... (about 12 s)"));
            return Task.Run(() =>
            {
                lock (_busLock)
                {
                    try
                    {
                        OrbitModules modules = _server.Networks[0].Modules;
                        int added = modules.Ping();
                        _log.Add(T($"스캔 완료 - 이번에 {added}개 추가, 전체 {modules.Count}개", $"Scan done - {added} added, {modules.Count} total"));
                        return modules.Count;
                    }
                    catch (Exception ex)
                    {
                        _errors.Fail(T("오류: ", "ERROR: ") + ex.Message);
                        return 0;
                    }
                }
            });
        }

        /// <summary>
        /// 한 번도 등록되지 않은 새 모듈을 추가한다. 호출 후 대상 프로브를 움직이면 인식되어 true 로 끝난다.
        /// 움직일 때까지 끝나지 않으며, <see cref="Disconnect"/> 하면 false 로 취소된다.
        /// </summary>
        public Task<bool> NotifyAddModuleAsync()
        {
            if (!HasNetwork())
            {
                _errors.Fail(T("연결되어 있지 않습니다", "Not connected"));
                return Task.FromResult(false);
            }

            _log.Add(T("새 모듈 대기 중 - 등록할 프로브를 움직여 주세요...", "Waiting for a new module - move the probe to register..."));
            return Task.Run(() =>
            {
                lock (_busLock)
                {
                    try
                    {
                        bool added = _server.Networks[0].Modules.NotifyAddModule();
                        _log.Add(added ? T("모듈 추가됨", "Module added") : T("모듈 추가 취소됨", "Module add cancelled"));
                        return added;
                    }
                    catch (Exception ex)
                    {
                        _errors.Fail(T("오류: ", "ERROR: ") + ex.Message);
                        return false;
                    }
                }
            });
        }

        #endregion

        #region 읽기

        /// <summary>
        /// 모든 모듈의 현재값을 한 번 읽는다 (영점 적용, µm). 스캔 등 다른 작업 중이면 기다리지 않고 Busy 를 돌려준다.
        /// 한 모듈이라도 통신 오류면 Status = CommError (각 채널의 IsError/ErrorMessage 로 어느 모듈인지 확인).
        /// CommError 를 받으면 반복 호출을 멈추고 케이블 확인 후 다시 호출한다 (클래스 설명 참고).
        /// </summary>
        public ProbeReadResult ReadAll()
        {
            if (!Monitor.TryEnter(_busLock))
            {
                return new ProbeReadResult(ProbeReadStatus.Busy);
            }
            try
            {
                if (!HasNetwork())
                {
                    return new ProbeReadResult(ProbeReadStatus.NotConnected);
                }
                OrbitModules modules = _server.Networks[0].Modules;
                var channels = new List<ProbeChannel>();
                for (int i = 0; i < modules.Count; i++)
                {
                    channels.Add(ReadChannel(modules[i], i));
                }
                return new ProbeReadResult(channels.Any(ch => ch.IsError) ? ProbeReadStatus.CommError : ProbeReadStatus.Ok, channels);
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>
        /// 모듈 하나만 한 번 읽는다 (영점 적용, µm). 다른 작업 중이면 최대 1초 기다린 뒤 Busy.
        /// 결과의 Channels 에는 해당 모듈 하나만 들어 있다 (Ok / CommError 일 때).
        /// </summary>
        public ProbeReadResult Read(string moduleId)
        {
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                _log.Add(T("프로브가 사용 중(스캔 등)이라 읽지 못했습니다. 잠시 후 다시 시도하세요.", "Probe is busy (scanning etc.). Try again later."));
                return new ProbeReadResult(ProbeReadStatus.Busy);
            }
            try
            {
                if (!HasNetwork())
                {
                    return new ProbeReadResult(ProbeReadStatus.NotConnected);
                }
                OrbitModules modules = _server.Networks[0].Modules;
                int index = -1;
                for (int i = 0; i < modules.Count; i++)
                {
                    if (modules[i].ModuleID == moduleId)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    return new ProbeReadResult(ProbeReadStatus.ModuleNotFound);
                }
                ProbeChannel channel = ReadChannel(modules[index], index);
                return new ProbeReadResult(channel.IsError ? ProbeReadStatus.CommError : ProbeReadStatus.Ok, new List<ProbeChannel> { channel });
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>
        /// 값은 읽지 않고 등록된 모듈 목록(ModuleId, Label)만 가져온다 (버스 통신 없음). 다른 작업 중이면 최대 1초 기다린 뒤 Busy.
        /// </summary>
        public ProbeReadResult GetModuleList()
        {
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                return new ProbeReadResult(ProbeReadStatus.Busy);
            }
            try
            {
                if (!HasNetwork())
                {
                    return new ProbeReadResult(ProbeReadStatus.NotConnected);
                }
                OrbitModules modules = _server.Networks[0].Modules;
                var channels = new List<ProbeChannel>();
                for (int i = 0; i < modules.Count; i++)
                {
                    string moduleId = modules[i].ModuleID;
                    channels.Add(new ProbeChannel { Index = i, ModuleId = moduleId, Label = LabelOf(moduleId) });
                }
                return new ProbeReadResult(ProbeReadStatus.Ok, channels);
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        #endregion

        #region 영점 / 라벨

        /// <summary>모듈 하나의 현재값을 영점(소프트웨어 오프셋)으로 잡는다. 이후 읽기 값은 이 값을 뺀 값이다.</summary>
        public bool Zero(string moduleId)
        {
            // 주기 읽기가 잠깐 잡고 있을 수 있으므로 조금 기다린다.
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                _errors.Fail(T("프로브가 사용 중(스캔 등)이라 영점을 잡지 못했습니다. 잠시 후 다시 시도하세요.", "Probe is busy (scanning etc.) - zero not set. Try again later."));
                return false;
            }
            try
            {
                if (!HasNetwork())
                {
                    throw new DeviceException(T("연결되어 있지 않습니다", "Not connected"));
                }
                OrbitModule m = _server.Networks[0].Modules.GetModuleByID(moduleId);
                if (m == null)
                {
                    throw new DeviceException(T($"모듈 {moduleId} 이(가) 없습니다", $"Module {moduleId} not found"));
                }
                _zeroOffsets[moduleId] = ReadMicrometers(m);
                return true;
            }
            catch (Exception ex)
            {
                _errors.Fail(T($"오류: {moduleId} 영점 실패 - {ex.Message}", $"ERROR: {moduleId} zero failed - {ex.Message}"));
                return false;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>모든 모듈의 현재값을 영점으로 잡는다. 하나라도 실패하면 false (나머지는 적용됨).</summary>
        public bool ZeroAll()
        {
            // 주기 읽기가 잠깐 잡고 있을 수 있으므로 조금 기다린다.
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                _errors.Fail(T("프로브가 사용 중(스캔 등)이라 영점을 잡지 못했습니다. 잠시 후 다시 시도하세요.", "Probe is busy (scanning etc.) - zero not set. Try again later."));
                return false;
            }
            try
            {
                if (!HasNetwork())
                {
                    _errors.Fail(T("연결되어 있지 않습니다", "Not connected"));
                    return false;
                }
                bool ok = true;
                OrbitModules modules = _server.Networks[0].Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    try
                    {
                        _zeroOffsets[modules[i].ModuleID] = ReadMicrometers(modules[i]);
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        _errors.Fail(T($"오류: {modules[i].ModuleID} 영점 실패 - {ex.Message}", $"ERROR: {modules[i].ModuleID} zero failed - {ex.Message}"));
                    }
                }
                return ok;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>모듈의 라벨을 정하고 ProbeLabels.txt 에 저장한다.</summary>
        public void SetLabel(string moduleId, string label)
        {
            // ReadAll(백그라운드)이 동시에 읽으므로 잠근다.
            lock (_labels)
            {
                _labels[moduleId] = label;
                SaveLabels();
            }
        }

        #endregion

        #region private

        private ProbeChannel ReadChannel(OrbitModule m, int index)
        {
            var channel = new ProbeChannel { Index = index, ModuleId = m.ModuleID, Label = LabelOf(m.ModuleID) };
            try
            {
                channel.RawReading = ReadMicrometers(m);
                _zeroOffsets.TryGetValue(m.ModuleID, out double offset);
                channel.Reading = channel.RawReading - offset;
            }
            catch (Exception ex)
            {
                channel.IsError = true;
                channel.ErrorMessage = ex.Message;
            }
            return channel;
        }

        private string LabelOf(string moduleId)
        {
            string label;
            lock (_labels)
            {
                _labels.TryGetValue(moduleId, out label);
            }
            return string.IsNullOrEmpty(label) ? moduleId : label;
        }

        /// <summary>
        /// 모듈 값을 µm 로 읽는다. SDK 의 ReadingInUnits 는 모듈의 UnitsOfMeasure(DP 는 "mm") 단위라서 환산한다.
        /// 모르는 단위면 예외를 던져 오류로 보이게 한다 (잘못 환산된 값을 조용히 돌려주지 않기 위함).
        /// </summary>
        private static double ReadMicrometers(OrbitModule module)
        {
            double value = module.ReadingInUnits;
            string unit = (module.UnitsOfMeasure ?? "").Trim().ToLowerInvariant();
            switch (unit)
            {
                case "mm":
                    return value * 1000.0;
                case "um":
                case "µm":
                case "micron":
                case "microns":
                    return value;
                case "in":
                case "inch":
                case "inches":
                    return value * 25400.0;
                default:
                    throw new DeviceException(T($"지원하지 않는 단위: '{module.UnitsOfMeasure}'", $"Unsupported unit: '{module.UnitsOfMeasure}'"));
            }
        }

        private bool HasNetwork()
        {
            return _server.Connected && _server.Networks.Count > 0;
        }

        private void LoadLabels()
        {
            try
            {
                if (!File.Exists(_labelFilePath))
                {
                    return;
                }
                foreach (var line in File.ReadAllLines(_labelFilePath))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        _labels[parts[0]] = parts[1];
                    }
                }
            }
            catch
            {
                // 라벨 로드 실패는 무시한다 - 모듈ID 그대로 표시된다.
            }
        }

        private void SaveLabels()
        {
            try
            {
                File.WriteAllLines(_labelFilePath, _labels.Select(kv => $"{kv.Key}={kv.Value}"));
            }
            catch
            {
                // 라벨 저장 실패는 무시한다.
            }
        }

        #endregion
    }

    /// <summary>프로브 모듈 하나의 값.</summary>
    public class ProbeChannel
    {
        /// <summary>네트워크 안의 모듈 순서</summary>
        public int Index;
        /// <summary>모듈 ID (Orbit 고유값)</summary>
        public string ModuleId;
        /// <summary>라벨 (지정하지 않았으면 모듈 ID)</summary>
        public string Label;
        /// <summary>영점 적용된 값 (µm)</summary>
        public double Reading;
        /// <summary>영점 적용 전 원시값 (µm)</summary>
        public double RawReading;
        /// <summary>이 모듈 읽기 실패 여부</summary>
        public bool IsError;
        /// <summary>실패 원인 (SDK 메시지, 예: "Receive Timeout")</summary>
        public string ErrorMessage;
    }

    /// <summary>프로브 읽기 결과 상태.</summary>
    public enum ProbeReadStatus
    {
        /// <summary>정상</summary>
        Ok,
        /// <summary>Orbit 컨트롤러 미연결 (버스 통신 안 함)</summary>
        NotConnected,
        /// <summary>스캔 등 다른 작업 중이라 읽지 않음 - 잠시 후 다시 호출</summary>
        Busy,
        /// <summary>하나 이상의 모듈 통신 오류 - 반복 호출을 멈추고 케이블 확인 후 다시 호출</summary>
        CommError,
        /// <summary>지정한 모듈이 없음 (<see cref="Probe.Read"/>)</summary>
        ModuleNotFound
    }

    /// <summary>프로브 읽기 결과.</summary>
    public class ProbeReadResult
    {
        /// <summary>상태</summary>
        public ProbeReadStatus Status { get; }
        /// <summary>모듈별 값. NotConnected / Busy / ModuleNotFound 면 빈 목록.</summary>
        public List<ProbeChannel> Channels { get; }

        internal ProbeReadResult(ProbeReadStatus status, List<ProbeChannel> channels = null)
        {
            Status = status;
            Channels = channels ?? new List<ProbeChannel>();
        }
    }
}
