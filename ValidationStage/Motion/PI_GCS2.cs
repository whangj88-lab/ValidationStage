/////////////////////////////////////////////////////////////////////////////
// This is a part of the PI-Software Sources
// (c)2008-2015 Physik Instrumente (PI) GmbH & Co. KG
// All rights reserved.
//

/////////////////////////////////////////////////////////////////////////////
// Program: PI_GCS2_DLL
//
// File: PI_GCS2.cs
/////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;
using System.Text;


namespace PI
{

    /// <summary>
    /// Summary description for PI_G.
    /// </summary>
    public class PI_GCS2
    {

        ////////////////////////////////
        // PI_GCS2 Bits (PI_BIT_XXX). //
        ////////////////////////////////

        /* Curve Controll PI_BIT_WGO_XXX */
        public const uint PI_BIT_WGO_START_DEFAULT              = 0x00000001;
        public const uint PI_BIT_WGO_START_EXTERN_TRIGGER       = 0x00000002;
        public const uint PI_BIT_WGO_WITH_DDL_INITIALISATION    = 0x00000040;
        public const uint PI_BIT_WGO_WITH_DDL                   = 0x00000080;
        public const uint PI_BIT_WGO_START_AT_ENDPOSITION       = 0x00000100;
        public const uint PI_BIT_WGO_SINGLE_RUN_DDL_TEST        = 0x00000200;
        public const uint PI_BIT_WGO_EXTERN_WAVE_GENERATOR      = 0x00000400;
        public const uint PI_BIT_WGO_SAVE_BIT_1                 = 0x00100000;
        public const uint PI_BIT_WGO_SAVE_BIT_2                 = 0x00200000;
        public const uint PI_BIT_WGO_SAVE_BIT_3                 = 0x00400000;

        /* Wave-Trigger PI_BIT_TRG_XXX */
        public const uint PI_BIT_TRG_LINE_1                     = 0x0001;
        public const uint PI_BIT_TRG_LINE_2                     = 0x0002;
        public const uint PI_BIT_TRG_LINE_3                     = 0x0003;
        public const uint PI_BIT_TRG_LINE_4                     = 0x0008;
        public const uint PI_BIT_TRG_ALL_CURVE_POINTS           = 0x0100;

        /* Data Record Configuration PI_DRC_XXX */
        public const uint PI_DRC_DEFAULT                        = 0;
        public const uint PI_DRC_AXIS_TARGET_POS                = 1;
        public const uint PI_DRC_AXIS_ACTUAL_POS                = 2;
        public const uint PI_DRC_AXIS_POS_ERROR                 = 3;
        public const uint PI_DRC_AXIS_DDL_DATA                  = 4;
        public const uint PI_DRC_AXIS_DRIVING_VO                = 5;
        public const uint PI_DRC_PIEZO_MODEL_VOL                = 6;
        public const uint PI_DRC_PIEZO_VOL                      = 7;
        public const uint PI_DRC_SENSOR_POS                     = 8;

        /* P(arameter)I(nfo)F(lag)_M(emory)T(ype)_XX */
        public const uint PI_PIF_MT_RAM                         = 0x00000001;
        public const uint PI_PIF_MT_EPROM                       = 0x00000002;
        public const uint PI_PIF_MT_ALL                         = (PI_PIF_MT_RAM | PI_PIF_MT_EPROM);

        /* P(arameter)I(nfo)F(lag)_D(ata)T(ype)_XX */
        public const uint PI_PIF_DT_INT                         = 1;
        public const uint PI_PIF_DT_FLOAT                       = 2;
        public const uint PI_PIF_DT_CHAR                        = 3;

        public const uint PI_CONFIGURATION_TYPE_ALL      = 0xFFFFFFFF;
        public const uint PI_CONFIGURATION_TYPE_USER     = 0x00000001;
        public const uint PI_CONFIGURATION_TYPE_STANDARD = 0x00000002;
        public const uint PI_CONFIGURATION_TYPE_CUSTOM   = 0x00000004;

//#if X64
        public const string PI_GCS2_DLL_NAME = "PI_GCS2_DLL_x64.dll";
//#else
//        public const string PI_GCS2_DLL_NAME = "PI_GCS2_DLL.dll";
//#endif

        /////////////////////////////////////////////////////////////////////////////
        #region DLL initialization and comm functions
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_InterfaceSetupDlg")]          public static extern int    InterfaceSetupDlg(string sRegKeyName);

        /**
        * Check if thread with given ID is running trying to establish communication
        * \return TRUE if thread is running, FALSE if no thread is running with given ID
        */
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsConnecting")]               public static extern int    IsConnecting(int threadID, ref int bCOnnecting);

        /**
        * Get ID of connected controller for given threadID
        * \return ID of new controller (>=0), error code (<0) if there was an error, no thread running, or thread has not finished yet
        */
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetControllerID")]            public static extern int    GetControllerID(int threadID);

        /**
        * Cancel connecting thread with given ID
        * \return TRUE if thread was cancelled, FALSE if no thread with given ID was running
        */

        // Cancels the connecting thread with given thread ID
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CancelConnect")]              public static extern int    CancelConnect(int threadID);


        #region RS232, DaisyChain, NIgpib, USB
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectRS232")]               public static extern int    ConnectRS232(int nPortNr, int nBaudRate);
        /**
        * Starts background thread which tries to establish connection to controller with given RS-Settings.
        * \param ?
        * \return ID of new thread (>=0), error code (<0) if not
        */
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TryConnectRS232")]            public static extern int    TryConnectRS232(int port, int baudrate);

        /**
        * Starts background thread which tries to establish connection to controller with given USB-Settings.
        * \param ?
        * \return ID of new thread (>=0), error code (<0) if not
        */
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TryConnectUSB")]              public static extern int    TryConnectUSB(string sDescription);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OpenRS232DaisyChain")]        public static extern int    OpenRS232DaisyChain(int iPortNumber, int iBaudRate, ref int pNumberOfConnectedDaisyChainDevices, StringBuilder sDeviceIDNs, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectDaisyChainDevice")]    public static extern int    ConnectDaisyChainDevice(int iPortId, int iDeviceNumber);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CloseDaisyChain")]            public static extern void   CloseDaisyChain(int iPortId);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_StartDaisyChainScanTCPIP")]   public static extern int    StartDaisyChainScanTCPIP( string szHostname, int port);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_StartDaisyChainScanRS232")]   public static extern int    StartDaisyChainScanRS232(int iPortNumber, int iBaudRate);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_StartDaisyChainScanUSB")]     public static extern int    StartDaisyChainScanUSB(string szDescription);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DaisyChainScanning")]         public static extern int    DaisyChainScanning(int threadId, ref int scanning, ref double progressPercentage);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetDaisyChainID")]            public static extern int    GetDaisyChainID(int threadId);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetDevicesInDaisyChain")]     public static extern int    GetDevicesInDaisyChain(int portId, ref int numberOfDevices, StringBuilder buffer, int bufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_StopDaisyChainScan")]         public static extern int    StopDaisyChainScan(int threadId);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetConnectedDaisyChains")]    public static extern int    GetConnectedDaisyChains(int[] daisyChainIds, int nrDaisyChainsIds);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetNrConnectedDaisyChains")]  public static extern int    GetNrConnectedDaisyChains();
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CloseAllDaisyChains")]        public static extern void   CloseAllDaisyChains();
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectNIgpib")]              public static extern int    ConnectNIgpib(int nBoard, int nDevAddr);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OpenTCPIPDaisyChain")]        public static extern int    OpenTCPIPDaisyChain(string sHostname, int port, ref int pNumberOfConnectedDaisyChainDevices, StringBuilder sDeviceIDNs, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EnumerateUSB")]               public static extern int    EnumerateUSB(StringBuilder sBuffer, int iBufferSize, string sFilter);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectUSB")]                 public static extern int    ConnectUSB(string sDescription);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectUSBWithBaudRate")]     public static extern int    ConnectUSBWithBaudRate(string sDescription, int iBaudRate);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OpenUSBDaisyChain")]          public static extern int    OpenUSBDaisyChain(string sDescription, ref int pNumberOfConnectedDaisyChainDevices, StringBuilder sDeviceIDNs, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SetDaisyChainScanMaxDeviceID")]   public static extern int    SetDaisyChainScanMaxDeviceID(int maxID);
        #endregion
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EnableBaudRateScan")]             public static extern void   EnableBaudRateScan(int enableBaudRateScan);


        // TCPIP
        //Connect: the port is always 50000, Returns: Integer value with the ID of the new object. 실패 -1
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectTCPIP")]               public static extern int    ConnectTCPIP(string sHostname, int port);
        // PI_EnumerateTCPIPDevices() 함수가 찾을 수 있는 장치들을 필터링하는 역할 1 = UDP, 2 = XPORT
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EnableTCPIPScan")]            public static extern int    EnableTCPIPScan(int iMask);
        // EnumerateTCPIPDevices: return :  the number of controllers in the list, 에러 < 0
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EnumerateTCPIPDevices")]      public static extern int    EnumerateTCPIPDevices(StringBuilder sBuffer, int iBufferSize, string sFilter);
        // ConnectTCPIPByDescription() : EnumerateTCPIPDevices에서 얻은 Description으로 Connect
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ConnectTCPIPByDescription")]  public static extern int    ConnectTCPIPByDescription(string szDescription);


        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsConnected")]                public static extern int    IsConnected(int ID);
        // Closes the connection to the controller associated with ID
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CloseConnection")]            public static extern void   CloseConnection(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetError")]                   public static extern int    GetError(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetInitError")]               public static extern int    GetInitError(); // 연결 실패 시 오류 코드
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SetErrorCheck")]              public static extern int    SetErrorCheck(int ID, int bErrorCheck);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TranslateError")]             public static extern int    TranslateError(int errNr, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SetTimeout")]                 public static extern int    SetTimeout(int ID, int timeoutInMS);


        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EnableReconnect")]                public static extern int    EnableReconnect(int ID, int bEnable);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SetNrTimeoutsBeforeClose")]       public static extern int    SetNrTimeoutsBeforeClose(int ID, int nrTimeoutsBeforeClose);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetInterfaceDescription")]        public static extern int    GetInterfaceDescription(int ID, StringBuilder sBuffer, int iBufferSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SetConnectTimeout")]              public static extern void   SetConnectTimeout(int timeoutInMS);

        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region general
        // Get Error Numbe
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qERR")]          public static extern int qERR(int ID, ref int pnError);
        // Get Device Identification
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qIDN")]          public static extern int qIDN(int ID, StringBuilder sBuffer, int iBufferSize);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_INI")]           public static extern int INI(int ID, string sAxes);
        
        // 사용할 수 있는 모든 명령어의 목록
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHLP")]          public static extern int qHLP(int ID, StringBuilder sBuffer, int iBufferSize);
        // 사용할 수 있는 모든 파라미터 목록
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHPA")]          public static extern int qHPA(int ID, StringBuilder sBuffer, int iBufferSize);
        // 현재 BIOS에서 접근 가능한 디스크 영역의 정보
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHPV")]          public static extern int qHPV(int ID, StringBuilder sBuffer, int iBufferSize);
        // GCS 펌웨어에서 사용되는 버전
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCSV")]          public static extern int qCSV(int ID, ref double dCommandSyntaxVersion);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOVF")]          public static extern int qOVF(int ID, string sAxes, int[] iValueArray);

        // 시스템을 재부팅
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RBT")]           public static extern int RBT(int ID);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REP")]           public static extern int REP(int ID);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_BDR")]           public static extern int BDR(int ID, int iBaudRate);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qBDR")]          public static extern int qBDR(int ID, ref int iBaudRate);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DBR")]           public static extern int DBR(int ID, int iBaudRate);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDBR")]          public static extern int qDBR(int ID, ref int iBaudRate);

        // C-887 펌웨어와 드라이버의 버전
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVER")]          public static extern int qVER(int ID, StringBuilder sBuffer, int iBufferSize);
        //  C-887 장치의 일련 번호
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSSN")]          public static extern int qSSN(int ID, StringBuilder sSerialNumber, int iBufferSize);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CCT")]           public static extern int CCT(int ID, int iCommandType);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCCT")]          public static extern int qCCT(int ID, ref int iCommandType);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTVI")]          public static extern int qTVI(int ID, StringBuilder sBuffer, int iBufferSize);

        // [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IFC")]           public static extern int IFC(int ID, string sParameters, string sValues);
        
        // 현재 통신 인터페이스 파라미터의 값
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qIFC")]          public static extern int qIFC(int ID, string sParameters, StringBuilder sBuffer, int iBufferSize);
        // 인터페이스 파라미터를 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IFS")]           public static extern int IFS(int ID, string sPassword, string sParameters, string sValues);
        //  현재 설정된 인터페이스 파라미터의 값을 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qIFS")]          public static extern int qIFS(int ID, string sParameters, StringBuilder sBuffer, int iBufferSize);
        // 현재 에코 모드의 상태를 반환
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qECO")]          public static extern int qECO(int ID, string sSendString, StringBuilder sValues, int iBufferSize);
        // 절대 목표 위치를 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MOV")]           public static extern int MOV(int ID, string sAxes, double[] dValueArray);
        // 마지막으로 명령된 목표 위치 반환
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qMOV")]          public static extern int qMOV(int ID, string sAxes, double[] dValueArray);
        // 현재 위치로부터 상대적인 목표 위치
        //Trajectory Source parameter (ID 0x19001900)가 1일 때는 MVR 명령어를 사용할 수 없습니다.(default: 0 연속 Mov: 1)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MVR")]           public static extern int MVR(int ID, string sAxes, double[] dValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MVE")]           public static extern int MVE(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_POS")]           public static extern int POS(int ID, string sAxes, double[] dValueArray);

        // 현재 축 위치를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qPOS")]          public static extern int qPOS(int ID, string sAxes, double[] dValueArray);
        // 모션 상태 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsMoving")]      public static extern int IsMoving(int ID, string sAxes, int[] bValueArray);
        // 지정된 축의 모션을 부드럽게 정지
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HLT")]           public static extern int HLT(int ID, string sAxes);
        // 모든 축을 즉시 정지
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_STP")]           public static extern int STP(int ID);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_STF")]           public static extern int STF(int ID);

        // # 24, 모든 축을 즉시 정지, 에러코드 10
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_StopAll")]       public static extern int StopAll(int ID);

        // 축이 목표 값에 도달했으면 "1", 그렇지 않으면 "0"을 반환
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qONT")]          public static extern int qONT(int ID, string sAxes, int[] bValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RTO")]           public static extern int RTO(int ID, string sAxes);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qRTO")]          public static extern int qRTO(int ID, string sAxes, int[] iValueArray);
        //// Starts an automatic zero-point calibration
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ATZ")]           public static extern int ATZ(int ID, string sAxes, double[] dLowvoltageArray, int[] fUseDefaultArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qATZ")]          public static extern int qATZ(int ID, string sAxes, int[] iAtzResultArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_AOS")]           public static extern int AOS(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qAOS")]          public static extern int qAOS(int ID, string sAxes, double[] dValueArray);

        // #6, 축 위치 바뀜, 반환 값은 The answer <uint> is bit-mapped
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HasPosChanged")] public static extern int HasPosChanged(int ID, string sAxes, int[] bValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetErrorStatus")]    public static extern int GetErrorStatus(int ID, int[] bIsReferencedArray, ref int bIsReferencing, int[] bIsMovingArray, int[] bIsMotionErrorArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SVA")]           public static extern int SVA(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSVA")]          public static extern int qSVA(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SVR")]           public static extern int SVR(int ID, string sAxes, double[] dValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DFH")]           public static extern int DFH(int ID, string sAxes);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDFH")]          public static extern int qDFH(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GOH")]           public static extern int GOH(int ID, string sAxes);

        // A, B축 만 사용 포지셔너 유형 할당
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCST")]          public static extern int qCST(int ID, string sAxes, StringBuilder sNames, int iBufferSize);
        // A, B축 만 사용 포지셔너 유형 할당
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CST")]           public static extern int CST(int ID, string sAxes, string sNames);
        //C-887과 연결 가능한 포지셔너 유형을 나열
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVST")]          public static extern int qVST(int ID, StringBuilder sBuffer, int iBufferSize);
        // 위치 단위 조회 (mm, um)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qPUN")]          public static extern int qPUN(int ID, string sAxes, StringBuilder sUnit, int iBufferSize);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_EAX")]           public static extern int EAX(int ID, string sAxes, int[] iValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qEAX")]          public static extern int qEAX(int ID, string sAxes, int[] iValueArray);

        // 서보 모드 설정 (Openloop, CloseLoop)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SVO")]           public static extern int SVO(int ID, string sAxes, int[] iValueArray);
        //  서보 모드 상태(on/off)를 쿼리
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSVO")]          public static extern int qSVO(int ID, string sAxes, int[] iValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SMO")]           public static extern int SMO( int ID, string sAxes, int[] iValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSMO")]          public static extern int qSMO( int ID, string sAxes, int[] iValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DCO")]           public static extern int DCO(int ID, string sAxes, int[] bValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDCO")]          public static extern int qDCO(int ID, string sAxes, int[] bValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_BRA")]           public static extern int BRA(int ID, string sAxes, int[] iValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qBRA")]          public static extern int qBRA(int ID, string sAxes, int[] iValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RON")]           public static extern int RON(int ID, string sAxes, int[] bValueArray);
        
        // Get Reference Mode
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qRON")]          public static extern int qRON(int ID, string sAxes, int[] bValueArray);
        // 속도 설정    // A,B 축만 가능
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VEL")]           public static extern int VEL(int ID, string sAxes, double[] dValueArray);
        // 현재 속도를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVEL")]          public static extern int qVEL(int ID, string sAxes, double[] dValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_JOG")]           public static extern int JOG(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJOG")]          public static extern int qJOG(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTCV")]          public static extern int qTCV(int ID, string sAxes, double[] dValueArray);

        //  전체 시스템 속도를 설정
        // Trajectory Velocity (Phys. Unit/s) Limited by the parameters Maximum System Velocity (Phys. Unit/s) (ID 0x19001500) and Minimum System Velocity (Phys. Unit/s) (ID 0x19001501).
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VLS")]           public static extern int VLS(int ID, double dSystemVelocity);
        // 전체 시스템 속도를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVLS")]          public static extern int qVLS(int ID, ref double dSystemVelocity);

        // 지정축 가속도 설정. Close loop에서만 설정 적용됨.
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ACC")]           public static extern int ACC(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qACC")]          public static extern int qACC(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DEC")]           public static extern int DEC(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDEC")]          public static extern int qDEC(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VCO")]           public static extern int VCO(int ID, string sAxes, int[] bValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVCO")]          public static extern int qVCO(int ID, string sAxes, int[] bValueArray);

        // 특정 요소의 매개변수를 휘발성 메모리(RAM)에서 설정
        // 감소계수 설정하여 TRA?의 응답 값을 제한  -> 반올림으로 발생하는 문제 해결, -> 실제로 도달 가능한 범위 내 위치만 표시 (0.9)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPA")] public static extern int SPA(int ID, string sAxes, uint[] iParameterArray, double[] dValueArray, string sStrings);
        // 휘발성 메모리(RAM)에서 특정 요소의 매개변수 값을 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPA")]          public static extern int qSPA(int ID, string sAxes, uint[] iParameterArray, double[] dValueArray, StringBuilder sStrings, int iMaxNameSize);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SEP")]           public static extern int SEP(int ID, string sPassword, string sAxes, uint[] iParameterArray, double[] dValueArray, string sStrings);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSEP")]          public static extern int qSEP(int ID, string sAxes, uint[] iParameterArray, double[] dValueArray, StringBuilder sStrings, int iMaxNameSize);

        // 현재 휘발성 메모리에 저장된 설정을 비휘발성 메모리에 저장
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WPA")]           public static extern int WPA(int ID, string sPassword, string sAxes, uint[] iParameterArray);
        // 매개변수 값과 매개변수와 독립적인 설정을 공장 설정으로 재설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DPA")]           public static extern int DPA(int ID, string sPassword, string sAxes, uint[] iParameterArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TIM")]           public static extern int TIM(int ID, double dTimer);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTIM")]          public static extern int qTIM(int ID, ref double dTimer);

        // 휘발성 메모리에 저장된 모든 파라미터를 공장 초기 설정으로 복구
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RPA")]           public static extern int RPA(int ID, string sAxes, uint[] iParameterArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPA_String")]    public static extern int SPA_String(int ID, string sAxes, uint[] iParameterArray, string sStrings);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPA_String")]   public static extern int qSPA_String(int ID, string sAxes, uint[] iParameterArray, StringBuilder sStrings, int iMaxNameSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SEP_String")]    public static extern int SEP_String(int ID, string sPassword, string sAxes, uint[] iParameterArray, string sStrings);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSEP_String")]   public static extern int qSEP_String(int ID, string sAxes, uint[] iParameterArray, StringBuilder sStrings, int iMaxNameSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPA_int64")]	   public static extern int SPA_int64(int ID, string sAxes, uint[] iParameterArray, long[] iValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPA_int64")]    public static extern int qSPA_int64(int ID, string sAxes, uint[] iParameterArray, long[] iValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SEP_int64")]     public static extern int SEP_int64(int ID, string sPassword, string sAxes, uint[] iParameterArray, long[] iValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSEP_int64")]	   public static extern int qSEP_int64(int ID, string sAxes, uint[] iParameterArray, long[] iValueArray);

        // 지정된 축에 대해 스텝 응답을 시작하고 기록
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_STE")]            public static extern int STE(int ID, string sAxes, double[] dOffsetArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSTE")]           public static extern int qSTE(int ID, string sAxes, double[] pdValueArray);

        // 지정된 축에 대해 임펄스를 시작하고 응답을 기록
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IMP")]            public static extern int IMP(int ID, string sAxes, double[] dImpulseSize);
        // ?
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IMP_PulseWidth")] public static extern int IMP_PulseWidth(int ID, char cAxis, double dOffset, int iPulseWidth);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qIMP")]           public static extern int qIMP(int ID, string sAxes, double[] dValueArray);

        // 특정 축의 식별자를 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SAI")]           public static extern int SAI(int ID, string sOldAxes, string sNewAxes);
        // 사용 가능한 축 식별자를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSAI")]          public static extern int qSAI(int ID, StringBuilder sAxes, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSAI_ALL")]      public static extern int qSAI_ALL(int ID, StringBuilder sAxes, int iBufferSize);
        // 실행 중인 매크로를 즉시 중단
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CCL")]           public static extern int CCL(int ID, int iComandLevel, string sPassWord);
        // 매크로 실행 상태를 확인
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCCL")]          public static extern int qCCL(int ID, ref int iComandLevel);
        // 지정된 축의 평균 속도를 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_AVG")]           public static extern int AVG(int ID, int iAverrageTime);
        // 지정된 축의 현재 평균 속도를 
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qAVG")]          public static extern int qAVG(int ID, ref int iAverrageTime);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHAR")]          public static extern int qHAR(int ID, string sAxes, int[] bValueArray);

        // 리미트 스위치가 설치되어 있는지
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qLIM")]          public static extern int qLIM(int ID, string sAxes, int[] bValueArray);
        // 참조 스위치
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTRS")]          public static extern int qTRS(int ID, string sAxes, int[] bValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FNL")]           public static extern int FNL(int ID, string sAxes);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFPH")]          public static extern int qFPH(int ID, string sAxes, double[] dValueArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FPH")]           public static extern int FPH(int ID, string sAxes);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FPL")]           public static extern int FPL(int ID, string sAxes);

        //// Fast Reference Move To Reference Switch
        /// 참조 이동을 수행하여 기준 위치를 설정
        // 증분 센서를 사용하는 경우 레퍼런스 스위치로 이동시켜 초기 위치를 설정. 절대 측정 센서의 경우 이동을 시작하지 않고 대신 현재 위치를 목표 위치로 설정됨.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FRF")]           public static extern int FRF(int ID, string sAxes);
        // 모든 축에 대해 끝단 스위치로 이동하여 각 축의 최대 이동 범위를 확인
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FED")]           public static extern int FED(int ID, string sAxes, int[] iEdgeArray, int[] piParamArray);
        // 특정 축이 참조 이동(referencing move)을 완료했는지 확인
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRF")]          public static extern int qFRF(int ID, string sAxes, int[] bValueArray); // Get Referencing Result
        //  디지털 출력 라인을 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DIO")]           public static extern int DIO(int ID, int[] iChannelsArray, int[] bValueArray, int iArraySize);
        // 디지털 입력 라인의 상태를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDIO")]          public static extern int qDIO(int ID, int[] iChannelsArray, int[] bValueArray, int iArraySize);
        // 설치된 디지털 I/O 라인의 총 수를 반환
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTIO")]          public static extern int qTIO(int ID, ref int iInputNr, ref int iOutputNr);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsControllerReady")] public static extern int IsControllerReady(int ID, ref int iControllerReady);
        // 레지스터의 상태 값을 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSRG")]          public static extern int qSRG(int ID, string sAxes, int[] iRegisterArray, int[] iValArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ATC")]           public static extern int ATC(int ID, int[] iChannels, int[] iValueArray, int iArraySize);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qATC")]          public static extern int qATC(int ID, int[] iChannels, int[] iValueArray, int iArraySize);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qATS")]          public static extern int qATS(int ID, int[] iChannels, int[] iOptions, int[] iValueArray, int iArraySize);

        // 회전 중심(
        //
        //
        //
        //
        // point)을 좌표계의 원점에서 X, Y, Z 방향으로 이동    // 좌표계는 ZERO 또는 KSF 유형이어야함.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPI")]           public static extern int SPI(int ID, string sAxes, double[] dValueArray);
        // 현재 설정된 피벗 포인트 좌표를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPI")]          public static extern int qSPI(int ID, string sAxes, double[] dValueArray);
        // 동적 프로파일을 실행하기 위한 주기
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SCT")]           public static extern int SCT(int ID, double dCycleTime);
        // 현재 설정된 주기 시간을 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSCT")]          public static extern int qSCT(int ID, ref double pdCycleTime);
        // Set Step Size
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SST")]           public static extern int SST(int ID, string sAxes, double[] dValueArray);
        // Get Step Size
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSST")]          public static extern int qSST(int ID, string sAxes, double[] dValueArray);

        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCTV")]          public static extern int qCTV(int ID, string sAxes, double[] dValarray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CTV")]           public static extern int CTV(int ID, string sAxes, double[] dValarray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CTR")]           public static extern int CTR(int ID, string sAxes, double[] dValarray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCAV")]          public static extern int qCAV(int ID, string sAxes, double[] dValarray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCCV")]          public static extern int qCCV(int ID, string sAxes, double[] dValarray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCMO")]          public static extern int qCMO(int ID, string sAxes, int[] iValArray);
        //[DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CMO")]           public static extern int CMO(int ID, string sAxes, int[] iValArray);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region E-754K001 commands
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_STD")]           public static extern int STD(int ID, int tableType, int tableID, string data);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RTD")]           public static extern int RTD(int ID, int tableType, int tableID, string name);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qRTD")]          public static extern int qRTD(int ID, int tableType, int tableID, int infoID, StringBuilder buffer, int bufsize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qLST")]          public static extern int qLST(int ID, StringBuilder buffer, int bufsize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DLT")]           public static extern int DLT(int ID, string name);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Macro commande
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsRunningMacro")] public static extern int IsRunningMacro(int ID, int[] bRunningMacro);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_BEG")]       public static extern int MAC_BEG(int ID, string sMacroName);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_START")]     public static extern int MAC_START(int ID, string sMacroName);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_NSTART")]    public static extern int MAC_NSTART(int ID, string sMacroName, int nrRuns);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_START_Args")]    public static extern int MAC_START_Args(int ID, string sMacroName, string[] sArgs);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_NSTART_Args")]   public static extern int MAC_NSTART_Args(int ID, string sMacroName, int nrRuns, string sArgs);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_END")]       public static extern int MAC_END(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_DEL")]       public static extern int MAC_DEL(int ID, string sMacroName);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_DEF")]       public static extern int MAC_DEF(int ID, string sMacroName);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_qDEF")]      public static extern int MAC_qDEF(int ID, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_qERR")]      public static extern int MAC_qERR(int ID, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MAC_qFREE")]     public static extern int MAC_qFREE(int ID, ref int iFreeSpace);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qMAC")]          public static extern int qMAC(int ID, string sMacroName, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qRMC")]          public static extern int qRMC(int ID, StringBuilder sBuffer, int iBufferSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DEL")]           public static extern int DEL(int ID, int nMilliSeconds);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAC")]           public static extern int WAC(int ID, string sCondition);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MEX")]           public static extern int MEX(int ID, string sCondition);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VAR")]           public static extern int VAR(int ID, string sVariable, string sValue);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVAR")]          public static extern int qVAR(int ID, string sVariables, StringBuilder sValues,  int iBufferSize);
        // sVariable에 value1+value2 저장
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ADD")]           public static extern int ADD(int ID, string sVariable, double value1, double value2);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CPY")]           public static extern int CPY(int ID, string sVariable, string sCommand);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region String commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GcsCommandset")]    public static extern int GcsCommandset(int ID, string sCommand);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GcsGetAnswer")]     public static extern int GcsGetAnswer(int ID, StringBuilder sAnswer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GcsGetAnswerSize")] public static extern int GcsGetAnswerSize(int ID, ref int iAnswerSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region limits.
        // Get Minimum Commandable Position // 제약조건 : 모든 다른 축이 0 위치에 있을 때, 기본 좌표계, 피벗 포인트의 기본 좌표 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTMN")]          public static extern int qTMN(int ID, string sAxes, double[] dValueArray);
        // Get Maximum Commandable Position // 제약조건 : 모든 다른 축이 0 위치에 있을 때, 기본 좌표계, 피벗 포인트의 기본 좌표 설정
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTMX")]          public static extern int qTMX(int ID, string sAxes, double[] dValueArray);
        // Soft Limit
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_NLM")]           public static extern int NLM(int ID, string sAxes, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qNLM")]          public static extern int qNLM(int ID, string sAxes, double[] dValueArray);
        // Soft Limit
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_PLM")]           public static extern int PLM(int ID, string sAxes, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qPLM")]          public static extern int qPLM(int ID, string sAxes, double[] dValueArray);
        // Soft Limit
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SSL")]           public static extern int SSL(int ID, string sAxes, int[] bValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSSL")]          public static extern int qSSL(int ID, string sAxes, int[] bValueArray);
        // 목표 위치 도달 가능성 확인
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVMO")]          public static extern int qVMO(int ID, string sAxes, double[] dValarray, int[] bMovePossible);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCMN")]          public static extern int qCMN(int ID, string sAxes, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCMX")]          public static extern int qCMX(int ID, string sAxes, double[] dValueArray);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Wave commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsGeneratorRunning")] public static extern int IsGeneratorRunning(int ID, int[] iWaveGeneratorIds, int[] bValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTWG")]          public static extern int qTWG(int ID, ref int iWaveGenerators);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_SIN_P")]     public static extern int WAV_SIN_P(int ID, int iWaveTableId, int iOffsetOfFirstPointInWaveTable, int iNumberOfPoints, int iAddAppendWave, int iCenterPointOfWave, double dAmplitudeOfWave, double dOffsetOfWave, int iSegmentLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_LIN")]       public static extern int WAV_LIN(int ID, int iWaveTableId, int iOffsetOfFirstPointInWaveTable, int iNumberOfPoints, int iAddAppendWave, int iNumberOfSpeedUpDownPointsInWave, double dAmplitudeOfWave, double dOffsetOfWave, int iSegmentLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_NOISE")]     public static extern int WAV_NOISE(int ID, int iWaveTableId, int iAddAppendWave, double dAmplitudeOfWave, double dOffsetOfWave, int iSegmentLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_SWEEP")]     public static extern int WAV_SWEEP(int ID, int iWaveTableId, int iAddAppendWave, uint iStarFequencytValueInPoints, uint iStopFrequencyValueInPoints, uint nLengthOfWave, double dAmplitudeOfWave, double dOffsetOfWave);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_RAMP")]      public static extern int WAV_RAMP(int ID, int iWaveTableId, int iOffsetOfFirstPointInWaveTable, int iNumberOfPoints, int iAddAppendWave, int iCenterPointOfWave, int iNumberOfSpeedUpDownPointsInWave, double dAmplitudeOfWave, double dOffsetOfWave, int iSegmentLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WAV_PNT")]       public static extern int WAV_PNT(int ID, int iWaveTableId, int iOffsetOfFirstPointInWaveTable, int iNumberOfPoints, int iAddAppendWave, double[] dWavePoints);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWAV")]          public static extern int qWAV(int ID, int[] iWaveTableIdsArray, int[] iParamereIdsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WGO")]           public static extern int WGO(int ID, int[] iWaveGeneratorIdsArray, int[] iStartModArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWGO")]          public static extern int qWGO(int ID, int[] iWaveGeneratorIdsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WGC")]           public static extern int WGC(int ID, int[] iWaveGeneratorIdsArray, int[] iNumberOfCyclesArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWGC")]          public static extern int qWGC(int ID, int[] iWaveGeneratorIdsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWGI")]          public static extern int qWGI(int ID, int[] iWaveGeneratorIdsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWGN")]          public static extern int qWGN(int ID, int[] iWaveGeneratorIdsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWGS")]          public static extern int qWGS(int ID, int iWaveGeneratorId, string sItems, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WSL")]           public static extern int WSL(int ID, int[] iWaveGeneratorIdsArray, int[] iWaveTableIdsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWSL")]          public static extern int qWSL(int ID, int[] iWaveGeneratorIdsArray, int[] iWaveTableIdsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DTC")]           public static extern int DTC(int ID, int[] iDdlTableIdsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDTL")]          public static extern int qDTL(int ID, int[] iDdlTableIdsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WCL")]           public static extern int WCL(int ID, int[] iWaveTableIdsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTLT")]          public static extern int qTLT(int ID, int[] iNumberOfDdlTables);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qGWD_SYNC")]     public static extern int qGWD_SYNC(int ID, int iWaveTableId, int iOffsetOfFirstPointInWaveTable, int iNumberOfValues, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qGWD")]          public static extern int qGWD(int ID, int[] iWaveTableIdsArray, int iNumberOfWaveTables, int iOffset, int nrValues, ref IntPtr dValarray, StringBuilder sGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WOS")]           public static extern int WOS(int ID, int[] iWaveTableIdsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWOS")]          public static extern int qWOS(int ID, int[] iWaveTableIdsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WTR")]           public static extern int WTR(int ID, int[] iWaveGeneratorIdsArray, int[] iTableRateArray, int[] iInterpolationTypeArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWTR")]          public static extern int qWTR(int ID, int[] iWaveGeneratorIdsArray, int[] iTableRateArray, int[] iInterpolationTypeArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DDL")]           public static extern int DDL(int ID, int iDdlTableId,  int iOffsetOfFirstPointInDdlTable,  int iNumberOfValues, double[] pdValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDDL_SYNC")]     public static extern int qDDL_SYNC(int ID,  int iDdlTableId,  int iOffsetOfFirstPointInDdlTable,  int iNumberOfValues, double[] pdValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDDL")]          public static extern int qDDL(int ID, int[] iDdlTableIdsArray, int iNumberOfDdlTables, int iOffset, int nrValues, ref IntPtr  dValarray, StringBuilder szGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DPO")]           public static extern int DPO(int ID, string sAxes);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWMS")]          public static extern int qWMS(int ID, int[] iWaveTableIds, int[] iWaveTableMaimumSize, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TWE")]           public static extern int TWE(int ID, int[] iWaveTableIdsArray, int[] iWaveTableStartIndexArray, int[] iWaveTableEndIndexArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTWE")]          public static extern int qTWE(int ID, int[] iWaveTableIdsArray, int[] iWaveTableStartIndexArray, int[] iWaveTableEndIndexArray, int iArraySize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Trigger commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TWC")]           public static extern int TWC(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TWS")]           public static extern int TWS(int ID, int[] iTriggerChannelIdsArray, int[] piPointNumberArray, int[] piSwitchArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTWS")]          public static extern int qTWS(int ID, int[] iTriggerChannelIdsArray, int iNumberOfTriggerChannels, int iOffset, int nrValues, ref IntPtr dValarray, StringBuilder szGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CTO")]           public static extern int CTO(int ID, int[] iTriggerOutputIdsArray, int[] iTriggerParameterArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CTOString")]     public static extern int CTOString(int ID, int[] iTriggerOutputIds, int[] iTriggerParameterArray, string sValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCTO")]          public static extern int qCTO(int ID, int[] iTriggerOutputIdsArray, int[] iTriggerParameterArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCTOString")]    public static extern int qCTOString(int ID, int[] piTriggerOutputIds, int[] piTriggerParameterArray, StringBuilder sValueArray, int iArraySize, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TRO")]           public static extern int TRO(int ID, int[] iTriggerChannelIds, int[] bTriggerChannelEnabel, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTRO")]          public static extern int qTRO(int ID, int[] iTriggerChannelIds, int[] bTriggerChannelEnabel, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TRI")]           public static extern int TRI(int ID, int[] iTriggerInputIds, int[] bTriggerState, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTRI")]          public static extern int qTRI(int ID, int[] iTriggerInputIds, int[] bTriggerState, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CTI")]           public static extern int CTI(int ID, int[] iTriggerInputIds, int[] iTriggerParameterArray, string sValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCTI")]          public static extern int qCTI(int ID,  int[] iTriggerInputIds, int[] iTriggerParameterArray, StringBuilder sValueArray, int iArraySize, int iBufferSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Record tabel commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHDR")]          public static extern int qHDR(int ID, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTNR")]          public static extern int qTNR(int ID, ref int iNumberOfRecordCannels);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DRC")]           public static extern int DRC(int ID, int[] iRecordTableIdsArray, string sRecordSourceIds, int[] iRecordOptionArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDRC")]          public static extern int qDRC(int ID, int[] iRecordTableIdsArray, StringBuilder szRecordSourceIds, int[] iRecordOptionArray, int iRecordSourceIdsBufferSize, int iRecordOptionArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDRR_SYNC")]     public static extern int qDRR_SYNC(int ID,  int iRecordTablelId,  int iOffsetOfFirstPointInRecordTable,  int iNumberOfValues, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDRR")]          public static extern int qDRR(int ID, int[] iRecTableIdsArray, int iNumberOfRecChannels, int iOffsetOfFirstPointInRecordTable, int iNumberOfValues, ref IntPtr dValueArray, StringBuilder sGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_DRT")]           public static extern int DRT(int ID, int[] iRecordChannelIdsArray, int[] iTriggerSourceArray, string sValues, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDRT")]          public static extern int qDRT(int ID, int[] iRecordChannelIdsArray, int[] iTriggerSourceArray, StringBuilder sValues, int iArraySize, int iValueBufferLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RTR")]           public static extern int RTR(int ID, int iReportTableRate);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qRTR")]          public static extern int qRTR(int ID, ref int iReportTableRate);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WGR")]           public static extern int WGR(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDRL")]          public static extern int qDRL(int ID, int[] iRecordChannelIdsArray, int[] iNuberOfRecordedValuesArray, int iArraySize);
        #endregion
        //////////////////////////////////////////////////////////////////////////
        #region System Response commands
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WFR")]           public static extern int WFR(int ID, string sAxes, int iMode, double dAmplitude, double dLowFrequency, double dHighFrequency, int iNumberOfFrequencies);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qWFR")]          public static extern int qWFR(int ID, string sAxes, int iMode, ref IntPtr dValueArray, StringBuilder sGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Piezo-Channel commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VMA")]           public static extern int VMA(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVMA")]          public static extern int qVMA(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VMI")]           public static extern int VMI(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVMI")]          public static extern int qVMI(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_VOL")]           public static extern int VOL(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qVOL")]          public static extern int qVOL(int ID, int[] iPiezoChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTPC")]          public static extern int qTPC(int ID, ref int iNumberOfPiezoChannels);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ONL")]           public static extern int ONL(int ID, int[] iPiezoChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qONL")]          public static extern int qONL(int ID, int[] iPiezoChannelsArray, int[] iValueArray, int iArraySize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Sensor-Channel commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTAD")]          public static extern int qTAD(int ID, int[] iSensorsChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTNS")]          public static extern int qTNS(int ID, int[] iSensorsChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TSP")]           public static extern int TSP(int ID, int[] iSensorsChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTSP")]          public static extern int qTSP(int ID, int[] iSensorsChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SCN")]           public static extern int SCN(int ID, int[] iSensorsChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSCN")]          public static extern int qSCN(int ID, int[] iSensorsChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTSC")]          public static extern int qTSC(int ID, ref int iNumberOfSensorChannels);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region PIEZOWALK(R)-Channel commands.
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_APG")]           public static extern int APG(int ID, int[] iPIEZOWALKChannelsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qAPG")]          public static extern int qAPG(int ID, int[] iPIEZOWALKChannelsArray, int[] iValueArray, int iArraySize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OAC")]           public static extern int OAC(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOAC")]          public static extern int qOAC(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OAD")]           public static extern int OAD(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOAD")]          public static extern int qOAD(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ODC")]           public static extern int ODC(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qODC")]          public static extern int qODC(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OCD")]           public static extern int OCD(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOCD")]          public static extern int qOCD(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OSM")]           public static extern int OSM(int ID, int[] iPIEZOWALKChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOSM")]          public static extern int qOSM(int ID, int[] iPIEZOWALKChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OSMf")]          public static extern int OSMf(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOSMf")]         public static extern int qOSMf(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OSMstringIDs")]  public static extern int OSMstringIDs(int ID, string sAxisOrChannelIds, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOSMstringIDs")] public static extern int qOSMstringIDs(int ID, string sAxisOrChannelIds, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OVL")]           public static extern int OVL(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOVL")]          public static extern int qOVL(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOSN")]          public static extern int qOSN(int ID, int[] iPIEZOWALKChannelsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOSNstringIDs")] public static extern int qOSNstringIDs(int ID, string sAxisOrChannelIds, int[] iValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SSA")]           public static extern int SSA(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSSA")]          public static extern int qSSA(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RNP")]           public static extern int RNP(int ID, int[] iPIEZOWALKChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_PGS")]           public static extern int PGS(int ID, int[] iPIEZOWALKChannelsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTAC")]          public static extern int qTAC(int ID, ref int nNrChannels);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTAV")]          public static extern int qTAV(int ID, int[] iChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OMA")]           public static extern int OMA(int ID, string sAxes, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOMA")]          public static extern int qOMA(int ID, string sAxes, double[] dValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OMR")]           public static extern int OMR(int ID, string sAxes, double[] dValueArray);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Joystick
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJAS")]          public static extern int qJAS(int ID, int[] iJoystickIDsArray, int[] iAxesIDsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_JAX")]           public static extern int JAX(int ID,  int iJoystickID,  int iAxesID, string sAxesBuffer);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJAX")]          public static extern int qJAX(int ID, int[] iJoystickIDsArray, int[] iAxesIDsArray, int iArraySize, StringBuilder sAxesBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJBS")]          public static extern int qJBS(int ID, int[] iJoystickIDsArray, int[] iButtonIDsArray, int[] bValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_JDT")]           public static extern int JDT(int ID, int[] iJoystickIDsArray, int[] iAxisIDsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_JLT")]           public static extern int JLT(int ID, int iJoystickID, int iAxisID, int iStartAdress, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJLT")]          public static extern int qJLT(int ID, int[] iJoystickIDsArray, int[] iAxisIDsArray,  int iNumberOfTables,  int iOffsetOfFirstPointInTable, int iNumberOfValues, ref IntPtr dValueArray, StringBuilder sGcsArrayHeader, int iGcsArrayHeaderMaxSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_JON")]           public static extern int JON(int ID, int[] iJoystickIDsArray, int[] bValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qJON")]          public static extern int qJON(int ID, int[] iJoystickIDsArray, int[] bValueArray, int iArraySize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region fast scan commands
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_AAP")]           public static extern int AAP(int ID, char[] sAxis1, double dLength1, char[] sAxis2, double dLength2, double dAlignStep, int iNrRepeatedPositions, int iAnalogInput);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FIO")]           public static extern int FIO(int ID, char[] sAxis1, double dLength1, char[] sAxis2, double dLength2, double dThreshold, double dLinearStep, double dAngleScan, int iAnalogInput);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FLM")]           public static extern int FLM(int ID, char[] sAxis, double dLength, double dThreshold, int iAnalogInput, int iDirection);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FLS")]           public static extern int FLS(int ID, char[] sAxis, double dLength, double dThreshold, int iAnalogInput, int iDirection);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FSA")]           public static extern int FSA(int ID, char[] sAxis1, double dLength1, char[] sAxis2, double dLength2, double dThreshold, double dDistance, double dAlignStep, int iAnalogInput);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FSC")]           public static extern int FSC(int ID, char[] sAxis1, double dLength1, char[] sAxis2, double dLength2, double dThreshold, double dDistance, int iAnalogInput);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FSM")]           public static extern int FSM(int ID, char[] sAxis1, double dLength1, char[]  sAxis2, double dLength2, double dThreshold, double dDistance, int iAnalogInput);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFSS")]          public static extern int qFSS(int ID, ref int piResult);
         #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region optical boards (hexapod)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SGA")]           public static extern int SGA(int ID, int[] iAnalogChannelIds, int[] iGainValues, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSGA")]          public static extern int qSGA(int ID, int[] iAnalogChannelIds, int[] iGainValues, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_NAV")]           public static extern int NAV(int ID, int[] iAnalogChannelIds, int[] iNrReadingsValues, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qNAV")]          public static extern int qNAV(int ID, int[] iAnalogChannelIds, int[] iNrReadinPI_FNLgsValues, int iArraySize);
        // more hexapod specific
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetDynamicMoveBufferSize")]	public static extern int GetDynamicMoveBufferSize(int ID, ref int iSize);

        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region PIShift
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qCOV")]          public static extern int qCOV(int ID, int[] iChannelsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MOD")]           public static extern int MOD(int ID, string sItems, uint[] iModeArray, string sValues);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qMOD")]          public static extern int qMOD(int ID, string sItems, uint[] iModeArray, StringBuilder sValues, int iMaxValuesSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qDIA")]          public static extern int qDIA(int ID, uint[] iIDArray, StringBuilder sValues,  int iBufferSize, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHDI")]          public static extern int qHDI(int ID, StringBuilder sBuffer,  int iBufferSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region HID
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIS")]          public static extern int qHIS (int ID, StringBuilder sBuffer,  int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HIS")]           public static extern int HIS  (int ID, int[]iDeviceIDsArray, int[] iItemIDsArray, int[] iPropertyIDArray, string sValues, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIE")]          public static extern int qHIE (int ID, int[] iDeviceIDsArray, int[] iAxesIDsArray, double[] dValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIB")]          public static extern int qHIB (int ID, int[] iDeviceIDsArray, int[] iButtonIDsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HIL")]           public static extern int HIL  (int ID, int[] iDeviceIDsArray, int[] iLED_IDsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIL")]          public static extern int qHIL (int ID, int[] iDeviceIDsArray, int[] iLED_IDsArray, int[] iValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HIN")]           public static extern int HIN  (int ID, string sAxes, int[] bValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIN")]          public static extern int qHIN (int ID, string sAxes, int[] bValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HIA")]           public static extern int HIA  (int ID, string sAxes, int[] iFunctionArray, int[] iDeviceIDsArray, int[] iAxesIDsArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIA")]          public static extern int qHIA (int ID, string sAxes, int[] iFunctionArray, int[] iDeviceIDsArray, int[] iAxesIDsArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HDT")]           public static extern int HDT  (int ID, int[] iDeviceIDsArray, int[] iAxisIDsArray, int[] piValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHDT")]          public static extern int qHDT (int ID, int[] iDeviceIDsArray, int[] iAxisIDsArray, int[] piValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_HIT")]           public static extern int HIT  (int ID, int[] iTableIdsArray, int[] iPointNumberArray, double[] pdValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qHIT")]          public static extern int qHIT (int ID, int[] iTableIdsArray,  int iNumberOfTables,  int iOffsetOfFirstPointInTable,  int iNumberOfValues, ref IntPtr pdValueArray, StringBuilder sGcsArrayHeader, int iGcsArrayHeaderMaxSize);

        #endregion
        /////////////////////////////////////////////////////////////////////////////
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qMAN")]          public static extern int qMAN(int ID, String sCommand, StringBuilder sBuffer,  int iBufferSize);
        /////////////////////////////////////////////////////////////////////////////
        #region 좌표계
        //  현재 위치에서 Operating 좌표계를 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KSF")]           public static extern int KSF  (int ID, string sNameOfCoordSystem);
        // 지정된 좌표계를 활성화 (위치 값에 영향)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KEN")]           public static extern int KEN  (int ID, string sNameOfCoordSystem);
        // 좌표계를 제거
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KRM")]           public static extern int KRM  (int ID, string sNameOfCoordSystem);
        // 정렬 오류를 영구적으로 수정하기 위해 현재 위치에서 레벨링 좌표계를 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KLF")]           public static extern int KLF  (int ID, string sNameOfCoordSystem);
        // 값을 지정하여 Operating 좌표계를 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KSD")]           public static extern int KSD  (int ID, string sNameOfCoordSystem, string sAxes, double[] dValueArray);
        // Tool 좌표계 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KST")]           public static extern int KST  (int ID, string sNameOfCoordSystem, string sAxes, double[] dValueArray);
        // Work 좌표계 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KSW")]           public static extern int KSW  (int ID, string sNameOfCoordSystem, string sAxes, double[] dValueArray);
        // 오프셋 값을 지정하여 레벨링 좌표계를 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KLD")]           public static extern int KLD  (int ID, string sNameOfCoordSystem, string sAxes, double[] dValueArray);
        // 회전 각도를 지정하여 방향 좌표계를 정의
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KSB")]           public static extern int KSB  (int ID, string sNameOfCoordSystem, string sAxes, double[] dValueArray);
        //  Tool Coordinate System Relative Motion
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MRT")]           public static extern int MRT  (int ID, string sAxes, double[] dValueArray);
        // Work Coordinate System Relative Motion
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_MRW")]           public static extern int MRW  (int ID, string sAxes, double[] dValueArray);
        // 상위 시스템에서 루트 시스템까지의 오프셋을 기반 X, Y, Z, U, V 및 W 축에 대한 오프셋 값을 제공
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKLT")]          public static extern int qKLT (int ID, string sStartCoordSystem, string sEndCoordSystem, StringBuilder buffer, int bufsize);
        // 활성 좌표계의 이름
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKEN")]          public static extern int qKEN (int ID, string sNamesOfCoordSystems, StringBuilder buffer, int bufsize);
        // 활성 좌표계 유형을 나열
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKET")]          public static extern int qKET (int ID, string sTypes, StringBuilder buffer, int bufsize);
        //  메모리의 좌표계 속성을 제공 (휘발)
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKLS")]          public static extern int qKLS (int ID, string sNameOfCoordSystem, string sItem1, string sItem2, StringBuilder buffer, int bufsize);
        // 두 좌표계를 연결하여 상위-하위 관계를 생성
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KLN")]           public static extern int KLN  (int ID, string sNameOfChild, string sNameOfParent);
        // 기존 좌표계 체인을 표시
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKLN")]          public static extern int qKLN (int ID, string sNamesOfCoordSystems, StringBuilder buffer, int bufsize);
        // 특정 방향 벡터를 따라 이동할 때 명령할 수 있는 최대 절대 위치를 조회
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTRA")]          public static extern int qTRA (int ID, string sAxes, double[] dComponents, double[] dValueArray);
        // 작업-도구 개념의 좌표계 조합 속성을 나열
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qKLC")]          public static extern int qKLC (int ID, string sNameOfCoordSystem1, string sNameOfCoordSystem2, string sItem1, string sItem2, StringBuilder buffer, int bufsize);
        // Copy Coordinate System
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_KCP")]           public static extern int KCP  (int ID, string sSource, string sDestination);

        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Spezial
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetSupportedFunctions")]     public static extern int GetSupportedFunctions(int ID, int[] iComandLevelArray, int iBufferSize, StringBuilder sFunctionNames, int iMaxFunctioNamesLength);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetSupportedParameters")]    public static extern int GetSupportedParameters(int ID, int[] iParameterIdArray, int[] iComandLevelArray, int[] iMennoryLocationArray, int[] iDataTypeArray, int[] iNumberOfItems, int iBufferSize, StringBuilder sParameterName, int iMaxParameterNameSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetSupportedControllers")]   public static extern int GetSupportedControllers(StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetAsyncBufferIndex")]       public static extern int GetAsyncBufferIndex(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetAsyncBuffer")]            public static extern int GetAsyncBuffer(int ID, ref IntPtr pdValueArray);


        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_AddStage")]                  public static extern int AddStage(int ID, string sAxes);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RemoveStage")]               public static extern int RemoveStage(int ID, string sStageName);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OpenUserStagesEditDialog")]  public static extern int OpenUserStagesEditDialog(int ID);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OpenPiStagesEditDialog")]    public static extern int OpenPiStagesEditDialog(int ID);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WriteConfigurationFromDatabaseToController")]             public static extern int WriteConfigurationFromDatabaseToController(int ID, string sFilter, string sConfigurationName, string sWarnings, int warningsBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_WriteConfigurationFromDatabaseToControllerAndSave")]             public static extern int WriteConfigurationFromDatabaseToControllerAndSave(int ID, string sFilter, string sConfigurationName, string sWarnings, int warningsBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_ReadConfigurationFromControllerToDatabase")]              public static extern int ReadConfigurationFromControllerToDatabase(int ID, string sFilter, string sConfigurationName, string sWarnings, int warningsBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetAvailableControllerConfigurationsFromDatabase")]       public static extern int GetAvailableControllerConfigurationsFromDatabase(int ID, string sConfigurationNames, int configurationNamesBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetAvailableControllerConfigurationsFromDatabaseByType")] public static extern int GetAvailableControllerConfigurationsFromDatabaseByType(int ID, string sConfigurationNames, int configurationNamesBufferSize, uint type);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_IsAvailable")]                                         public static extern int IsAvailable(int ID);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetDllVersionInformation")] public static extern int GetDllVersionInformation(int ID, StringBuilder dllVersionsInformationBuffer, int bufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_GetPIStages3VersionInformation")] public static extern int GetPIStages3VersionInformation(int ID, StringBuilder piStages3VersionsInformationBuffer, int bufferSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Fast Alignment
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FDG")]                       public static extern int FDG(int ID, string szScanRoutineName, string szScanAxis, string szStepAxis, string szParameters);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FDR")]                       public static extern int FDR(int ID, string szScanRoutineName, string szScanAxis, double dScanAxisRange, string szStepAxis, double dStepAxisRange, string szParameters);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FGC")]                       public static extern int FGC(int ID, string szProcessIds, double[] pdScanAxisCenterValueArray, double[] pdStepAxisCenterValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFGC")]                      public static extern int qFGC(int ID, string szProcessIds, double[] pdScanAxisCenterValueArray, double[] pdStepAxisCenterValueArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FRC")]                       public static extern int FRC(int ID, string szProcessIdBase, string szProcessIdsCouplet);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRC")]                      public static extern int qFRC(int ID, string szProcessIdsBase, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRH")]                      public static extern int qFRH(int ID, StringBuilder sBuffer, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FRP")]                       public static extern int FRP(int ID, string szScanRoutineNames,  int[] piOptionsArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRP")]                      public static extern int qFRP(int ID, string szScanRoutineNames, int[] piOptionsArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRR")]                      public static extern int qFRR(int ID, string szScanRoutineNames, int iResultId, StringBuilder sResult, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFRRArray")]                 public static extern int qFRRArray(int ID, string szScanRoutineNames, int[] piResultIds, StringBuilder sResult, int iBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FRS")]                       public static extern int FRS(int ID, string szScanRoutineNames);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTCI")]                      public static extern int qTCI(int ID, int[] piFastAlignmentInputIdsArray, double[] pdCalculatedInputValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SIC")]                       public static extern int SIC(int ID, int iFastAlignmentInputId, int iCalcType, double[] pdParameters, int iNumberOfParameters);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSIC")]                      public static extern int qSIC(int ID, int[] piFastAlignmentInputIdsArray, int iNumberOfInputIds, StringBuilder sBuffer, int iBufferSize);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Trajectory
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TGA")]                       public static extern int TGA(int ID, int[] iTrajectoryIDsArray, double[] dTrajectoryValueArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TGC")]                       public static extern int TGC(int ID, int[] iTrajectoryIDsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TGF")]                       public static extern int TGF(int ID, int[] iTrajectoryIDsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TGS")]                       public static extern int TGS(int ID, int[] iTrajectoryIDsArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTGL")]                      public static extern int qTGL(int ID, int[] iTrajectoryIDsArray, int[] dTrajectorySizesArray, int iArraySize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_TGT")]                       public static extern int TGT(int ID, int iTrajectoryTiming);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qTGT")]                      public static extern int qTGT(int ID, ref int iTrajectoryTiming);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region Surface scan
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_FSF")]                        public static extern int FSF(int ID, string sAxis, double forceValue1, double positionOffset, int useForceValue2, double forceValue2);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFSF")]                       public static extern int qFSF(int ID, string sAxes, double[] pForceValue1Array, double[] pPositionOffsetArray, double[] pForceValue2Array);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qFSR")]                       public static extern int qFSR(int ID, string sAxes, int[] pbValueArray);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region PIRest commands
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_POL")]                       public static extern int POL(int ID, string sAxes, int[] iValueArray);
        #endregion
        /////////////////////////////////////////////////////////////////////////////
        #region UMF commands
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REC_START")]  public static extern int REC_START(int ID, string recorderIds);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REC_STOP")]   public static extern int REC_STOP(int ID, string recorderIds);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REC_RATE")]   public static extern int REC_RATE(int ID, string recorderId, int rate);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_RATE")]  public static extern int qREC_RATE(int ID, string recorderIds, int[] rateValues);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REC_TRACE")]  public static extern int REC_TRACE(int ID, string recorderId, int traceId, string containerUnitId, string functionUnitId, string parameterId);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_REC_TRG")]    public static extern int REC_TRG(int ID, string recorderId, string triggerMode, string triggerOption1, string triggerOption2);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_NUM")]   public static extern int qREC_NUM(int ID, string recorderIds, int[] numDataValues);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_STATE")] public static extern int qREC_STATE(int ID, string recorderIds, StringBuilder statesBuffer, int statesBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_TRG")]   public static extern int qREC_TRG(int ID, string recorderIds, StringBuilder triggerConfigurationBuffer, int triggerConfigurationBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_TRACE")] public static extern int qREC_TRACE(int ID, string recorderId, int traceIndex, StringBuilder traceConfigurationBuffer, int traceConfigurationBufferSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qREC_DAT")]   public static extern int qREC_DAT(int ID, string recorderId, string dataFormat, int offset, int numberOfValue, int[] traceIndices, int numberOfTraceIndices, ref IntPtr dataValues, StringBuilder gcsArrayHeaderBuffer, int gcsArrayHeaderBufferSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_UCL")]  public static extern int UCL(int ID, string userCommandLevel, string password);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUCL")] public static extern int qUCL(int ID, StringBuilder userCommandLevel, int bufSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qIPR")] public static extern int qIPR(int ID, StringBuilder sBuffer, int iBufferSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG")]      public static extern int qUSG(int ID, StringBuilder usg, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG_CMD")]  public static extern int qUSG_CMD(int ID, string chapter, StringBuilder usg, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG_SYS")]  public static extern int qUSG_SYS(int ID, string chapter, StringBuilder usg, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG_PAM")]  public static extern int qUSG_PAM(int ID, string chapter, StringBuilder usg, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG_HW")]   public static extern int qUSG_HW(int ID, string chapter, StringBuilder usg, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qUSG_PROP")] public static extern int qUSG_PROP(int ID, string chapter, StringBuilder usg, int bufSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qLOG")] public static extern int qLOG(int ID, int startIndex, StringBuilder errorLog, int bufSize);

        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_Int32")]   public static extern int SPV_Int(int ID, string memType, string containerUnit, string functionUnit, string parameter, int value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_UInt32")]  public static extern int SPV_UInt(int ID, string memType, string containerUnit, string functionUnit, string parameter, uint value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_Int64")]   public static extern int SPV_Long(int ID, string memType, string containerUnit, string functionUnit, string parameter, long value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_UInt64")]  public static extern int SPV_ULong(int ID, string memType, string containerUnit, string functionUnit, string parameter, ulong value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_Double")]  public static extern int SPV_Double(int ID, string memType, string containerUnit, string functionUnit, string parameter, double value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SPV_String")]  public static extern int SPV_String(int ID, string memType, string containerUnit, string functionUnit, string parameter, string value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV")]        public static extern int qSPV(int ID, string memType, string containerUnit, string functionUnit, string parameter, StringBuilder answer, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_Int32")]  public static extern int qSPV_Int(int ID, string memType, string containerUnit, string functionUnit, string parameter, ref int value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_UInt32")] public static extern int qSPV_UInt(int ID, string memType, string containerUnit, string functionUnit, string parameter, ref uint value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_Int64")]  public static extern int qSPV_Long(int ID, string memType, string containerUnit, string functionUnit, string parameter, ref int value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_UInt64")] public static extern int qSPV_ULong(int ID, string memType, string containerUnit, string functionUnit, string parameter, ref uint value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_Double")] public static extern int qSPV_Double(int ID, string memType, string containerUnit, string functionUnit, string parameter, ref double value);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSPV_String")] public static extern int qSPV_String(int ID, string memType, string containerUnit, string functionUnit, string parameter, StringBuilder value, int bufSize);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_CPA")]         public static extern int CPA (int ID, string sourceMemType, string trgetMemType, string containerUnit, string functionUnit, string parameter);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSTV")]        public static extern int qSTV (int ID, string containerUnit, uint[] statusArray);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SAM")]         public static extern int SAM  (int ID, string axisContainerUnit, int axisOperationMode);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSAM")]        public static extern int qSAM (int ID, string axisContainerUnit, int[] axesOperationModes);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_RES")]         public static extern int RES (int ID, string axisContainerUnit);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_SMV")]         public static extern int SMV(int ID, string axisContainerUnits, double[] numberOfSteps);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSMV")]        public static extern int qSMV(int ID, string axisContainerUnits, double[] numberOfSteps);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qSMR")]        public static extern int qSMR(int ID, string axisContainerUnits, double[] numberOfSteps);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_OCV")]         public static extern int OCV(int ID, string axisContainerUnits, double[] controlValues);
        [DllImport(PI_GCS2_DLL_NAME, EntryPoint = "PI_qOCV")]        public static extern int qOCV(int ID, string axisContainerUnits, double[] controlValues);
        #endregion
    }
}
