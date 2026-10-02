using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotorizedStage_SK_PI
{
    public enum Axis
    {
        X, Y, Z, TX, TY, TZ, ALL
    }
    public class AxisStatus
    {
        public Axis Axis { get; set; }
        public double Position {  get; set; }   // 주기적으로 업데이트   // um or min
        public double Speed { get; set; }   // 설정할때 업데이트
        public int Alarm {  get; set; } // 주기적으로 업데이트
        public bool IsMoving { get; set; }  // 주기적으로 업데이트
        public bool IsPosLimit { get; set; }    // 주기적으로 업데이트
        public bool IsNegLimit { get; set; }    // 주기적으로 업데이트

        public AxisStatus(Axis axis) 
        {
            Axis = axis;
        }
    }
}
