using System;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using ValidationStage.Devices.View3D;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices
{
    /// <summary>
    /// 헥사포드 3D 보기 창 - PIMikroMove 의 3D 보기를 본떠 PI 모델 자료(STL + hexdata)로 그린다.
    /// 비모달로 띄워 두면(<c>Show()</c>) 100 ms 마다 헥사포드 자세를 읽어 갱신한다. 마우스: 드래그 회전, 휠 확대, 더블클릭 초기화.
    /// 형상 자료는 DLL 에 포함된 H-811.I2 를 쓰고, 다른 모델이면 PI 설치 폴더(C:\ProgramData\PI\PIHexapodDataFiles)에서 찾는다.
    /// </summary>
    /// <example><code>new Hexapod3DForm(system.Hexapod).Show(this);</code></example>
    /// <remarks>
    /// 자세 계산: qPOS 는 활성 좌표계 기준 값이라, KLT?(활성 → ZERO) 변환 K 로 ZERO 기준 상판 자세로 바꾼다.
    ///   P_zero = K · P_active · K⁻¹  (행 벡터 규약에서는 K⁻¹ · P · K). 예: TILTEDCS(U=180) 활성 + 위치 0 → 상판은 기준 자세.
    /// </remarks>
    public partial class Hexapod3DForm : Form
    {
        private const int CoordSystemRefreshTicks = 10;   // 활성 좌표계/변환은 1초마다 다시 읽는다

        private readonly Hexapod _hexapod;
        private HexapodScene _scene;
        private string _model;
        private string _activeCs = "ZERO";
        private Matrix3D _csToZero = Matrix3D.Identity;   // 활성 좌표계 → ZERO (행 벡터 규약)
        private int _tick;

        /// <summary>3D 창을 만든다. 헥사포드가 연결된 상태에서 띄운다 (모델 이름을 컨트롤러에서 읽음).</summary>
        public Hexapod3DForm(Hexapod hexapod)
        {
            InitializeComponent();
            _hexapod = hexapod;
            Text = T("헥사포드 3D 보기", "Hexapod 3D View");
        }

        private void Hexapod3DForm_Load(object sender, EventArgs e)
        {
            _model = _hexapod.GetModelName();
            if (_model == null)
            {
                _statusLabel.Text = T("헥사포드 모델을 읽지 못했습니다 (연결 상태 확인)", "Failed to read the hexapod model (check the connection)");
                return;
            }
            try
            {
                var geometry = HexapodGeometry.Load(_model);
                _scene = new HexapodScene(geometry);
                _host.Child = _scene.View;
                Text = T($"헥사포드 3D 보기 - {_model}", $"Hexapod 3D View - {_model}");
                if (geometry.PlatformStl == null || geometry.LowerStrutStl == null)
                {
                    _statusLabel.Text = T($"{_model}: 3D 형상(STL)이 없어 단순 형상으로 표시합니다", $"{_model}: no 3D shapes (STL) - showing a simple shape");
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = T($"{_model} 형상 자료를 읽지 못했습니다: {ex.Message}", $"Failed to read {_model} geometry: {ex.Message}");
                return;
            }
            _tick = 0;
            _timer.Start();
        }

        private void Hexapod3DForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_scene == null)
            {
                return;
            }
            if (!_hexapod.IsConnected)
            {
                _statusLabel.Text = T($"{_model} | 헥사포드 연결 안됨 - 마지막 자세를 표시 중", $"{_model} | hexapod not connected - showing the last pose");
                return;
            }

            if (_tick++ % CoordSystemRefreshTicks == 0)
            {
                RefreshCoordSystem();
            }

            double[] pose = _hexapod.GetRawPose();
            if (pose == null)
            {
                return;
            }
            Matrix3D active = PoseMatrix(pose);
            Matrix3D inverse = _csToZero;
            inverse.Invert();
            Matrix3D platform = inverse * active * _csToZero;
            _scene.Update(platform, _csToZero);

            string values = string.Join("  ", new[] { "X", "Y", "Z" }.Select((a, i) => $"{a} {pose[i]:0.000}"))
                + " mm   " + string.Join("  ", new[] { "U", "V", "W" }.Select((a, i) => $"{a} {pose[i + 3]:0.000}")) + " deg";
            _statusLabel.Text = T($"{_model} | 좌표계 {_activeCs}: {values} | 드래그 회전 · 휠 확대 · 더블클릭 초기화",
                $"{_model} | CS {_activeCs}: {values} | drag rotate · wheel zoom · double-click reset");
        }

        private void RefreshCoordSystem()
        {
            string cs = _hexapod.GetActiveUserCoordSystem();
            var chain = cs == null ? null : _hexapod.GetTransformToZero(cs);
            if (chain == null)
            {
                return;   // 다음 주기에 다시 시도
            }
            // 여러 단계면(하위 좌표계) KLT? 가 나열한 순서대로 이어 붙인다 - 행 벡터 규약이라 먼저 오는 쪽이 안쪽.
            Matrix3D k = Matrix3D.Identity;
            foreach (double[] step in chain)
            {
                k.Append(PoseMatrix(step));
            }
            if (cs != _activeCs)
            {
                _scene.SetCoordSystemName(cs);
            }
            _activeCs = cs;
            _csToZero = k;
        }

        /// <summary>
        /// (X,Y,Z mm, U,V,W deg) → 행 벡터 규약 행렬. PI 헥사포드 회전 순서: 고정축 기준 U(X) → V(Y) → W(Z).
        /// </summary>
        private static Matrix3D PoseMatrix(double[] v)
        {
            var m = Matrix3D.Identity;
            m.Rotate(new Quaternion(new Vector3D(1, 0, 0), v[3]));
            m.Rotate(new Quaternion(new Vector3D(0, 1, 0), v[4]));
            m.Rotate(new Quaternion(new Vector3D(0, 0, 1), v[5]));
            m.Translate(new Vector3D(v[0], v[1], v[2]));
            return m;
        }
    }
}
