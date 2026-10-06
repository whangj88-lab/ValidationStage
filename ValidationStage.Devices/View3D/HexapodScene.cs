using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ValidationStage.Devices.View3D
{
    /// <summary>
    /// 헥사포드 3D 장면 (WPF Viewport3D). F_Hexapod3D 가 ElementHost 로 띄운다.
    /// 좌표: 헥사포드 ZERO 좌표계 그대로 (mm, Z 위쪽). 원점 = 상판 연결점 평면 중심, 바닥 연결점은 z = -h0.
    /// 다리: 바닥 연결점 B 와 (상판 자세로 옮긴) 상판 연결점 A 사이. STL 은 +Y 가 다리 방향(B→A)이고,
    ///   아래 조각은 원점을 B + dir*LowerOffset.Z, 위 조각은 A - dir*UpperOffset.Z 에 둔다 (CAD ini 의 offset).
    /// 마우스: 왼쪽 드래그 = 회전, 휠 = 확대/축소, 더블클릭 = 시점 초기화.
    /// </summary>
    internal class HexapodScene
    {
        public FrameworkElement View => _root;

        private readonly HexapodGeometry _g;
        private readonly Border _root;
        private readonly Viewport3D _viewport;
        private readonly PerspectiveCamera _camera;
        private readonly MatrixTransform3D _platformTransform = new MatrixTransform3D();
        private readonly MatrixTransform3D _csTransform = new MatrixTransform3D();
        private readonly MatrixTransform3D[] _lowerTransforms = new MatrixTransform3D[6];
        private readonly MatrixTransform3D[] _upperTransforms = new MatrixTransform3D[6];
        private readonly Point3D _target;

        // 축 이름표: 3D 글자는 보는 방향에 따라 뒤집히므로(PIMikroMove 의 "X KSD" 처럼), 화살표 끝을 화면 좌표로
        // 투영해서 그 위에 2D 글자를 겹친다 - 항상 똑바로 읽힌다.
        private readonly Canvas _overlay = new Canvas { IsHitTestVisible = false };
        private readonly TextBlock[] _triadLabels = new TextBlock[3];
        private readonly TextBlock[] _worldLabels = new TextBlock[3];
        private readonly Point3D[] _worldTips = new Point3D[3];
        private double _triadLength;
        private static readonly string[] AxisNames = { "X", "Y", "Z" };
        private static readonly Vector3D[] AxisDirs = { new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), new Vector3D(0, 0, 1) };
        private static readonly Color[] AxisColors = { Colors.Red, Color.FromRgb(0x20, 0xB0, 0x20), Colors.Blue };

        // 시점 (구면 좌표): 방위각/고도각(deg), 거리(mm)
        private double _azimuth, _elevation, _distance;
        private Point _lastMouse;
        private bool _dragging;

        private static readonly Color PlatformColor = Color.FromRgb(0xC8, 0xA8, 0x78);
        private static readonly Color BaseColor = Color.FromRgb(0x4A, 0x4A, 0x4A);
        private static readonly Color StrutColor = Color.FromRgb(0xB8, 0xB8, 0xC0);

        public HexapodScene(HexapodGeometry geometry)
        {
            _g = geometry;
            _target = new Point3D(0, 0, -_g.H0 * 0.5);
            _camera = new PerspectiveCamera { FieldOfView = 40, UpDirection = new Vector3D(0, 0, 1) };
            ResetView();

            var scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(0x60, 0x60, 0x60)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(0xC0, 0xC0, 0xC0), new Vector3D(-1, -0.6, -1.2)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(0x50, 0x50, 0x50), new Vector3D(1, 1, 0.3)));

            AddGridAndWorldAxes(scene);
            AddBaseplate(scene);
            AddPlatform(scene);
            AddStruts(scene);
            AddCoordinateTriad(scene);

            _viewport = new Viewport3D { Camera = _camera, ClipToBounds = true };
            _viewport.Children.Add(new ModelVisual3D { Content = scene });

            for (int i = 0; i < 3; i++)
            {
                _triadLabels[i] = AxisLabel(AxisColors[i], 14);
                _worldLabels[i] = AxisLabel(AxisColors[i], 12);
                _worldLabels[i].Text = AxisNames[i] + " (ZERO)";
                _overlay.Children.Add(_triadLabels[i]);
                _overlay.Children.Add(_worldLabels[i]);
            }
            SetCoordSystemName("ZERO");

            var layers = new Grid();
            layers.Children.Add(_viewport);
            layers.Children.Add(_overlay);

            // Border 배경이 있어야 빈 공간에서도 마우스 이벤트를 받는다.
            _root = new Border { Background = Brushes.White, Child = layers };
            _root.MouseLeftButtonDown += OnMouseDown;
            _root.MouseMove += OnMouseMove;
            _root.MouseLeftButtonUp += (s, e) => { _dragging = false; _root.ReleaseMouseCapture(); };
            _root.MouseWheel += OnMouseWheel;
            _root.SizeChanged += (s, e) => UpdateLabels();

            Update(Matrix3D.Identity, Matrix3D.Identity);
        }

        /// <summary>상판 화살표 이름표에 붙일 좌표계 이름 (예: "X TILTEDCS").</summary>
        public void SetCoordSystemName(string name)
        {
            for (int i = 0; i < 3; i++)
            {
                _triadLabels[i].Text = $"{AxisNames[i]} {name}";
            }
        }

        private static TextBlock AxisLabel(Color color, double size)
        {
            return new TextBlock
            {
                Foreground = new SolidColorBrush(color),
                Background = new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)),
                FontWeight = FontWeights.Bold,
                FontSize = size,
                Padding = new Thickness(2, 0, 2, 0),
            };
        }

        /// <summary>화살표 끝(3D)을 화면에 투영해 이름표를 옮긴다. 카메라 뒤로 가면 숨긴다.</summary>
        private void UpdateLabels()
        {
            if (_root == null)
            {
                return;   // 생성자에서 카메라를 먼저 맞출 때
            }
            for (int i = 0; i < 3; i++)
            {
                Point3D tip = _csTransform.Matrix.Transform(new Point3D(0, 0, 0) + AxisDirs[i] * (_triadLength * 1.08));
                PlaceLabel(_triadLabels[i], tip);
                PlaceLabel(_worldLabels[i], _worldTips[i]);
            }
        }

        private void PlaceLabel(TextBlock label, Point3D world)
        {
            Point? screen = Project(world);
            label.Visibility = screen.HasValue ? Visibility.Visible : Visibility.Hidden;
            if (screen.HasValue)
            {
                Canvas.SetLeft(label, screen.Value.X + 4);
                Canvas.SetTop(label, screen.Value.Y - 10);
            }
        }

        /// <summary>월드 좌표 → 화면 좌표 (WPF PerspectiveCamera 의 FieldOfView 는 가로 시야각).</summary>
        private Point? Project(Point3D p)
        {
            double w = _root.ActualWidth, h = _root.ActualHeight;
            if (w <= 0 || h <= 0)
            {
                return null;
            }
            Vector3D look = _camera.LookDirection; look.Normalize();
            Vector3D right = Vector3D.CrossProduct(look, _camera.UpDirection); right.Normalize();
            Vector3D up = Vector3D.CrossProduct(right, look);
            Vector3D v = p - _camera.Position;
            double depth = Vector3D.DotProduct(v, look);
            if (depth <= 1)
            {
                return null;
            }
            double f = (w / 2) / Math.Tan(_camera.FieldOfView * Math.PI / 360);
            return new Point(w / 2 + Vector3D.DotProduct(v, right) * f / depth,
                             h / 2 - Vector3D.DotProduct(v, up) * f / depth);
        }

        /// <summary>
        /// 자세를 반영한다. platformPose = 상판 좌표 → ZERO (행 벡터 규약: p_world = p_local * M).
        /// csFrame = 표시할 좌표계 축의 상판 기준 방향 (활성 좌표계가 ZERO 면 Identity).
        /// </summary>
        public void Update(Matrix3D platformPose, Matrix3D csFrame)
        {
            _platformTransform.Matrix = platformPose;
            _csTransform.Matrix = csFrame * platformPose;

            for (int i = 0; i < 6; i++)
            {
                Point3D b = (Point3D)_g.BaseJoints[i];
                Point3D a = platformPose.Transform((Point3D)_g.PlatformJoints[i]);
                Vector3D dir = a - b;
                dir.Normalize();
                Matrix3D rotation = AlignYTo(dir, b);

                // 로컬 offset X/Y → 다리 방향으로 회전 → 연결점에서 다리 방향으로 offset Z 만큼 떨어진 곳
                Matrix3D lowerM = Matrix3D.Identity;
                lowerM.Translate(new Vector3D(_g.LowerStrutOffset.X, _g.LowerStrutOffset.Y, 0));
                lowerM.Append(rotation);
                lowerM.Translate((Vector3D)b + dir * _g.LowerStrutOffset.Z);
                _lowerTransforms[i].Matrix = lowerM;

                Matrix3D upperM = Matrix3D.Identity;
                upperM.Translate(new Vector3D(_g.UpperStrutOffset.X, _g.UpperStrutOffset.Y, 0));
                upperM.Append(rotation);
                upperM.Translate((Vector3D)a - dir * _g.UpperStrutOffset.Z);
                _upperTransforms[i].Matrix = upperM;
            }
            UpdateLabels();
        }

        /// <summary>
        /// 로컬 +Y 를 dir 로 돌리는 회전 (이동 없음). 다리 둘레 회전(롤)은 로컬 +X 가 헥사포드 중심축에서
        /// 바깥쪽(바닥 연결점 방향)을 향하도록 정한다.
        /// </summary>
        private static Matrix3D AlignYTo(Vector3D dir, Point3D baseJoint)
        {
            Vector3D radial = new Vector3D(baseJoint.X, baseJoint.Y, 0);
            Vector3D x = radial - Vector3D.DotProduct(radial, dir) * dir;   // dir 에 수직인 바깥 방향
            if (x.LengthSquared < 1e-9)
            {
                x = Vector3D.CrossProduct(dir, new Vector3D(0, 0, 1));
            }
            x.Normalize();
            Vector3D z = Vector3D.CrossProduct(x, dir);
            // 행 벡터 규약: 각 행 = 로컬 축이 월드에서 향하는 방향
            return new Matrix3D(x.X, x.Y, x.Z, 0,
                                dir.X, dir.Y, dir.Z, 0,
                                z.X, z.Y, z.Z, 0,
                                0, 0, 0, 1);
        }

        #region 부품

        private void AddBaseplate(Model3DGroup scene)
        {
            Model3D model;
            if (_g.BaseplateStl != null)
            {
                MeshGeometry3D mesh = StlReader.Load(_g.BaseplateStl);
                Rect3D bounds = mesh.Bounds;
                // STL 원점이 중심이 아니라서(바닥판) XY 는 외곽 중심을 맞추고, Z 는 바닥 연결점 평면 기준 offset.
                var m = Matrix3D.Identity;
                m.Translate(new Vector3D(-(bounds.X + bounds.SizeX / 2), -(bounds.Y + bounds.SizeY / 2), -bounds.Z));
                m.Translate(new Vector3D(_g.BaseplateOffset.X, _g.BaseplateOffset.Y, -_g.H0 + _g.BaseplateOffset.Z));
                model = new GeometryModel3D(mesh, Material(BaseColor)) { Transform = new MatrixTransform3D(m) };
            }
            else
            {
                model = new GeometryModel3D(Cylinder(new Point3D(0, 0, -_g.H0 - 12), new Point3D(0, 0, -_g.H0), _g.PlatformOuterRadius + 15, 64),
                    Material(BaseColor));
            }
            scene.Children.Add(model);
        }

        private void AddPlatform(Model3DGroup scene)
        {
            Model3D model;
            if (_g.PlatformStl != null)
            {
                MeshGeometry3D mesh = StlReader.Load(_g.PlatformStl);
                Rect3D bounds = mesh.Bounds;
                var m = Matrix3D.Identity;
                m.Translate(new Vector3D(-(bounds.X + bounds.SizeX / 2), -(bounds.Y + bounds.SizeY / 2), -(bounds.Z + bounds.SizeZ / 2)));
                m.Translate(_g.PlatformOffset);
                model = new GeometryModel3D(mesh, Material(PlatformColor)) { Transform = new MatrixTransform3D(m) };
            }
            else
            {
                model = new GeometryModel3D(Cylinder(new Point3D(0, 0, 0), new Point3D(0, 0, 9), _g.PlatformOuterRadius, 64),
                    Material(PlatformColor));
            }
            var group = new Model3DGroup { Transform = _platformTransform };
            group.Children.Add(model);
            scene.Children.Add(group);
        }

        private void AddStruts(Model3DGroup scene)
        {
            MeshGeometry3D lower = _g.LowerStrutStl != null ? StlReader.Load(_g.LowerStrutStl) : null;
            MeshGeometry3D upper = _g.UpperStrutStl != null ? StlReader.Load(_g.UpperStrutStl) : null;
            double r = _g.StrutDiameter / 2;
            double length = (_g.PlatformJoints[0] - _g.BaseJoints[0]).Length;
            for (int i = 0; i < 6; i++)
            {
                _lowerTransforms[i] = new MatrixTransform3D();
                _upperTransforms[i] = new MatrixTransform3D();
                // STL 이 없으면 원기둥 두 개 (아래 굵게, 위 가늘게)로 대신한다.
                MeshGeometry3D lowerMesh = lower ?? Cylinder(new Point3D(0, -_g.LowerStrutOffset.Z, 0), new Point3D(0, length * 0.6 - _g.LowerStrutOffset.Z, 0), r, 24);
                MeshGeometry3D upperMesh = upper ?? Cylinder(new Point3D(0, -length * 0.6 + _g.UpperStrutOffset.Z, 0), new Point3D(0, _g.UpperStrutOffset.Z, 0), r * 0.6, 24);
                scene.Children.Add(new GeometryModel3D(lowerMesh, Material(StrutColor)) { Transform = _lowerTransforms[i] });
                scene.Children.Add(new GeometryModel3D(upperMesh, Material(StrutColor)) { Transform = _upperTransforms[i] });
            }
        }

        /// <summary>활성 좌표계 축 (빨강 X, 초록 Y, 파랑 Z) - 상판과 같이 움직인다.</summary>
        private void AddCoordinateTriad(Model3DGroup scene)
        {
            var triad = new Model3DGroup { Transform = _csTransform };
            double len = _g.PlatformOuterRadius * 1.5;   // 상판(반지름) 밖까지 나와야 가려지지 않는다
            _triadLength = len;
            triad.Children.Add(Arrow(new Vector3D(1, 0, 0), len, Colors.Red));
            triad.Children.Add(Arrow(new Vector3D(0, 1, 0), len, Color.FromRgb(0x20, 0xB0, 0x20)));
            triad.Children.Add(Arrow(new Vector3D(0, 0, 1), len, Colors.Blue));
            triad.Children.Add(new GeometryModel3D(Sphere(new Point3D(0, 0, 0), 3, 16), Material(Colors.Black)));
            scene.Children.Add(triad);
        }

        private void AddGridAndWorldAxes(Model3DGroup scene)
        {
            double floor = -_g.H0 - 20;
            double half = 150, step = 25, w = 0.4;
            var grid = new MeshGeometry3D();
            for (double v = -half; v <= half + 0.01; v += step)
            {
                AddQuad(grid, new Point3D(-half, v - w, floor), new Point3D(half, v - w, floor), new Point3D(half, v + w, floor), new Point3D(-half, v + w, floor));
                AddQuad(grid, new Point3D(v - w, -half, floor), new Point3D(v + w, -half, floor), new Point3D(v + w, half, floor), new Point3D(v - w, half, floor));
            }
            var gridMaterial = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)));
            scene.Children.Add(new GeometryModel3D(grid, gridMaterial) { BackMaterial = gridMaterial });

            // 바닥 위 월드 축 (ZERO 방향)
            var origin = new Point3D(0, 0, floor);
            scene.Children.Add(new GeometryModel3D(Cylinder(origin, origin + new Vector3D(half, 0, 0), 0.8, 8), Material(Colors.Red)));
            scene.Children.Add(new GeometryModel3D(Cylinder(origin, origin + new Vector3D(0, half, 0), 0.8, 8), Material(Color.FromRgb(0x20, 0xB0, 0x20))));
            scene.Children.Add(new GeometryModel3D(Cylinder(origin, new Point3D(0, 0, 60), 0.8, 8), Material(Colors.Blue)));
            _worldTips[0] = origin + new Vector3D(half, 0, 0);
            _worldTips[1] = origin + new Vector3D(0, half, 0);
            _worldTips[2] = new Point3D(0, 0, 60);
        }

        #endregion

        #region 시점 조작

        public void ResetView()
        {
            _azimuth = -55; _elevation = 25; _distance = 520;
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            double az = _azimuth * Math.PI / 180, el = _elevation * Math.PI / 180;
            var offset = new Vector3D(Math.Cos(el) * Math.Cos(az), Math.Cos(el) * Math.Sin(az), Math.Sin(el)) * _distance;
            _camera.Position = _target + offset;
            _camera.LookDirection = -offset;
            UpdateLabels();
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ResetView();
                return;
            }
            _dragging = true;
            _lastMouse = e.GetPosition(_root);
            _root.CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                return;
            }
            Point p = e.GetPosition(_root);
            _azimuth -= (p.X - _lastMouse.X) * 0.4;
            _elevation = Math.Max(-85, Math.Min(85, _elevation + (p.Y - _lastMouse.Y) * 0.4));
            _lastMouse = p;
            ApplyCamera();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _distance = Math.Max(120, Math.Min(2000, _distance * (e.Delta > 0 ? 0.9 : 1.1)));
            ApplyCamera();
        }

        #endregion

        #region 메시 도우미

        private static Material Material(Color color)
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)), 20));
            return group;
        }

        private static void AddQuad(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d)
        {
            int i = mesh.Positions.Count;
            mesh.Positions.Add(a); mesh.Positions.Add(b); mesh.Positions.Add(c); mesh.Positions.Add(d);
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
            mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
        }

        /// <summary>from → to 원기둥 (양 끝 막음).</summary>
        private static MeshGeometry3D Cylinder(Point3D from, Point3D to, double radius, int segments)
        {
            var mesh = new MeshGeometry3D();
            Vector3D axis = to - from;
            Vector3D u = Vector3D.CrossProduct(axis, Math.Abs(axis.Z) < 0.9 * axis.Length ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0));
            u.Normalize();
            Vector3D v = Vector3D.CrossProduct(axis, u);
            v.Normalize();
            int c0 = mesh.Positions.Count; mesh.Positions.Add(from);
            int c1 = mesh.Positions.Count; mesh.Positions.Add(to);
            for (int s = 0; s < segments; s++)
            {
                double a0 = 2 * Math.PI * s / segments, a1 = 2 * Math.PI * (s + 1) / segments;
                Vector3D d0 = (u * Math.Cos(a0) + v * Math.Sin(a0)) * radius;
                Vector3D d1 = (u * Math.Cos(a1) + v * Math.Sin(a1)) * radius;
                int i = mesh.Positions.Count;
                mesh.Positions.Add(from + d0); mesh.Positions.Add(from + d1); mesh.Positions.Add(to + d1); mesh.Positions.Add(to + d0);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
                mesh.TriangleIndices.Add(c0); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i);
                mesh.TriangleIndices.Add(c1); mesh.TriangleIndices.Add(i + 3); mesh.TriangleIndices.Add(i + 2);
            }
            return mesh;
        }

        private static MeshGeometry3D Cone(Point3D baseCenter, Point3D tip, double radius, int segments)
        {
            var mesh = new MeshGeometry3D();
            Vector3D axis = tip - baseCenter;
            Vector3D u = Vector3D.CrossProduct(axis, Math.Abs(axis.Z) < 0.9 * axis.Length ? new Vector3D(0, 0, 1) : new Vector3D(1, 0, 0));
            u.Normalize();
            Vector3D v = Vector3D.CrossProduct(axis, u);
            v.Normalize();
            int c = mesh.Positions.Count; mesh.Positions.Add(baseCenter);
            int t = mesh.Positions.Count; mesh.Positions.Add(tip);
            for (int s = 0; s < segments; s++)
            {
                double a0 = 2 * Math.PI * s / segments, a1 = 2 * Math.PI * (s + 1) / segments;
                int i = mesh.Positions.Count;
                mesh.Positions.Add(baseCenter + (u * Math.Cos(a0) + v * Math.Sin(a0)) * radius);
                mesh.Positions.Add(baseCenter + (u * Math.Cos(a1) + v * Math.Sin(a1)) * radius);
                mesh.TriangleIndices.Add(t); mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1);
                mesh.TriangleIndices.Add(c); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i);
            }
            return mesh;
        }

        private static MeshGeometry3D Sphere(Point3D center, double radius, int segments)
        {
            var mesh = new MeshGeometry3D();
            for (int i = 0; i <= segments; i++)
            {
                double phi = Math.PI * i / segments;
                for (int j = 0; j <= segments; j++)
                {
                    double theta = 2 * Math.PI * j / segments;
                    mesh.Positions.Add(center + new Vector3D(Math.Sin(phi) * Math.Cos(theta), Math.Sin(phi) * Math.Sin(theta), Math.Cos(phi)) * radius);
                }
            }
            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * (segments + 1) + j, b = a + segments + 1;
                    mesh.TriangleIndices.Add(a); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(a + 1);
                    mesh.TriangleIndices.Add(a + 1); mesh.TriangleIndices.Add(b); mesh.TriangleIndices.Add(b + 1);
                }
            }
            return mesh;
        }

        private static Model3D Arrow(Vector3D dir, double length, Color color)
        {
            var group = new Model3DGroup();
            var origin = new Point3D(0, 0, 0);
            Point3D shaftEnd = origin + dir * (length * 0.8);
            group.Children.Add(new GeometryModel3D(Cylinder(origin, shaftEnd, 1.3, 12), Material(color)));
            group.Children.Add(new GeometryModel3D(Cone(shaftEnd, origin + dir * length, 4, 16), Material(color)));
            return group;
        }

        #endregion
    }
}
