using MotorizedStage_SK_PI;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using ValidationStage.View3D;

namespace ValidationStage
{
    /// <summary>
    /// 헥사포드 3D 보기 창 - PIMikroMove 의 3D 보기를 본떠 PI 모델 자료(STL + hexdata)로 그린다 (HexapodScene).
    /// 비모달 창이라 띄워 둔 채로 조그/이동하면 100 ms 마다 자세가 갱신된다.
    /// 자세 계산: qPOS 는 활성 좌표계 기준 값이라, KLT?(활성 → ZERO) 변환 K 로 ZERO 기준 상판 자세로 바꾼다.
    ///   P_zero = K · P_active · K⁻¹  (행 벡터 규약에서는 K⁻¹ · P · K). 예: TILTEDCS(U=180) 활성 + 위치 0 → 상판은 기준 자세.
    /// </summary>
    public partial class F_Hexapod3D : Form
    {
        private const int CoordSystemRefreshTicks = 10;   // 활성 좌표계/변환은 1초마다 다시 읽는다

        private readonly MotionController _motion;
        private HexapodScene _scene;
        private string _model;
        private string _activeCs = "ZERO";
        private Matrix3D _csToZero = Matrix3D.Identity;   // 활성 좌표계 → ZERO (행 벡터 규약)
        private int _tick;

        public F_Hexapod3D(MotionController motion)
        {
            InitializeComponent();
            _motion = motion;
        }

        private void F_Hexapod3D_Load(object sender, EventArgs e)
        {
            _model = _motion.GetHexapodModelName();
            if (_model == null)
            {
                _statusLabel.Text = "헥사포드 모델을 읽지 못했습니다 (연결 상태 확인)";
                return;
            }
            try
            {
                var geometry = HexapodGeometry.Load(_model);
                _scene = new HexapodScene(geometry);
                _host.Child = _scene.View;
                Text = $"헥사포드 3D 보기 - {_model}";
                if (geometry.PlatformStl == null || geometry.LowerStrutStl == null)
                {
                    _statusLabel.Text = $"{_model}: 3D 형상(STL)이 없어 단순 형상으로 표시합니다";
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"{_model} 형상 자료를 읽지 못했습니다: {ex.Message}";
                return;
            }
            _tick = 0;
            _timer.Start();
        }

        private void F_Hexapod3D_FormClosing(object sender, FormClosingEventArgs e)
        {
            _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_scene == null)
            {
                return;
            }
            if (!_motion.Hexapod.IsConnected)
            {
                _statusLabel.Text = $"{_model} | 헥사포드 연결 안됨 - 마지막 자세를 표시 중";
                return;
            }

            if (_tick++ % CoordSystemRefreshTicks == 0)
            {
                RefreshCoordSystem();
            }

            double[] pose = _motion.GetHexapodRawPose();
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
            _statusLabel.Text = $"{_model} | 좌표계 {_activeCs}: {values} | 드래그 회전 · 휠 확대 · 더블클릭 초기화";
        }

        private void RefreshCoordSystem()
        {
            string cs = _motion.GetHexapodActiveUserCoordSystem();
            var chain = cs == null ? null : _motion.GetHexapodTransformToZero(cs);
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
