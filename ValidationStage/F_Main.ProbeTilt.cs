using System;
using System.Drawing;
using System.Windows.Forms;
using ValidationStage.Devices;

namespace ValidationStage
{
    // 프로브 기울기 보정 측정 (사내 전용). 업체 배포본에는 넣지 않는다: Package\Build-Package.ps1 이 파일 이름에 ProbeTilt 가
    // 들어간 파일을 빼고, 남은 샘플에 관련 코드가 있으면 멈춘다. 그래서 다른 파일(F_Main.cs, 디자이너)에는 관련 코드를 두지 않고
    // 여기서 재정의(OnLoad 등)와 이벤트 구독으로만 연결한다 (버튼도 디자이너가 아니라 여기서 만든다).
    public partial class F_Main
    {
        private readonly Button _probeTiltButton = new Button();

        protected override void OnLoad(EventArgs e)
        {
            _probeTiltButton.Name = "_probeTiltButton";
            _probeTiltButton.Size = new Size(120, 23);
            _probeTiltButton.TabIndex = 5;
            _probeTiltButton.Text = "기울기 보정 측정";
            _probeTiltButton.UseVisualStyleBackColor = true;
            _probeTiltButton.Visible = false;
            _probeTiltButton.Click += ProbeTiltButton_Click;
            _probeBtnRow.Controls.Add(_probeTiltButton);
            _probeBtnRow.Controls.SetChildIndex(_probeTiltButton, _probeBtnRow.Controls.GetChildIndex(_probeConnLabel));

            _statusTimer.Tick += (s, args) => UpdateProbeTiltBanner();
            _stopAllButton.Click += (s, args) =>
            {
                if (_system.IsProbeTiltRunning)
                {
                    _systemInitStopPressed = true;
                    UpdateProbeTiltBanner();
                }
            };
            _systemInitBanner.Click += (s, args) =>
            {
                if (_system.IsProbeTiltRunning)
                {
                    _systemInitBanner.Visible = true;   // 실행 중에는 닫지 않는다 (SystemInitBanner_Click 이 닫은 것을 되돌린다)
                }
            };
            base.OnLoad(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_system.IsProbeTiltRunning && e.CloseReason == CloseReason.UserClosing)
            {
                MessageBox.Show("기울기 보정 측정 중에는 종료할 수 없습니다.\n멈추려면 [전체 정지 (비상)]을 누르세요.",
                    "기울기 보정 측정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// Ctrl+Shift+F12: 평소에 숨겨 둔 [기울기 보정 측정] 버튼(설비 점검용)을 보이거나 숨긴다.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Shift | Keys.F12))
            {
                _probeTiltButton.Visible = !_probeTiltButton.Visible;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// [기울기 보정 측정]: X, Y 를 ±1600 µm 움직이며 프로브 기울기 보정 계수를 구하고 결과 창을 띄운다 (적용은 결과 창의 [적용]).
        /// 전체 시스템 초기화가 된 상태에서만 할 수 있다.
        /// </summary>
        private async void ProbeTiltButton_Click(object sender, EventArgs e)
        {
            if (!RequireStageConnected() || !RequireHexapodConnected() || !RequireProbeConnected())
            {
                return;
            }
            if (!_system.IsSystemInitialized)
            {
                AppendLog(L("[System] 기울기 보정 측정은 전체 시스템 초기화를 한 뒤에 할 수 있습니다", "[System] Run system init before the probe tilt measurement"));
                MessageBox.Show("전체 시스템 초기화를 먼저 하세요.\n\n" +
                    "초기화한 뒤에 연결 해제, 원점 찾기, 헥사포드 레퍼런스/좌표계 변경, 프로브 영점/스캔/새 모듈 추가를 했다면 다시 초기화해야 합니다.",
                    "기울기 보정 측정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult answer = MessageBox.Show(
                "프로브 기울기 보정 계수를 자동으로 측정합니다. (10분 안팎)\n\n" +
                "1. 헥사포드와 스테이지를 0 (측정 원점)으로 이동\n" +
                "2. X 를 먼저, 그다음 Y 를 −1750 ~ +1600 µm 움직이며 측정 (축마다 예비 1회, 측정 3회, 검증 1회)\n" +
                "3. 0 으로 복귀, 결과 저장\n\n" +
                "속도는 저속으로 바뀝니다. 이 범위에서 프로브와 주변에 부딪힐 것이 없는지 확인하셨습니까?",
                "기울기 보정 측정", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.OK)
            {
                return;
            }

            ResetSpeedToLow();   // DLL 도 저속으로 바꾼다 - 화면의 속도 콤보를 맞춘다
            _systemInitWatch.Restart();
            LockUiForSystemInit();
            ShowSystemInitBanner(SystemInitBannerRunning, "기울기 보정 측정 시작 중...");
            _probeTiltButton.Text = "측정 중...";

            ProbeTiltResult result = await _system.MeasureProbeTiltAsync();
            _systemInitWatch.Stop();
            string elapsed = $"소요 {_systemInitWatch.Elapsed.TotalSeconds:0}초, {DateTime.Now:HH:mm:ss}";
            _probeTiltButton.Text = "기울기 보정 측정";
            UnlockUiAfterSystemInit();

            if (result != null)
            {
                ShowSystemInitBanner(SystemInitBannerDone, $"✔ 기울기 보정 측정 완료   ({elapsed})");
                using (var form = new F_ProbeTiltResult(_system, result))
                {
                    form.ShowDialog(this);
                }
            }
            else if (_system.ProbeTiltAborted)
            {
                ShowSystemInitBanner(SystemInitBannerFailed, $"■ 기울기 보정 측정 중단됨 (정지 명령)   ({elapsed})");
                MessageBox.Show("정지 명령으로 기울기 보정 측정을 중단했습니다. 결과는 저장되지 않았습니다.\n" +
                    "스테이지는 멈춘 위치에 있습니다. 주변을 확인한 뒤 [원점으로 이동]으로 되돌리세요.",
                    "기울기 보정 측정", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                ShowSystemInitBanner(SystemInitBannerFailed, $"✖ 기울기 보정 측정 실패   ({elapsed})");
                MessageBox.Show("기울기 보정 측정 실패 (결과는 저장되지 않았습니다):\n" + _system.LastError + "\n\n" +
                    "스테이지는 멈춘 위치에 있을 수 있습니다. 주변을 확인한 뒤 [원점으로 이동]으로 되돌리세요.",
                    "기울기 보정 측정", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string ProbeTiltStepText(ProbeTiltProgress progress)
        {
            string scan = $"스캔 {progress.Cycle}/{progress.CycleCount}, {progress.Axis} 이동   ({progress.DonePoints}/{progress.TotalPoints}점)";
            switch (progress.Phase)
            {
                case ProbeTiltPhase.Preparing: return "0 (측정 원점)으로 이동";
                case ProbeTiltPhase.WarmUp: return "예비 " + scan;
                case ProbeTiltPhase.Measuring: return "측정 " + scan;
                case ProbeTiltPhase.Verifying: return "검증 " + scan;
                case ProbeTiltPhase.Returning: return "결과 저장, 0 으로 복귀";
                default: return "시작/종료 중";
            }
        }

        private void UpdateProbeTiltBanner()
        {
            if (!_system.IsProbeTiltRunning)
            {
                return;
            }
            string tiltDots = new string('.', 1 + (int)(_systemInitWatch.ElapsedMilliseconds / 500 % 3));
            string tiltStep = ProbeTiltStepText(_system.CurrentProbeTiltProgress);
            SetRunningBanner(_systemInitStopPressed
                ? $"■ 정지 명령 - 기울기 보정 측정 중단 중{tiltDots}   ({tiltStep})"
                : $"⏳ 기울기 보정 측정 중{tiltDots}   {tiltStep}   " +
                  $"(경과 {_systemInitWatch.Elapsed.TotalSeconds:0}초)   ※ [전체 정지 (비상)]만 누를 수 있습니다");
        }
    }
}
