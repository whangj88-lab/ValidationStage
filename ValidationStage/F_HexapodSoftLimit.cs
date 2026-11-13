using ValidationStage.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ValidationStage
{
    /// <summary>
    /// 헥사포드 TX, TY, TZ 소프트웨어 리밋 설정 창. 좌표계마다 따로 저장된다 (HexapodHost.txt).
    /// 이동/조그 명령은 활성 좌표계의 리밋으로 DLL 이 검사하며, 벗어나는 이동은 명령 전체를 거부한다.
    /// </summary>
    public partial class F_HexapodSoftLimit : Form
    {
        private readonly ValidationSystem _system;
        private readonly Action<string> _log;
        private readonly NumericUpDown[] _minInputs;
        private readonly NumericUpDown[] _maxInputs;
        private readonly Label[] _positionLabels;
        private readonly string _activeCs;

        public F_HexapodSoftLimit(ValidationSystem system, Action<string> log)
        {
            InitializeComponent();
            _system = system;
            _log = log;
            _minInputs = new[] { _txMinInput, _tyMinInput, _tzMinInput };
            _maxInputs = new[] { _txMaxInput, _tyMaxInput, _tzMaxInput };
            _positionLabels = new[] { _txPositionLabel, _tyPositionLabel, _tzPositionLabel };

            // 좌표계 목록: PI 내부 좌표계는 뺀다. 목록을 못 읽으면 활성 좌표계만.
            _activeCs = _system.Hexapod.GetActiveUserCoordSystem();
            List<string> names = _system.Hexapod.GetCoordSystems()?
                .Where(cs => !cs.IsPiInternal).Select(cs => cs.Name).ToList() ?? new List<string>();
            if (_activeCs != null && !names.Contains(_activeCs))
            {
                names.Insert(0, _activeCs);
            }
            _activeLabel.Text = "활성: " + (_activeCs ?? "읽기 실패");
            _csCombo.Items.AddRange(names.Cast<object>().ToArray());
            // SelectedIndexChanged 가 선택한 좌표계의 값을 읽어 온다
            _csCombo.SelectedIndex = names.Count == 0 ? -1 : Math.Max(0, names.IndexOf(_activeCs));
            _applyButton.Enabled = names.Count > 0;
        }

        private string SelectedCs => _csCombo.SelectedItem as string;

        /// <summary>선택한 좌표계의 저장값을 입력칸에 넣는다 (저장 안 한 변경은 버려진다).</summary>
        private void CsCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SelectedCs == null)
            {
                return;
            }
            HexapodSoftLimit limit = _system.Hexapod.GetSoftLimit(SelectedCs);
            _enabledCheck.Checked = limit.Enabled;
            for (int i = 0; i < Hexapod.Axes.Length; i++)
            {
                _minInputs[i].Value = Clamp(_minInputs[i], limit.Min[i]);
                _maxInputs[i].Value = Clamp(_maxInputs[i], limit.Max[i]);
            }
            UpdatePositions();
        }

        /// <summary>현재 위치는 활성 좌표계 기준 값이라, 활성 좌표계를 선택했을 때만 보여 준다.</summary>
        private void UpdatePositions()
        {
            double[] positions = SelectedCs == _activeCs ? _system.Hexapod.GetPositions() : null;
            for (int i = 0; i < _positionLabels.Length; i++)
            {
                _positionLabels[i].Text = positions != null ? positions[i].ToString("0.000") : "-";
            }
        }

        private static decimal Clamp(NumericUpDown input, double value)
        {
            return Math.Min(input.Maximum, Math.Max(input.Minimum, (decimal)value));
        }

        private void DefaultButton_Click(object sender, EventArgs e)
        {
            _enabledCheck.Checked = true;
            for (int i = 0; i < Hexapod.Axes.Length; i++)
            {
                _minInputs[i].Value = (decimal)-HexapodSoftLimit.DefaultLimit;
                _maxInputs[i].Value = (decimal)HexapodSoftLimit.DefaultLimit;
            }
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            var limit = new HexapodSoftLimit
            {
                Enabled = _enabledCheck.Checked,
                Min = _minInputs.Select(input => (double)input.Value).ToArray(),
                Max = _maxInputs.Select(input => (double)input.Value).ToArray(),
            };
            if (!_system.SetHexapodSoftLimit(SelectedCs, limit))
            {
                MessageBox.Show(this, $"소프트 리밋 저장 실패\n\n{_system.LastError}", "소프트 리밋", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string ranges = string.Join(", ", Hexapod.Axes.Select((axis, i) => $"{axis} {limit.Min[i]:0.###}~{limit.Max[i]:0.###}"));
            _log(limit.Enabled
                ? F_Main.L($"[Hexapod] 소프트 리밋 저장 ({SelectedCs}): {ranges} arcmin", $"[Hexapod] Software limit saved ({SelectedCs}): {ranges} arcmin")
                : F_Main.L($"[Hexapod] 소프트 리밋 저장 ({SelectedCs}): 사용 안 함", $"[Hexapod] Software limit saved ({SelectedCs}): disabled"));

            // 현재 위치가 이미 범위 밖이면 알려 준다 (안쪽으로 되돌리는 이동/조그는 된다).
            double[] positions = SelectedCs == _activeCs ? _system.Hexapod.GetPositions() : null;
            if (limit.Enabled && positions != null)
            {
                string outside = string.Join(", ", Hexapod.Axes
                    .Where((axis, i) => positions[i] < limit.Min[i] || positions[i] > limit.Max[i]));
                if (outside.Length > 0)
                {
                    MessageBox.Show(this, $"현재 위치가 리밋 범위 밖입니다 ({outside}).\n범위 안쪽으로 되돌리는 이동/조그만 할 수 있습니다.",
                        "소프트 리밋", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
