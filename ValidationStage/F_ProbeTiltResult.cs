using ValidationStage.Devices;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace ValidationStage
{
    /// <summary>
    /// 프로브 기울기 측정 결과 창: 기존/새 계수와 검증 스캔 결과를 보여 주고, [적용]하면 새 계수로 바꾼다 (기존 파일은 백업됨).
    /// </summary>
    public partial class F_ProbeTiltResult : Form
    {
        // 이보다 크면 주황색으로 알린다 (2026-11-20 실측: RMS 0.05~0.1 µm, 회차별 편차 0.0002 이하)
        private const double RmsWarning = 0.2;
        private const double SpreadWarning = 0.0002;

        private readonly ValidationSystem _system;
        private readonly ProbeTiltResult _result;

        public F_ProbeTiltResult(ValidationSystem system, ProbeTiltResult result)
        {
            InitializeComponent();
            _system = system;
            _result = result;

            foreach (ProbeTiltItem item in result.Items)
            {
                int index = _grid.Rows.Add($"{item.MoveAxis} 이동 → {item.Label}  ({item.Name})",
                    Coefficient(item.OldValue), Coefficient(item.NewValue), item.Spread.ToString("0.000000", CultureInfo.InvariantCulture),
                    Micrometer(item.Rms), Micrometer(item.ChangeBefore), Micrometer(item.ChangeOld), Micrometer(item.ChangeNew),
                    VerdictText(item.Verdict));
                DataGridViewRow row = _grid.Rows[index];
                row.Cells[_colVerdict.Index].Style.ForeColor = VerdictColor(item.Verdict);
                row.Cells[_colNew.Index].Style.Font = new Font(Font, FontStyle.Bold);
                row.Cells[_colChangeNew.Index].Style.Font = new Font(Font, FontStyle.Bold);
                if (item.Spread > SpreadWarning)
                {
                    row.Cells[_colSpread.Index].Style.ForeColor = Color.DarkOrange;
                }
                if (item.Rms > RmsWarning)
                {
                    row.Cells[_colRms.Index].Style.ForeColor = Color.DarkOrange;
                }
            }

            _verdictLabel.Text = "전체 판정: " + VerdictText(result.Verdict);
            _verdictLabel.ForeColor = VerdictColor(result.Verdict);
            if (result.Warnings.Count > 0)
            {
                _warningLabel.Text = "⚠ " + string.Join("\r\n⚠ ", result.Warnings);
            }
            _fileLabel.Text = "저장: " + result.FilePath;
        }

        // 선택 색이 판정/경고 색을 가리지 않게 한다.
        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            _grid.ClearSelection();
        }

        private static string VerdictText(ProbeTiltVerdict verdict)
        {
            switch (verdict)
            {
                case ProbeTiltVerdict.Good: return "양호";
                case ProbeTiltVerdict.Caution: return "주의 (재측정 권장)";
                default: return "불량";
            }
        }

        private static Color VerdictColor(ProbeTiltVerdict verdict)
        {
            switch (verdict)
            {
                case ProbeTiltVerdict.Good: return Color.ForestGreen;
                case ProbeTiltVerdict.Caution: return Color.DarkOrange;
                default: return Color.Red;
            }
        }

        private static string Coefficient(double value)
        {
            return value.ToString("0.000000000", CultureInfo.InvariantCulture);
        }

        private static string Micrometer(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private void ApplyButton_Click(object sender, EventArgs e)
        {
            string message = "새 계수를 적용합니다. 이후 프로브 측정값이 새 계수로 보정됩니다.\n기존 계수 파일은 백업됩니다.\n\n적용하시겠습니까?";
            if (_result.Verdict != ProbeTiltVerdict.Good)
            {
                message = $"전체 판정이 '{VerdictText(_result.Verdict)}' 입니다. 표와 경고를 확인하세요. 프로브 접촉/고정 상태를 확인하고 다시 측정하는 것을 권장합니다.\n\n" + message;
            }
            DialogResult answer = MessageBox.Show(this, message, "프로브 기울기 보정", MessageBoxButtons.YesNo,
                _result.Verdict == ProbeTiltVerdict.Good ? MessageBoxIcon.Question : MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }
            if (!_system.ApplyProbeTilt(_result))
            {
                MessageBox.Show(this, "적용 실패:\n" + _system.LastError, "프로브 기울기 보정", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _applyButton.Enabled = false;
            _applyButton.Text = "적용됨";
        }

        private void FolderButton_Click(object sender, EventArgs e)
        {
            if (File.Exists(_result.FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{_result.FilePath}\"");
            }
        }
    }
}
