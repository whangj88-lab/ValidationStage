using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Solartron.Orbit3;

namespace ValidationStage.Probe
{
    /// <summary>
    /// Solartron Orbit3 네트워크(RS232IM / USBIM / ETHIM 컨트롤러 한 대에 DP10, DP20 등 Digital Probe
    /// 모듈이 데이지체인으로 여러 개 연결된 구성)를 감싸는 얇은 래퍼.
    ///
    /// A 프로젝트의 GageCounter 처럼 채널 수/COM 포트를 고정하지 않는다.
    /// Orbit 쪽은 OrbitServer.Connect() 한 번으로 사용 가능한 네트워크를 전부 찾고,
    /// 네트워크에 연결된 모듈들은 Scan(Ping)/NotifyAddModule 로 자동 인식하는 구조이므로,
    /// 여기서도 "연결된 모듈 = 채널"로 취급하고 개수는 실행 중에 결정되게 둔다.
    /// 모듈ID <-> 사람이 읽는 라벨(X, TZ1 등) 매핑만 파일로 저장해서 재실행시 유지한다.
    /// </summary>
    public class SolartronProbe
    {
        private readonly OrbitServer _server = new OrbitServer();
        private readonly Dictionary<string, double> _zeroOffsets = new Dictionary<string, double>();
        private readonly Dictionary<string, string> _labels = new Dictionary<string, string>();
        private readonly string _labelFilePath;

        // SDK(모듈 목록/읽기) 접근을 직렬화한다. Ping/Notify/주기 읽기(ReadAll)는 모두 백그라운드 스레드에서 돈다.
        // ReadAll 은 다른 작업 중이면 기다리지 않고 이번 주기를 건너뛰고, 영점/해제는 잠깐 기다린 뒤 포기한다.
        private readonly object _busLock = new object();

        public event EventHandler<string> Logged;

        public bool Connected => _server.Connected;

        /// <param name="labelFilePath">상대 경로면 exe 폴더 기준 (실행 위치/바로가기 시작 위치와 무관하게 같은 파일을 쓰도록)</param>
        public SolartronProbe(string labelFilePath = "ProbeLabels.txt")
        {
            _labelFilePath = Path.IsPathRooted(labelFilePath)
                ? labelFilePath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, labelFilePath);
            LoadLabels();
        }

        /// <summary>
        /// Orbit 서버에 연결한다. 컨트롤러 초기화에 시간이 걸리므로 백그라운드로 실행한다.
        /// </summary>
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
                            Logged?.Invoke(this, "Orbit 연결 실패 (컨트롤러가 연결되어 있는지 확인하세요)");
                            return false;
                        }
                        Logged?.Invoke(this, $"Orbit 연결됨 - 네트워크 {_server.Networks.Count}개 발견");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logged?.Invoke(this, "ERROR: " + ex.Message);
                        return _server.Connected;
                    }
                }
            });
        }

        /// <summary>
        /// [새 모듈 추가] 대기 중이면 대기를 취소하고 해제한다.
        /// 스캔 등 다른 작업이 끝나지 않으면 false 를 반환하고 아무것도 하지 않는다.
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
                    Logged?.Invoke(this, "스캔 등 작업 중에는 해제할 수 없습니다. 끝난 뒤 다시 시도하세요.");
                    return false;
                }
            }
            try
            {
                if (_server.Connected)
                {
                    _server.Disconnect();
                }
                Logged?.Invoke(this, "Orbit 연결 해제");
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, "ERROR: " + ex.Message);
                return false;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>
        /// 네트워크에 연결된 모듈들을 핑으로 스캔한다. 약 12초 정도 걸리므로 백그라운드로 실행한다.
        /// </summary>
        public Task<int> ScanAsync()
        {
            if (!HasNetwork())
            {
                return Task.FromResult(0);
            }

            Logged?.Invoke(this, "프로브 스캔 중... (약 12초 소요)");
            return Task.Run(() =>
            {
                lock (_busLock)
                {
                    try
                    {
                        OrbitModules modules = _server.Networks[0].Modules;
                        int added = modules.Ping();
                        Logged?.Invoke(this, $"스캔 완료 - 이번에 {added}개 추가, 전체 {modules.Count}개");
                        return modules.Count;
                    }
                    catch (Exception ex)
                    {
                        Logged?.Invoke(this, "ERROR: " + ex.Message);
                        return 0;
                    }
                }
            });
        }

        /// <summary>
        /// 한 번도 등록되지 않은 새 모듈을 추가한다. 호출 후 대상 프로브를 움직이면 인식된다.
        /// (SDK 내부적으로 모듈이 움직임을 감지할 때까지 대기하는 블로킹 호출이라 백그라운드로 돌린다.)
        /// </summary>
        public Task<bool> NotifyAddModuleAsync()
        {
            if (!HasNetwork())
            {
                return Task.FromResult(false);
            }

            Logged?.Invoke(this, "새 모듈 대기 중 - 등록할 프로브를 움직여 주세요...");
            return Task.Run(() =>
            {
                lock (_busLock)
                {
                    try
                    {
                        bool added = _server.Networks[0].Modules.NotifyAddModule();
                        Logged?.Invoke(this, added ? "모듈 추가됨" : "모듈 추가 취소됨");
                        return added;
                    }
                    catch (Exception ex)
                    {
                        Logged?.Invoke(this, "ERROR: " + ex.Message);
                        return false;
                    }
                }
            });
        }

        /// <summary>
        /// 연결된 모든 모듈의 현재값을 읽어온다.
        /// 백그라운드 스레드에서 호출한다 (끊긴 모듈은 Receive Timeout 까지 블록되므로).
        /// 스캔 등 다른 작업 중이면 null 을 반환한다 (호출 측은 이번 주기를 건너뛴다).
        /// </summary>
        public List<ProbeChannel> ReadAll()
        {
            var result = new List<ProbeChannel>();
            if (!Monitor.TryEnter(_busLock))
            {
                return null;
            }
            try
            {
                if (!HasNetwork())
                {
                    return result;
                }
                OrbitModules modules = _server.Networks[0].Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    OrbitModule m = modules[i];
                    var channel = new ProbeChannel { Index = i, ModuleId = m.ModuleID };
                    string label;
                    lock (_labels)
                    {
                        _labels.TryGetValue(m.ModuleID, out label);
                    }
                    channel.Label = string.IsNullOrEmpty(label) ? m.ModuleID : label;

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

                    result.Add(channel);
                }
                return result;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>
        /// 값은 읽지 않고 현재 등록된 모듈 목록(ModuleId, Label)만 가져온다. 읽기 중지 상태에서 스캔 결과를 화면에 반영할 때 쓴다.
        /// 다른 작업 중이라 잠깐 기다려도 못 잡으면 null 을 반환한다.
        /// </summary>
        public List<ProbeChannel> GetModuleList()
        {
            var result = new List<ProbeChannel>();
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                return null;
            }
            try
            {
                if (!HasNetwork())
                {
                    return result;
                }
                OrbitModules modules = _server.Networks[0].Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    string moduleId = modules[i].ModuleID;
                    string label;
                    lock (_labels)
                    {
                        _labels.TryGetValue(moduleId, out label);
                    }
                    result.Add(new ProbeChannel
                    {
                        Index = i,
                        ModuleId = moduleId,
                        Label = string.IsNullOrEmpty(label) ? moduleId : label
                    });
                }
                return result;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>
        /// 지정한 모듈 하나만 현재값을 한 번 읽는다 (영점 오프셋 적용). 끊긴 모듈은 Receive Timeout 까지 블록되므로
        /// 백그라운드 스레드에서 호출한다. 다른 작업 중이라 잠깐 기다려도 못 잡거나 모듈이 없으면 null.
        /// 통신 실패는 IsError/ErrorMessage 로 돌려준다 (ReadAll 과 같은 형식).
        /// </summary>
        public ProbeChannel Read(string moduleId)
        {
            // 주기 읽기가 잠깐 잡고 있을 수 있으므로 조금 기다린다.
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                Logged?.Invoke(this, "프로브가 사용 중(스캔 등)이라 읽지 못했습니다. 잠시 후 다시 시도하세요.");
                return null;
            }
            try
            {
                if (!HasNetwork())
                {
                    return null;
                }
                OrbitModules modules = _server.Networks[0].Modules;
                OrbitModule m = modules.GetModuleByID(moduleId);
                if (m == null)
                {
                    return null;
                }

                var channel = new ProbeChannel { ModuleId = moduleId };
                string label;
                lock (_labels)
                {
                    _labels.TryGetValue(moduleId, out label);
                }
                channel.Label = string.IsNullOrEmpty(label) ? moduleId : label;
                try
                {
                    channel.RawReading = ReadMicrometers(m);
                    _zeroOffsets.TryGetValue(moduleId, out double offset);
                    channel.Reading = channel.RawReading - offset;
                }
                catch (Exception ex)
                {
                    channel.IsError = true;
                    channel.ErrorMessage = ex.Message;
                }
                return channel;
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        /// <summary>지정한 모듈 하나만 영점(소프트웨어 오프셋)을 잡는다.</summary>
        public void Zero(string moduleId)
        {
            // 주기 읽기가 잠깐 잡고 있을 수 있으므로 조금 기다린다.
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                Logged?.Invoke(this, "프로브가 사용 중(스캔 등)이라 영점을 잡지 못했습니다. 잠시 후 다시 시도하세요.");
                return;
            }
            try
            {
                if (!HasNetwork())
                {
                    return;
                }
                OrbitModule m = _server.Networks[0].Modules.GetModuleByID(moduleId);
                if (m == null)
                {
                    return;
                }
                _zeroOffsets[moduleId] = ReadMicrometers(m);
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, $"ERROR: {moduleId} 영점 실패 - {ex.Message}");
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        public void ZeroAll()
        {
            // 주기 읽기가 잠깐 잡고 있을 수 있으므로 조금 기다린다.
            if (!Monitor.TryEnter(_busLock, 1000))
            {
                Logged?.Invoke(this, "프로브가 사용 중(스캔 등)이라 영점을 잡지 못했습니다. 잠시 후 다시 시도하세요.");
                return;
            }
            try
            {
                if (!HasNetwork())
                {
                    return;
                }
                OrbitModules modules = _server.Networks[0].Modules;
                for (int i = 0; i < modules.Count; i++)
                {
                    try
                    {
                        _zeroOffsets[modules[i].ModuleID] = ReadMicrometers(modules[i]);
                    }
                    catch (Exception ex)
                    {
                        Logged?.Invoke(this, $"ERROR: {modules[i].ModuleID} 영점 실패 - {ex.Message}");
                    }
                }
            }
            finally
            {
                Monitor.Exit(_busLock);
            }
        }

        public void SetLabel(string moduleId, string label)
        {
            // ReadAll(백그라운드)이 동시에 읽으므로 잠근다.
            lock (_labels)
            {
                _labels[moduleId] = label;
                SaveLabels();
            }
        }

        /// <summary>
        /// 모듈 값을 µm 로 읽는다. SDK 의 ReadingInUnits 는 모듈의 UnitsOfMeasure(DP 는 "mm") 단위라서 환산한다.
        /// 모르는 단위면 예외를 던져 그리드에 오류로 보이게 한다 (잘못 환산된 값을 조용히 보여주지 않기 위함).
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
                    throw new Exception($"지원하지 않는 단위: '{module.UnitsOfMeasure}'");
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
                // 라벨 로드 실패는 무시한다 - 화면에는 모듈ID 그대로 표시된다.
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
    }
}
