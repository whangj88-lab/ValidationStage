using System;
using System.Threading;
using System.Windows.Forms;

namespace ValidationStage
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (var mutex = new Mutex(true, "ValidationStage_SingleInstance", out bool isNew))
            {
                if (!isNew)
                {
                    MessageBox.Show("이미 실행 중입니다.", "Motion + Probe Station", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new F_Main());
            }
        }
    }
}
