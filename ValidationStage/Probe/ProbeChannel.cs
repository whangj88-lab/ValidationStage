namespace ValidationStage.Probe
{
    /// <summary>
    /// Orbit3 네트워크에서 발견된 프로브 모듈 하나에 대한 화면 표시용 스냅샷.
    /// </summary>
    public class ProbeChannel
    {
        public int Index;
        public string ModuleId;
        public string Label;
        public double Reading;      // 영점 적용된 값 (µm)
        public double RawReading;   // 영점 적용 전 원시값 (µm)
        public bool IsError;
        public string ErrorMessage;
    }
}
