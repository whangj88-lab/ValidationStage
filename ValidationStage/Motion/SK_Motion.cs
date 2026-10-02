using PI;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace MotorizedStage_SK_PI
{
    public class SK_Motion
    {

        private SerialPort _serialPort;
        private readonly object _serialLock = new object();

        private const int _cntMaxAxis = 6;
        private Axis[] _axes;
        private int _cntAxis;
        private AxisStatus[] _statuses;
        private readonly object _axisLock = new object();

        private CancellationTokenSource _cancelTokenSource;
        public event EventHandler<string> Logged = null; 
        public event EventHandler<bool> OnConnectionChange = null;
        public event EventHandler<AxisStatus[]> OnStatusChanged = null;

        public double[][] DefaultSpeedLevelValues = new double[_cntMaxAxis][]
        {
                // SK
                new double[3] { 20000.0, 100000.0, 500000.0 },  // X축
                new double[3] { 20000.0, 100000.0, 500000.0 },  // Y축
                new double[3] { 20000.0, 70000.0, 150000.0 },   // Z축
                new double[3] { 10000.0, 60000.0, 120000.0},    // TX
                new double[3] { 10000.0, 80000.0, 160000.0 },   // TY
                new double[3] { 10000.0, 50000.0, 100000.0 }    //TZ
        };

        public SK_Motion()
        {
            _serialPort = new SerialPort();
            _serialPort.PortName = "COM13";     // Default value
            _serialPort.BaudRate = 38400;       // Default value
            _serialPort.NewLine = "\r\n"; ;      // Default value
            _serialPort.ReadTimeout = 5000;    // Default value
            _serialPort.RtsEnable = true;   // 
        }

        // PortName
        public string PortName => _serialPort.PortName;

        public bool IsConnected => _serialPort.IsOpen;
        public string[] GetPortNames()
        {
            return SerialPort.GetPortNames();
        }

        public bool Connect(Axis[] axes, string portName)
        {
            bool openedHere = false;
            try
            {
                if (axes.Length > _cntMaxAxis)
                {
                    throw new Exception($": The number of axes exceeds the maximum allowed limit of {_cntMaxAxis}.");
                }
                _axes = axes;
                _cntAxis = _axes.Length;
                _serialPort.PortName = portName;
                Logged?.Invoke(this, "-- Try to Connect --");
                OpenPort();
                openedHere = true;
                _serialPort.DiscardInBuffer();  // In case of buffer clear-> GIP101, the level is entered first

                // 포트는 열렸지만 SK 컨트롤러가 아닌 경우(다른 장비/전원 OFF) 여기서 1회(ReadTimeout)만 기다리고 실패시킨다.
                WriteRead(_StatusCurPos(), out _, maxAttempts: 1);

                _statuses = new AxisStatus[_cntAxis];
                for (int i = 0; i < _cntAxis; i++)
                {
                    _statuses[i] = new AxisStatus(axes[i]);
                }

                _cancelTokenSource = new CancellationTokenSource();
                Task.Run(() => PollingStatus(_cancelTokenSource.Token));
                UpdateSpeedStatus();
                OnConnectionChange?.Invoke(this, true);
                Logged?.Invoke(this, ": Successfully connected.");
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, $"Error : Connect \n\t{ex.Message}");
                // 이번 호출에서 연 포트가 실패하면 닫아둔다. 안 그러면 IsConnected 가 true 로 남고 재시도 시 "already open" 이 된다.
                // (이미 연결된 상태에서 다시 Connect 한 경우는 기존 연결을 건드리지 않는다.)
                if (openedHere)
                {
                    _cancelTokenSource?.Cancel();
                    try { ClosePort(); } catch { }
                }
                return false;
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                _cancelTokenSource?.Cancel();
                Thread.Sleep(100);
                ClosePort();
                OnConnectionChange?.Invoke(this, false);
                Logged?.Invoke(this, ": Disconnected");
            }

        }
        
        public async Task<bool> MoveAsync(Axis [] axes, double[] positions, bool isAbsolute)
        {
            try
            {
                if (axes.Length != positions.Length)
                {
                    throw new Exception("Move Command: The length of 'axes' must match the length of 'positions'.");
                }
                
                foreach (Axis axis in axes)
                {
                    if (!_axes.Contains(axis))
                    {
                        throw new Exception($"Move Command: The axis '{axis}' is not in the allowed list of axes.");
                    }
                }

                if (IsMoving)
                {
                    throw new Exception($"Move Command is not ready");
                }

                WriteRead(_Move(axes, positions, isAbsolute), out string rCmd);

                if (rCmd != "OK")
                {
                    throw new Exception("Failed Move Command");
                }

                await WaitForReadyAsync();

                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }
        
        public bool JogRun(Axis axis, bool dir)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                if (!_axes.Contains(axis))
                {
                    throw new Exception($"JogMove Command: The axis '{axis}' is not recognized or is missing from the allowed set of axes.");
                }
                if (IsMoving)
                {
                    throw new Exception($"JogMove Command is not ready");
                }

                WriteRead(_Jog(new Axis[1] { axis }, new bool[1] { dir }), out string rCmd);
                Logged?.Invoke(this, "Jog");

                if (rCmd != "OK")
                {
                    throw new Exception("Failed JogMove Command");
                }

                WaitForReadyAsync();
                //await WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public bool JogStop(Axis axis)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }

                if (!_axes.Contains(axis))
                {
                    throw new Exception($"Stop Command: The axis '{axis}' is not in the allowed list of axes.");
                }

                WriteRead(_Stop(new Axis[] { axis }, new bool[] { true }), out string rCmd);
                Logged?.Invoke(this, "Stop");

                if (rCmd != "OK")
                {
                    throw new Exception("Failed Stop Command");
                }

                WaitForReadyAsync();

                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task<bool> ReferenceAsync(Axis[] axes, bool[] originCmd)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }

                if (axes.Length != originCmd.Length)
                {
                    throw new Exception("MoveOrigin Command: The length of 'axes' must match the length of 'origin'.");
                }

                foreach (Axis axis in axes)
                {
                    if (!_axes.Contains(axis))
                    {
                        throw new Exception($"MoveOrigin Command: The axis '{axis}' is not in the allowed list of axes.");
                    }
                }

                if (IsMoving)
                {
                    throw new Exception($"Move Origin Command is not ready");
                }
            

                WriteRead(_MoveOrigin(axes, originCmd), out string rCmd);

                if (rCmd != "OK")
                {
                    throw new Exception("Failed MoveOrigin Command");
                }

                await WaitForReadyAsync();

                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public async Task<bool> StopAsync(Axis[] axes, bool[] stopCmd)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }

                if (axes.Length != stopCmd.Length)
                {
                    throw new Exception("Stop Command: The length of 'axes' must match the length of 'stopCmd'.");
                }

                foreach (Axis axis in axes)
                {
                    if (!_axes.Contains(axis))
                    {
                        throw new Exception($"Stop Command: The axis '{axis}' is not in the allowed list of axes.");
                    }
                }

                WriteRead(_Stop(axes, stopCmd), out string rCmd);
                Logged?.Invoke(this, "Stop");

                if (rCmd != "OK")
                {
                    throw new Exception("Failed Stop Command");
                }

                await WaitForReadyAsync();

                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }
        
        public async Task<bool> StopEmergencyAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }

                WriteRead(_StopEmergency(), out string rCmd);
                Logged?.Invoke(this, "StopEmergency");

                if (rCmd != "OK")
                {
                    throw new Exception("Failed StopEmergency Command");
                }
                await WaitForReadyAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }
        public bool SetSpeed(Axis axis, int slow, int fast, int rate)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                if (!_axes.Contains(axis))
                {
                    throw new Exception($"SetSpeed Command: The axis '{axis}' is not in the allowed list of axes.");
                }

                if (IsMoving)
                {
                    throw new Exception($"SetSpeed Command is not ready");
                }


                WriteRead(_SetSpeed(axis, slow, fast, rate), out string rCmd);
                //Logged?.Invoke(this, "SetSpeed"); ;
                if (rCmd != "OK")
                {
                    throw new Exception("Failed SetSpeed Command");
                }

                UpdateSpeedStatus();
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }
        public double[] GetPositions()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                lock (_axisLock)
                {
                   return _statuses.Select(status => status.Position).ToArray();
                }
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return null;
            }
        }
        public double[] GetSpeeds()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not connected");
                }
                lock (_axisLock)
                {
                    return _statuses.Select(status => status.Speed).ToArray();
                }
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return null;
            }
        }
        public AxisStatus[] GetAxisStatuses()
        {
            if (!IsConnected)
            {
                throw new Exception("Not connected");
            }
            lock (_axisLock)
            {
                return _statuses.Select(status => status).ToArray();
            }
        }

        #region private 함수

        private bool IsMoving
        {
            get
            {
                lock (_axisLock)
                {
                    foreach (var status in _statuses)
                    {
                        if (status.IsMoving) return true;
                    }
                    return false;
                }
            }
        }       

        private async Task WaitForReadyAsync()
        { 
            while(true)
            {
                UpdateMovingStatus();

                if (!IsMoving) return;
                
                await Task.Delay(10);
            }
        }
        // Update Statuses
        private async Task PollingStatus(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100,token);
                    UpdateGeneralStatus();
                }
                catch (OperationCanceledException)
                {
                    break; // 취소되면 루프 종료
                }
                catch (Exception ex)
                {
                    Logged?.Invoke(this, $"Error : PollingStatus {ex.Message}");
                    Disconnect();
                }
            }
        }
        private void UpdateGeneralStatus()
        {
            //--Current Position--//
            double[] positions = UpdatePositions();
            //--- Is Moving---//
            bool[] isMovings = UpdateIsMovings();
            //--- Alarm, Is Limit ---//
            UpdateAlarmsLimits(out int[] alarms, out bool[] isPosLimits, out bool[] isNegLimits);
            UpdateStatuses(positions, isMovings, alarms, isPosLimits, isNegLimits);
        }
        
        private void UpdateMovingStatus()
        {
            //--Current Position--//
            double[] positions = UpdatePositions();
            //--- Is Moving---//
            bool[] isMovings = UpdateIsMovings();
            UpdateStatuses(positions, isMovings);
        }

        private void UpdateSpeedStatus()
        {
            string rCmd;
            double[] speeds = new double[_cntAxis];
            // Travel Speed
            for (int i = 0; i < _cntAxis; i++)
            {
                string para = $"D{(int)_axes[i]}";
                WriteRead(_StatusInfo(para), out rCmd);

                if (string.IsNullOrWhiteSpace(rCmd))
                    throw new Exception("Failed to retrieve Position.");
                
                string[] strSpeeds = rCmd.Split(',');
                speeds[i] = double.Parse(strSpeeds[1]);
            }

            lock (_axisLock)
            {
                bool isChanged = false;
                for (int i = 0; i < _cntAxis; i++)
                {
                    if (speeds[i] != 0 && _statuses[i].Speed != speeds[i])
                    {
                        _statuses[i].Speed = speeds[i];
                        isChanged = true;
                    }
                }

                if (isChanged)
                {
                    OnStatusChanged?.Invoke(this, (AxisStatus[])_statuses.Clone());
                }
            }
        }

        private void UpdateStatuses(double[] positions, bool[] isMovings, int[] alarms = null, bool[] isPosLimits = null, bool[] isNegLimits = null)
        {
            lock (_axisLock)
            {
                bool isChanged = false;
                for (int i = 0; i < _cntAxis; i++)
                {
                    // positions이 null이 아닌 경우 업데이트
                    if (positions != null && _statuses[i].Position != positions[i])
                    {
                        _statuses[i].Position = positions[i];
                        isChanged = true;
                    }

                    // isMovings이 null이 아닌 경우 업데이트
                    if (isMovings != null && _statuses[i].IsMoving != isMovings[i])
                    {
                        _statuses[i].IsMoving = isMovings[i];
                        isChanged = true;
                    }

                    // alarms이 null이 아닌 경우 업데이트
                    if (alarms != null && _statuses[i].Alarm != alarms[i])
                    {
                        _statuses[i].Alarm = alarms[i];
                        isChanged = true;
                    }

                    // isPosLimits이 null이 아닌 경우 업데이트
                    if (isPosLimits != null && _statuses[i].IsPosLimit != isPosLimits[i])
                    {
                        _statuses[i].IsPosLimit = isPosLimits[i];
                        isChanged = true;
                    }

                    // isNegLimits이 null이 아닌 경우 업데이트
                    if (isNegLimits != null && _statuses[i].IsNegLimit != isNegLimits[i])
                    {
                        _statuses[i].IsNegLimit = isNegLimits[i];
                        isChanged = true;
                    }
                }
            

                if (isChanged)
                {
                    OnStatusChanged?.Invoke(this, (AxisStatus[])_statuses.Clone());
                }
            }
        }

        private double[] UpdatePositions()
        {
            string rCmd;

            //--Current Position--//
            WriteRead(_StatusCurPos(), out rCmd);
            if (string.IsNullOrWhiteSpace(rCmd))
                throw new Exception("Failed to retrieve Position.");

            string[] strPositions = rCmd.Split(',');

            double[] positions = new double[_cntAxis];
            for (int i = 0; i < _cntAxis; i++)
            {
                int iAxis = (int)_axes[i];
                if (double.TryParse(strPositions[iAxis], out double position))
                {
                    if (_axes[i] < Axis.TY)
                    {
                        positions[i] = position * 0.01;
                    }
                    else
                    {
                        positions[i] = position * 60 / 10000;
                    }
                }
            }
            return positions;
        }
        private bool[] UpdateIsMovings()
        {
            string rCmd;

            //--- Is Moving---//
            WriteRead(_StatusIsBusy(), out rCmd);
            if (string.IsNullOrWhiteSpace(rCmd))
                throw new Exception("Failed to retrieve IsMoving.");

            string[] strIsMovings = rCmd.Split(',');

            bool[] isMovings = new bool[_cntAxis];
            for (int i = 0; i < _cntAxis; i++)
            {
                int iAxis = (int)_axes[i];
                isMovings[i] = (strIsMovings[iAxis] == "1");
            }
            return isMovings;
        }
        private void UpdateAlarmsLimits(out int[] alarms, out bool[] isPosLimits, out bool[] isNegLimits)
        {
            string rCmd;

            WriteRead(_StatusSensor(), out rCmd);
            if (string.IsNullOrWhiteSpace(rCmd))
                throw new Exception("Failed to retrieve Sensor Status.");

            string[] strSensors = rCmd.Split(',').Skip(1).ToArray();

            alarms = new int[_cntAxis];
            isPosLimits = new bool[_cntAxis];
            isNegLimits = new bool[_cntAxis];

            for (int i = 0; i < _cntAxis; i++)
            {
                int iAxis = (int)_axes[i];

                if (int.TryParse(strSensors[iAxis], System.Globalization.NumberStyles.HexNumber, null, out int sensor))
                {
                    bool isDrvAlarms = (sensor & (1 << 6)) != 0;
                    bool isScaleAlarm = (sensor & (1 << 5)) != 0;
                    bool isPosLimit = (sensor & (1 << 1)) != 0;
                    bool isNegLimit = (sensor & (1 << 0)) != 0;

                    if (isDrvAlarms)
                        alarms[i] = 40;
                    else if (isScaleAlarm)
                        alarms[i] = 20;
                    else if (isPosLimit && isNegLimit)
                        alarms[i] = 10;
                    else
                        alarms[i] = 0;

                    isPosLimits[i] = isPosLimit;
                    isNegLimits[i] = isNegLimit;
                }
                else
                {
                    alarms[i] = 1;
                    isPosLimits[i] = false;
                    isNegLimits[i] = false;
                }
            }
        }
        // Write & Read
        private void WriteRead(string cmd, out string response, int maxAttempts = 3)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception($"WriteRead : SerialPort Is Not Connected");
                }

                lock (_serialLock)
                {
                    _serialPort.WriteLine(cmd);
                    // 원래(A) 무한 재시도였음 -> 응답 없는 포트에서 영원히 멈추므로 횟수 제한. 마지막 타임아웃은 그대로 throw.
                    for (int attempt = 1; ; attempt++)
                    {
                        try
                        {
                            response = _serialPort.ReadLine();
                            break;
                        }
                        catch (TimeoutException) when (attempt < maxAttempts)
                        {
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"WriteRead : {ex.Message}");
            }
        }

        // Port Open
        private void OpenPort()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("The port is already open.");
                }
                else
                {
                    _serialPort.Open();
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        // Port Close
        private void ClosePort()
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch
            {
                throw new Exception("Error occurred at the time of the device close.");
            }
        }

        // Move Position (Absolute, Relative)
        private string _Move(Axis[] axes, double[] positions, bool isAbsolute)
        {
            int[] iPositions = new int[axes.Length];

            for(int i=0; i<axes.Length; i++)
            {
                if (axes[i] < Axis.TY) // X, Y, Z
                {
                    //if (axes[i] == Axis.Y)
                    //{
                    //    iPositions[i] = -(int)(positions[i] * 100);
                    //}
                    //else
                    //{
                        iPositions[i] = (int)(positions[i] * 100);
                    //}
                }
                else
                {
                    iPositions[i] = (int)(positions[i] / 60 * 10000);
                }
            }
       
            string cmd;
            if (isAbsolute)
                cmd = "A:";
            else
                cmd = "M:";

            foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            {
                int index = Array.IndexOf(axes, axis);
                if (index == -1)
                {
                    cmd += ",";
                }
                else
                {
                    cmd += iPositions[index].ToString();
                    cmd += ",";
                }
            }
            return cmd;
        }

        // Move with JOG
        private string _Jog(Axis[] axes, bool[] dirs)
        {
            string cmd = "J:";

            foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            {
                int index = Array.IndexOf(axes, axis);
                if (index == -1)
                {
                    cmd += ",";
                }
                else
                {
                    //if (axis == Axis.Y)
                    //{
                    //    dirs[index] = !dirs[index];
                    //}

                    cmd += (dirs[index] ? "+" : "-");
                    cmd += ",";
                }
            }
            return cmd;
        }

        // Move Origin
        private string _MoveOrigin(Axis[] axes, bool[] valueArray)
        {
            string cmd = "H:";

            foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            {
                int index = Array.IndexOf(axes, axis);
                if (index == -1)
                {
                    cmd += ",";
                }
                else
                {
                    cmd += valueArray[index] ? "1" : "0";
                    cmd += ",";
                }
            }
            return cmd;
        }

        // Emergency Stop
        private string _StopEmergency()
        {
            return "L:E";
        }
        // Stop
        private string _Stop(Axis[] axes, bool[] valueArray)
        {
            
            string cmd = "L:";
            foreach (Axis axis in Enum.GetValues(typeof(Axis)))
            {
                int index = Array.IndexOf(axes, axis);
                if (index == -1)
                {
                    cmd += ",";
                }
                else
                {
                    cmd += valueArray[index] ? "1" : "0";
                    cmd += ",";
                }
            }
            return cmd;
        }

        // Get the status "Q"   : Current Position
        private string _StatusCurPos()
        {
            return "Q:";
        }

        // Get the status "!"   : Busy or Ready
        private string _StatusIsBusy()
        {
            return "!:";
        }
        // Get the status "Q:S" : Alarms, Sensors
        private string _StatusSensor()
        {
            return "Q:S";
        }

        // Get the status "?"   : 
        public string _StatusInfo(string Para)
        {
            return "?:" + Para;
        }

        // Set Speed
        private string _SetSpeed(Axis axis, int slow, int fast, int rate)
        {
            string cmd;

            if (slow < 1 || slow > 999999999 || fast < 1 || fast > 999999999 || rate < 0 || rate > 1000)
            {
                return "";
            }

            if (_axes.Contains(axis))   // Each axis    // Hit Mode는 Multi axis으로 Set Speed 못함
            {
                cmd = $"D:{(int)axis},{Math.Abs(slow)},{Math.Abs(fast)},{Math.Abs(rate)}";
                return cmd;
            }
            else
            {
                return "";
            }  
        }
        #endregion
    }
}

