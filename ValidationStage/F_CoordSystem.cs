using ValidationStage.Devices;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ValidationStage
{
    /// <summary>
    /// 헥사포드 좌표계 관리 창 - PIMikroMove 의 "Manage Coordinate Systems" 화면을 최대한 같은 모양으로 옮겼다.
    /// 지원: 목록(트리)/속성 보기, 생성·재정의(KSD/KST/KSW, [+] → [Set Coord. Sys.]), 활성화([Activate CS]).
    /// 삭제/저장(Save and Reset)은 범위 밖이라 버튼만 두고 비활성화했다.
    /// 값은 컨트롤러 단위 그대로 (X,Y,Z mm / U,V,W deg). 메인 화면의 arcmin(= deg * -60) 과는 부호/단위가 다르다.
    /// </summary>
    public partial class F_CoordSystem : Form
    {
        private static readonly string[] Axes = { "X", "Y", "Z", "U", "V", "W" };

        private readonly ValidationSystem _system;
        private readonly Action<string> _log;
        private List<HexapodCoordSystem> _systems = new List<HexapodCoordSystem>();
        private TextBox[] _positionBoxes;
        private bool _creatingNew;
        private string _newParent = "ZERO";   // [+] 를 누를 때 트리에서 선택돼 있던 좌표계 = 새 좌표계의 부모
        private int _collapsedWidth;

        public F_CoordSystem(ValidationSystem system, Action<string> log)
        {
            InitializeComponent();
            _system = system;
            _log = log;
            _positionBoxes = new[] { _xBox, _yBox, _zBox, _uBox, _vBox, _wBox };
            Text = $"Manage Coordinate Systems ({system.HexapodHost})";

            const string notSupported = "이 프로그램에서는 지원하지 않습니다 (PIMikroMove 에서 하세요)";
            _toolTip.SetToolTip(_addButton, "새 좌표계 (선택한 좌표계의 하위로)");
            _toolTip.SetToolTip(_deleteButton, "선택한 좌표계 삭제");
            _toolTip.SetToolTip(_refreshButton, "새로고침");
            _toolTip.SetToolTip(_saveButton, notSupported);
            _toolTip.SetToolTip(_saveMenuButton, notSupported);
            _toolTip.SetToolTip(_expandButton, "추가 속성 (이동 한계 등) 보기");
            _toolTip.SetToolTip(_clearButton, "메시지 지우기");
        }

        private void F_CoordSystem_Load(object sender, EventArgs e)
        {
            _collapsedWidth = ClientSize.Width;
            RefreshTree();
        }

        #region 목록

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            string selected = (_csTree.SelectedNode?.Tag as HexapodCoordSystem)?.Name;
            RefreshTree(selected);
        }

        private void RefreshTree(string selectName = null)
        {
            _creatingNew = false;
            if (!_system.Hexapod.IsConnected)
            {
                SetStatus("헥사포드가 연결되어 있지 않습니다");
                return;
            }
            var systems = _system.Hexapod.GetCoordSystems();
            if (systems == null)
            {
                SetStatus("좌표계 목록을 읽지 못했습니다 - 메인 화면 로그를 확인하세요");
                return;
            }
            _systems = systems;

            // PIMikroMove 처럼 PI 내부 좌표계(PI_BASE, PI_LEVELLING)는 숨기고, 보이는 부모 밑에 트리로 붙인다.
            var visible = _systems.Where(cs => !cs.IsPiInternal).ToList();
            var nodes = new Dictionary<string, TreeNode>();
            _csTree.BeginUpdate();
            _csTree.Nodes.Clear();
            foreach (var cs in visible)
            {
                var node = new TreeNode(cs.IsActive ? $"{cs.Name} ({cs.Type})   ---active---" : $"{cs.Name} ({cs.Type})") { Tag = cs };
                if (cs.IsActive)
                {
                    node.NodeFont = new Font(_csTree.Font, FontStyle.Bold);
                }
                nodes[cs.Name] = node;
            }
            foreach (var cs in visible)
            {
                if (cs.Parent != null && nodes.TryGetValue(cs.Parent, out TreeNode parent))
                {
                    parent.Nodes.Add(nodes[cs.Name]);
                }
                else
                {
                    _csTree.Nodes.Add(nodes[cs.Name]);
                }
            }
            _csTree.ExpandAll();
            _csTree.EndUpdate();

            TreeNode toSelect = (selectName != null && nodes.TryGetValue(selectName, out TreeNode named) ? named : null)
                ?? nodes.Values.FirstOrDefault(n => ((HexapodCoordSystem)n.Tag).IsActive)
                ?? nodes.Values.FirstOrDefault();
            _csTree.SelectedNode = toSelect;
            if (toSelect == null)
            {
                ShowProperties(null);
            }
        }

        private void CsTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            _creatingNew = false;
            ShowProperties(e.Node?.Tag as HexapodCoordSystem);
        }

        #endregion

        #region CS Properties

        /// <summary>선택한 좌표계를 오른쪽 CS Properties 에 표시한다. 직접 만든 타입(KSD/KST/KSW)이고 비활성이면 위치를 고쳐 재정의할 수 있다.</summary>
        private void ShowProperties(HexapodCoordSystem cs)
        {
            _nameBox.Text = cs?.Name ?? "";
            _nameBox.ReadOnly = true;
            _typeCombo.Items.Clear();
            if (cs != null)
            {
                _typeCombo.Items.Add(cs.Type);
                _typeCombo.SelectedIndex = 0;
            }
            _typeCombo.Enabled = false;

            Dictionary<string, string> pos = null;
            cs?.Items.TryGetValue("POS", out pos);
            for (int i = 0; i < Axes.Length; i++)
            {
                string raw = null;
                pos?.TryGetValue(Axes[i], out raw);
                _positionBoxes[i].Text = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                    ? v.ToString("0.000", CultureInfo.InvariantCulture) : "";
            }

            bool editable = cs != null && !cs.IsActive && Hexapod.DefinableCoordSystemTypes.Contains(cs.Type);
            SetPositionsEditable(editable);
            _setButton.Enabled = editable;
            _activateButton.Enabled = cs != null && !cs.IsActive;
            _deleteButton.Enabled = IsDeletable(cs);

            _extraBox.Text = FormatExtraItems(cs);
        }

        /// <summary>
        /// 추가 속성(이동 한계 등): POS 외의 항목을 축(행) × 항목(열) 표로 보여 준다. 고정폭 정렬이 깨지지 않게 머리글은 영문만 쓴다.
        /// NLM/PLM = 소프트 한계 하한/상한, SSL = 소프트 한계 사용(1/0), SST = 스텝 크기. 축이 X..W 가 아닌 항목(SPI 피벗 R,S,T)은 아래에 따로.
        /// </summary>
        private static string FormatExtraItems(HexapodCoordSystem cs)
        {
            if (cs == null)
            {
                return "";
            }
            var items = cs.Items.Where(item => item.Key != "POS").ToList();
            if (items.Count == 0)
            {
                return "(no extra properties)";
            }

            var axisItems = items.Where(item => item.Value.Keys.All(k => Axes.Contains(k))).ToList();
            var otherItems = items.Except(axisItems).ToList();
            var lines = new List<string>();
            if (axisItems.Count > 0)
            {
                lines.Add("Axis" + string.Join("", axisItems.Select(item => item.Key.PadLeft(11))));
                foreach (string axis in Axes)
                {
                    lines.Add(axis.PadRight(4) + string.Join("", axisItems.Select(item =>
                        (item.Value.TryGetValue(axis, out string raw) ? FormatNumber(raw) : "-").PadLeft(11))));
                }
                lines.Add("");
                lines.Add("NLM/PLM = soft limit -/+ , SSL = limit on(1), SST = step");
            }
            foreach (var item in otherItems)
            {
                lines.Add("");
                lines.Add(item.Key + (item.Key == "SPI" ? " (pivot)" : "") + ":  " +
                          string.Join("   ", item.Value.Select(kv => $"{kv.Key}={FormatNumber(kv.Value)}")));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatNumber(string raw)
        {
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && raw.Contains(".")
                ? v.ToString("0.000", CultureInfo.InvariantCulture) : raw;
        }

        private void SetPositionsEditable(bool editable)
        {
            foreach (var box in _positionBoxes)
            {
                box.ReadOnly = !editable;
            }
        }

        private void ExpandButton_Click(object sender, EventArgs e)
        {
            bool expand = !_extraBox.Visible;
            _extraBox.Visible = expand;
            _expandButton.Text = expand ? "<" : ">";
            ClientSize = new Size(expand ? _collapsedWidth + _extraBox.Width + 8 : _collapsedWidth, ClientSize.Height);
        }

        #endregion

        #region 생성 / 활성화

        /// <summary>
        /// [+]: 새 좌표계 입력 상태로 바꾼다. 실제 생성은 [Set Coord. Sys.] 에서.
        /// PIMikroMove 처럼 트리에서 선택돼 있던 좌표계의 하위로 만든다 (선택이 없으면 ZERO 하위).
        /// </summary>
        private void AddButton_Click(object sender, EventArgs e)
        {
            var selected = _csTree.SelectedNode?.Tag as HexapodCoordSystem;
            _newParent = selected?.Name ?? "ZERO";
            _creatingNew = true;   // 트리 선택은 그대로 둬서 부모가 어디인지 보이게 한다

            _nameBox.Text = "";
            _nameBox.ReadOnly = false;
            _typeCombo.Items.Clear();
            _typeCombo.Items.AddRange(Hexapod.DefinableCoordSystemTypes);
            _typeCombo.SelectedIndex = 0;   // KSD
            _typeCombo.Enabled = true;
            foreach (var box in _positionBoxes)
            {
                box.Text = "0.000";
            }
            SetPositionsEditable(true);
            _setButton.Enabled = true;
            _activateButton.Enabled = false;
            _deleteButton.Enabled = false;
            _extraBox.Text = "";
            SetStatus($"새 좌표계 ('{_newParent}' 하위): Name/Type/Position 입력 후 [Set Coord. Sys.]");
            _nameBox.Focus();
        }

        /// <summary>[Set Coord. Sys.]: 새 좌표계를 만들거나, 선택한 (비활성) 좌표계를 입력한 위치로 재정의한다.</summary>
        private void SetButton_Click(object sender, EventArgs e)
        {
            string name = _nameBox.Text.Trim();
            string type = Convert.ToString(_typeCombo.SelectedItem);
            if (!Regex.IsMatch(name, @"^[A-Za-z0-9_]+$"))
            {
                SetStatus("Name 은 영문/숫자/_ 만 쓸 수 있습니다 (공백 불가)");
                return;
            }
            if (_systems.Any(cs => cs.Name == name && (cs.IsPiInternal || cs.Name == "ZERO")))
            {
                SetStatus($"'{name}' 은(는) 기본/PI 내부 좌표계라 정의할 수 없습니다");
                return;
            }

            var values = new double[Axes.Length];
            for (int i = 0; i < Axes.Length; i++)
            {
                if (!double.TryParse(_positionBoxes[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                {
                    SetStatus($"{Axes[i]} 값이 숫자가 아닙니다");
                    _positionBoxes[i].Focus();
                    return;
                }
            }

            var existing = _systems.FirstOrDefault(cs => cs.Name == name);
            if (_creatingNew && existing != null && MessageBox.Show(
                    $"'{name}' 좌표계가 이미 있습니다. 새 값으로 덮어쓸까요?",
                    "Set Coord. Sys.", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }

            if (_creatingNew && name == _newParent)
            {
                SetStatus("자기 자신을 부모로 할 수 없습니다 - 다른 이름을 쓰세요");
                return;
            }

            if (!_system.Hexapod.DefineCoordSystem(type, name, values))
            {
                SetStatus("정의 실패 - 메인 화면 로그를 확인하세요 (활성 상태인 좌표계는 수정할 수 없습니다)");
                return;
            }
            string detail = string.Join(" ", Axes.Select((axis, i) => $"{axis}={values[i]:0.000}"));
            _log($"[Hexapod] 좌표계 '{name}' 정의 ({type} {detail}, mm/deg)");

            // 정의만 하면 ZERO 하위가 되므로, 새로 만들 때 선택돼 있던 좌표계가 ZERO 가 아니면 그 밑으로 연결한다.
            if (_creatingNew && _newParent != "ZERO")
            {
                if (_system.Hexapod.LinkCoordSystem(name, _newParent))
                {
                    _log($"[Hexapod] 좌표계 '{name}' → '{_newParent}' 하위로 연결");
                }
                else
                {
                    SetStatus($"'{name}' 은(는) 만들어졌지만 '{_newParent}' 하위 연결 실패 - 메인 화면 로그를 확인하세요");
                    RefreshTree(name);
                    return;
                }
            }
            SetStatus($"'{name}' ({type}) 정의 완료 - 사용하려면 [Activate CS]");
            RefreshTree(name);
        }

        /// <summary>삭제 가능: 직접 만든 좌표계이면서 비활성. ZERO / PI 내부(PI_BASE 등) / 활성 좌표계는 안 된다.</summary>
        private static bool IsDeletable(HexapodCoordSystem cs)
        {
            return cs != null && !cs.IsActive && !cs.IsPiInternal && cs.Name != "ZERO";
        }

        /// <summary>[휴지통]: 선택한 좌표계를 삭제(KRM)한다. 하위 좌표계가 있으면 컨트롤러가 거부할 수 있다.</summary>
        private void DeleteButton_Click(object sender, EventArgs e)
        {
            var cs = _csTree.SelectedNode?.Tag as HexapodCoordSystem;
            if (!IsDeletable(cs))
            {
                SetStatus("ZERO / PI 내부 / 활성 좌표계는 삭제할 수 없습니다");
                return;
            }
            bool hasChildren = _systems.Any(other => other.Parent == cs.Name);
            if (MessageBox.Show(
                    $"'{cs.Name}' 좌표계를 삭제합니다." + (hasChildren ? "\n(하위 좌표계가 있어 컨트롤러가 거부할 수 있습니다)" : "") + "\n계속할까요?",
                    "좌표계 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }
            if (_system.Hexapod.DeleteCoordSystem(cs.Name))
            {
                SetStatus($"'{cs.Name}' 삭제 완료");
                _log($"[Hexapod] 좌표계 '{cs.Name}' 삭제");
                RefreshTree(cs.Parent);
            }
            else
            {
                SetStatus("삭제 실패 - 메인 화면 로그를 확인하세요");
            }
        }

        private void ActivateButton_Click(object sender, EventArgs e)
        {
            if (!(_csTree.SelectedNode?.Tag is HexapodCoordSystem cs))
            {
                SetStatus("활성화할 좌표계를 목록에서 고르세요");
                return;
            }
            DialogResult answer = MessageBox.Show(
                $"'{cs.Name}' 좌표계를 활성화합니다.\n헥사포드는 움직이지 않지만 표시 위치와 이동 기준(0 위치 포함)이 바뀝니다. 계속할까요?",
                "Activate CS", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (answer != DialogResult.OK)
            {
                return;
            }
            if (_system.Hexapod.ActivateCoordSystem(cs.Name))
            {
                SetStatus($"'{cs.Name}' 활성화 완료");
                _log($"[Hexapod] 좌표계 '{cs.Name}' 활성화");
            }
            else
            {
                SetStatus("활성화 실패 - 메인 화면 로그를 확인하세요");
            }
            RefreshTree(cs.Name);
        }

        #endregion

        private void ClearButton_Click(object sender, EventArgs e)
        {
            _statusBox.Text = "";
        }

        private void SetStatus(string text)
        {
            _statusBox.Text = text;
        }
    }
}
