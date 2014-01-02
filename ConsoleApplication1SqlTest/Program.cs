using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace ConsoleApplication1SqlTest
{
    class Program
    {
        #region using WTSSendMessage
        public static IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

        public static int ShowMessageBoxFromWTSSendMessage(string message, string title, int timeOutInSec, int style)
        {
            int resp = 0;
            WTSSendMessage(
                WTS_CURRENT_SERVER_HANDLE,
                WTSGetActiveConsoleSessionId(),
                title, title.Length,
                message, message.Length,
                style, timeOutInSec, out resp, true);
            return resp;
        }
        

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        public static extern bool WTSSendMessage(
            IntPtr hServer,
            int SessionId,
            String pTitle,
            int TitleLength,
            String pMessage,
            int MessageLength,
            int Style,
            int Timeout,
            out int pResponse,
            bool bWait);
        #endregion


        static void Main(string[] args)
        {
            ShowMessageBoxFromWTSSendMessage("test", "title", 5, 0x30);
            SqlClient sql_client = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
            while (!sql_client.connect())
            {
            }
            
            Console.WriteLine("press any key to exist...");
            Console.Read();

        }
    }
}
