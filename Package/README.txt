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
- 프로그램 플랫폼은 반드시 x64 (PI_GCS2_DLL_x64.dll 이 64비트 전용입니다. AnyCPU 의 "32비트 선호"도 끄십시오)
- Motorized Stage: PC 랜 카드를 192.168.0.x 대역으로 설정 (컨트롤러 기본 주소 192.168.0.123:5001)
- Hexapod: 컨트롤러와 같은 네트워크 대역 (주소를 모르면 Hexapod.ScanControllersAsync 로 검색)
- 프로브: USB 컨트롤러(USBIM)를 쓰는 PC 에는 FTDI 드라이버가 필요합니다
  (Solartron "Orbit3 Support Pack for Windows" 설치 시 함께 설치됨)


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

// 연결 - 성공한 주소는 exe 폴더의 StageHost.txt / HexapodHost.txt 에 저장되어 다음에 system.StageHost 등으로 읽힘
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
- Motorized Stage 는 컨트롤러 전원을 켜면 위치가 0 부터 시작합니다. 필요하면 HomeStageAsync(기계 원점 찾기)를 먼저 하십시오.
- 오류/로그 메시지 언어: Messages.Language = Language.Korean / Language.English


6. 예제 프로그램
----------------
Sample\ValidationStage.sln 을 Visual Studio 2019 이상에서 열고 x64 로 빌드합니다.
(Visual Studio 설치 시 ".NET 데스크톱 개발" 워크로드와 .NET Framework 4.8 대상 팩이 필요합니다)
F_Main.cs 가 위 사용법을 모두 보여 줍니다 (연결, 저속 설정, 조그/이동/정지, 원점, 상태 표시, 프로브 읽기).
예제가 쓰는 설정 파일(StageHost.txt, HexapodHost.txt, ProbeLabels.txt)은 exe 폴더에 자동으로 만들어집니다.
