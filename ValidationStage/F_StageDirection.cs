using ValidationStage.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace ValidationStage
{
    /// <summary>
    /// 스테이지 X, Y, Z 모터 회전 방향(MMT "REV") 설정 창. 값은 컨트롤러에 저장된다.
    /// 방향을 바꾸면 조그/이동/원점 찾기 방향이 모두 반대가 된다.
    /// </summary>
    public partial class F_StageDirection : Form
    {
        private readonly MotorizedStage _stage;
        private readonly Action<string> _log;
        private readonly Dictionary<Axis, Label> _currentLabels;
        private readonly Dictionary<Axis, ComboBox> _combos;
        private readonly Dictionary<Axis, MotorDirection?> _current = new Dictionary<Axis, MotorDirection?>();

        public F_StageDirection(MotorizedStage stage, Action<string> log)
        {
            InitializeComponent();
            _stage = stage;
            _log = log;
            _currentLabels = new Dictionary<Axis, Label> { { Axis.X, _xCurrentLabel }, { Axis.Y, _yCurrentLabel }, { Axis.Z, _zCurrentLabel } };
            _combos = new Dictionary<Axis, ComboBox> { { Axis.X, _xCombo }, { Axis.Y, _yCombo }, { Axis.Z, _zCombo } };
            ReadAll();
        }

        /// <summary>X, Y, Z 의 현재 REV 값을 읽어 표시한다. 콤보도 현재 값으로 되돌린다.</summary>
        private void ReadAll()
        {
            foreach (Axis axis in MotorizedStage.Axes)
            {
                MotorDirection? dir = _stage.GetMotorDirection(axis);
                _current[axis] = dir;
                _currentLabels[axis].Text = dir.HasValue ? DirectionText(dir.Value) : "읽기 실패";
                _combos[axis].SelectedIndex = dir.HasValue ? (int)dir.Value : -1;
            }
        }

        private static string DirectionText(MotorDirection dir)
        {
            return dir == MotorDirection.Cw ? "CW (REV1)" : "CCW (REV0)";
        }

        private void ReadButton_Click(object sender, EventArgs e)
        {
            ReadAll();
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            Axis[] changed = MotorizedStage.Axes
                .Where(axis => _combos[axis].SelectedIndex >= 0 && _current[axis] != (MotorDirection)_combos[axis].SelectedIndex)
                .ToArray();
            if (changed.Length == 0)
            {
                MessageBox.Show(this, "바뀐 축이 없습니다.", "모터 방향", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string list = string.Join("\n", changed.Select(axis => $"{axis}: {DirectionText((MotorDirection)_combos[axis].SelectedIndex)}"));
            DialogResult answer = MessageBox.Show(this,
                $"다음 축의 모터 방향을 바꿉니다 (컨트롤러에 저장됨).\n\n{list}\n\n" +
                "조그/이동/원점 찾기 방향이 모두 반대가 됩니다.\n바꾼 뒤에는 저속으로 양쪽 리밋에서 멈추는지 먼저 확인하십시오.",
                "모터 방향", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return;
            }

            foreach (Axis axis in changed)
            {
                if (_stage.SetMotorDirection(axis, (MotorDirection)_combos[axis].SelectedIndex))
                {
                    _log(F_Main.L($"[Stage] {axis} 모터 방향 변경 완료", $"[Stage] {axis} motor direction changed"));
                }
            }
            ReadAll();
        }
    }
}
