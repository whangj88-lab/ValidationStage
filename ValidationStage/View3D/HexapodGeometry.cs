using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;

namespace ValidationStage.View3D
{
    /// <summary>
    /// PI 가 설치해 두는 헥사포드 모델 자료(C:\ProgramData\PI\PIHexapodDataFiles)에서 3D 표시용 형상을 읽는다.
    /// - HexdataCollisionData\hexdata_&lt;모델&gt;.dat : 다리 연결점. "Base set" b[1..3,1..6](바닥, z = -h0), "Platform set" a0[1..3,1..6](상판, z = 0).
    ///   단위 mm, 원점 = 상판 연결점 평면의 중심 = 헥사포드 ZERO 좌표계 원점.
    /// - 3D\*\*_CAD.ini 의 [모델] 섹션 : 부품 STL 파일 이름과 offset (PIMikroMove 3D 보기와 같은 자료).
    /// 모델 이름은 컨트롤러 CST? 에서 온다 (예: "H-811.I2").
    /// </summary>
    public class HexapodGeometry
    {
        public const string DataRoot = @"C:\ProgramData\PI\PIHexapodDataFiles";

        public string Model;
        public double H0;                                   // 바닥 연결점 평면 ~ 상판 연결점 평면 높이
        public Vector3D[] BaseJoints = new Vector3D[6];     // 바닥판 쪽 다리 연결점 (고정)
        public Vector3D[] PlatformJoints = new Vector3D[6]; // 상판 쪽 다리 연결점 (상판 좌표)
        public double PlatformOuterRadius = 50;
        public double StrutDiameter = 20;

        public string PlatformStl, BaseplateStl, LowerStrutStl, UpperStrutStl;   // 전체 경로, 없으면 null
        public Vector3D PlatformOffset, BaseplateOffset, LowerStrutOffset, UpperStrutOffset;

        public static HexapodGeometry Load(string model)
        {
            var g = new HexapodGeometry { Model = model };
            g.LoadHexData(Path.Combine(DataRoot, "HexdataCollisionData", $"hexdata_{model}.dat"));
            g.LoadCadIni();
            return g;
        }

        private void LoadHexData(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"헥사포드 형상 파일이 없습니다: {path}");
            }
            // 줄 형식: "<값>  <이름>  <번호>  { 설명 }"
            var values = new Dictionary<string, double>();
            var line = new Regex(@"^\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(\S+)");
            var keyValue = new Regex(@"^\s*([A-Z_]+)\[[^\]]*\]\s*=\s*(-?\d+(?:\.\d+)?)");
            foreach (string text in File.ReadAllLines(path))
            {
                Match m = line.Match(text);
                if (m.Success)
                {
                    values[m.Groups[2].Value] = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    continue;
                }
                m = keyValue.Match(text);   // 예: PLATFORM_OUTER_RADIUS[mm]=50
                if (m.Success)
                {
                    values[m.Groups[1].Value] = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                }
            }

            double Get(string name) => values.TryGetValue(name, out double v)
                ? v : throw new InvalidDataException($"{Path.GetFileName(path)} 에 {name} 값이 없습니다");

            H0 = Get("h0");
            for (int i = 0; i < 6; i++)
            {
                int n = i + 1;
                BaseJoints[i] = new Vector3D(Get($"b[1,{n}]"), Get($"b[2,{n}]"), Get($"b[3,{n}]"));
                PlatformJoints[i] = new Vector3D(Get($"a0[1,{n}]"), Get($"a0[2,{n}]"), Get($"a0[3,{n}]"));
            }
            if (values.TryGetValue("PLATFORM_OUTER_RADIUS", out double r)) PlatformOuterRadius = r;
            if (values.TryGetValue("STRUT_DIAMETER", out double d)) StrutDiameter = d;
        }

        private void LoadCadIni()
        {
            string root = Path.Combine(DataRoot, "3D");
            if (!Directory.Exists(root))
            {
                return;
            }
            foreach (string ini in Directory.GetFiles(root, "*_CAD.ini", SearchOption.AllDirectories))
            {
                Dictionary<string, string> section = ReadIniSection(ini, Model);
                if (section == null)
                {
                    continue;
                }
                string dir = Path.GetDirectoryName(ini);
                PlatformStl = StlPath(dir, section, "Platform");
                BaseplateStl = StlPath(dir, section, "Baseplate");
                LowerStrutStl = StlPath(dir, section, "LowerStrut");
                UpperStrutStl = StlPath(dir, section, "UpperStrut");
                PlatformOffset = Offset(section, "Platform");
                BaseplateOffset = Offset(section, "Baseplate");
                LowerStrutOffset = Offset(section, "LowerStrut");
                UpperStrutOffset = Offset(section, "UpperStrut");
                return;
            }
        }

        private static string StlPath(string dir, Dictionary<string, string> section, string key)
        {
            if (!section.TryGetValue(key, out string file))
            {
                return null;
            }
            string path = Path.Combine(dir, file);
            return File.Exists(path) ? path : null;
        }

        private static Vector3D Offset(Dictionary<string, string> section, string key)
        {
            double Get(string axis) => section.TryGetValue($"{key}Offset_{axis}", out string v)
                && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
            return new Vector3D(Get("X"), Get("Y"), Get("Z"));
        }

        /// <summary>ini 에서 [name] 섹션의 key=value 를 읽는다. 섹션이 없으면 null.</summary>
        private static Dictionary<string, string> ReadIniSection(string path, string name)
        {
            Dictionary<string, string> result = null;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    if (result != null)
                    {
                        break;   // 다음 섹션 시작
                    }
                    if (string.Equals(line.Substring(1, line.Length - 2), name, StringComparison.OrdinalIgnoreCase))
                    {
                        result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    continue;
                }
                if (result == null || line.StartsWith(";") || !line.Contains("="))
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                result[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return result;
        }
    }
}
