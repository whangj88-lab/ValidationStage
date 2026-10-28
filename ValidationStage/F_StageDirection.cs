using ValidationStage.Devices;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ValidationStage
{
    /// <summary>
    /// 스테이지 X, Y, Z 모터 회전 방향(MMT "REV") 설정 창. 값은 컨트롤러에 저장된다.
    /// 방향을 바꾸면 조그/이동/원점 찾기 방향이 모두 반대가 된다. 리밋 방향(HLS)은 DLL 이 같이 맞추고, 적용 후 자동으로 다시 연결한다.
    /// </summary>
    public partial class F_StageDirection : Form
    {
        private readonly MotorizedStage _stage;
        private readonly Action<string> _log;
        private readonly Dictionary<Axis, Label> _currentLabels;
        private readonly Dictionary<Axis, ComboBox> _combos;
        private readonly Dictionary<Axis, MotorDirection?> _current = new Dictionary<Axis, MotorDirection?>();
        private readonly Dictionary<Axis, bool?> _limitMatched = new Dictionary<Axis, bool?>();
        private bool _applying;

        public F_StageDirection(MotorizedStage stage, Action<string> log)
        {
            InitializeComponent();
            _stage = stage;
            _log = log;
            _currentLabels = new Dictionary<Axis, Label> { { Axis.X, _xCurrentLabel }, { Axis.Y, _yCurrentLabel }, { Axis.Z, _zCurrentLabel } };
            _combos = new Dictionary<Axis, ComboBox> { { Axis.X, _xCombo }, { Axis.Y, _yCombo }, { Axis.Z, _zCombo } };
            ReadAll();
        }

        /// <summary>X, Y, Z 의 현재 REV 값과 리밋 방향 일치 여부를 읽어 표시한다. 콤보도 현재 값으로 되돌린다.</summary>
        private void ReadAll()
        {
            foreach (Axis axis in MotorizedStage.Axes)
            {
                MotorDirection? dir = _stage.GetMotorDirection(axis);
                bool? matched = dir.HasValue ? _stage.IsLimitDirectionMatched(axis) : null;
                _current[axis] = dir;
                _limitMatched[axis] = matched;

                Label label = _currentLabels[axis];
                if (!dir.HasValue)
                {
                    label.Text = "읽기 실패";
                    label.ForeColor = Color.Red;
                }
                else if (matched == false)
                {
                    label.Text = DirectionText(dir.Value) + " / 리밋 불일치";
                    label.ForeColor = Color.Red;
                }
                else
                {
                    label.Text = DirectionText(dir.Value) + (matched == true ? " / 리밋 OK" : " / 리밋 ?");
                    label.ForeColor = SystemColors.ControlText;
                }
                _combos[axis].SelectedIndex = dir.HasValue ? (int)dir.Value : -1;
            }
        }

        private static string DirectionText(MotorDirection dir)
        {
            return dir == MotorDirection.Cw ? "CW (REV1)" : "CCW (REV0)";
        }

        /// <summary>적용(재연결) 중에는 창을 닫지 않는다 - 끝난 뒤 결과를 표시해야 한다.</summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_applying)
            {
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        private void ReadButton_Click(object sender, EventArgs e)
        {
            ReadAll();
        }

        private async void ApplyButton_Click(object sender, EventArgs e)
        {
            // 방향이 바뀐 축 + 방향은 같지만 리밋 방향이 어긋난 축 (다시 적용해서 HLS 를 맞춘다)
            Axis[] changed = MotorizedStage.Axes
                .Where(axis => _combos[axis].SelectedIndex >= 0
                    && (_current[axis] != (MotorDirection)_combos[axis].SelectedIndex || _limitMatched[axis] == false))
                .ToArray();
            if (changed.Length == 0)
            {
                MessageBox.Show(this, "바뀐 축이 없습니다.", "모터 방향", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MotorDirection[] directions = changed.Select(axis => (MotorDirection)_combos[axis].SelectedIndex).ToArray();
            string list = string.Join("\n", changed.Select((axis, i) => $"{axis}: {DirectionText(directions[i])}"));
            DialogResult answer = MessageBox.Show(this,
                $"다음 축의 모터 방향을 바꿉니다 (컨트롤러에 저장됨).\n\n{list}\n\n" +
                "리밋 방향도 같이 바뀌고, 적용을 위해 스테이지 연결을 끊었다가 다시 연결합니다.\n" +
                "조그/이동/원점 찾기 방향과 위치 방향이 모두 반대가 되므로 적용 후 기계 원점 찾기를 다시 하십시오.\n" +
                "처음 적용한 뒤에는 저속으로 양쪽 리밋에서 멈추는지 먼저 확인하십시오.",
                "모터 방향", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return;
            }

            _applyButton.Enabled = false;
            _readButton.Enabled = false;
            _closeButton.Enabled = false;
            UseWaitCursor = true;
            _applying = true;
            bool ok = await Task.Run(() => _stage.SetMotorDirections(changed, directions));
            _applying = false;
            UseWaitCursor = false;
            _applyButton.Enabled = true;
            _readButton.Enabled = true;
            _closeButton.Enabled = true;

            if (!ok)
            {
                MessageBox.Show(this, $"모터 방향 적용 실패\n\n{_stage.LastError}", "모터 방향", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (!_stage.IsConnected)
                {
                    Close();
                    return;
                }
            }
            else
            {
                _log(F_Main.L($"[Stage] 모터 방향 변경 완료 ({string.Join(", ", changed)}) - 기계 원점 찾기를 다시 하세요",
                    $"[Stage] Motor direction changed ({string.Join(", ", changed)}) - run homing again"));
            }
            ReadAll();
        }
    }
}
