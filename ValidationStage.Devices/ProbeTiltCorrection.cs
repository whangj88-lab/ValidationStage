using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using static ValidationStage.Devices.Messages;

namespace ValidationStage.Devices
{
    /// <summary>
    /// 프로브 기울기 보정 (18.CSH030EX_4Line_Calibration 의 GageCounter.ReadPortAllCore 보정식과 같다).
    /// 계수는 ValidationSystem.SettingsDirectory 의 ProbeTiltCorrection.xml (참고 프로젝트와 같은 XmlSerializer 형식)에서 읽으며 공개하지 않는다.
    /// 파일이 없으면 모든 계수 0(보정 없음)으로 만든다.
    /// </summary>
    internal class ProbeTiltCorrection
    {
        /// <summary>입력/출력 순서 = 이 라벨 순서 (참고 프로젝트의 res[0..6]).</summary>
        public static readonly string[] Labels = { "X", "Y", "TZ1", "TZ2", "TX", "TY1", "TY2" };

        // XmlSerializer 가 쓰는 요소 순서 그대로
        private static readonly string[] CoefficientNames = { "XtoTX", "XtoTY1", "XtoTY2", "YtoTX", "YtoTY1", "YtoTY2", "XtoY", "YtoX" };

        private double XtoTX, XtoTY1, XtoTY2, YtoTX, YtoTY1, YtoTY2, XtoY, YtoX;

        /// <summary>파일을 읽는다. 없으면 0 으로 만들고, 읽지 못하거나 NaN/Infinity 가 있으면 0 (보정 없음)으로 쓰고 로그를 남긴다.</summary>
        public static ProbeTiltCorrection Load(string filePath, LogBuffer log)
        {
            var correction = new ProbeTiltCorrection();
            try
            {
                if (!File.Exists(filePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    correction.Save(filePath);
                    return correction;
                }

                XElement root = XDocument.Load(filePath).Root;
                var values = new double[CoefficientNames.Length];
                for (int i = 0; i < CoefficientNames.Length; i++)
                {
                    XElement element = root?.Elements().FirstOrDefault(e => e.Name.LocalName == CoefficientNames[i]);
                    values[i] = element == null ? 0 : XmlConvert.ToDouble(element.Value.Trim());
                    if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    {
                        throw new DeviceException(T("보정값에 NaN 또는 Infinity 가 있습니다", "Correction contains NaN or Infinity"));
                    }
                }
                correction.XtoTX = values[0];
                correction.XtoTY1 = values[1];
                correction.XtoTY2 = values[2];
                correction.YtoTX = values[3];
                correction.YtoTY1 = values[4];
                correction.YtoTY2 = values[5];
                correction.XtoY = values[6];
                correction.YtoX = values[7];
                return correction;
            }
            catch (Exception ex)
            {
                log.Add(T($"프로브 보정값 읽기 실패 ({filePath}) - 보정 없이 동작합니다: {ex.Message}",
                    $"Failed to read probe correction ({filePath}) - running without correction: {ex.Message}"));
                return new ProbeTiltCorrection();
            }
        }

        private void Save(string filePath)
        {
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
            double[] values = { XtoTX, XtoTY1, XtoTY2, YtoTX, YtoTY1, YtoTY2, XtoY, YtoX };
            var root = new XElement("ProbeTiltCorrection",
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", xsd),
                CoefficientNames.Select((name, i) => new XElement(name, XmlConvert.ToString(values[i]))));
            new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(filePath);
        }

        /// <summary>
        /// 보정값을 계산한다. 참고 프로젝트와 달리 프로브 부호를 뒤집지 않는다 (모두 +, 필요하면 쓰는 쪽에서 바꾼다 - 사용자 결정 2026-11-20).
        /// </summary>
        /// <param name="readings">영점 적용 값 (µm, <see cref="Labels"/> 순서)</param>
        /// <returns>보정된 값 (µm, 같은 순서)</returns>
        public double[] Apply(double[] readings)
        {
            double[] res = (double[])readings.Clone();

            double originalProbeX = res[0];
            double originalProbeY = res[1];

            double ltz = Math.Atan((res[3] - res[2]) / 45000);   // rad

            // 절대 0 기준으로 TX, TY1, TY2 기울기 보정
            res[4] = res[4] - XtoTX * (res[0] + Math.Sin(ltz) * 83000) - YtoTX * res[1];
            res[5] = res[5] - XtoTY1 * (res[0] + (1 - Math.Cos(ltz)) * 60000) - YtoTY1 * (res[1] + Math.Sin(ltz) * 60000);
            res[6] = res[6] - XtoTY2 * (res[0] - (1 - Math.Cos(ltz)) * 60000) - YtoTY2 * (res[1] - Math.Sin(ltz) * 60000);

            // XtoY, YtoX 기울기 보정 (보정 전 X, Y 값 기준)
            res[0] -= YtoX * originalProbeY;
            res[1] -= XtoY * originalProbeX;

            // Glass Offset (글라스 두께에 따른 보정)
            double ltx = Math.Atan((res[4] - (res[5] + res[6]) / 2) / 83000);   // rad
            double lty = Math.Atan((res[5] - res[6]) / 120000);   // rad
            double ldt = Math.Sqrt(ltx * ltx + lty * lty);
            double ldComp = -670 * (1 / Math.Cos(ldt) - 1);
            res[4] += ldComp;
            res[5] += ldComp;
            res[6] += ldComp;

            return res;
        }
    }
}
