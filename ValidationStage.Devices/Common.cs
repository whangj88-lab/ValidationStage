using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ValidationStage.Devices
{
    /// <summary>
    /// 축. X, Y, Z 는 Motorized Stage(MMT), TX, TY, TZ 는 Hexapod(PI) 가 담당한다.
    /// 단위: X, Y, Z = µm / TX, TY, TZ = arcmin (헥사포드 컨트롤러의 deg × −60, 부호가 반대임에 주의).
    /// </summary>
    public enum Axis
    {
        /// <summary>스테이지 X (µm)</summary>
        X,
        /// <summary>스테이지 Y (µm)</summary>
        Y,
        /// <summary>스테이지 Z (µm)</summary>
        Z,
        /// <summary>헥사포드 X축 회전 (arcmin)</summary>
        TX,
        /// <summary>헥사포드 Y축 회전 (arcmin)</summary>
        TY,
        /// <summary>헥사포드 Z축 회전 (arcmin)</summary>
        TZ
    }

    /// <summary>축 하나의 상태 스냅샷. 장비 내부에서 약 100 ms 마다 갱신되며, 조회 시 복사본을 돌려준다.</summary>
    public class AxisStatus
    {
        /// <summary>축</summary>
        public Axis Axis { get; set; }
        /// <summary>현재 위치 (X,Y,Z: µm / TX,TY,TZ: arcmin)</summary>
        public double Position { get; set; }
        /// <summary>설정된 속도 (스테이지: µm/s, 헥사포드: 컨트롤러 시스템 속도 VLS 값)</summary>
        public double Speed { get; set; }
        /// <summary>알람 (0 = 없음. 스테이지: 1 = 드라이버 에러, 2 = 과열)</summary>
        public int Alarm { get; set; }
        /// <summary>이동 중 여부</summary>
        public bool IsMoving { get; set; }
        /// <summary>+ 방향 리밋 감지</summary>
        public bool IsPosLimit { get; set; }
        /// <summary>− 방향 리밋 감지</summary>
        public bool IsNegLimit { get; set; }

        /// <summary>축을 지정해 만든다.</summary>
        public AxisStatus(Axis axis)
        {
            Axis = axis;
        }

        internal AxisStatus Clone()
        {
            return (AxisStatus)MemberwiseClone();
        }
    }

    /// <summary>속도 단계. 스테이지와 헥사포드 모두 같은 단계를 쓴다.</summary>
    public enum SpeedLevel
    {
        /// <summary>저속</summary>
        Low = 0,
        /// <summary>중속</summary>
        Medium = 1,
        /// <summary>고속</summary>
        High = 2
    }

    /// <summary>
    /// 마지막으로 실패한 명령의 원인. 각 장비의 <c>LastError</c> 로 조회한다.
    /// 명령이 성공해도 지워지지 않으므로, 함수가 false/null 을 돌려줬을 때만 확인한다.
    /// </summary>
    public class DeviceError
    {
        /// <summary>
        /// 컨트롤러 에러 코드. 헥사포드는 PI GCS 에러 코드(예: 5 = 레퍼런스 안 된 축 이동 시도)이고,
        /// 컨트롤러 코드가 없는 오류(미연결, 통신 오류 등)는 0.
        /// </summary>
        public int Code { get; }
        /// <summary>오류 내용 (<see cref="Messages.Language"/> 언어. 컨트롤러가 돌려준 설명은 원문 그대로).</summary>
        public string Message { get; }
        /// <summary>발생 시각</summary>
        public DateTime Time { get; }

        internal DeviceError(int code, string message)
        {
            Code = code;
            Message = message;
            Time = DateTime.Now;
        }

        /// <summary>"[코드] 내용" 형식 문자열</summary>
        public override string ToString()
        {
            return Code != 0 ? $"[{Code}] {Message}" : Message;
        }
    }

    /// <summary>로그 한 줄.</summary>
    public class LogEntry
    {
        /// <summary>발생 시각</summary>
        public DateTime Time { get; }
        /// <summary>출처: "Stage", "Hexapod", "Probe"</summary>
        public string Source { get; }
        /// <summary>내용</summary>
        public string Message { get; }

        internal LogEntry(string source, string message)
        {
            Time = DateTime.Now;
            Source = source;
            Message = message;
        }

        /// <summary>"[출처] 내용" 형식 문자열</summary>
        public override string ToString()
        {
            return $"[{Source}] {Message}";
        }
    }

    /// <summary>DLL 이 내보내는 로그/오류 메시지 언어.</summary>
    public enum Language
    {
        /// <summary>한국어</summary>
        Korean,
        /// <summary>영어</summary>
        English
    }

    /// <summary>메시지 언어 설정. 프로그램 시작 시 한 번 설정한다 (기본: 한국어).</summary>
    public static class Messages
    {
        /// <summary>로그와 <see cref="DeviceError.Message"/> 의 언어. 컨트롤러/SDK 가 직접 돌려준 설명은 원문 그대로 나온다.</summary>
        public static Language Language { get; set; } = Language.Korean;

        internal static string T(string korean, string english)
        {
            return Language == Language.English ? english : korean;
        }
    }

    /// <summary>컨트롤러 에러 코드를 실어 나르는 내부 예외.</summary>
    internal class DeviceException : Exception
    {
        public int Code { get; }

        public DeviceException(string message, int code = 0) : base(message)
        {
            Code = code;
        }
    }

    /// <summary>
    /// 장비별 로그 버퍼 (조회 방식). 꺼내 간 로그는 지워진다. 오래 안 꺼내 가면 오래된 것부터 버린다.
    /// </summary>
    internal class LogBuffer
    {
        private const int MaxEntries = 1000;
        private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();
        private readonly string _source;

        public LogBuffer(string source)
        {
            _source = source;
        }

        public void Add(string message)
        {
            _queue.Enqueue(new LogEntry(_source, message));
            while (_queue.Count > MaxEntries && _queue.TryDequeue(out _))
            {
            }
        }

        public List<LogEntry> Drain()
        {
            var list = new List<LogEntry>();
            while (_queue.TryDequeue(out LogEntry entry))
            {
                list.Add(entry);
            }
            return list;
        }
    }

    /// <summary>LastError + 로그를 함께 기록하는 장비 공통 도우미.</summary>
    internal class DeviceErrors
    {
        private readonly LogBuffer _log;

        public DeviceErrors(LogBuffer log)
        {
            _log = log;
        }

        public DeviceError Last { get; private set; }

        /// <summary>실패를 기록한다 (LastError 갱신 + 로그).</summary>
        public void Fail(string message, int code = 0)
        {
            Last = new DeviceError(code, message);
            _log.Add(message);
        }

        public void Fail(Exception ex)
        {
            Fail(ex.Message, (ex as DeviceException)?.Code ?? 0);
        }
    }

    internal static class AxisStatusExtensions
    {
        public static AxisStatus[] CloneAll(this IEnumerable<AxisStatus> statuses)
        {
            return statuses.Select(status => status.Clone()).ToArray();
        }
    }
}
