using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static ValidationStage.Devices.Messages;

// 고객사에 알리지 않는 기능이라 API 설명 파일(xml)에 나오지 않게 /// 대신 // 로 쓰고, 자동 완성에서도 숨긴다 (EditorBrowsable, 2026-11-23 사용자 결정).
#pragma warning disable 1591   // public 설명(///) 없음 경고

namespace ValidationStage.Devices
{
    // 프로브 기울기 보정 계수 자동 측정. 순서/계산은 ProbeTiltMeasurement, 여기는 장비 동작과 중단 처리, 파일 적용.
    public partial class ValidationSystem
    {
        // 프로브 기울기 측정 결과를 저장하는 폴더.
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static readonly string ProbeTiltDirectory = SettingPath("ProbeTilt");

        private static readonly string ProbeTiltFile = SettingPath("ProbeTiltCorrection.xml");

        // 목표 위치와 이만큼 (µm) 넘게 다르면 도달하지 못한 것으로 본다 (리밋에서 멈춘 경우 등).
        private const double TiltPositionTolerance = 1.0;

        // 마지막으로 전체 시스템 초기화를 끝까지 마쳤을 때의 장비별 기준 버전. 초기화된 적 없으면 null.
        private int[] _systemInitVersions;

        private volatile bool _tiltRunning;
        private volatile bool _tiltStopRequested;
        private volatile ProbeTiltProgress _tiltProgress = new ProbeTiltProgress(ProbeTiltPhase.None, 0, 0, Axis.X, 0);

        /// <summary>
        /// 전체 시스템 초기화(<see cref="HomeAndZeroProbesAsync"/>)를 끝까지(프로브 영점까지) 마친 상태가 유지되고 있는지.
        /// 그 뒤 장비 연결 해제, 원점 찾기, 헥사포드 레퍼런스/좌표계 변경, 프로브 영점/스캔/새 모듈 추가를 하면 false 로 돌아간다 (조그/이동은 상관없음).
        /// </summary>
        public bool IsSystemInitialized
        {
            get
            {
                int[] versions = _systemInitVersions;
                return versions != null && Stage.IsConnected && Hexapod.IsConnected && Probe.IsConnected
                    && versions.SequenceEqual(CurrentReferenceVersions());
            }
        }

        // 프로브 기울기 측정 실행 중 여부.
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsProbeTiltRunning => _tiltRunning;

        // 프로브 기울기 측정 진행 상태 (실행 중이 아니면 Phase = None).
        [EditorBrowsable(EditorBrowsableState.Never)]
        public ProbeTiltProgress CurrentProbeTiltProgress => _tiltProgress;

        // 마지막 프로브 기울기 측정이 정지 명령으로 중단됐으면 true (다음 실행을 시작하면 false 로 돌아간다).
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ProbeTiltAborted { get; private set; }

        // 프로브 기울기 보정 계수를 자동으로 측정하고 ProbeTiltDirectory 에 저장한다 (적용은 ApplyProbeTilt).
        // 전체 시스템 초기화가 된 상태(IsSystemInitialized)에서만 실행되며, 속도를 저속으로 바꾼다.
        // 헥사포드/스테이지를 0 으로 보낸 뒤 X, Y 를 각각 ±1600 µm (시작점은 −1750 µm) 구간에서 100 µm 간격으로 움직이며 읽는다:
        // X 를 예비 1회(버림) → 측정 3회 → 검증 1회 한 뒤 Y 를 똑같이 한다. 끝나면 0 으로 돌아온다 (− 쪽에서 도착). 10분 안팎 걸린다.
        // 리밋 감지, 목표 위치 미도달, 프로브 읽기 실패면 중단한다. 실행 중 정지 명령(EmergencyStopAllAsync,
        // StopAllAsync, StopAxisAsync)을 부르면 그 자리에서 중단하고 ProbeTiltAborted = true.
        // 측정 결과. 실패/중단이면 null (원인은 LastError).
        [EditorBrowsable(EditorBrowsableState.Never)]
        public async Task<ProbeTiltResult> MeasureProbeTiltAsync()
        {
            lock (_sequenceLock)
            {
                if (_tiltRunning || _systemInitRunning)
                {
                    Fail(T("전체 시스템 초기화 또는 프로브 기울기 측정이 이미 실행 중입니다", "System init or probe tilt measurement is already running"));
                    return null;
                }
                _tiltStopRequested = false;   // 실행 표시보다 먼저 지워야 막 시작할 때 들어온 정지 요청을 잃지 않는다
                _tiltRunning = true;
            }
            ProbeTiltAborted = false;
            try
            {
                // 이동 사이의 프로브 읽기가 블록되므로 호출한 스레드(UI)를 막지 않게 백그라운드에서 한다.
                return await Task.Run(() => RunProbeTiltAsync());
            }
            catch (Exception ex)
            {
                Fail(T("프로브 기울기 측정 실패: ", "Probe tilt measurement failed: ") + ex.Message);
                return null;
            }
            finally
            {
                _tiltProgress = new ProbeTiltProgress(ProbeTiltPhase.None, 0, 0, Axis.X, 0);
                _tiltRunning = false;
            }
        }

        // 측정 결과의 계수를 적용한다: 지금 ProbeTiltCorrection.xml 을 ProbeTiltDirectory 에 백업(…_backup_날짜.xml)한 뒤
        // 새 계수로 바꾸고, 다음 읽기부터 바로 쓴다 (프로그램을 다시 켤 필요 없음).
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ApplyProbeTilt(ProbeTiltResult result)
        {
            if (result?.Correction == null)
            {
                return Fail(T("적용할 프로브 기울기 측정 결과가 없습니다", "No probe tilt result to apply"));
            }
            try
            {
                if (File.Exists(ProbeTiltFile))
                {
                    Directory.CreateDirectory(ProbeTiltDirectory);
                    string backup = Path.Combine(ProbeTiltDirectory, $"ProbeTiltCorrection_backup_{DateTime.Now:yyMMdd_HHmmss}.xml");
                    File.Copy(ProbeTiltFile, backup, true);
                    _log.Add(T("기존 프로브 기울기 보정값 백업: ", "Backed up previous probe tilt correction: ") + backup);
                }
                // 쓰다가 실패해도 기존 파일이 깨지지 않게 임시 파일에 쓴 뒤 바꾼다.
                string temp = ProbeTiltFile + ".tmp";
                result.Correction.Save(temp);
                File.Copy(temp, ProbeTiltFile, true);
                File.Delete(temp);
                Probe.TiltCorrection = result.Correction;
                _log.Add(T("프로브 기울기 보정값 적용: ", "Applied probe tilt correction: ") + result.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                return Fail(T("프로브 기울기 보정값 적용 실패: ", "Failed to apply probe tilt correction: ") + ex.Message);
            }
        }

        private async Task<ProbeTiltResult> RunProbeTiltAsync()
        {
            if (!IsSystemInitialized)
            {
                Fail(T("전체 시스템 초기화를 먼저 하세요 (초기화 후 영점/원점/연결이 바뀌면 다시 해야 합니다)",
                    "Run system init first (again if zero/home/connection changed after it)"));
                return null;
            }
            if (!Track(Probe.CheckTiltModules(), Probe.LastError))
            {
                return null;
            }
            if (!Track(Stage.SetSpeedLevel(SpeedLevel.Low), Stage.LastError) || !Track(Hexapod.SetSpeedLevel(SpeedLevel.Low), Hexapod.LastError))
            {
                return null;
            }

            ReportTilt(new ProbeTiltProgress(ProbeTiltPhase.Preparing, 0, 0, Axis.X, 0));
            _log.Add(T("프로브 기울기 측정 시작: 헥사포드/스테이지 0 으로 이동", "Probe tilt measurement: moving hexapod/stage to 0"));
            if (!TiltStepOk(await MoveHexapodToZeroAsync()) || !TiltStepOk(await MoveStageToZeroAsync()))
            {
                return null;
            }

            var measurement = new ProbeTiltMeasurement(TiltMoveAsync, TiltRead, TiltDelayAsync, ReportTilt);
            ProbeTiltResult result = await measurement.RunAsync(Probe.TiltCorrection);
            if (result == null)
            {
                return null;
            }

            ReportTilt(new ProbeTiltProgress(ProbeTiltPhase.Returning, 0, 0, Axis.X, ProbeTiltMeasurement.TotalPoints));
            ProbeTiltMeasurement.Save(result, measurement.Points, ProbeTiltDirectory, DateTime.Now);
            _log.Add(T("프로브 기울기 측정 결과 저장: ", "Probe tilt result saved: ") + result.FilePath);
            foreach (string warning in result.Warnings)
            {
                _log.Add(T("경고: ", "Warning: ") + warning);
            }
            if (!await measurement.ParkAsync(Axis.Y))
            {
                _log.Add(T("프로브 기울기 측정은 끝났지만 원점 복귀에 실패했습니다", "Probe tilt measurement done, but returning to 0 failed"));
            }
            return result;
        }

        private void ReportTilt(ProbeTiltProgress progress)
        {
            if (progress.Phase != _tiltProgress.Phase || progress.Cycle != _tiltProgress.Cycle || progress.Axis != _tiltProgress.Axis)
            {
                string name = null;
                switch (progress.Phase)
                {
                    case ProbeTiltPhase.WarmUp: name = T("예비", "warm-up"); break;
                    case ProbeTiltPhase.Measuring: name = T("측정", "measure"); break;
                    case ProbeTiltPhase.Verifying: name = T("검증", "verify"); break;
                }
                if (name != null)
                {
                    _log.Add(T($"프로브 기울기 {name} 스캔 {progress.Cycle}/{progress.CycleCount}: {progress.Axis}", $"Probe tilt {name} scan {progress.Cycle}/{progress.CycleCount}: {progress.Axis}"));
                }
            }
            _tiltProgress = progress;
        }

        // 한 축을 절대 이동하고 도달했는지 확인한다.
        private async Task<bool> TiltMoveAsync(Axis axis, double target)
        {
            if (CheckTiltAborted() || !TiltStepOk(Track(await Stage.MoveAsync(new[] { axis }, new[] { target }, true), Stage.LastError)))
            {
                return false;
            }
            AxisStatus status = Stage.GetAxisStatuses()?.FirstOrDefault(s => s.Axis == axis);
            if (status == null)
            {
                return Fail(T("스테이지 상태를 읽지 못해 프로브 기울기 측정을 중단합니다", "Probe tilt measurement stopped - stage status not available"));
            }
            if (status.IsPosLimit || status.IsNegLimit)
            {
                return Fail(T($"{axis} 축 리밋이 감지되어 프로브 기울기 측정을 중단합니다", $"Probe tilt measurement stopped - {axis} limit detected"));
            }
            if (Math.Abs(status.Position - target) > TiltPositionTolerance)
            {
                return Fail(T($"{axis} 축이 목표 위치에 도달하지 못해 프로브 기울기 측정을 중단합니다 (목표 {target:0.0}, 현재 {status.Position:0.0} µm)",
                    $"Probe tilt measurement stopped - {axis} did not reach the target (target {target:0.0}, actual {status.Position:0.0} um)"));
            }
            return true;
        }

        private double[] TiltRead()
        {
            if (CheckTiltAborted())
            {
                return null;
            }
            if (!IsSystemInitialized)
            {
                Fail(T("측정 중에 영점/원점/연결이 바뀌어 프로브 기울기 측정을 중단합니다", "Probe tilt measurement stopped - zero/home/connection changed"));
                return null;
            }
            double[] values = Probe.ReadZeroed();
            Track(values != null, Probe.LastError);
            return values;
        }

        private async Task<bool> TiltDelayAsync(int milliseconds)
        {
            await Task.Delay(milliseconds);
            return !CheckTiltAborted();
        }

        // 정지 요청이 들어왔으면 중단 처리 (LastError + 로그) 후 true.
        private bool CheckTiltAborted()
        {
            if (!_tiltStopRequested)
            {
                return false;
            }
            ProbeTiltAborted = true;
            Fail(T("프로브 기울기 측정이 정지 명령으로 중단되었습니다", "Probe tilt measurement was interrupted by a stop command"));
            return true;
        }

        private bool TiltStepOk(bool ok)
        {
            return !CheckTiltAborted() && ok;
        }

        private void RequestTiltStop()
        {
            if (_tiltRunning)
            {
                _tiltStopRequested = true;
            }
        }

        private int[] CurrentReferenceVersions()
        {
            return new[] { Stage.ReferenceVersion, Hexapod.ReferenceVersion, Probe.ReferenceVersion };
        }
    }
}
