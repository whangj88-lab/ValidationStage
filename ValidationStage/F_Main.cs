using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ValidationStage.Devices;

namespace ValidationStage
{
    /// <summary>
    /// 모션(리니어 스테이지 X,Y,Z + 헥사포드 TX,TY,TZ)과 Solartron 프로브만 다루는 단순 조작 화면.
    /// 레시피/자동 측정/Pass-Fail 판정은 없음 - 수동 Jog + 실시간 값 표시가 전부다.
    /// 컨트롤 배치는 F_Main.Designer.cs (디자이너)에 있다.
    /// ValidationStage.Devices.dll 사용 예제를 겸한다: 장비 상태/로그는 _statusTimer 에서 조회 방식으로 받는다.
    /// </summary>
    public partial class F_Main : Form
    {
        private readonly ValidationSystem _system = new ValidationSystem();

        // 프로브 연속 읽기 중인지. Solartron OrbitLibraryTest 의 Start/Stop Continuous 와 같은 개념:
        // 통신 오류(Receive Timeout 등)가 나면 즉시 읽기를 멈춰 버스를 조용히 두고, 케이블 재연결 후 [읽기 시작]으로 재개한다.
        // (오류 난 모듈을 계속 읽고 있으면 재연결해도 전원 재투입 전까지 복구되지 않았음)
        private bool _probeReading;
        private bool _probeReadInFlight;
        private readonly HashSet<string> _singleReadsInFlight = new HashSet<string>();
        private Hexapod3DForm _hexapod3DForm;

        // 연결 상태 표시를 바뀔 때만 갱신하려고 마지막 상태를 기억한다 ("연결 중..."/"연결 실패" 표시를 덮어쓰지 않게).
        private bool _stageConnected;
        private bool _hexapodConnected;

        private readonly Dictionary<Axis, Label> _posLabels = new Dictionary<Axis, Label>();
        private readonly Dictionary<Axis, Label> _statusLabels = new Dictionary<Axis, Label>();

        public F_Main()
        {
            InitializeComponent();

            // DLL 이 내보내는 로그/오류 메시지 언어 (영어: Language.English)
            Messages.Language = Language.Korean;

            // SelectedIndexChanged 연결 전에 초기값을 넣어 시작 시 ApplySpeed 가 호출되지 않게 한다.
            _speedCombo.SelectedIndex = 0;
            _speedCombo.SelectedIndexChanged += (s, e) => ApplySpeed(_speedCombo.SelectedIndex);

            BindAxis(Axis.X, _xMinusButton, _xPlusButton, _xPosLabel, _xStatusLabel);
            BindAxis(Axis.Y, _yMinusButton, _yPlusButton, _yPosLabel, _yStatusLabel);
            BindAxis(Axis.Z, _zMinusButton, _zPlusButton, _zPosLabel, _zStatusLabel);
            BindAxis(Axis.TX, _txMinusButton, _txPlusButton, _txPosLabel, _txStatusLabel);
            BindAxis(Axis.TY, _tyMinusButton, _tyPlusButton, _tyPosLabel, _tyStatusLabel);
            BindAxis(Axis.TZ, _tzMinusButton, _tzPlusButton, _tzPosLabel, _tzStatusLabel);

            BindAbsMove(Axis.X, _xTargetInput, _xMoveButton);
            BindAbsMove(Axis.Y, _yTargetInput, _yMoveButton);
            BindAbsMove(Axis.Z, _zTargetInput, _zMoveButton);
            BindAbsMove(Axis.TX, _txTargetInput, _txMoveButton);
            BindAbsMove(Axis.TY, _tyTargetInput, _tyMoveButton);
            BindAbsMove(Axis.TZ, _tzTargetInput, _tzMoveButton);

            BindStop(Axis.X, _xStopButton);
            BindStop(Axis.Y, _yStopButton);
            BindStop(Axis.Z, _zStopButton);
            BindStop(Axis.TX, _txStopButton);
            BindStop(Axis.TY, _tyStopButton);
            BindStop(Axis.TZ, _tzStopButton);

            UpdateHomeLabels();
            _stageHostBox.Text = _system.StageHost;
            _hexapodHostBox.Text = _system.HexapodHost;
            UpdateProbeButtons();

            _statusTimer.Start();
            _probeTimer.Start();
        }

        /// <summary>
        /// 축 한 줄의 Jog 버튼/표시 라벨을 연결한다. ValidationSystem.JogRun/JogStop 이 축에 따라 스테이지/헥사포드로 나눠 보낸다.
        /// </summary>
        private void BindAxis(Axis axis, Button minusBtn, Button plusBtn, Label posLabel, Label statusLabel)
        {
            // 헥사포드 Jog 는 이동 한계까지 MOV 를 보내 두는 방식이라 정지가 빠지면 한계까지 간다.
            // MouseUp 없이 마우스 캡처를 잃는 경우(누른 채 Alt+Tab 등)에도 MouseCaptureChanged 로 정지한다 (한 번만).
            bool jogging = false;
            void Start(bool dir) { jogging = _system.JogRun(axis, dir); }
            void Stop() { if (jogging) { jogging = false; _system.JogStop(axis); } }

            minusBtn.MouseDown += (s, e) => Start(false);
            minusBtn.MouseUp += (s, e) => Stop();
            minusBtn.MouseCaptureChanged += (s, e) => Stop();
            plusBtn.MouseDown += (s, e) => Start(true);
            plusBtn.MouseUp += (s, e) => Stop();
            plusBtn.MouseCaptureChanged += (s, e) => Stop();
            _posLabels[axis] = posLabel;
            _statusLabels[axis] = statusLabel;
        }

        /// <summary>
        /// 축 한 줄의 [정지] 버튼을 연결한다 (감속 정지). 이동/원점찾기 중에도 눌러야 하므로 버튼은 막지 않는다.
        /// </summary>
        private void BindStop(Axis axis, Button stopBtn)
        {
            stopBtn.Click += async (s, e) =>
            {
                await _system.StopAxisAsync(axis);
            };
        }

        /// <summary>
        /// 축 한 줄의 절대 이동 입력칸/[이동] 버튼을 연결한다. 목표값은 기계 좌표 (X,Y,Z: µm / TX,TY,TZ: arcmin).
        /// </summary>
        private void BindAbsMove(Axis axis, NumericUpDown targetInput, Button moveBtn)
        {
            moveBtn.Click += async (s, e) =>
            {
                moveBtn.Enabled = false;
                await _system.MoveAbsAsync(axis, (double)targetInput.Value);
                moveBtn.Enabled = true;
            };
        }

        #region UI 이벤트

        private void F_Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            _statusTimer.Stop();
            _probeTimer.Stop();
            _system.DisconnectAll();
        }

        private async void StageConnectButton_Click(object sender, EventArgs e)
        {
            if (_system.Stage.IsConnected)
            {
                AppendLog("[Stage] 이미 연결되어 있습니다");
                return;
            }

            _stageConnectButton.Enabled = false;
            _stageHostBox.Enabled = false;
            _stageConnLabel.Text = "연결 중...";
            _stageConnLabel.ForeColor = Color.DarkOrange;

            bool connected = await _system.ConnectStageAsync(_stageHostBox.Text);

            _stageConnectButton.Enabled = true;
            _stageHostBox.Enabled = true;
            if (!connected)
            {
                _stageConnLabel.Text = "연결 실패";
                _stageConnLabel.ForeColor = Color.Red;
                return;
            }
            ResetSpeedToLow();
        }

        private void StageDisconnectButton_Click(object sender, EventArgs e)
        {
            _system.DisconnectStage();
        }

        private async void HexapodConnectButton_Click(object sender, EventArgs e)
        {
            if (!_system.ConnectHexapod(_hexapodHostBox.Text))
            {
                return;
            }
            ResetSpeedToLow();

            // 컨트롤러 전원을 켤 때마다 레퍼런스가 풀린다. 안 잡혀 있으면 이동(조그/절대이동/원점이동)이 모두 거부된다.
            if (_system.Hexapod.IsReferenced() == false)
            {
                AppendLog("[Hexapod] 레퍼런스가 잡혀 있지 않습니다 - [레퍼런스]를 눌러야 이동할 수 있습니다");
                await RunHexapodReferenceAsync();
            }
        }

        /// <summary>
        /// [스캔]: 네트워크의 PI 컨트롤러를 찾아 IP 를 입력칸에 채운다. 하나면 바로 채우고, 여러 개면 목록에서 고르게 한다.
        /// 연결은 하지 않는다 - 확인 후 [연결]을 누른다.
        /// </summary>
        private async void HexapodScanButton_Click(object sender, EventArgs e)
        {
            _hexapodScanButton.Enabled = false;
            AppendLog("[Hexapod] 컨트롤러 검색 중...");
            string[] found = await _system.Hexapod.ScanControllersAsync();
            _hexapodScanButton.Enabled = true;
            if (found == null)
            {
                return;   // 실패 원인은 DLL 로그로 표시된다
            }

            if (found.Length == 0)
            {
                AppendLog("[Hexapod] 컨트롤러를 찾지 못했습니다 (PC 네트워크 대역/케이블/방화벽 확인)");
                return;
            }
            foreach (string description in found)
            {
                AppendLog("[Hexapod] 발견: " + description);
            }
            if (found.Length == 1)
            {
                SelectScannedHexapod(found[0]);
                return;
            }

            var menu = new ContextMenuStrip();
            foreach (string description in found)
            {
                menu.Items.Add(description, null, (s, args) => SelectScannedHexapod(description));
            }
            menu.Show(_hexapodScanButton, new Point(0, _hexapodScanButton.Height));
        }

        private void SelectScannedHexapod(string description)
        {
            string host = Hexapod.ParseHostFromDescription(description, out int port);
            if (host == null)
            {
                AppendLog("[Hexapod] 검색 결과에서 IP 를 찾지 못했습니다: " + description);
                return;
            }
            // 헥사포드는 포트 50000 고정으로 접속한다.
            if (port != Hexapod.Port)
            {
                AppendLog($"[Hexapod] 주의: 포트가 {port} 입니다 - 이 프로그램은 {Hexapod.Port} 으로만 접속합니다");
            }
            _hexapodHostBox.Text = host;
            AppendLog($"[Hexapod] IP {host} 선택 - [연결]을 누르세요");
        }

        private void HexapodCoordSystemButton_Click(object sender, EventArgs e)
        {
            if (!_system.Hexapod.IsConnected)
            {
                AppendLog("[Hexapod] 먼저 [연결]하세요");
                return;
            }
            using (var form = new F_CoordSystem(_system, AppendLog))
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>[3D 보기]: 헥사포드 3D 창을 띄운다 (비모달 - 띄운 채로 조그하면 자세가 따라 움직인다). 이미 열려 있으면 앞으로.</summary>
        private void Hexapod3DButton_Click(object sender, EventArgs e)
        {
            if (!_system.Hexapod.IsConnected)
            {
                AppendLog("[Hexapod] 먼저 [연결]하세요");
                return;
            }
            if (_hexapod3DForm == null || _hexapod3DForm.IsDisposed)
            {
                _hexapod3DForm = new Hexapod3DForm(_system.Hexapod);   // DLL 제공 3D 창
                _hexapod3DForm.Show(this);
            }
            else
            {
                _hexapod3DForm.Activate();
            }
        }

        private async void HexapodReferenceButton_Click(object sender, EventArgs e)
        {
            await RunHexapodReferenceAsync();
        }

        /// <summary>확인 창을 띄운 뒤 헥사포드 레퍼런스(FRF)를 잡는다. 헥사포드가 레퍼런스 위치(6축 0)로 실제 이동한다.</summary>
        private async Task RunHexapodReferenceAsync()
        {
            if (!_system.Hexapod.IsConnected)
            {
                AppendLog("[Hexapod] 먼저 [연결]하세요");
                return;
            }
            DialogResult answer = MessageBox.Show(
                "헥사포드가 레퍼런스 위치(모든 축 0)로 이동하며 레퍼런스를 잡습니다.\n주변에 부딪힐 것이 없는지 확인하셨습니까?",
                "헥사포드 레퍼런스", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return;
            }

            // 레퍼런스 중에는 일반 정지(헥사포드 축별 [정지], [전체 정지])를 막는다 (사용자 요청).
            // [전체 정지 (비상)]은 안전을 위해 그대로 둔다.
            Button[] stopButtons = { _txStopButton, _tyStopButton, _tzStopButton, _stopAllNormalButton };
            _hexapodReferenceButton.Enabled = false;
            foreach (var button in stopButtons)
            {
                button.Enabled = false;
            }
            try
            {
                AppendLog("[Hexapod] 레퍼런스 잡는 중...");
                if (await _system.Hexapod.ReferenceAsync())
                {
                    AppendLog("[Hexapod] 레퍼런스 완료");
                }
            }
            finally
            {
                _hexapodReferenceButton.Enabled = true;
                foreach (var button in stopButtons)
                {
                    button.Enabled = true;
                }
            }
        }

        /// <summary>
        /// 연결 직후 컨트롤러에 남아 있던 속도로 움직이지 않도록 항상 저속으로 맞춘다.
        /// 속도 콤보가 두 장비 공용이므로, 이미 연결된 다른 장비도 함께 저속이 된다 (콤보 표시와 실제 속도를 일치시키기 위함).
        /// </summary>
        private void ResetSpeedToLow()
        {
            if (_speedCombo.SelectedIndex != 0)
            {
                // SelectedIndexChanged 핸들러가 ApplySpeed(0) 을 호출한다.
                _speedCombo.SelectedIndex = 0;
            }
            else
            {
                ApplySpeed(0);
            }
        }

        /// <summary>
        /// 속도 단계를 적용하고 결과를 로그로 남긴다. 콤보 변경과 연결 시 저속 설정이 모두 이 함수를 거친다.
        /// 콤보 순서(저속/중속/고속)가 SpeedLevel 값(Low=0, Medium=1, High=2)과 같다.
        /// </summary>
        private void ApplySpeed(int level)
        {
            string result = _system.SetSpeedLevel((SpeedLevel)level);
            AppendLog($"속도: {_speedCombo.Items[level]} ({result})");
        }

        private void HexapodDisconnectButton_Click(object sender, EventArgs e)
        {
            _system.DisconnectHexapod();
        }

        private async void StageFindHomeButton_Click(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                "X, Y, Z 축이 (-)리밋 방향으로 이동해 기계 원점을 찾습니다.\n주변에 부딪힐 것이 없는지 확인하셨습니까?",
                "기계 원점 찾기", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return;
            }
            _stageFindHomeButton.Enabled = false;
            if (await _system.HomeStageAsync())
            {
                AppendLog("[Stage] 기계 원점 찾기 완료");
            }
            _stageFindHomeButton.Enabled = true;
        }

        private void StageSetHomeButton_Click(object sender, EventArgs e)
        {
            if (_system.SetStageZero())
            {
                AppendLog("[Stage] 현재 위치를 원점(0)으로 설정");
            }
            else
            {
                AppendLog("[Stage] 원점 설정 실패");
            }
        }

        private async void StageMoveHomeButton_Click(object sender, EventArgs e)
        {
            _stageMoveHomeButton.Enabled = false;
            await _system.MoveStageToZeroAsync();
            _stageMoveHomeButton.Enabled = true;
        }

        private void HexapodSetHomeButton_Click(object sender, EventArgs e)
        {
            if (_system.SetHexapodHomeFromCurrent())
            {
                AppendLog("[Hexapod] 현재 위치를 원점으로 저장");
            }
            else
            {
                AppendLog("[Hexapod] 원점 저장 실패");
            }
            UpdateHomeLabels();
        }

        private async void HexapodMoveHomeButton_Click(object sender, EventArgs e)
        {
            if (_system.HexapodHome == null)
            {
                AppendLog("[Hexapod] 저장된 원점이 없습니다");
                return;
            }
            _hexapodMoveHomeButton.Enabled = false;
            await _system.MoveHexapodHomeAsync();
            _hexapodMoveHomeButton.Enabled = true;
        }

        private async void StopAllNormalButton_Click(object sender, EventArgs e)
        {
            await _system.StopAllAsync();
        }

        private async void StopAllButton_Click(object sender, EventArgs e)
        {
            await _system.EmergencyStopAllAsync();
        }

        private async void ProbeConnectButton_Click(object sender, EventArgs e)
        {
            if (_system.Probe.IsConnected)
            {
                AppendLog("[Probe] 이미 연결되어 있습니다");
                return;
            }

            _probeConnectButton.Enabled = false;
            _probeConnLabel.Text = "연결 중...";
            _probeConnLabel.ForeColor = Color.DarkOrange;

            bool connected = await _system.Probe.ConnectAsync();

            UpdateProbeButtons();
            if (!connected)
            {
                _probeConnLabel.Text = "연결 실패";
                _probeConnLabel.ForeColor = Color.Red;
                return;
            }
            StartProbeReading();
        }

        private void ProbeDisconnectButton_Click(object sender, EventArgs e)
        {
            if (!_system.Probe.Disconnect())
            {
                return;
            }
            _probeReading = false;
            _probeReadButton.Text = "읽기 시작";
            // 끊긴 뒤 마지막 값이 그대로 남아 살아있는 값처럼 보이지 않게 비운다.
            _probeGrid.Rows.Clear();
            _probeConnLabel.Text = "연결 안됨";
            _probeConnLabel.ForeColor = Color.Red;
            UpdateProbeButtons();
        }

        /// <summary>
        /// 연결 전에는 [연결]만, 연결 후에는 [연결]을 뺀 나머지 버튼만 쓸 수 있게 한다.
        /// </summary>
        private void UpdateProbeButtons()
        {
            bool connected = _system.Probe.IsConnected;
            _probeConnectButton.Enabled = !connected;
            _probeDisconnectButton.Enabled = connected;
            _probeReadButton.Enabled = connected;
            _probeScanButton.Enabled = connected;
            _probeNotifyButton.Enabled = connected;
            _probeZeroAllButton.Enabled = connected;
        }

        private void ProbeReadButton_Click(object sender, EventArgs e)
        {
            if (_probeReading)
            {
                StopProbeReading("읽기 중지");
                AppendLog("[Probe] 읽기 중지");
                return;
            }
            if (!_system.Probe.IsConnected)
            {
                AppendLog("[Probe] 먼저 [연결]하세요");
                return;
            }
            StartProbeReading();
            AppendLog("[Probe] 읽기 시작");
        }

        private void StartProbeReading()
        {
            _probeReading = true;
            _probeReadButton.Text = "읽기 중지";
            _probeConnLabel.Text = "연결됨 - 읽는 중";
            _probeConnLabel.ForeColor = Color.Green;
        }

        private void StopProbeReading(string reason)
        {
            _probeReading = false;
            _probeReadButton.Text = "읽기 시작";
            _probeConnLabel.Text = "연결됨 - " + reason;
            _probeConnLabel.ForeColor = Color.DarkOrange;

            // 값이 더 이상 갱신되지 않음을 행마다 표시한다. 통신 오류가 난 행은 원인이 보이도록 오류 표시를 남긴다.
            foreach (DataGridViewRow row in _probeGrid.Rows)
            {
                string state = Convert.ToString(row.Cells[_colState.Index].Value);
                if (state.StartsWith("오류"))
                {
                    continue;
                }
                row.Cells[_colState.Index].Value = "읽기 중지";
                row.Cells[_colState.Index].Style.ForeColor = Color.Gray;
            }
        }

        private async void ProbeScanButton_Click(object sender, EventArgs e)
        {
            _probeScanButton.Enabled = false;
            await _system.Probe.ScanAsync();
            _probeScanButton.Enabled = _system.Probe.IsConnected;
            UpdateProbeListWhileStopped();
        }

        private async void ProbeNotifyButton_Click(object sender, EventArgs e)
        {
            _probeNotifyButton.Enabled = false;
            await _system.Probe.NotifyAddModuleAsync();
            // 대기 중 [해제]로 취소됐을 수 있으므로 연결 상태 기준으로 되살린다.
            _probeNotifyButton.Enabled = _system.Probe.IsConnected;
            UpdateProbeListWhileStopped();
        }

        private void ProbeZeroAllButton_Click(object sender, EventArgs e)
        {
            _system.Probe.ZeroAll();
        }

        private void ProbeGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != _colLabel.Index)
            {
                return;
            }
            string moduleId = Convert.ToString(_probeGrid.Rows[e.RowIndex].Cells[_colModuleId.Index].Value);
            string label = Convert.ToString(_probeGrid.Rows[e.RowIndex].Cells[_colLabel.Index].Value);
            if (!string.IsNullOrEmpty(moduleId))
            {
                _system.Probe.SetLabel(moduleId, label);
            }
        }

        private async void ProbeGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }
            string moduleId = Convert.ToString(_probeGrid.Rows[e.RowIndex].Cells[_colModuleId.Index].Value);
            if (string.IsNullOrEmpty(moduleId))
            {
                return;
            }

            if (e.ColumnIndex == _colZero.Index)
            {
                _system.Probe.Zero(moduleId);
            }
            else if (e.ColumnIndex == _colRead.Index)
            {
                await ReadSingleProbeAsync(moduleId);
            }
        }

        /// <summary>
        /// [읽기] 버튼: 해당 프로브 하나만 한 번 읽어 행에 표시한다. 연속 읽기 중지 상태에서도 쓸 수 있다.
        /// 통신 오류여도 연속 읽기를 멈추지는 않는다 (연속 읽기 중이면 주기 읽기 쪽에서 처리됨).
        /// </summary>
        private async Task ReadSingleProbeAsync(string moduleId)
        {
            if (!_system.Probe.IsConnected || !_singleReadsInFlight.Add(moduleId))
            {
                return;   // 미연결이거나 같은 프로브를 이미 읽는 중 (끊긴 모듈은 Receive Timeout 까지 걸림)
            }
            ProbeReadResult result;
            try
            {
                result = await Task.Run(() => _system.Probe.Read(moduleId));
            }
            finally
            {
                _singleReadsInFlight.Remove(moduleId);
            }
            // 미연결 / 사용 중(Busy) / 모듈 없음이면 값이 없다.
            if (result.Channels.Count == 0 || IsDisposed)
            {
                return;
            }
            ProbeChannel ch = result.Channels[0];

            // 읽는 동안 행이 지워졌을 수 있으므로(해제/스캔) 다시 찾는다.
            DataGridViewRow row = _probeGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(r => Convert.ToString(r.Cells[_colModuleId.Index].Value) == moduleId);
            if (row == null)
            {
                return;
            }
            row.Cells[_colReading.Index].Value = ch.IsError ? "-" : ch.Reading.ToString("0.000");
            row.Cells[_colState.Index].Value = ch.IsError ? ("오류: " + ch.ErrorMessage) : (_probeReading ? "정상" : "개별 읽기");
            row.Cells[_colState.Index].Style.ForeColor = ch.IsError ? Color.Red : Color.Black;
            if (ch.IsError)
            {
                AppendLog($"[Probe] {ch.Label} 읽기 실패: {ch.ErrorMessage}");
            }
        }

        private async void ProbeTimer_Tick(object sender, EventArgs e)
        {
            await RefreshProbeGridAsync();
        }

        /// <summary>장비 상태/로그 조회 (100 ms). DLL 은 이벤트 대신 조회 방식으로만 상태를 알려 준다.</summary>
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateConnectionLabel(_system.Stage.IsConnected, ref _stageConnected, _stageConnLabel);
            UpdateConnectionLabel(_system.Hexapod.IsConnected, ref _hexapodConnected, _hexapodConnLabel);
            UpdateAxisStatuses(_system.GetAxisStatuses());
            foreach (LogEntry entry in _system.GetLogs())
            {
                AppendLog(entry.ToString(), entry.Time);
            }
        }

        #endregion

        #region 상태 갱신

        private static void UpdateConnectionLabel(bool connected, ref bool lastConnected, Label label)
        {
            if (connected == lastConnected)
            {
                return;
            }
            lastConnected = connected;
            label.Text = connected ? "연결됨" : "연결 안됨";
            label.ForeColor = connected ? Color.Green : Color.Red;
        }

        private void UpdateAxisStatuses(AxisStatus[] statuses)
        {
            foreach (var status in statuses)
            {
                if (_posLabels.TryGetValue(status.Axis, out var posLabel))
                {
                    string unit = MotorizedStage.Axes.Contains(status.Axis) ? "µm" : "arcmin";
                    posLabel.Text = $"{status.Position:0.000} {unit}";
                }
                if (_statusLabels.TryGetValue(status.Axis, out var statusLabel))
                {
                    if (status.Alarm != 0)
                    {
                        statusLabel.Text = "ALARM";
                        statusLabel.ForeColor = Color.Red;
                    }
                    else if (status.IsPosLimit && status.IsNegLimit)
                    {
                        // 양쪽이 동시에 걸리는 건 보통 센서 배선/전원 이상이다.
                        statusLabel.Text = "±LIMIT";
                        statusLabel.ForeColor = Color.OrangeRed;
                    }
                    else if (status.IsPosLimit)
                    {
                        statusLabel.Text = "+LIMIT";
                        statusLabel.ForeColor = Color.OrangeRed;
                    }
                    else if (status.IsNegLimit)
                    {
                        statusLabel.Text = "-LIMIT";
                        statusLabel.ForeColor = Color.OrangeRed;
                    }
                    else if (status.IsMoving)
                    {
                        statusLabel.Text = "MOVING";
                        statusLabel.ForeColor = Color.DodgerBlue;
                    }
                    else
                    {
                        statusLabel.Text = "IDLE";
                        statusLabel.ForeColor = Color.Gray;
                    }
                }
            }
        }

        private async Task RefreshProbeGridAsync()
        {
            // 이전 주기 읽기가 아직 안 끝났으면(끊긴 모듈이 Receive Timeout 대기 중 등) 겹쳐서 읽지 않는다.
            if (!_system.Probe.IsConnected || !_probeReading || _probeReadInFlight)
            {
                return;
            }

            // 읽기는 백그라운드에서 - 끊긴 모듈 읽기가 타임아웃까지 블록돼도 화면이 멈추지 않게 한다.
            ProbeReadResult result;
            _probeReadInFlight = true;
            try
            {
                result = await Task.Run(() => _system.Probe.ReadAll());
            }
            finally
            {
                _probeReadInFlight = false;
            }

            // 스캔 등 다른 작업 중이면 이번 주기는 건너뛴다. 읽는 동안 [읽기 중지]/[해제]/창 닫기가 된 경우 결과를 버린다.
            if (result.Status == ProbeReadStatus.Busy || !_probeReading || IsDisposed)
            {
                return;
            }

            List<ProbeChannel> channels = result.Channels;
            RemoveStaleProbeRows(channels);
            foreach (var ch in channels)
            {
                DataGridViewRow row = SyncProbeRow(ch);
                row.Cells[_colReading.Index].Value = ch.IsError ? "-" : ch.Reading.ToString("0.000");
                row.Cells[_colState.Index].Value = ch.IsError ? ("오류: " + ch.ErrorMessage) : "정상";
                row.Cells[_colState.Index].Style.ForeColor = ch.IsError ? Color.Red : Color.Black;
            }

            // 통신 오류면 반복 읽기를 멈춘다 (DLL 은 읽기 상태를 기억하지 않으므로 반복을 멈추는 것은 호출하는 쪽의 몫).
            if (result.Status == ProbeReadStatus.CommError)
            {
                StopProbeReading("통신 오류로 읽기 중지");
                foreach (var ch in channels.Where(ch => ch.IsError))
                {
                    AppendLog($"[Probe] {ch.Label} 통신 오류: {ch.ErrorMessage}");
                }
                AppendLog("[Probe] 읽기를 중지했습니다. 케이블 연결을 확인한 뒤 [읽기 시작]을 누르세요.");
            }
        }

        /// <summary>
        /// 읽기 중지 상태에서 스캔/새 모듈 추가 결과(모듈 목록)만 그리드에 반영한다. 값은 읽지 않는다.
        /// (읽는 중이면 다음 주기 읽기에서 자동으로 반영되므로 아무것도 하지 않는다.)
        /// </summary>
        private void UpdateProbeListWhileStopped()
        {
            if (_probeReading || !_system.Probe.IsConnected)
            {
                return;
            }
            ProbeReadResult result = _system.Probe.GetModuleList();
            if (result.Status == ProbeReadStatus.Busy)
            {
                return;
            }

            RemoveStaleProbeRows(result.Channels);
            foreach (var ch in result.Channels)
            {
                DataGridViewRow row = SyncProbeRow(ch);
                // 새로 추가된 행만 채운다. 기존 행은 마지막으로 읽은 값/상태를 그대로 둔다.
                if (row.Cells[_colReading.Index].Value == null)
                {
                    row.Cells[_colReading.Index].Value = "-";
                    row.Cells[_colState.Index].Value = "읽기 중지";
                    row.Cells[_colState.Index].Style.ForeColor = Color.Gray;
                }
            }
        }

        /// <summary>더 이상 모듈 목록에 없는 행은 지운다.</summary>
        private void RemoveStaleProbeRows(List<ProbeChannel> channels)
        {
            var liveIds = new HashSet<string>(channels.Select(ch => ch.ModuleId));
            for (int i = _probeGrid.Rows.Count - 1; i >= 0; i--)
            {
                if (!liveIds.Contains(Convert.ToString(_probeGrid.Rows[i].Cells[_colModuleId.Index].Value)))
                {
                    _probeGrid.Rows.RemoveAt(i);
                }
            }
        }

        /// <summary>모듈에 해당하는 행을 찾거나 새로 만들고, 라벨을 갱신한다.</summary>
        private DataGridViewRow SyncProbeRow(ProbeChannel ch)
        {
            DataGridViewRow row = _probeGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(r => Convert.ToString(r.Cells[_colModuleId.Index].Value) == ch.ModuleId);

            if (row == null)
            {
                int idx = _probeGrid.Rows.Add();
                row = _probeGrid.Rows[idx];
                row.Cells[_colModuleId.Index].Value = ch.ModuleId;
            }

            // 사용자가 지금 편집 중인 셀은 덮어쓰지 않는다.
            if (!_probeGrid.IsCurrentCellInEditMode || _probeGrid.CurrentCell?.OwningRow != row)
            {
                row.Cells[_colLabel.Index].Value = ch.Label;
            }
            return row;
        }

        private void UpdateHomeLabels()
        {
            _hexapodHomeLabel.Text = FormatHome(_system.HexapodHome, Hexapod.Axes, "arcmin");
        }

        private static string FormatHome(double[] home, Axis[] axes, string unit)
        {
            if (home == null)
            {
                return "원점: 미설정";
            }
            return "원점: " + string.Join(", ", axes.Select((axis, i) => $"{axis} {home[i]:0.000}")) + " " + unit;
        }

        private void AppendLog(string message)
        {
            AppendLog(message, DateTime.Now);
        }

        private void AppendLog(string message, DateTime time)
        {
            // 비동기 작업이 창을 닫은 뒤에 끝나 로그를 남기려 할 수 있다.
            if (IsDisposed)
            {
                return;
            }
            _logBox.AppendText($"[{time:HH:mm:ss}] {message}\r\n");
        }

        #endregion
    }
}
