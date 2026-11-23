ValidationStage.Devices SDK  v{VERSION}
==========================================

Validation Stage 장비(Motorized Stage + Hexapod + Solartron 프로브)를 제어하는 .NET DLL 과 사용 예제입니다.

- X, Y, Z   : MMT Motorized Stage (MMDC-ST466, Ethernet)
- TX, TY, TZ: PI Hexapod (C-887 + H-811.I2, TCP/IP)
- 프로브     : Solartron Orbit3 + DP10/DP20 모듈


1. 폴더 구성
------------
Bin\                         업체 프로그램에 넣을 파일 (5개 모두 필요)
  ValidationStage.Devices.dll    장비 제어 DLL
  ValidationStage.Devices.xml    API 설명 (DLL 과 같은 폴더에 두면 Visual Studio 툴팁/IntelliSense 에 표시됨)
  PI_GCS2_DLL_x64.dll            PI Hexapod 드라이버 (64비트 네이티브 DLL)
  OrbitLibrary.dll               Solartron Orbit3 라이브러리
  InTheHand.Net.Personal.dll     OrbitLibrary 가 사용하는 라이브러리
Sample\                      사용 예제 프로그램 소스 (WinForms)
  ValidationStage.sln            Visual Studio 로 열어 바로 빌드/실행할 수 있습니다


2. 사용 조건
------------
- 압축 풀기 전에 zip 파일 오른쪽 클릭 > 속성 > "차단 해제" 체크 > 확인
  (메일/인터넷으로 받은 zip 을 그대로 풀면 Windows 가 파일마다 차단 표시를 붙여,
   예제 빌드 시 F_Main.resx 오류(MSB3821)가 납니다)
- .NET Framework 4.8
- 프로그램 플랫폼은 반드시 x64 (PI_GCS2_DLL_x64.dll 이 64비트 전용입니다)
- Motorized Stage: PC 랜 카드를 192.168.0.x 대역으로 설정 (컨트롤러 기본 주소 192.168.0.123:5001)
- Hexapod: 컨트롤러와 같은 네트워크 대역 (주소를 모르면 Hexapod.ScanControllersAsync 로 검색)


3. 프로젝트에 추가하는 방법
---------------------------
1) Bin 폴더의 파일 5개를 프로그램 폴더(예: 솔루션 아래 Lib\)에 복사합니다.
2) 프로젝트 > 참조 추가 > 찾아보기 로 ValidationStage.Devices.dll 을 추가합니다.
   (OrbitLibrary.dll, InTheHand.Net.Personal.dll 은 빌드 시 자동으로 출력 폴더에 복사됩니다)
3) PI_GCS2_DLL_x64.dll 은 참조할 수 없는 네이티브 DLL 이므로 출력 폴더(exe 옆)에 직접 복사되게 합니다.
   예) 프로젝트에 파일을 추가하고 속성에서 "출력 디렉터리로 복사 = 새 버전이면 복사"
4) 프로젝트 속성 > 빌드 > 플랫폼 대상 = x64


4. 기본 사용법
--------------
using ValidationStage.Devices;

var system = new ValidationSystem();

// 연결 - 성공한 주소는 C:\ValidationStage\DoNotTouch 의 StageHost.txt / HexapodHost.txt 에 저장되어 다음에 system.StageHost 등으로 읽힘
if (!await system.ConnectStageAsync("192.168.0.123"))
    Console.WriteLine(system.LastError);          // 실패 원인 (DeviceError: Code + Message)
system.ConnectHexapod(system.HexapodHost);

// 연결 직후에는 반드시 저속으로 - 컨트롤러에 남아 있던 속도로 움직이지 않게 (DLL 이 자동으로 하지 않음)
system.SetSpeedLevel(SpeedLevel.Low);

// 이동 (X,Y,Z = µm / TX,TY,TZ = arcmin, 컨트롤러 좌표 기준 절대 위치)
await system.MoveAbsAsync(Axis.X, 1000);
await system.MoveAbsAsync(Axis.TX, 5);

// 조그 - 버튼 누름에 JogRun, 뗌에 JogStop
system.JogRun(Axis.Y, true);
system.JogStop(Axis.Y);

// 상태/로그는 이벤트가 없고 조회(폴링) 방식 - 예제는 100 ms 타이머에서 호출
AxisStatus[] statuses = system.GetAxisStatuses();   // 위치, 이동 중, 리밋, 알람
List<LogEntry> logs = system.GetLogs();             // 꺼낸 로그는 지워짐

// 프로브
await system.Probe.ConnectAsync();
await system.Probe.ScanAsync();                      // 모듈 검색 (약 12초)
ProbeReadResult result = system.Probe.ReadAll();     // 값 단위 µm

system.DisconnectAll();


5. 사용 원칙
------------
- 모든 함수는 예외를 던지지 않습니다. 실패하면 false 또는 null 을 돌려주고, 원인은 각 장비의 LastError 에 남습니다.
  (ValidationSystem 을 통해 호출한 경우 system.LastError 에도 남습니다)
- 상태(GetAxisStatuses), 로그(GetLogs), 연결 상태(IsConnected) 는 주기적으로 조회해서 씁니다.
  GetLogs 는 ValidationSystem 것과 장비별(Stage/Hexapod/Probe) 것 중 한쪽만 쓰십시오 (서로 로그를 나눠 가져갑니다).
- 프로브 연속 읽기는 호출하는 쪽에서 반복합니다. ReadAll 결과가 CommError 이거나 채널 중 IsError 가 있으면
  반복을 멈추고, 프로브를 다시 연결(꽂기)한 뒤 다시 시작하십시오 (예제의 [읽기 시작/중지] 참고).
  ReadAll 이 Busy 를 돌려주면 다른 작업이 버스를 쓰는 중이므로 그 주기만 건너뜁니다.
- Hexapod 컨트롤러 전원을 켠 뒤에는 레퍼런스(Hexapod.ReferenceAsync)를 먼저 해야 움직입니다.
  (안 하면 LastError.Code = 5 "unreferenced")
- Hexapod 연결(ConnectHexapod) 직후 좌표계 CSHCS(ZERO 하위, U=180) 와 TILTEDCS(CSHCS 하위, 값 0) 가 없으면 만들고
  TILTEDCS 를 활성화합니다. 이미 있는 좌표계는 그대로 둡니다. 플랫폼은 움직이지 않습니다.
- Hexapod TX, TY, TZ 에는 DLL 소프트웨어 리밋이 있습니다 (단위 arcmin, 좌표계별, 저장한 적 없는 좌표계는 사용 + ±300 arcmin).
  이동(MoveAsync/MoveAbsAsync)은 목표가 활성 좌표계의 리밋을 벗어나면 명령 전체를 보내지 않고 false 를 돌려주며,
  조그(JogRun)는 리밋에서 멈춥니다. 읽기 Hexapod.GetSoftLimit("TILTEDCS"), 설정+저장(HexapodHost.txt)
  system.SetHexapodSoftLimit("TILTEDCS", new HexapodSoftLimit { Min = new[] { -100.0, -100, -100 }, Max = new[] { 100.0, 100, 100 } }).
  DLL 을 거치지 않는 이동(PIMikroMove 등)과 레퍼런스(FRF)는 검사하지 않습니다.
- Hexapod 회전 중심(피벗)은 Hexapod.SetPivot(x, y, z) 로 설정하고 Hexapod.GetPivot() 으로 읽습니다 (mm, TILTEDCS 축 기준, 플랫폼은 움직이지 않음).
  레퍼런스 완료, 정지, TX = TY = TZ = 0, TILTEDCS 활성일 때만 되며 아니면 false 입니다. 다시 바꿀 때까지 컨트롤러에 남습니다.
  바꾼 뒤에는 전체 시스템 초기화를 다시 하십시오.
- Motorized Stage 는 컨트롤러 전원을 켜면 위치가 0 부터 시작합니다. 필요하면 HomeStageAsync(기계 원점 찾기)를 먼저 하십시오.
  찾는 방향은 축별로 지정합니다 (기본 (-)리밋 방향). 예: HomeStageAsync(HomeDirection.Negative, HomeDirection.Negative, HomeDirection.Positive)
  한 축만 찾을 때는 HomeStageAxisAsync(Axis.Z, HomeDirection.Positive)
  찾는 방향은 SetStageHomeDirection(axis, direction) 으로 StageHost.txt 에 저장해 두고 GetStageHomeDirection(axis) 로
  다시 읽을 수 있습니다 (저장만 하며 원점 찾기에 자동으로 쓰이지는 않음 - 읽은 값을 HomeStageAsync 에 넘기십시오).
- 스테이지 축의 모터 회전 방향은 Stage.GetMotorDirection(axis) / Stage.SetMotorDirections(axes, directions) 로
  읽고 바꿉니다 (컨트롤러에 저장됨, 한 축만 바꿀 때는 SetMotorDirection(axis, MotorDirection.Ccw 또는 Cw)).
  리밋 방향(HLS)도 같이 바뀌고, 컨트롤러에 반영되도록 스테이지 연결을 자동으로 끊었다가 다시 연결합니다 (X, Y, Z 모두 정지 상태에서 호출).
  바꾸면 조그/이동/원점 찾기 방향과 위치 방향이 모두 반대가 되므로 기계 원점 찾기를 다시 하고, 저속으로 양쪽 리밋 동작을 먼저 확인하십시오.
  리밋 방향이 모터 방향과 맞는지는 Stage.IsLimitDirectionMatched(axis) 로 확인합니다.
- 설비의 모터 방향은 StageHost.txt 에 MotorDirX=Ccw (REV0) 또는 Cw (REV1) 형식으로 적어 둡니다 (Y, Z 도 같음, 적은 축만 검사).
  system.GetStageMotorDirection(axis) 로 설정값을, system.GetStageMotorDirectionMismatches() 로 컨트롤러 값과 다른 축을 읽습니다.
  다른 축이 있으면 system.HomeStageAsync / HomeStageAxisAsync / HomeAndZeroProbesAsync 는 움직이지 않고 false 를 돌려줍니다
  (조그/이동과 Stage.HomeAsync 직접 호출은 막지 않음). 컨트롤러에 자동으로 쓰지는 않으므로 Stage.SetMotorDirections 로 적용하십시오
  (예제는 [모터 방향] 창의 [적용]). 새 설비/새 컨트롤러는 기존 설비의 StageHost.txt 를 복사한 뒤 한 번 적용하면 됩니다.
- 전체 시스템 초기화 (측정 전 프로브 영점): system.HomeAndZeroProbesAsync(x, y, z 찾는 방향)
  = 헥사포드 TX, TY, TZ -> 0 (활성 좌표계) -> 스테이지 기계 원점 찾기 -> 저장된 측정 원점으로 이동 -> 그 위치를 스테이지 0 으로
  -> 프로브 전체 영점. 끝나면 스테이지 0 / 헥사포드 0 위치에서 프로브가 0 입니다.
  시작 전에 세 장비 연결, 스테이지 모터 방향, 헥사포드 레퍼런스를 확인하고, 아니면 아무것도 움직이지 않습니다.
  측정 원점(스테이지 X, Y, Z 만 - 헥사포드는 항상 TX, TY, TZ = 0)은 처음 한 번 정합니다:
  기계 원점 찾기(HomeStageAsync) -> 조그로 측정 위치에 맞춤 -> system.SaveStageMeasureOriginFromCurrent()
  (기계 원점 기준 좌표를 StageHost.txt 에 저장).
  스테이지 현재 위치를 임의로 0 으로 바꾸는 기능은 없습니다 (운용 중 원점이 바뀌는 실수 방지).
  실행 중 상태: system.IsSystemInitRunning, system.CurrentSystemInitStep (SystemInitStep 1~4 단계).
  실행 중 system.EmergencyStopAllAsync() 또는 StopAllAsync() 를 부르면 그 자리에서 중단하고 다음 단계로 넘어가지 않습니다
  (false 반환, system.SystemInitAborted = true). 실행 중에는 다른 이동 명령을 보내지 마십시오 (샘플은 비상 정지 외 화면을 막음).
- 이동/원점 찾기(MoveAsync, MoveAbsAsync, HomeStageAsync 등)는 기다리는 사이 그 축에 정지/비상 정지 명령이 들어오면
  멈춘 뒤 false 를 돌려줍니다 (LastError = "정지 명령으로 중단"). 원점 찾기가 중단되면 그 축은 다시 원점 찾기를 해야 합니다.
- 프로브 측정값은 ReadAll 결과의 ProbeChannel.CorrectedReading (µm, 영점 + 기울기 보정 적용) 입니다.
  라벨이 X, Y, TZ1, TZ2, TX, TY1, TY2 인 모듈 7개가 모두 있어야 하며, 하나라도 없거나 중복/오류면 모두 null 입니다.
  보정 전 값과 모듈 하나만 읽는 기능은 제공하지 않습니다.
  측정값의 부호는 뒤집지 않습니다 (모두 +). 반대 방향이 필요하면 가져가서 부호를 바꿔 쓰십시오.
  (보정 계수는 X 는 +, 나머지 프로브는 - 부호 기준의 값이며 DLL 안에서 맞춰 계산합니다)
  보정 계수는 C:\ValidationStage\DoNotTouch\ProbeTiltCorrection.xml 에서 읽습니다 (없으면 모두 0 = 보정 없음으로 만들어짐).
- 오류/로그 메시지 언어: Messages.Language = Language.Korean / Language.English


6. 예제 프로그램
----------------
Sample\ValidationStage.sln 을 Visual Studio 2019 이상에서 열고 x64 로 빌드합니다.
(Visual Studio 설치 시 ".NET 데스크톱 개발" 워크로드와 .NET Framework 4.8 대상 팩이 필요합니다)
F_Main.cs 가 위 사용법을 모두 보여 줍니다 (연결, 저속 설정, 조그/이동/정지, 원점, 상태 표시, 프로브 읽기).
예제가 쓰는 설정 파일(StageHost.txt, HexapodHost.txt, ProbeLabels.txt, VisionHost.txt)은 C:\ValidationStage\DoNotTouch
(ValidationSystem.SettingsDirectory) 에 자동으로 만들어집니다.
(StageHost.txt 에는 스테이지 주소, 축별 원점 찾는 방향, 모터 방향, 측정 원점이 "Host=..." / "HomeDirX=Negative" /
 "MotorDirX=Ccw" (직접 적은 경우만) / "MeasureOrigin=x,y,z" 형식으로,
 HexapodHost.txt 에는 헥사포드 주소와 소프트웨어 리밋이 "Host=..." / "SoftLimit.TILTEDCS.TX=-300,300" 형식으로 들어갑니다)
