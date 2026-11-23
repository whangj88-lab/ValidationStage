using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ValidationStage.Devices.Messages;

// 고객사에 알리지 않는 기능이라 API 설명 파일(xml)에 나오지 않게 /// 대신 // 로 쓰고, 자동 완성에서도 숨긴다 (EditorBrowsable, 2026-11-23 사용자 결정).
#pragma warning disable 1591   // public 설명(///) 없음 경고

namespace ValidationStage.Devices
{
    // 프로브 기울기 측정(ValidationSystem.MeasureProbeTiltAsync)의 진행 단계.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum ProbeTiltPhase
    {
        // 실행 중 아님
        None = 0,
        // 헥사포드/스테이지를 0(측정 원점)으로 이동
        Preparing = 1,
        // 예비 스캔 (첫 회는 값이 튈 수 있어 계산에 쓰지 않음)
        WarmUp = 2,
        // 계수 측정 스캔
        Measuring = 3,
        // 새 계수 검증 스캔
        Verifying = 4,
        // 0(측정 원점)으로 복귀, 파일 저장
        Returning = 5
    }

    // 프로브 기울기 측정 진행 상태 (어느 순간의 복사본).
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProbeTiltProgress
    {
        // 단계
        public ProbeTiltPhase Phase { get; }
        // 그 단계의 몇 번째 스캔인지 (1부터, 스캔 단계가 아니면 0)
        public int Cycle { get; }
        // 그 단계의 전체 스캔 횟수
        public int CycleCount { get; }
        // 움직이는 축 (X 또는 Y, 스캔 단계에서만 의미 있음)
        public Axis Axis { get; }
        // 지금까지 측정한 점 수 (전체 단계 합)
        public int DonePoints { get; }
        // 측정할 전체 점 수
        public int TotalPoints { get; }

        internal ProbeTiltProgress(ProbeTiltPhase phase, int cycle, int cycleCount, Axis axis, int donePoints)
        {
            Phase = phase;
            Cycle = cycle;
            CycleCount = cycleCount;
            Axis = axis;
            DonePoints = donePoints;
            TotalPoints = ProbeTiltMeasurement.TotalPoints;
        }
    }

    // 프로브 기울기 보정 판정. 검증 스캔에서 새 계수로 보정한 뒤 남은 기울기로 정한다.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public enum ProbeTiltVerdict
    {
        // 양호 (남은 기울기 0.0001 이하 = 3200 µm 이동에 0.32 µm 이하)
        Good = 0,
        // 주의 (0.0002 이하 = 0.64 µm 이하) - 쓸 수 있지만 재측정 권장
        Caution = 1,
        // 불량 (그보다 큼)
        Bad = 2
    }

    // 보정 계수 하나의 측정/검증 결과. 변화량은 이동축이 3200 µm (±1600) 움직일 때 항목 값이 변하는 양 (µm, 직선 기울기 × 3200).
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProbeTiltItem
    {
        // 계수 이름 (예: "XtoTX" = X 이동이 TX 에 주는 영향)
        public string Name { get; internal set; }
        // 이동축 (X 또는 Y)
        public Axis MoveAxis { get; internal set; }
        // 영향을 받는 프로브 라벨
        public string Label { get; internal set; }
        // 현재 적용 중인 계수
        public double OldValue { get; internal set; }
        // 새로 구한 계수 (측정 회차 평균)
        public double NewValue { get; internal set; }
        // 측정 회차별 계수
        public double[] CycleValues { get; internal set; }
        // 회차별 계수의 최대 − 최소. 크면 프로브 고정/접촉 상태를 확인한다.
        public double Spread => CycleValues.Max() - CycleValues.Min();
        // 직선에서 벗어난 정도 (µm RMS, 측정 회차 평균). 크면 글라스 면/이물을 확인한다.
        public double Rms { get; internal set; }
        // 검증 스캔: 보정 전 변화량 (µm)
        public double ChangeBefore { get; internal set; }
        // 검증 스캔: 현재 적용 중인 계수로 보정한 변화량 (µm)
        public double ChangeOld { get; internal set; }
        // 검증 스캔: 새 계수로 보정한 변화량 (µm). 0 에 가까울수록 좋다.
        public double ChangeNew { get; internal set; }
        // 판정 (ChangeNew 기준)
        public ProbeTiltVerdict Verdict { get; internal set; }
    }

    // 프로브 기울기 측정 결과. 적용은 ValidationSystem.ApplyProbeTilt.
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ProbeTiltResult
    {
        // 계수별 결과 (8개)
        public List<ProbeTiltItem> Items { get; } = new List<ProbeTiltItem>();
        // 측정 조건 경고 (없으면 빈 목록). 예: 스캔 중 쉬는 축의 프로브 값이 0 에서 10 µm 넘게 벗어남.
        public List<string> Warnings { get; } = new List<string>();
        // 전체 판정 (가장 나쁜 항목, 경고가 있으면 최소 Caution)
        public ProbeTiltVerdict Verdict
        {
            get
            {
                ProbeTiltVerdict worst = Items.Max(item => item.Verdict);
                return Warnings.Count > 0 && worst == ProbeTiltVerdict.Good ? ProbeTiltVerdict.Caution : worst;
            }
        }
        // 저장한 계수 파일 (ProbeTiltCorrection_yyMMdd_HHmmss.xml)
        public string FilePath { get; internal set; }
        // 저장한 측정 원본 (…_Raw.csv)
        public string RawFilePath { get; internal set; }
        // 저장한 결과 표 (…_Result.csv)
        public string ResultFilePath { get; internal set; }

        internal ProbeTiltCorrection Correction { get; set; }
    }

    // 프로브 기울기 측정 순서와 계산. 장비 동작은 생성자로 받은 함수로만 한다.
    // 순서: X 를 예비 1회(버림) → 측정 3회(계수 평균) → 검증 1회 한 뒤, Y 를 똑같이 한다. 스캔 1회 = ±1600 µm (100 µm 간격, 33점, 항상 − → +).
    // 계수 = 이동축 프로브 값에 대한 각 프로브 값의 직선 기울기 (보정식 부호 기준, ProbeTiltCorrection.Signs).
    internal class ProbeTiltMeasurement
    {
        public const double Stroke = 1600;      // µm
        public const double Step = 100;         // µm
        public const double Backlash = 150;     // 백래시 제거용으로 시작점보다 더 나가는 거리 (µm)
        public const int WarmUpCycles = 1;
        public const int MeasureCycles = 3;
        public const int VerifyCycles = 1;
        public const int SettleMs = 500;        // 점마다 정지 후 대기
        public const int Samples = 5;           // 점마다 읽어 평균하는 횟수
        public const int SampleIntervalMs = 50;

        public const double GoodSlope = 0.0001;
        public const double CautionSlope = 0.0002;

        public const int PointsPerScan = 33;
        public const int TotalPoints = (WarmUpCycles + MeasureCycles + VerifyCycles) * 2 * PointsPerScan;

        // 쉬는 축의 프로브 값이 0 에서 이보다 (µm) 벗어나면 경고한다.
        private const double OffAxisLimit = 10;

        // 이동축 프로브 값이 스테이지 이동량과 이 비율 이상 다르면 라벨/접촉이 잘못된 것으로 보고 중단한다.
        private const double ScaleTolerance = 0.1;

        // 측정점 하나 (값은 영점 적용, 보정 전, µm, 라벨 순서 = ProbeTiltCorrection.Labels).
        public class Point
        {
            public ProbeTiltPhase Phase;
            public int Cycle;
            public Axis Axis;
            public double Target;
            public double Seconds;      // 측정 시작부터 지난 시간
            public double[] Values;
        }

        private readonly Func<Axis, double, Task<bool>> _move;      // 절대 이동 (µm), 실패/중단이면 false
        private readonly Func<double[]> _read;                      // 7개 프로브 값, 실패면 null
        private readonly Func<int, Task<bool>> _delay;              // 대기 (ms), 중단 요청이면 false
        private readonly Action<ProbeTiltProgress> _report;
        private readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();
        private int _donePoints;

        public ProbeTiltMeasurement(Func<Axis, double, Task<bool>> move, Func<double[]> read, Func<int, Task<bool>> delay, Action<ProbeTiltProgress> report)
        {
            _move = move;
            _read = read;
            _delay = delay;
            _report = report;
        }

        public List<Point> Points { get; } = new List<Point>();

        // 스캔을 모두 하고 결과를 계산한다. 동작 실패/중단이면 null (원인은 받은 함수 쪽에서 기록), 값이 이상하면 DeviceException.
        public async Task<ProbeTiltResult> RunAsync(ProbeTiltCorrection current)
        {
            // 쉬는 축이 움직인 직후에는 멈춘 뒤에도 밀려서 기울기에 섞인다 (2026-11-23 실측: X/Y 를 번갈아 스캔하니 XtoY 가 2~3배로 나옴).
            // 그래서 한 축의 스캔을 모두 끝낸 뒤 다른 축으로 넘어가고, 쉬는 축은 스캔 점과 같은 − 쪽에서 0 에 세워 둔다.
            if (!await ParkAsync(Axis.X) || !await ParkAsync(Axis.Y))
            {
                return null;
            }
            foreach (Axis axis in new[] { Axis.X, Axis.Y })
            {
                foreach (var step in new[]
                {
                    new { Phase = ProbeTiltPhase.WarmUp, Count = WarmUpCycles },
                    new { Phase = ProbeTiltPhase.Measuring, Count = MeasureCycles },
                    new { Phase = ProbeTiltPhase.Verifying, Count = VerifyCycles }
                })
                {
                    for (int cycle = 1; cycle <= step.Count; cycle++)
                    {
                        if (!await ScanAsync(step.Phase, cycle, step.Count, axis))
                        {
                            return null;
                        }
                    }
                }
                // Y 는 결과를 저장한 뒤 호출하는 쪽에서 세운다 (복귀에 실패해도 결과를 잃지 않게).
                if (axis == Axis.X && !await ParkAsync(axis))
                {
                    return null;
                }
            }
            return Compute(Points, current);
        }

        // 축을 − 쪽에서 0 으로 보낸다 (백래시: + 쪽에서 오면 실측 X 15, Y 30 µm 안팎 어긋남).
        public async Task<bool> ParkAsync(Axis axis)
        {
            return await _move(axis, -Backlash) && await _move(axis, 0);
        }

        private async Task<bool> ScanAsync(ProbeTiltPhase phase, int cycle, int cycleCount, Axis axis)
        {
            _report(new ProbeTiltProgress(phase, cycle, cycleCount, axis, _donePoints));
            if (!await _move(axis, -Stroke - Backlash))
            {
                return false;
            }
            var scan = new List<Point>();
            for (int i = 0; i < PointsPerScan; i++)
            {
                double target = -Stroke + Step * i;
                if (!await _move(axis, target) || !await _delay(SettleMs))
                {
                    return false;
                }
                double[] values = await ReadAverageAsync();
                if (values == null)
                {
                    return false;
                }
                scan.Add(new Point { Phase = phase, Cycle = cycle, Axis = axis, Target = target, Seconds = _watch.Elapsed.TotalSeconds, Values = values });
                _donePoints++;
                _report(new ProbeTiltProgress(phase, cycle, cycleCount, axis, _donePoints));
            }
            Points.AddRange(scan);
            CheckScale(axis, scan);
            return true;
        }

        private async Task<double[]> ReadAverageAsync()
        {
            var sum = new double[ProbeTiltCorrection.Labels.Length];
            for (int n = 0; n < Samples; n++)
            {
                if (n > 0 && !await _delay(SampleIntervalMs))
                {
                    return null;
                }
                double[] values = _read();
                if (values == null)
                {
                    return null;
                }
                for (int i = 0; i < sum.Length; i++)
                {
                    sum[i] += values[i];
                }
            }
            return sum.Select(v => v / Samples).ToArray();
        }

        // 움직인 축의 프로브가 이동량만큼 변했는지 확인한다 (라벨 뒤바뀜, 접촉 안 됨, 측정 범위 초과를 걸러낸다).
        private static void CheckScale(Axis axis, List<Point> scan)
        {
            string label = axis == Axis.X ? "X" : "Y";
            int index = Array.IndexOf(ProbeTiltCorrection.Labels, label);
            LineFit fit = LineFit.Of(scan.Select(p => p.Target), scan.Select(p => p.Values[index]));
            if (Math.Abs(Math.Abs(fit.Slope) - 1) > ScaleTolerance)
            {
                string ratio = (fit.Slope * 100).ToString("0.0", CultureInfo.InvariantCulture);
                throw new DeviceException(T($"{label} 프로브 값이 스테이지 {label} 이동량과 맞지 않습니다 (이동량의 {ratio}%). 프로브 라벨/접촉/측정 범위를 확인하십시오",
                    $"Probe {label} does not follow stage {label} ({ratio}% of the movement). Check probe labels, contact and range"));
            }
        }

        #region 계산 / 저장

        // 측정점에서 계수와 검증 결과를 계산한다.
        public static ProbeTiltResult Compute(List<Point> points, ProbeTiltCorrection current)
        {
            int count = ProbeTiltCorrection.CoefficientNames.Length;
            var cycleValues = new double[count][];
            var rms = new double[count];
            var coefficients = new double[count];
            for (int i = 0; i < count; i++)
            {
                LineFit[] fits = Enumerable.Range(1, MeasureCycles)
                    .Select(cycle => FitCoefficient(points, cycle, i))
                    .ToArray();
                cycleValues[i] = fits.Select(fit => fit.Slope).ToArray();
                rms[i] = fits.Average(fit => fit.Rms);
                coefficients[i] = cycleValues[i].Average();
            }

            var correction = new ProbeTiltCorrection(coefficients);
            var result = new ProbeTiltResult { Correction = correction };
            for (int i = 0; i < count; i++)
            {
                double residual = VerifySlope(points, i, correction);
                result.Items.Add(new ProbeTiltItem
                {
                    Name = ProbeTiltCorrection.CoefficientNames[i],
                    MoveAxis = MoveAxis(i),
                    Label = ProbeTiltCorrection.Labels[ProbeTiltCorrection.TargetIndexes[i]],
                    OldValue = current.Coefficients[i],
                    NewValue = coefficients[i],
                    CycleValues = cycleValues[i],
                    Rms = rms[i],
                    ChangeBefore = VerifySlope(points, i, null) * 2 * Stroke,
                    ChangeOld = VerifySlope(points, i, current) * 2 * Stroke,
                    ChangeNew = residual * 2 * Stroke,
                    Verdict = Math.Abs(residual) <= GoodSlope ? ProbeTiltVerdict.Good
                        : Math.Abs(residual) <= CautionSlope ? ProbeTiltVerdict.Caution : ProbeTiltVerdict.Bad
                });
            }
            AddOffAxisWarning(result, points, Axis.X, 1);
            AddOffAxisWarning(result, points, Axis.Y, 0);
            return result;
        }

        // 스캔하는 동안 쉬는 축(X 스캔이면 Y)의 프로브 값이 0 에서 크게 벗어났으면 경고한다.
        private static void AddOffAxisWarning(ProbeTiltResult result, List<Point> points, Axis scanAxis, int offIndex)
        {
            double[] values = points.Where(p => p.Axis == scanAxis && p.Phase != ProbeTiltPhase.WarmUp).Select(p => p.Values[offIndex]).ToArray();
            double worst = values.Aggregate(0.0, (max, v) => Math.Abs(v) > Math.Abs(max) ? v : max);
            if (Math.Abs(worst) <= OffAxisLimit)
            {
                return;
            }
            string off = ProbeTiltCorrection.Labels[offIndex];
            string value = Number(worst, "0.0");
            result.Warnings.Add(T($"{scanAxis} 스캔 중 멈춰 있어야 할 {off} 축의 프로브 값이 0 에서 벗어났습니다 (최대 {value} µm) - {scanAxis} 이동 계수({scanAxis}to{off} 등)를 믿기 어렵습니다",
                $"Probe {off} (axis at rest) was off zero during the {scanAxis} scans (max {value} um) - {scanAxis} coefficients (e.g. {scanAxis}to{off}) are unreliable"));
        }

        private static Axis MoveAxis(int coefficient)
        {
            return ProbeTiltCorrection.SourceIndexes[coefficient] == 0 ? Axis.X : Axis.Y;
        }

        // 측정 회차 하나의 계수 (보정식 부호로 맞춘 값의 기울기)
        private static LineFit FitCoefficient(List<Point> points, int cycle, int coefficient)
        {
            int source = ProbeTiltCorrection.SourceIndexes[coefficient];
            int target = ProbeTiltCorrection.TargetIndexes[coefficient];
            Axis axis = MoveAxis(coefficient);
            Point[] scan = points.Where(p => p.Phase == ProbeTiltPhase.Measuring && p.Cycle == cycle && p.Axis == axis).ToArray();
            return LineFit.Of(scan.Select(p => ProbeTiltCorrection.Signs[source] * p.Values[source]),
                scan.Select(p => ProbeTiltCorrection.Signs[target] * p.Values[target]));
        }

        // 검증 스캔에서 남은 기울기 (화면에 보이는 부호 기준). correction 이 null 이면 보정 전 값.
        private static double VerifySlope(List<Point> points, int coefficient, ProbeTiltCorrection correction)
        {
            int source = ProbeTiltCorrection.SourceIndexes[coefficient];
            int target = ProbeTiltCorrection.TargetIndexes[coefficient];
            Axis axis = MoveAxis(coefficient);
            Point[] scan = points.Where(p => p.Phase == ProbeTiltPhase.Verifying && p.Axis == axis).ToArray();
            return LineFit.Of(scan.Select(p => p.Values[source]),
                scan.Select(p => (correction == null ? p.Values : correction.Apply(p.Values))[target])).Slope;
        }

        // 계수 xml, 측정 원본 csv, 결과 표 csv 를 folder 에 저장하고 result 에 경로를 남긴다. 이름에 시각이 들어가 기존 파일을 덮어쓰지 않는다.
        public static void Save(ProbeTiltResult result, List<Point> points, string folder, DateTime time)
        {
            Directory.CreateDirectory(folder);
            string name = "ProbeTiltCorrection_" + time.ToString("yyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string basePath = Path.Combine(folder, name);
            for (int n = 2; File.Exists(basePath + ".xml"); n++)
            {
                basePath = Path.Combine(folder, name + "_" + n);
            }

            result.FilePath = basePath + ".xml";
            result.RawFilePath = basePath + "_Raw.csv";
            result.ResultFilePath = basePath + "_Result.csv";
            result.Correction.Save(result.FilePath);
            File.WriteAllLines(result.RawFilePath, RawLines(result, points), new UTF8Encoding(true));
            File.WriteAllLines(result.ResultFilePath, ResultLines(result), new UTF8Encoding(true));
        }

        private static IEnumerable<string> RawLines(ProbeTiltResult result, List<Point> points)
        {
            // 값: 영점 적용 (µm). 앞 7개 = 보정 전, 뒤 7개 = 새 계수로 보정한 값.
            yield return "Phase,Cycle,Axis,Target,Time(s)," + string.Join(",", ProbeTiltCorrection.Labels) + ","
                + string.Join(",", ProbeTiltCorrection.Labels.Select(label => label + "_Corrected"));
            foreach (Point p in points)
            {
                yield return string.Join(",", new[] { p.Phase.ToString(), p.Cycle.ToString(), p.Axis.ToString(), Number(p.Target, "0.###"), Number(p.Seconds, "0.0") }
                    .Concat(p.Values.Concat(result.Correction.Apply(p.Values)).Select(v => Number(v, "0.00000"))));
            }
        }

        private static IEnumerable<string> ResultLines(ProbeTiltResult result)
        {
            yield return "Coefficient,MoveAxis,Probe,Old,New," + string.Join(",", Enumerable.Range(1, MeasureCycles).Select(n => "Cycle" + n))
                + ",Spread,RMS(um),ChangeBefore(um/3200),ChangeOld(um/3200),ChangeNew(um/3200),Verdict";
            foreach (ProbeTiltItem item in result.Items)
            {
                yield return string.Join(",", new[] { item.Name, item.MoveAxis.ToString(), item.Label }
                    .Concat(new[] { item.OldValue, item.NewValue }.Concat(item.CycleValues).Concat(new[] { item.Spread }).Select(v => Number(v, "0.000000000")))
                    .Concat(new[] { item.Rms, item.ChangeBefore, item.ChangeOld, item.ChangeNew }.Select(v => Number(v, "0.000")))
                    .Concat(new[] { item.Verdict.ToString() }));
            }
            foreach (string warning in result.Warnings)
            {
                yield return "Warning,\"" + warning + "\"";
            }
        }

        private static string Number(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        // 최소제곱 직선.
        public class LineFit
        {
            public double Slope;
            public double Rms;

            public static LineFit Of(IEnumerable<double> xs, IEnumerable<double> ys)
            {
                double[] x = xs.ToArray();
                double[] y = ys.ToArray();
                double meanX = x.Average();
                double meanY = y.Average();
                double sxx = x.Sum(v => (v - meanX) * (v - meanX));
                double sxy = x.Select((v, i) => (v - meanX) * (y[i] - meanY)).Sum();
                if (sxx <= 0)
                {
                    throw new DeviceException(T("이동축 프로브 값이 변하지 않았습니다", "The probe of the moving axis did not change"));
                }
                double slope = sxy / sxx;
                double squares = x.Select((v, i) => y[i] - meanY - slope * (v - meanX)).Sum(e => e * e);
                return new LineFit { Slope = slope, Rms = Math.Sqrt(squares / x.Length) };
            }
        }

        #endregion
    }
}
