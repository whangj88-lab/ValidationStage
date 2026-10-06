using System;
using System.Globalization;
using System.Text;
using System.Windows.Media.Media3D;

namespace ValidationStage.Devices.View3D
{
    /// <summary>STL(바이너리/ASCII) → WPF MeshGeometry3D. 법선은 비워 두면 WPF 가 계산한다.</summary>
    internal static class StlReader
    {
        public static MeshGeometry3D Load(byte[] bytes)
        {
            var mesh = new MeshGeometry3D();

            // 바이너리 STL 도 헤더가 "solid" 로 시작할 수 있어서 크기로 판별한다: 84 + 50 * 삼각형 수.
            bool binary = bytes.Length >= 84 && 84 + 50L * BitConverter.ToUInt32(bytes, 80) == bytes.Length;
            if (binary)
            {
                uint count = BitConverter.ToUInt32(bytes, 80);
                var positions = new Point3DCollection((int)count * 3);
                var indices = new System.Windows.Media.Int32Collection((int)count * 3);
                for (int t = 0; t < count; t++)
                {
                    int offset = 84 + 50 * t + 12;   // 법선 12 바이트는 건너뛴다
                    for (int v = 0; v < 3; v++)
                    {
                        int o = offset + 12 * v;
                        positions.Add(new Point3D(
                            BitConverter.ToSingle(bytes, o),
                            BitConverter.ToSingle(bytes, o + 4),
                            BitConverter.ToSingle(bytes, o + 8)));
                        indices.Add(positions.Count - 1);
                    }
                }
                mesh.Positions = positions;
                mesh.TriangleIndices = indices;
            }
            else
            {
                var positions = new Point3DCollection();
                var indices = new System.Windows.Media.Int32Collection();
                foreach (string raw in Encoding.ASCII.GetString(bytes).Split('\n'))
                {
                    string line = raw.Trim();
                    if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string[] p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    positions.Add(new Point3D(
                        double.Parse(p[1], CultureInfo.InvariantCulture),
                        double.Parse(p[2], CultureInfo.InvariantCulture),
                        double.Parse(p[3], CultureInfo.InvariantCulture)));
                    indices.Add(positions.Count - 1);
                }
                mesh.Positions = positions;
                mesh.TriangleIndices = indices;
            }
            mesh.Freeze();
            return mesh;
        }
    }
}
