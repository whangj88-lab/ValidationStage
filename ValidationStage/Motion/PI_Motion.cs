using PI;
using System;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace MotorizedStage_SK_PI
{
    public class PI_Motion
    {

        private int _deviceId = -1;
        private const int PI_RESULT_FAILURE = 0;
        private const int PI_TRUE = 1;
        private const int PI_FALSE = 0;
        private enum PI_Axis 
        {
            X, Y, Z, U, V, W
        };

        private const int _cntMaxAxis = 6;
        private PI_Axis[] _allAxes = new PI_Axis[_cntMaxAxis] { PI_Axis.X, PI_Axis.Y, PI_Axis.Z, PI_Axis.U, PI_Axis.V, PI_Axis.W };

        private PI_Axis[] _axes = null;
        private int _cntAxis;
        private AxisStatus[] _statuses;
        private readonly object _axisLock = new object();

        private CancellationTokenSource _cancelTokenSource;
        public event EventHandler<string> Logged = null;
        public event EventHandler<bool> OnConnectionChange = null;
        public event EventHandler<AxisStatus[]> OnStatusChanged = null;

        private double[] _pivot = new double[3];
        private double[] _coordinateSystem = new double[6];
        public double[] DefaultSpeedLevelValues = new double[3] { 0.2, 3.0, 10.0 };
        
        public bool IsConnected
        {
            get
            {
                if (PI_GCS2.IsConnected(_deviceId) == PI_TRUE) return true;
                else return false;
            }
        }

        public bool Connect(Axis[] axes, string hostName = "localhost")
        {

            try
            {
                if (axes.Length > _cntMaxAxis)
                {
                    throw new Exception($": The number of axes exceeds the maximum allowed limit of {_cntMaxAxis}.");
                }

                _cntAxis = axes.Length;
                _axes = new PI_Axis[_cntAxis];
                for (int i = 0; i < _cntAxis; i++)
                {
                    _axes[i] = (PI_Axis)axes[i];
                }

                Logged?.Invoke(this, "-- Try to Connect --");
                OpenConnect(hostName);
                _statuses = new AxisStatus[_cntAxis];
                for (int i = 0; i < _cntAxis; i++)
                {
                    _statuses[i] = new AxisStatus(axes[i]);
                }

                _cancelTokenSource = new CancellationTokenSource();
                Task.Run(() => PollingStatus(_cancelTokenSource.Token));
                // UpdatePivot();
                UpdateSpeedStatus();
                OnConnectionChange?.Invoke(this, true);
                Logged?.Invoke(this, ": Successfully connected.");
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, $"Error : Connect \n\t{ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                _cancelTokenSource.Cancel();
                Thread.Sleep(100);
                CloseConnect();
                OnConnectionChange?.Invoke(this, false);
                Logged?.Invoke(this, ": Disconnected");
            }
        }

        public async Task<bool> MoveAsync(Axis[] axes, double[] positions, bool isAbsolute)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not Connected");
                }

                if (axes.Length != positions.Length || axes.Length > _cntMaxAxis)
                {
                    throw new Exception("Move Command: Axis Length");
                }

                if (IsMoving)
                {
                    throw new Exception($"Move Command is not ready");
                }

                PI_Axis[] pI_Axes = new PI_Axis[axes.Length];
                for (int i = 0; i < pI_Axes.Length; i++)
                {
                    pI_Axes[i] = (PI_Axis)axes[i];
                }
                string sAxes = string.Join(" ", pI_Axes);

                for(int i  = 0; i < pI_Axes.Length; i++)
                {
                    if (pI_Axes[i] < PI_Axis.U)
                    {
                        positions[i] *= 0.001;  // um -> mm
                    }
                    else
                    {
                        positions[i] /= -60;     // min -> deg
                    }
                }

                int ret;
                if (isAbsolute)
                {
                    ret = PI_GCS2.MOV(_deviceId, sAxes, positions);
                }
                else
                {
                    ret = PI_GCS2.MVR(_deviceId, sAxes, positions);
                }


                if (ret == PI_RESULT_FAILURE)
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
                    throw new Exception("Not Connected");
                }

                if (IsMoving)
                {
                    throw new Exception($"JogMove Command is not ready");
                }

                PI_Axis pI_Axis= (PI_Axis)axis;
                if (pI_Axis >= PI_Axis.U)
                {
                    dir = !dir;
                }

                // 이동 가능한 범위 조회해서 pos에 넣어주기
                double pos = 0.100; // 0.1mm , 0.1deg
                if (!dir)
                {
                    pos *= -1;
                }
                int ret = PI_GCS2.MVR(_deviceId, pI_Axis.ToString(), new double[1] { pos }); // 0.1mm 수정하기 

                if (ret == PI_RESULT_FAILURE)
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
                PI_Axis pI_Axis = (PI_Axis)axis;
                int ret = PI_GCS2.HLT(_deviceId, pI_Axis.ToString());

                if (ret == PI_RESULT_FAILURE)
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

        public async Task<bool> ReferenceAsync()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not Connected");
                }

                if (IsMoving)
                {
                    throw new Exception($"Reference Command is not ready");
                }

                string sAxes = string.Join(" ", _allAxes);
                int ret = PI_GCS2.FRF(_deviceId, sAxes);

                if (ret == PI_RESULT_FAILURE)
                {
                    throw new Exception("Failed Reference Command");
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
                if (axes.Length != stopCmd.Length || axes.Length > _cntMaxAxis)
                {
                    throw new Exception("Stop Command: Axis Length");
                }

                PI_Axis[] pI_Axes = new PI_Axis[axes.Length];
                for (int i = 0; i < pI_Axes.Length; i++)
                {
                    pI_Axes[i] = (PI_Axis)axes[i];
                }
                string sAxes = string.Join(" ", pI_Axes);
                int ret = PI_GCS2.HLT(_deviceId, sAxes);

                if (ret == PI_RESULT_FAILURE)
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
                int ret = PI_GCS2.STP(_deviceId);
                if (ret == PI_RESULT_FAILURE)
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

        public double[] GetAllPositions()
        {
            if (!IsConnected)
            {
                throw new Exception("Not connected");
            }

            string sAllAxes = string.Join(" ", _allAxes);
            double[] positions = new double[_cntMaxAxis];
            int ret = PI_GCS2.qPOS(_deviceId, sAllAxes, positions);
            if (ret == PI_RESULT_FAILURE)
            {
                throw new Exception($"Failed to retrieve Position.\r\n");
            }

            for (int axis = 0; axis < _cntMaxAxis; axis++)
            {
                if (axis < (int)PI_Axis.U)
                {
                    positions[axis] *= 1000;   // um
                }
                else
                {
                    positions[axis] *= -60;     // min
                }
                positions[axis] = Math.Round(positions[axis], 4);
            }

            return positions;
        }

        public bool SetSpeed(double fast)
        {
            try
            {
                if (IsMoving)
                {
                    throw new Exception($"SetSpeed Command is not ready");
                }

                int ret = PI_GCS2.VLS(_deviceId, fast);

                if (ret == PI_RESULT_FAILURE)
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
        public double[] GetSpeeds()
        {
            try
            {
                if (!IsConnected)
                {
                    //bool Really = PI_GCS2.IsConnected(_deviceId);
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
        
        public bool SetPivot(double x, double y, double z)
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("Not Connected");
                }
                if (IsMoving)
                {
                    throw new Exception($"SetPivot(SPI) Command Command is not ready");
                }

                if (PI_GCS2.SPI(_deviceId, "X Y Z", new double[] { x, y, z }) == PI_RESULT_FAILURE)
                {
                    throw new Exception("SetPivot(SPI) Command failed. ");
                }

                UpdatePivot();
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }
        public double[] GetPivot()
        {
            return _pivot.Select(pivot => pivot).ToArray();
        }
        public double[] GetCoordinateSystem()
        {
            return _coordinateSystem.Select(cs => cs).ToArray();
        }
        public bool SetCoordinateSystem(double x, double y, double z, double u, double v, double w)
        {
            x *= 0.001;    //  mm 로 바꾼다
            y *= 0.001;    //  mm 로 바꾼다
            z *= 0.001;    //  mm 로 바꾼다
            u /= 60;    //  deg 로 바꾼다
            v /= 60;    //  deg 로 바꾼다
            w /= 60;    //  deg 로 바꾼다

            try
            {
                // 연결됐는지 확인
                if (!IsConnected)
                {
                    throw new Exception("Not Connected");
                }
                // 동작중인지 확인
                if (IsMoving)
                {
                    throw new Exception($"SetCoordinateSystem Command Command is not ready");
                }
                
                // 현재 활성 좌표계인지 확인
                string csName = "TILTEDCS";
                StringBuilder buffer = new StringBuilder(1024); // 결과를 저장할 버퍼
                int bufsize = buffer.Capacity; // 버퍼 크기
                int ret = PI_GCS2.qKEN(_deviceId, csName, buffer, bufsize);

                // csName가 활성 좌표계 이라면 비활성(ZERO 좌표계 활성화)
                if (ret != PI_RESULT_FAILURE && buffer.ToString().Contains(csName))
                {
                    if (PI_GCS2.KEN(_deviceId, "ZERO") == PI_RESULT_FAILURE)
                    {
                        throw new Exception($"KEN Command is not Error");
                    }
                }
                // 좌표계 재정의
                ret = PI_GCS2.KSD(_deviceId, csName, "X Y Z U V W", new double[] { x, y, z, -u, -v, -w });
                if (ret == PI_RESULT_FAILURE)
                {
                    throw new Exception($"KSD Command is not Error");
                }

                // 좌표계 활성화
                if (PI_GCS2.KEN(_deviceId, csName) == PI_RESULT_FAILURE)
                {
                    throw new Exception($"KEN Command is not Error");
                }

                // 좌표계 적용 확인하기.
                buffer.Clear();
                ret = PI_GCS2.qKEN(_deviceId, csName, buffer, bufsize);
                if (ret == PI_RESULT_FAILURE)
                {
                    throw new Exception($"Coordinate system '{csName}' is not active.");
                }

                UpdateCoordinateSystem(csName);
                return true;
            }
            catch (Exception ex)
            {
                Logged?.Invoke(this, ex.Message);
                return false;
            }
        }

        public AxisStatus[] GetAxisStatuses()
        {
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
            while (true)
            {
                UpdateMovingStatus();

                if (!IsMoving) return;

                await Task.Delay(10);  // 빠른 주기로 상태 확인
            }
        }

        // Update Statuses
        private async Task PollingStatus(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(100, token);
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

        private void UpdateMovingStatus()
        {
            //--Current Position--//
            double[] positions = UpdatePositions();
            //--- Is Moving---//
            bool[] isMovings = UpdateIsMovings();

            UpdateStatuses(positions, isMovings);
        }
        private void UpdateGeneralStatus()
        {
            double[] positions = UpdatePositions();
            bool[] isMovings = UpdateIsMovings();
            int[] alarms = null;
            bool[] isPosLimits = null;
            bool[] isNegLimits = null;
            UpdateStatuses(positions, isMovings, alarms, isPosLimits, isNegLimits);
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
        private void UpdateSpeedStatus()
        {
            if (!IsConnected)
            {
                throw new Exception("Not Connected.");
            }

            //--Velocity--//
            double velocity = 0.0;
            int ret = PI_GCS2.qVLS(_deviceId, ref velocity);
            if (ret == PI_RESULT_FAILURE)
            {
                throw new Exception($"Failed to retrieve Position.\r\n");
            }

            lock (_axisLock)
            {
                bool isChanged = false;
                for (int i = 0; i < _cntAxis; i++)
                {
                    if (velocity != 0 && _statuses[i].Speed != velocity)
                    {
                        _statuses[i].Speed = velocity;
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
            if (!IsConnected)
            {
                throw new Exception("Not Connected.");
            }

            string sAxes = string.Join(" ", _axes);
            //--Current Position--//
            double[] positions = new double[_cntAxis];
            int ret = PI_GCS2.qPOS(_deviceId, sAxes, positions);
            if (ret == PI_RESULT_FAILURE)
            {
                throw new Exception($"Failed to retrieve Position.\r\n");
            }

            for (int i = 0; i < _cntAxis; i++)
            {
                if (_axes[i] < PI_Axis.U)
                {
                    positions[i] *= 1000;   // um
                }
                else
                {
                    positions[i] *= -60;     // min
                }
                positions[i] = Math.Round(positions[i], 3);
            }

            return positions;

        }
        private bool[] UpdateIsMovings()    // 다축 get 되는지 확인필요
        {
            if (!IsConnected)
            {
                throw new Exception("Not Connected.");
            }

            string sAxes = string.Join(" ", _axes);

            //--- Is Moving---//
            int[] iIsMovings = new int[_cntAxis];
            int ret = PI_GCS2.IsMoving(_deviceId, sAxes, iIsMovings);
            if (ret == PI_RESULT_FAILURE)
                throw new Exception($"Failed to retrieve IsMoving.\r\n");

            bool[] isMovings = iIsMovings.Select(isMoving => isMoving == PI_TRUE ? true : false).ToArray();
            return isMovings;
        }
        private int[] UpdateAlarms()
        {
            if (!IsConnected)
            {
                throw new Exception("Not Connected.");
            }
            //--- Alarm ---//
            int alarm = 0;
            int ret = PI_GCS2.qERR(_deviceId, ref alarm);
            if (ret == PI_RESULT_FAILURE)
                throw new Exception($"Failed to retrieve Alarm.\r\n");

            if (alarm != 0)
            {
                //--Skip case--//
                if (alarm == 1005)
                {
                    alarm = 0;
                }
            }

            int[] alarms = Enumerable.Repeat(alarm, _cntAxis).ToArray();
            return alarms;
        }

        //private bool[] UpdateLimits()
        //{
        //    string sAxes = string.Join(" ", _axes);
        //    int[] limit = new int[_cntAxis];

        //    //int ret = PI_GCS2.qLIM(_deviceId, sAxes, limit);  // 설치되어있는지 확인하는거임.

        //    //if (ret == PI_RESULT_FAILURE)
        //    //    throw new Exception($"Failed to retrieve Limit.\r\n error :{ret}");

        //    //for(int i = 0; i < _cntAxis; i++)
        //    //{
        //    //    limit[i].IsPlusLimitOn = limit[1] == 1;
        //    //    limit[i].IsMinusLimitOn = limit[0] == 1;
        //    //}

        //}
        // TCP/IP Open Connect

        private void UpdatePivot()
        {
            if (PI_GCS2.qSPI(_deviceId, "X Y Z", _pivot) == PI_RESULT_FAILURE)
            {
                Logged?.Invoke(this, "GetPivot(SPI) failed. ");
            }
        }
        private void UpdateCoordinateSystem(string csName)
        {
            StringBuilder buffer = new StringBuilder(1024); // 결과를 저장할 버퍼
            int bufsize = buffer.Capacity; // 버퍼 크기

            //if (PI_GCS2.qKLT(_deviceId, csName, "ZERO", buffer, bufsize) == PI_RESULT_FAILURE)
            //{
            //    Logged?.Invoke(this, "GetCoordinateSystem(qKLT) failed. ");
            //}
            //string[] rCmd = buffer.ToString().Split(new string[] { "\t" }, StringSplitOptions.None);
            //if (!rCmd[0].Contains(csName))
            //{
            //    Logged?.Invoke(this, $"SetCoordinateSystem failed. {rCmd[0]}");
            //}

            if (PI_GCS2.qKEN(_deviceId, csName, buffer, bufsize) == PI_RESULT_FAILURE)
            {
                throw(new Exception("GetCoordinateSystem(qKEN) failed. "));
            }

            buffer.Clear();
            if (PI_GCS2.qKLS(_deviceId, csName, "POS", null, buffer, bufsize) == PI_RESULT_FAILURE)
            {
                throw (new Exception("GetCoordinateSystem(qKEN) failed. "));
            }

            string[] rCmd = buffer.ToString().Split(' ');


            for (int i = 0;i < _cntMaxAxis; i++)
            {
                string sPos = rCmd[i + 1].Split('"')[1];
                double pos = double.Parse(sPos);
                if (i >= 3) 
                {
                    pos *= -60;  // min
                }
                else
                {
                    pos *= 1000;  // um
                }
                _coordinateSystem[i] = pos;
            }
        }

        private void OpenConnect(string hostName = "localhost")
        {
            try
            {
                if (IsConnected)
                {
                    throw new Exception("The port is already open.");
                }
                else
                {
                    _deviceId = PI_GCS2.ConnectTCPIP(hostName, 50000);
                    if (_deviceId == -1)
                    {
                        throw new Exception("Connection fail");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        // TCP/IP Close Connect
        private void CloseConnect()
        {
            try
            {
                if (!IsConnected)
                {
                    throw new Exception("The port is already closed");
                }

                if (_deviceId >= 0)
                {
                    PI_GCS2.CloseConnection(_deviceId);
                    _deviceId = -1;
                }
            }
            catch
            {
                throw new Exception("Error occurred at the time of the device close.");
            }
        }
        #endregion
    }
}
