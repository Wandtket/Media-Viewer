using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;


namespace MediaViewer.Extensions
{
    public class CameraExtensions
    {
        [DllImport("\\Dependencies\\w32-pthreads.dll", EntryPoint = "aiSetGimbalMotorAngleR", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 aiSetGimbalMotorAngleR(float pitch, float yaw, float roll = -1000f);



        [DllImport("\\Dependencies\\w32-pthreads.dll", EntryPoint = "aiRstGimbalBootPosR", CallingConvention = CallingConvention.Cdecl)]
        public static extern Int32 aiRstGimbalBootPosR();
    }
}
