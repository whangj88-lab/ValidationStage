using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices.View3D
{
    /// <summary>
    /// PI 헥사포드 모델 자료(PIMikroMove 3D 보기와 같은 자료)에서 3D 표시용 형상을 읽는다.
    /// - HexdataCollisionData\hexdata_&lt;모델&gt;.dat : 다리 연결점. "Base set" b[1..3,1..6](바닥, z = -h0), "Platform set" a0[1..3,1..6](상판, z = 0).
    ///   단위 mm, 원점 = 상판 연결점 평면의 중심 = 헥사포드 ZERO 좌표계 원점.
    /// - 3D\*\*_CAD.ini 의 [모델] 섹션 : 부품 STL 파일 이름과 offset.
    /// 자료는 DLL 에 포함된 것(H-811.I2)을 먼저 쓰고, 없는 모델이면 PI 설치 폴더(C:\ProgramData\PI\PIHexapodDataFiles)에서 찾는다.
    /// 모델 이름은 컨트롤러 CST? 에서 온다 (예: "H-811.I2").
    /// </summary>
    internal class HexapodGeometry
    {
        public string Model;
        public string DataSource;                           // 자료를 읽은 곳 (표시용)
        public double H0;                                   // 바닥 연결점 평면 ~ 상판 연결점 평면 높이
        public Vector3D[] BaseJoints = new Vector3D[6];     // 바닥판 쪽 다리 연결점 (고정)
        public Vector3D[] PlatformJoints = new Vector3D[6]; // 상판 쪽 다리 연결점 (상판 좌표)
        public double PlatformOuterRadius = 50;
        public double StrutDiameter = 20;

        public byte[] PlatformStl, BaseplateStl, LowerStrutStl, UpperStrutStl;   // STL 파일 내용, 없으면 null
        public Vector3D PlatformOffset, BaseplateOffset, LowerStrutOffset, UpperStrutOffset;

        private HexapodDataFiles _files;

        public static HexapodGeometry Load(string model)
        {
            string hexdata = $"HexdataCollisionData/hexdata_{model}.dat";
            HexapodDataFiles files = new HexapodDataFiles[] { HexapodDataFiles.Embedded, HexapodDataFiles.Installed }
                .FirstOrDefault(source => source.Exists(hexdata));
            if (files == null)
            {
                throw new FileNotFoundException(T($"헥사포드 형상 파일이 없습니다: {HexapodDataFiles.InstalledRoot}\\{hexdata}",
                    $"Hexapod geometry file not found: {HexapodDataFiles.InstalledRoot}\\{hexdata}"));
            }

            var g = new HexapodGeometry { Model = model, DataSource = files.Name, _files = files };
            g.LoadHexData(hexdata);
            g.LoadCadIni();
            return g;
        }

        private void LoadHexData(string path)
        {
            // 줄 형식: "<값>  <이름>  <번호>  { 설명 }"
            var values = new Dictionary<string, double>();
            var line = new Regex(@"^\s*(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)\s+(\S+)");
            var keyValue = new Regex(@"^\s*([A-Z_]+)\[[^\]]*\]\s*=\s*(-?\d+(?:\.\d+)?)");
            foreach (string text in _files.ReadLines(path))
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
                ? v : throw new InvalidDataException(T($"{Path.GetFileName(path)} 에 {name} 값이 없습니다", $"{name} missing in {Path.GetFileName(path)}"));

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
            foreach (string ini in _files.FindCadInis())
            {
                Dictionary<string, string> section = ReadIniSection(ini, Model);
                if (section == null)
                {
                    continue;
                }
                string dir = ini.Substring(0, ini.LastIndexOf('/') + 1);
                PlatformStl = Stl(dir, section, "Platform");
                BaseplateStl = Stl(dir, section, "Baseplate");
                LowerStrutStl = Stl(dir, section, "LowerStrut");
                UpperStrutStl = Stl(dir, section, "UpperStrut");
                PlatformOffset = Offset(section, "Platform");
                BaseplateOffset = Offset(section, "Baseplate");
                LowerStrutOffset = Offset(section, "LowerStrut");
                UpperStrutOffset = Offset(section, "UpperStrut");
                return;
            }
        }

        private byte[] Stl(string dir, Dictionary<string, string> section, string key)
        {
            if (!section.TryGetValue(key, out string file))
            {
                return null;
            }
            string path = dir + file;
            return _files.Exists(path) ? _files.ReadBytes(path) : null;
        }

        private static Vector3D Offset(Dictionary<string, string> section, string key)
        {
            double Get(string axis) => section.TryGetValue($"{key}Offset_{axis}", out string v)
                && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
            return new Vector3D(Get("X"), Get("Y"), Get("Z"));
        }

        /// <summary>ini 에서 [name] 섹션의 key=value 를 읽는다. 섹션이 없으면 null.</summary>
        private Dictionary<string, string> ReadIniSection(string path, string name)
        {
            Dictionary<string, string> result = null;
            foreach (string raw in _files.ReadLines(path))
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

    /// <summary>
    /// PI 헥사포드 자료 폴더 (PIHexapodDataFiles 구조, 경로는 '/' 구분 상대 경로).
    /// Embedded = DLL 에 포함된 자료 (csproj 의 EmbeddedResource LogicalName "PIHexapodData/..."), Installed = PI 설치 폴더.
    /// </summary>
    internal class HexapodDataFiles
    {
        public const string InstalledRoot = @"C:\ProgramData\PI\PIHexapodDataFiles";
        private const string ResourcePrefix = "PIHexapodData/";

        public static readonly HexapodDataFiles Embedded = new HexapodDataFiles(null);
        public static readonly HexapodDataFiles Installed = new HexapodDataFiles(InstalledRoot);

        private readonly string _root;   // null = DLL 리소스
        private static readonly Assembly ResourceAssembly = typeof(HexapodDataFiles).Assembly;

        private HexapodDataFiles(string root)
        {
            _root = root;
        }

        public string Name => _root == null ? T("DLL 내장 자료", "built-in data") : _root;

        public bool Exists(string path)
        {
            return _root == null
                ? ResourceAssembly.GetManifestResourceInfo(ResourcePrefix + path) != null
                : File.Exists(FullPath(path));
        }

        public byte[] ReadBytes(string path)
        {
            if (_root != null)
            {
                return File.ReadAllBytes(FullPath(path));
            }
            using (Stream stream = ResourceAssembly.GetManifestResourceStream(ResourcePrefix + path))
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        public string[] ReadLines(string path)
        {
            return Encoding.UTF8.GetString(ReadBytes(path)).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        /// <summary>3D 폴더 아래의 모든 *_CAD.ini (상대 경로).</summary>
        public IEnumerable<string> FindCadInis()
        {
            if (_root == null)
            {
                return ResourceAssembly.GetManifestResourceNames()
                    .Where(name => name.StartsWith(ResourcePrefix + "3D/") && name.EndsWith("_CAD.ini"))
                    .Select(name => name.Substring(ResourcePrefix.Length));
            }
            string root3D = Path.Combine(_root, "3D");
            if (!Directory.Exists(root3D))
            {
                return Enumerable.Empty<string>();
            }
            return Directory.GetFiles(root3D, "*_CAD.ini", SearchOption.AllDirectories)
                .Select(full => full.Substring(_root.Length + 1).Replace('\\', '/'));
        }

        private string FullPath(string path)
        {
            return Path.Combine(_root, path.Replace('/', '\\'));
        }
    }
}
