using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;
using System.Configuration;
using System.Net;
using System.ComponentModel;
using System.IO;
using System.Collections;
using System.Timers;
using log4net;
using log4net.Config;
using System.Globalization;
using System.Data;
/*--------------------------------------------------
 * 2014/02/21
 * 
 * Todo change:get site value from "Requester's Affiliated Site" in condition :Call Activity Update - Start of Call<- for all call type
 *---------------------------------------------------
 * get channel condition
 * "Call Activity Update"->"Active RF Sites/Channel Info List" only-
 * private call :Start of Call
 * group call : Start of Call
 * land<->mobile : 
 * 1.Start of Call->get channel/site and uid by Radio Type Qualifier
 * 2.Interconnect Call Billing Info Packet-> get target, communication time period by call type
 * 
 * private_call:Radio Type Qualifier = (Astro call),Device Type = Radio
 * radioToLan:Radio Type Qualifier = (Interconnect,Astro call),Device Type = Radio
 * lanToRadio:Radio Type Qualifier = (Interconnect,Landline call,Astro call),Device Type = Landline
 * 
 * callType:
 * Individual_Call:2
 * Group_Call:1
 * Land_to_Mobile:4
 * Mobile_to_Land:3
 * **/

namespace ATIA_2
{
    class Program
    {
        //every device has only one uid , so to use uid just fine without gid or snd_id
        const bool access_avls_server_send_timestamp_now = false;
        private const bool avlsGetLastLocation = true;

        private static object fileStreaWriteLock = new object();
        private static Mutex mut = new Mutex();
        static object SpinLock = new object();
            //static Byte[] receiveBytes;
            //static ATIA_PACKAGE_Header_and_NumOffset struct_header = new ATIA_PACKAGE_Header_and_NumOffset();
            //static string returnData;
            //static Hashtable parse_package = new Hashtable();//cmd,opcode,result,uid,timestamp
            //static SortedDictionary<string, string> parse_package = new SortedDictionary<string, string>();
            const int OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS = 18;
            const int DEVIATION_OF_OFFSET_FIELDS_OF_VALUES = 0;
            private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            private static Hashtable Power_status = new Hashtable();
            private static Hashtable Call_status = new Hashtable();
           

            // ManualResetEvent instances signal completion.
            private static ManualResetEvent connectDone =
                new ManualResetEvent(false);
            private static ManualResetEvent sendDone =
                new ManualResetEvent(false);
            private static ManualResetEvent receiveDone =
                new ManualResetEvent(false);


            public struct AUTO_SQL_DATA
            {

                public string _id;
                public string _uid;
                public string _status;//max 2 length
                public string _time;
                public string _validity;
                public string _lat;
                public string _lon;
                public string _altitude;
                public string _speed;
                public string _course;
                public string _distance;
                //
                public string _or_lon;
                public string _or_lat;
                public string _satellites;
                public string _temperature;
                public string _voltage;
                //
                public string j_5;//radius
                public string j_6;//emergency on/off
                public string j_7;//present/absent
                public string j_8;//Ignition on/Off
                public string _option0;//info-time
                public string _option1;//server-time
                public string _option2;//result-code
                public string _option3;//result_msg , event-info

            }

            public struct AVLS_UNIT_Report_Packet
            {
                public string ID;
                public string GPS_Valid;//only L or A (L->valid , A->not valid)
                /*
                 * The format is: 
                Year month day hour minute second 
                For example, the string 040315131415 means: 
                The date is “Year 2004, March, Day 15 “ 
                and the time is “13:14:15 ” 
                 * */
                public string Date_Time;
                /*
                 * For example, the string N2446.5321E12120.4231 means
    “North 24 degrees 46.5321 minutes 
    “East 121 degrees 20.4231 minutes” 
     Or S2446.5281W01234.5678 means 
    “South 24 degrees 46. 5281 minutes” = “South 24.7755 degrees” = “South 24 
    degrees 46 minutes 31.69 seconds” 
    “West 12 degrees 34.5678 minutes” = “West 12.57613 degrees” = “West 12 
    degrees 34 minutes 34.07 seconds”
                 * */
                public string Loc;
                /*
                 * from 0 to 999
                 * km/hr
                 */
                public string Speed;
                /*
                 the GPS direction in degrees. And this value is between 0 and 359 
    degree. (no decimal)*/
                public string Dir;
                /*
                 * The string provides the temperature in the format UNIT-sign-degrees. (Non-fixed 
    length 0 to 999 and no decimal. 
    For example, 
    the string F103 means “103 degree Fahrenheit” 
    the string C-12 means “-12 degree Celsius” 
    If the UNIT does not include a Temperature sensor, it will report ’NA’.
                 */
                public string Temp;
                /*
                 We use eight ASCII characters to represent 32 bit binary number. Each bit represents 
    a flag in the UNITs status register. The status string will be represented in HEX for 
    each set of the byte. To display a four-byte string, there will be 8 digits string */
                public string Status;//17
                public string Event;//150
                public string Message;
            }

            enum Block_Command_Type_Values
            {
                Flexible_Radio_Command=101,
                Flexible_Command_Status=102,
                Flexible_Command_Control=103,
                Flexible_Database_Request=104,
                Flexible_Database_Response=105,
                Flexible_Call_Activity_Update=106,
                Flexible_End_of_Call=107,
                Flexible_Radio_Status_Traffic=108,
                Flexible_System_Activity_Update=111,
                Flexible_System_Activity_Request=112,
                Flexible_DFB_Channel_Update =113,
                Flexible_Controlling_Zone_Update=114,
                Flexible_Resource_Removed=115,
                Flexible_Mobility_Update=116,
                Flexible_Access_Method_Type_Update =117,
                Flexible_Call_Termination=131,
                Flexible_ZC_Link_Status=132,
                Flexible_Secure_Key_Acknowledgement=133,
                Flexible_Authentication_Logging=134,
                Flexible_Conventional_Call_Activity_Update=135,
                Flexible_Conventional_End_of_Call_Update=136,
                Flexible_Conventional_Mobility_Update=137,
                Flexible_Conventional_Radio_Status_Traffic_Update=138,
                Flexible_Subscriber_Tagging_Activity=140,
                Flexible_Dynamic_Config_Update=141,
                Flexible_Site_Monitor_Update=142,
                Flexible_Interconnect_Call_Billing_Info_Packet = 131 //Flexible Call Termination

            };
            enum Flexible_Controlling_Zone_Update_opcode
            {
                Start_of_Call = 1,
                PTT_ID_Active_Control=2,
                PTT_ID_Active_No_Control=3,
                PTT_ID_Busy_Control=4,
                End_of_Call=7
            };
            enum Flexible_Mobility_Update_opcode
            {
                //power on
                Unit_Registration=1,
                Console_Registration=2,
                Request_for_Registration=3,
                Group_Affiliation =4,//for power on get gid/uid
                Location_Registration=6,
                //power off
                Deregistration=7,
                IncorrectSystemDeregistration =8,
                //power on
                RemoteZoneRegistration=12,
                //power off
                RemoteZoneDeregistration=13
            };
            enum Call_Status
            {
                Global_Active=1,
                Global_Busy=2,
                Active_and_Busy=3,
                Not_Active_or_Busy=4,
                Active_and_Busy_from_FastStart=5
            };
            enum Reason_for_Busy
            {
                No_resources=1,
                Talkgroup_and_Multigroup_Contention=3,
                No_Busy=8
            };
            enum Flexible_Call_Activity_Update_opcode
            {
                Start_of_Call = 1,
                PTT_ID_Update_Active = 2,
                PTT_ID_Update_Active_No_Control = 3,
                PTT_ID_Update_Busy = 4,
                PTT_ID_Update_Busy_No_Control=5,
                Call_State_Change =8
            };
            enum Flexible_End_of_Call_opcode
            { 
            End_of_Call=1
            };

            [StructLayout(LayoutKind.Explicit, Size = 22, CharSet = CharSet.Ansi)]
            public struct ATIA_PACKAGE_Header_and_NumOffset
            {
                /// <summary>
                /// Package total length
                /// </summary>
                [FieldOffset(0)]
                public uint PackageTotalLength;//4 byte
                /// <summary>
                /// Header Section
                /// </summary>
                [FieldOffset(4)]
                public byte BlockSource;//1 byte
                [FieldOffset(5)]
                public byte BlockDestination;//1 byte
                [FieldOffset(6)]
                public ushort BlockLength;// 2 byte
                [FieldOffset(8)]
                public ushort BlockCommandType;// 2 byte
                [FieldOffset(10)]
                public ushort BlockOpcode;// 2 byte
                [FieldOffset(12)]
                public ushort MajorVersion;// 2 byte
                [FieldOffset(14)]
                public ushort MinorVersion;// 2 byte
                [FieldOffset(16)]
                public uint LoggingSequenceNumber;//4 byte
                /// <summary>
                /// Offset Section
                /// </summary>
                [FieldOffset(20)]
                public ushort NumOffsets;// 2 byte
            }

            [StructLayout(LayoutKind.Explicit, Size = 67, CharSet = CharSet.Ansi)]
            public struct ATIA_PACKAGE
            {
                /// <summary>
                /// Header Section
                /// </summary>
                [FieldOffset(0)]
                public byte BlockSource;//1 byte
                [FieldOffset(1)]
                public byte BlockDestination;//1 byte
                [FieldOffset(2)]
                public ushort BlockLength;// 2 byte
                [FieldOffset(4)]
                public ushort BlockCommandType;// 2 byte
                [FieldOffset(6)]
                public ushort BlockOpcode;// 2 byte
                [FieldOffset(8)]
                public ushort MajorVersion;// 2 byte
                [FieldOffset(10)]
                public ushort MinorVersion;// 2 byte
                [FieldOffset(12)]
                public uint LoggingSequenceNumber;//4 byte
                /// <summary>
                /// Offset Section
                /// </summary>
                [FieldOffset(16)]
                public ushort NumOffsets;// 2 byte
                [FieldOffset(18)]
                public ushort OffsettoReservedSection;// 2 byte
                [FieldOffset(20)]
                public ushort OffsettoCallSection;// 2 byte
                [FieldOffset(22)]
                public ushort OffsettoTargetSection;// 2 byte
                [FieldOffset(24)]
                public ushort OffsettoRequesterSection;// 2 byte
                [FieldOffset(26)]
                public ushort OffsettoSecuritySection;// 2 byte
                [FieldOffset(28)]
                public ushort OffsettoAliasSection;// 2 byte
                /// <summary>
                /// Call Section
                /// </summary>
                [FieldOffset(30)]
                [MarshalAs(UnmanagedType.AnsiBStr, SizeConst = 8)]
                public string Timestamp;//8 byte
                [FieldOffset(38)]
                public uint CommandNumber;//4 byte
                [FieldOffset(42)]
                public ushort StatusMessageNumber;// 2 byte
                [FieldOffset(44)]
                public ushort LocalZoneID;// 2 byte
                /// <summary>
                /// Target Section
                /// </summary>
                [FieldOffset(46)]
                public uint TargetPrimaryID;//4 byte
                [FieldOffset(50)]
                public uint TargetSecondaryID;//4 byte
                /// <summary>
                /// Requester Section
                /// </summary>
                [FieldOffset(54)]
                public uint RequesterID;//4 byte
                [FieldOffset(58)]
                public ushort RequesterZoneID;// 2 byte
                [FieldOffset(60)]
                public ushort RequesterSiteID;// 2 byte.
                /// <summary>
                /// Security Section
                /// </summary>
                [FieldOffset(62)]
                public ushort TargetPrimarySecurityID;// 2 byte.
                [FieldOffset(64)]
                public ushort TargetSecondarySecurityID;// 2 byte.
                [FieldOffset(66)]
                public ushort RequesterSecurityID;// 2 byte.
                /// <summary>
                /// Alias Section
                /// </summary>
                [FieldOffset(68)]
                public byte AliasEncoding;//1 byte
                [FieldOffset(42)]
                public ushort NumAliasOffsets;// 2 byte.
                [FieldOffset(44)]
                public ushort OffsettoRequesterAlias;// 2 byte.
                [FieldOffset(46)]
                public ushort OffsettoTargetAlias;// 2 byte.
                [FieldOffset(48)]
                public ushort SizeofRequesterAlias;// 2 byte.
                [FieldOffset(50)]
                [MarshalAs(UnmanagedType.AnsiBStr, SizeConst = 7)]
                public string RequesterAlias;//7 byte
                [FieldOffset(57)]
                public ushort SizeofTargetAlias;// 2 byte.
                [FieldOffset(59)]
                [MarshalAs(UnmanagedType.AnsiBStr, SizeConst = 8)]
                public string TargetAlias;//7 byte
            }
            private static IPAddress GetLocalIPAddress()
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var addr = ni.GetIPProperties().GatewayAddresses.FirstOrDefault();
                    if (addr != null && !addr.Address.Equals(new IPAddress(0x00000000)))
                    {
                        if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                        {
                            //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+ni.Name);
                            //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+addr.Address);
                            foreach (UnicastIPAddressInformation ip in ni.GetIPProperties().UnicastAddresses)
                            {
                                if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+ip.Address.ToString());
                                    return ip.Address;
                                }

                            }
                        }

                    }
                }
                return null;
            }
            private static void ConvertLocToAvlsLoc(ref string lat, ref string lon)
            {
                double tmpLat = double.Parse(lat);
                double tmpLon = double.Parse(lon);
                double latInt = Math.Truncate(tmpLat);
                double lonInt = Math.Truncate(tmpLon);
                double latNumberAfterPoint = tmpLat - latInt;
                double lonNumberAfterPoint = tmpLon - lonInt;
                lat = ((latNumberAfterPoint * 60 / 100 + latInt) * 100).ToString();
                lon = ((lonNumberAfterPoint * 60 / 100 + lonInt) * 100).ToString();
            }
            static void Main(string[] args)
            {
                byte[] testByte = new byte[]{0x04, 0x10,0x00,0x00};
                parse_Radio_Type_Qualifier(testByte);
              Console.WriteLine(GetLocalIPAddress());//current ip address
              Console.WriteLine(System.Environment.UserName);//current username
              Console.WriteLine(DateTime.Now.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss"));
              Console.WriteLine(DateTime.Now.ToString("O"));
              Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
                DateTime test = DateTime.Now;
                string test1 = test.ToString("yyyyMMddHHmmss").Substring(2, 12);
                DateTime tempDatetime = DateTime.ParseExact(test1, "yyMMddHHmmss", null);
                tempDatetime = tempDatetime.ToUniversalTime();
                //Thread read_thread = new Thread(() => read_thread_method(tcpClient, netStream, sql_client));
                Thread udp_server_8671 = new Thread(() => udp_server_t(int.Parse(ConfigurationManager.AppSettings["ATIA_SERVER_PORT_8671"]))); //(new ThreadStart(udp_server_t));
                Thread udp_server_8601 = new Thread(() => udp_server_t(int.Parse(ConfigurationManager.AppSettings["ATIA_SERVER_PORT_8601"])));
                udp_server_8601.Name = "thread_8601";
                udp_server_8671.Name = "thread_8671";
                udp_server_8671.Start();
                udp_server_8601.Start();

                var aTimer = new System.Timers.Timer(int.Parse(ConfigurationManager.AppSettings["aTimer_interval_sec"])*1000);
                aTimer.Elapsed += new ElapsedEventHandler(SendToAvlsEventColumnSetNegativeOneIfPowerOff);
                aTimer.Enabled = false;
                Console.ReadLine();
            }

        private static void SendToAvlsEventColumnSetNegativeOneIfPowerOff(object sender, ElapsedEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
            var sqlClient = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
            DataTable dt = new DataTable();
            /*select DISTINCT ON (1) 
custom.turn_onoff_log.uid,
custom.turn_onoff_log.create_time 
FROM
custom.turn_onoff_log
  INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.on_time IS NOT NULL AND
custom.turn_onoff_log.off_time is NOT NULL and 
  custom.turn_onoff_log.create_time < current_timestamp- interval '1 hour'
and custom.turn_onoff_log.uid not in
(
SELECT DISTINCT
R.uid
FROM
(SELECT  DISTINCT ON (1)
	custom.turn_onoff_log.uid,
  custom.turn_onoff_log.create_time
FROM
  custom.turn_onoff_log
  INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.on_time IS NOT NULL AND
custom.turn_onoff_log.off_time is NULL and 
  custom.turn_onoff_log.create_time < current_timestamp- interval '1 hour' 
  ORDER  BY 1, 2 DESC
) AS R
)*/

            string sqlCmd = @"select DISTINCT ON (1) 
custom.turn_onoff_log.uid,
custom.turn_onoff_log.create_time 
FROM
custom.turn_onoff_log
  INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.on_time IS NOT NULL AND
custom.turn_onoff_log.off_time is NOT NULL and 
  custom.turn_onoff_log.create_time < current_timestamp- interval '" + ConfigurationManager.AppSettings["setNegativeOneToAvlsInterval"] + @"'
and custom.turn_onoff_log.uid not in
(
SELECT DISTINCT
R.uid
FROM
(SELECT  DISTINCT ON (1)
	custom.turn_onoff_log.uid,
  custom.turn_onoff_log.create_time
FROM
  custom.turn_onoff_log
  INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.on_time IS NOT NULL AND
custom.turn_onoff_log.off_time is NULL and 
  custom.turn_onoff_log.create_time < current_timestamp- interval '" + ConfigurationManager.AppSettings["setNegativeOneToAvlsInterval"] + @"' 
  ORDER  BY 1, 2 DESC
) AS R
)";
            while (!sqlClient.connect())
            {
                Thread.Sleep(300);
            }
            
            dt = sqlClient.get_DataTable(sqlCmd);
            sqlClient.disconnect();
            string uid = string.Empty;
            if (dt != null && dt.Rows.Count != 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    uid = row[0].ToString();
                    SendPackageToAvlsOnlyByUidAndLocGetFromSql(uid,"-1");
                }
            }

        }

        private static void SendPackageToAvlsOnlyByUidAndLocGetFromSql(string uid, string eventStatus)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us");
            TcpClient avls_tcpClient;
            string send_string = string.Empty;
            AVLS_UNIT_Report_Packet avls_package = new AVLS_UNIT_Report_Packet();
            //string ipAddress = "127.0.0.1";
            string ipAddress = ConfigurationManager.AppSettings["AVLS_SERVER_IP"];
            //int port = 23;
            int port = int.Parse(ConfigurationManager.AppSettings["AVLS_SERVER_PORT"]);

            avls_tcpClient = new TcpClient();
            connectDone.Reset();
            avls_tcpClient.BeginConnect(ipAddress, port, new AsyncCallback(AvlsConnectCallback), avls_tcpClient);
            connectDone.WaitOne();

            //avls_tcpClient.NoDelay = false;

            //Keeplive.keep(avls_tcpClient.Client);
            NetworkStream netStream = avls_tcpClient.GetStream();
            avls_package.Event = eventStatus+",";
            avls_package.Date_Time = string.Format("{0:yyMMddHHmmss}", DateTime.Now.ToUniversalTime()) + ",";
            avls_package.ID = uid;
            avls_package.GPS_Valid = "A,";

            if (avlsGetLastLocation)
            {
                var avlsSqlClient = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
                string avlsSqlCmd = @"SELECT 
  public._gps_log._lat,
  public._gps_log._lon
FROM
  public._gps_log
WHERE
  public._gps_log._time < now() AND 
  public._gps_log._uid = '" + avls_package.ID + @"'
ORDER BY
  public._gps_log._time DESC
LIMIT 1";
                log.Info("avlsSqlCmd=" + Environment.NewLine + avlsSqlCmd);
                while (!avlsSqlClient.connect())
                {
                    Thread.Sleep(300);
                }
                
                var dt = avlsSqlClient.get_DataTable(avlsSqlCmd);
                avlsSqlClient.disconnect();
                if (dt != null && dt.Rows.Count != 0)
                {
                    string avlsLat = string.Empty, avlsLon = string.Empty;
                    foreach (DataRow row in dt.Rows)
                    {
                        avlsLat = row[0].ToString();
                        avlsLon = row[1].ToString();
                    }
                    string zero = "0";
                    if (avlsLat.Equals(zero) || avlsLon.Equals(zero))
                    {
                        GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                    }
                    //GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                    //GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                    //string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                    //string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                    //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                    string lat_str = avlsLat, long_str = avlsLon;
                    ConvertLocToAvlsLoc(ref lat_str,ref long_str);
                    avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                }
                else
                {
                    string avlsLat = string.Empty, avlsLon = string.Empty;
                    GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                    //GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                    //GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                    //string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                    //string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                    //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                    string lat_str = avlsLat, long_str = avlsLon;
                    ConvertLocToAvlsLoc(ref lat_str, ref long_str); 
                    avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                    //avls_package.Loc = "N00000.0000E00000.0000,";
                }
            }
            else
            {
                string avlsLat = string.Empty, avlsLon = string.Empty;
                GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                //avls_package.Loc = "N00000.0000E00000.0000,";
            }

            avls_package.Speed = "0,";
            avls_package.Dir = "0,";
            avls_package.Temp = "NA,";
            avls_package.Status = "00000000,";
            switch (eventStatus)
            {
                case "-1":
                    avls_package.Message = "power_off";
                    break;
                default:
                    avls_package.Message = "test";
                    break;

            }
            

            //}
            avls_package.ID += ",";
            send_string = "%%" + avls_package.ID + avls_package.GPS_Valid + avls_package.Date_Time + avls_package.Loc + avls_package.Speed + avls_package.Dir + avls_package.Temp + avls_package.Status + avls_package.Event + avls_package.Message + "\r\n";


            sendDone.Reset();
            avls_WriteLine(netStream, System.Text.Encoding.Default.GetBytes(send_string), send_string);
            sendDone.WaitOne();

            //ReadLine(avls_tcpClient, netStream, send_string.Length);
            netStream.Close();
            avls_tcpClient.Close();
            log.Info("-AccessAvlsServer:if");
            Console.WriteLine("-AccessAvlsServer:if");
        }

        static void udp_server_t(int port)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-us"); 
                UdpClient udpClient = new UdpClient(port);
                //IPEndPoint object will allow us to read datagrams sent from any source.
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                //int c = int.Parse(ConfigurationManager.AppSettings["raw_log_counter"].ToString());

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    Console.WriteLine(Thread.CurrentThread.Name);
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(DateTime.Now.ToString("O"));
                    Console.ResetColor();
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    byte[] receiveBytes_original = new byte[receiveBytes.Length];
                    SortedDictionary<string, string> parse_package = new SortedDictionary<string, string>();
                    Array.Copy(receiveBytes, receiveBytes_original, receiveBytes_original.Length);
                    
                    //lock (fileStreaWriteLock)
                    /*
                    {
                        mut.WaitOne();
                        if (bool.Parse(ConfigurationManager.AppSettings["log_raw_data"]))
                        {

                            //using (var stream = new FileStream("raw" + c + ".txt", FileMode.Append))
                          Console.WriteLine("write to file:" + "raw" + DateTime.Now.ToString("yyyy-MM-dd_H.mm.ss.fffffff") + ".atia");
                            using (
                                var stream =
                                    new FileStream(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)+@"\"+
                                        "raw" + DateTime.Now.ToString("yyyy-MM-dd_H.mm.ss.fffffff") + ".atia",
                                        FileMode.Append))
                            {
                                stream.Write(receiveBytes, 0, receiveBytes.Length);
                                stream.Close();
                            }
                            

                        }
                        mut.ReleaseMutex();
                    }
                    */
                    //log.Info(ByteToHexBitFiddle(receiveBytes));
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+BitConverter.ToString(receiveBytes).Replace("-"," "));
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+ByteToHexBitFiddle(receiveBytes));

                    if (receiveBytes.Length != BitConverter.ToUInt32(receiveBytes.Skip(0).Take(4).Reverse().ToArray(), 0) + 4)
                    {
                      Console.WriteLine("size embedded in the packet does not match bytes received");
                        continue;
                    }

                    //c++;
                    //Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    //configuration.AppSettings.Settings["raw_log_counter"].Value = c.ToString();
                    //configuration.Save();
                    //ConfigurationManager.RefreshSection("appSettings");

                    string returnData = Encoding.ASCII.GetString(receiveBytes);
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"receive_length=" + receiveBytes.Length);
                    array_reverse_ATIA_PACKAGE_Header_and_NumOffset(ref receiveBytes);
                    ATIA_PACKAGE_Header_and_NumOffset struct_header = new ATIA_PACKAGE_Header_and_NumOffset();
                    struct_header = (ATIA_PACKAGE_Header_and_NumOffset)BytesToStruct(receiveBytes, struct_header.GetType());
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"package lenght exclude first 4 byte :"+BitConverter.ToUInt32(receiveBytes.Skip(0).Take(4).Reverse().ToArray(), 0)); 

                    // Uses the IPEndPoint object to determine which of these two hosts responded.
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"This is the message you received :" +
                                                 //returnData.ToString());
                    parse_header_and_numoffset_package(struct_header, ref parse_package);

                    parse_data_section(receiveBytes.Skip(4).ToArray(), ref parse_package);//skip first 4 package_length byte

                    if (parse_package.ContainsKey("sec"))
                    if (parse_package.ContainsValue("Land_to_Mobile") || parse_package.ContainsValue("Mobile_to_Land"))
                    {
                        //get start call timestamp
                        DateTimeFormatInfo myDateTimeFormat = new CultureInfo("zh-TW", false).DateTimeFormat;


                        myDateTimeFormat.FullDateTimePattern = "yyyyMMddHHmmssffff";


                        DateTime _EndTime = DateTime.ParseExact(parse_package["timestamp"].ToString(), myDateTimeFormat.FullDateTimePattern, myDateTimeFormat);
                        double sec = Convert.ToDouble(parse_package["sec"]);
                        DateTime _StartTime = _EndTime.AddSeconds(0 - sec);
                        string start_time = _StartTime.ToString("yyyyMMddHHmmssffff");
                        parse_package.Add("start_call_time", start_time);
                    }
                    if (parse_package.ContainsKey("source_id"))
                    {
                        if (CheckIfUidExist(parse_package["source_id"].ToString()))
                        {
                            //do nothing
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                    Thread accessSql = new Thread(() => AccessSqlServer(parse_package));
                    Thread accessAvls = new Thread(() => AccessAvlsServer(parse_package));
                    if (bool.Parse(ConfigurationManager.AppSettings["SQL_ACCESS"]))
                    {
                        
                        accessSql.Start();
                    }
                        
                    if (bool.Parse(ConfigurationManager.AppSettings["AVLS_ACCESS"]))
                    {
                        
                        accessAvls.Start();
                    }
                    if (bool.Parse(ConfigurationManager.AppSettings["SQL_ACCESS"]))
                        accessSql.Join();
                    if (bool.Parse(ConfigurationManager.AppSettings["AVLS_ACCESS"]))
                        accessAvls.Join();


                    if (parse_package.Count != 0)
                    {
                        StringBuilder s = new StringBuilder();
                        foreach (var e in parse_package)
                            s.Append(e.Key + ":" + e.Value + Environment.NewLine);
                        //s.Append(Environment.NewLine);
                      Console.WriteLine("####################################################");
                        /*
                        using (StreamWriter w = File.AppendText("log.txt"))
                        {
                            //string raw_data_without_first_4_byte = ByteToHexBitFiddle(receiveBytes_original.Skip(4).ToArray());
                            //Log("raw:" + raw_data_without_first_4_byte+Environment.NewLine+s.ToString(), w);
                            Log(Environment.NewLine + s.ToString(), w);
                          Console.WriteLine(s.ToString());
                        }
                         * */
                        try
                        {
                            log.Info(s.ToString()+Environment.NewLine+ByteToHexBitFiddle(receiveBytes));
                            
                          Console.WriteLine(s.ToString());
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message);
                        }
                      Console.WriteLine("####################################################");
                    }


                    

                    parse_package.Clear();
                    
                    Thread.Sleep(300);
                }

            }

        private static bool CheckIfUidExist(string uid)
        {
            SqlClient sql_client = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
            string sqlCmd = @"SELECT 
  sd.equipment.uid
FROM
  sd.equipment
WHERE
  sd.equipment.uid = '"+uid+@"'
LIMIT 1";
            while (!sql_client.connect())
            {
                Thread.Sleep(300);
            }
            var dt = sql_client.get_DataTable(sqlCmd);
            sql_client.disconnect();
            if (dt != null && dt.Rows.Count != 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static void AccessAvlsServer(SortedDictionary<string, string> parsePackage)
            {
                SortedDictionary<string,
                           string> parse_package = new SortedDictionary<string, string>(parsePackage);
                if (CheckIfUidExist(parse_package["source_id"].ToString()))
                {}
                else
                {
                    return;
                }
                log.Info("+AccessAvlsServer");
                Console.WriteLine("+AccessAvlsServer");
                if (parse_package.ContainsKey("result") &&
                    (parse_package["result"].ToString().Equals("power_on") ||
                     parse_package["result"].ToString().Equals("power_off")
                     ))
                {
                    log.Info("+AccessAvlsServer:if");
                    Console.WriteLine("+AccessAvlsServer:if");
                    TcpClient avls_tcpClient;
                    string send_string = string.Empty;
                    AVLS_UNIT_Report_Packet avls_package = new AVLS_UNIT_Report_Packet();
                    avls_package.Message = "test";
                    //string ipAddress = "127.0.0.1";
                    string ipAddress = ConfigurationManager.AppSettings["AVLS_SERVER_IP"];
                    //int port = 23;
                    int port = int.Parse(ConfigurationManager.AppSettings["AVLS_SERVER_PORT"]);

                    avls_tcpClient = new TcpClient();

                    //avls_tcpClient.Connect(ipAddress, port);
                    connectDone.Reset();
                    avls_tcpClient.BeginConnect(ipAddress, port, new AsyncCallback(AvlsConnectCallback), avls_tcpClient);
                    connectDone.WaitOne();

                    //avls_tcpClient.NoDelay = false;

                    //Keeplive.keep(avls_tcpClient.Client);
                    NetworkStream netStream = avls_tcpClient.GetStream();

                    //if (parse_package.ContainsKey("result") && (parse_package["result"].ToString().Equals("power_on") || parse_package["result"].ToString().Equals("power_off")))
                    //{
                        switch (parse_package["result"].ToString())
                        {
                            case "power_on":
                                avls_package.Event = "181,";
                                avls_package.Message = parse_package["result"].ToString();
                                break;
                            case "power_off":
                                avls_package.Event = "182,";
                                avls_package.Message = parse_package["result"].ToString();
                                break;
                        }

                        if (access_avls_server_send_timestamp_now)
                            avls_package.Date_Time = string.Format("{0:yyMMddHHmmss}", DateTime.Now) + ",";
                        else
                            avls_package.Date_Time = parse_package["timestamp"].ToString().Substring(2, 12) ;
                        //log.Info("avls_package.Date_Time=" + avls_package.Date_Time);
                        DateTime tempDatetime = DateTime.ParseExact(avls_package.Date_Time, "yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                    tempDatetime = tempDatetime.ToUniversalTime();
                    avls_package.Date_Time = tempDatetime.ToString("yyMMddHHmmss") + ",";

                        avls_package.ID = parse_package["source_id"].ToString() ;
                        avls_package.GPS_Valid = "A,";
                        /*
                         * SELECT 
      public._gps_log._lat,
      public._gps_log._lon
    FROM
      public._gps_log
    WHERE
      public._gps_log._time < now() AND 
      public._gps_log._uid = 'avls_package.ID'
    ORDER BY
      public._gps_log._time DESC
    LIMIT 1
                         */
                    ///TODO:implement set last lat lon value
                    if (avlsGetLastLocation)
                    {
                        var avlsSqlClient = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
                        string avlsSqlCmd = @"SELECT 
  public._gps_log._lat,
  public._gps_log._lon
FROM
  public._gps_log
WHERE
  public._gps_log._time < now() AND 
  public._gps_log._uid = '" + avls_package.ID + @"'
ORDER BY
  public._gps_log._time DESC
LIMIT 1";
                        log.Info("avlsSqlCmd=" + Environment.NewLine + avlsSqlCmd);
                        while (!avlsSqlClient.connect())
                        {
                            Thread.Sleep(300);
                        }

                        var dt = avlsSqlClient.get_DataTable(avlsSqlCmd);
                        avlsSqlClient.disconnect();
                        if (dt != null && dt.Rows.Count != 0)
                        {
                            string avlsLat = string.Empty, avlsLon = string.Empty;
                            foreach (DataRow row in dt.Rows)
                            {
                                avlsLat = row[0].ToString();
                                avlsLon = row[1].ToString();
                            }
                            string zero = "0";
                            if (avlsLat.Equals(zero) || avlsLon.Equals(zero))
                            {
                                GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                            }
                            //GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                            //GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                            //string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                            //string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                            //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                            string lat_str = avlsLat, long_str = avlsLon;
                            ConvertLocToAvlsLoc(ref lat_str, ref long_str); 
                            avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                        }
                        else
                        {
                            string avlsLat = string.Empty, avlsLon = string.Empty;
                            GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                            //GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                            //GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                            //string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                            //string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                            //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                            string lat_str = avlsLat, long_str = avlsLon;
                            ConvertLocToAvlsLoc(ref lat_str, ref long_str); 
                            avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                            //avls_package.Loc = "N00000.0000E00000.0000,";
                        }
                    }
                    else
                    {
                        string avlsLat = string.Empty, avlsLon = string.Empty;
                        GetInitialLocationFromSql(ref avlsLat, ref avlsLon, avls_package.ID);
                        GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLat));
                        GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(avlsLon));
                        string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                        string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                        //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                        avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                        //avls_package.Loc = "N00000.0000E00000.0000,";
                    }
                        
                        avls_package.Speed = "0,";
                        avls_package.Dir = "0,";
                        avls_package.Temp = "NA,";
                        avls_package.Status = "00000000,";
                        

                    //}
                    avls_package.ID += ",";
                    send_string = "%%" + avls_package.ID + avls_package.GPS_Valid + avls_package.Date_Time + avls_package.Loc + avls_package.Speed + avls_package.Dir + avls_package.Temp + avls_package.Status + avls_package.Event + avls_package.Message + "\r\n";


                    sendDone.Reset();
                    avls_WriteLine(netStream, System.Text.Encoding.Default.GetBytes(send_string), send_string);
                    sendDone.WaitOne();

                    //ReadLine(avls_tcpClient, netStream, send_string.Length);
                    netStream.Close();
                    avls_tcpClient.Close();
                    log.Info("-AccessAvlsServer:if");
                    Console.WriteLine("-AccessAvlsServer:if");
                }
                log.Info("-AccessAvlsServer");
                Console.WriteLine("-AccessAvlsServer");
            }

            private static void GetInitialLocationFromSql(ref string lat, ref string lon, string id)
            {
                var sqlClient = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
                        string sqlCmd = @"SELECT 
  sd.initial_location.lat,
  sd.initial_location.lon
FROM
  sd.initial_location
WHERE
  sd.initial_location.uid = '" + id + @"'";
                        log.Info("GetInitialLocationFromSqllCmd=" + Environment.NewLine + sqlCmd);
                        while (!sqlClient.connect())
                        {
                            Thread.Sleep(300);
                        }

                        var dt = sqlClient.get_DataTable(sqlCmd);
                        sqlClient.disconnect();
                if (dt != null && dt.Rows.Count != 0)
                {
                   
                    foreach (DataRow row in dt.Rows)
                    {
                        lat = row[0].ToString();
                        lon = row[1].ToString();
                    }
                }
                else
                {
                    lat = lon = "0";
                    log.Error("Cannot find lat lon of deviceID: " + id + "in sql table: sd.initial_location ");
                }
            }

            private static void avls_WriteLine(NetworkStream netStream, byte[] writeData, string write)
            {
                if (netStream.CanWrite)
                {
                    //byte[] writeData = Encoding.ASCII.GetBytes(write);
                    try
                    {

                      Console.WriteLine("S----------------------------------------------------------------------------");
                      Console.WriteLine("Write:\r\n" + write);
                      Console.WriteLine("E----------------------------------------------------------------------------");

                       
                            log.Info("Write:\r\n"+write);
                            // Close the writer and underlying file.
                        

                        //send method1
                        //netStream.Write(writeData, 0, writeData.Length);
                        // 需等待資料真的已寫入 NetworkStream
                        //Thread.Sleep(3000);

                        //send method2
                        IAsyncResult result = netStream.BeginWrite(writeData, 0, writeData.Length, new AsyncCallback(avls_myWriteCallBack), netStream);
                        result.AsyncWaitHandle.WaitOne();
                    }
                    catch (Exception ex)
                    {
                      Console.WriteLine("avls_WriteLineError:\r\n" + ex.Message);
                        log.Error("avls_WriteLineError:\r\n" + ex.Message);
                    }


                }
            }

            public static void avls_myWriteCallBack(IAsyncResult ar)
            {
                try
                {
                    NetworkStream myNetworkStream = (NetworkStream)ar.AsyncState;
                    myNetworkStream.EndWrite(ar);
                    sendDone.Set();
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine(ex.Message);
                    log.Info(ex.Message);
                }
               

            }

            static void AvlsConnectCallback(IAsyncResult ar)
            {
                try
                {
                    TcpClient t = (TcpClient)ar.AsyncState;
                    if (t != null && t.Client != null)
                    {
                        t.EndConnect(ar);
                        connectDone.Set();
                    }
                }
                catch (Exception ex)
                {
                    
                    Console.WriteLine(ex.Message);
                    log.Error(ex.Message);
                    //string ipAddress = "127.0.0.1";
                    string ipAddress = ConfigurationManager.AppSettings["AVLS_SERVER_IP"];
                    //int port = 23;
                    int port = int.Parse(ConfigurationManager.AppSettings["AVLS_SERVER_PORT"]);
                    var avlsTcpClient = new TcpClient();
                    connectDone.Reset();
                    avlsTcpClient.BeginConnect(ipAddress, port, new AsyncCallback(AvlsConnectCallback), avlsTcpClient);
                    connectDone.WaitOne();
                }
                
                
            }

            private static void AccessSqlServer(SortedDictionary<string, string> parsePackage)
            {
                lock (SpinLock)
                {

                    {
                        SortedDictionary<string,
                        string> parse_package = new SortedDictionary<string, string>(parsePackage);
                        Console.WriteLine("+AccessSqlServer");
                        string sql_table_columns = string.Empty, sql_table_column_value = string.Empty, sql_cmd = string.Empty;
                        if (parse_package.ContainsKey("result") && (parse_package["result"].ToString().Equals("power_on") || parse_package["result"].ToString().Equals("power_off") || parse_package["result"].ToString().Equals("start_call") || parse_package["result"].ToString().Equals("end_call") || parse_package["result"].ToString().Equals("Call_State_Change") || parse_package["result"].Equals("Start_of_Call")))
                        {
                            if (parse_package.ContainsKey("call_type"))
                            {
                                Console.WriteLine("call_type=" + parse_package["call_type"]);
                                log.Info(Thread.CurrentThread.Name + "@" + "AccessSqlServer:call_type=" + parse_package["call_type"]);
                            }
                            else
                            {
                                Console.WriteLine("call_type=unknown");
                                log.Info(Thread.CurrentThread.Name + "@" + "AccessSqlServer:call_type=unknown");
                            }
                            SqlClient sql_client = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);

                            switch (parse_package["result"].ToString())
                            {
                                case "power_on":
                                    Device_power_status dev_power_status = new Device_power_status();
                                    dev_power_status.ID = parse_package["source_id"].ToString();
                                    dev_power_status.power_on_time = parse_package["timestamp"].ToString();
                                    string device_on_time = dev_power_status.power_on_time.Substring(0, 4) + "-" + dev_power_status.power_on_time.Substring(4, 2) + "-" +
                                        dev_power_status.power_on_time.Substring(6, 2) + " " + dev_power_status.power_on_time.Substring(8, 2) + ":" +
                                        dev_power_status.power_on_time.Substring(10, 2) + ":" + dev_power_status.power_on_time.Substring(12, 2);
                                    dev_power_status.power_on_time = device_on_time;
                                    string power_on_today = DateTime.Now.ToString("yyyyMMdd");
                                    #region access power status

                                    {
                                        string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                        sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  power = 'on',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }

                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                    }
                                    #endregion
                                    /*SELECT 
  custom.turn_onoff_log.serial_no
FROM
  custom.turn_onoff_log
WHERE
  custom.turn_onoff_log.uid = '910000'
ORDER BY
  custom.turn_onoff_log.create_time DESC
LIMIT 1
                             */
                                    //if (true)
                                    {
                                        sql_cmd = @"SELECT 
  custom.turn_onoff_log.serial_no
FROM
  custom.turn_onoff_log
INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.uid = '" + dev_power_status.ID + @"' AND 
custom.turn_onoff_log.on_time IS NOT NULL AND 
  custom.turn_onoff_log.off_time IS NULL
ORDER BY
  custom.turn_onoff_log.create_time DESC
LIMIT 1";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        var dt = sql_client.get_DataTable(sql_cmd);
                                        sql_client.disconnect();
                                        if (dt != null && dt.Rows.Count != 0)
                                        {
                                            break;
                                        }
                                    }
                                    #region
                                    {
                                        string sn = string.Empty;
                                        sql_cmd = @"SELECT 
  custom.turn_onoff_log.serial_no
FROM
  custom.turn_onoff_log
INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.uid = '" + dev_power_status.ID + @"' AND 
custom.turn_onoff_log.on_time IS NOT NULL AND 
  custom.turn_onoff_log.off_time IS NOT NULL
ORDER BY
  custom.turn_onoff_log.create_time DESC
LIMIT 1";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        var dt = sql_client.get_DataTable(sql_cmd);
                                        sql_client.disconnect();
                                        if (dt != null && dt.Rows.Count != 0)
                                        {
                                            foreach (DataRow row in dt.Rows)
                                            {
                                                sn = row[0].ToString();
                                            }

                                            Console.WriteLine("dev_power_status.ID.Length=" + dev_power_status.ID.Length);
                                            Console.WriteLine("dev_power_status.ID=" + dev_power_status.ID);

                                            string yyyyMMdd = sn.Substring(dev_power_status.ID.Length, 8);
                                            string count = sn.Substring(dev_power_status.ID.Length + yyyyMMdd.Length, 3);

                                            if (power_on_today.Equals(yyyyMMdd))
                                            {
                                                uint addCount = (uint.Parse(count) + 1);
                                                dev_power_status.SN = dev_power_status.ID + power_on_today + addCount.ToString("D3");
                                            }
                                            else
                                            {
                                                int iVal = 0;

                                                dev_power_status.SN = dev_power_status.ID + power_on_today + iVal.ToString("D3");
                                            }

                                            InsertPowerOnOffEventToPublicGpsLogAndToAvlsLog(dev_power_status, parse_package["result"].ToString(), parse_package);
                                        }
                                        else
                                        {
                                            int iVal = 0;

                                            dev_power_status.SN = dev_power_status.ID + power_on_today + iVal.ToString("D3");
                                            InsertPowerOnOffEventToPublicGpsLogAndToAvlsLog(dev_power_status, parse_package["result"].ToString(), parse_package);

                                        }
                                    }
                                    #endregion

                                    if (CheckIfUidInEquipmentTable(dev_power_status.ID))
                                    {
                                        sql_table_columns = "serial_no,uid,on_time,create_user,create_ip";
                                        sql_table_column_value = "\'" + dev_power_status.SN + "\'" + "," + "\'" + dev_power_status.ID + "\'" + "," + "\'" +
                                            device_on_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'";
                                        sql_cmd = "INSERT INTO custom.turn_onoff_log (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                        /*
                                        int iVal = 1;

                                        iVal.ToString("D3"); // = "001"
                                         * */
                                        if (!Power_status.ContainsKey(dev_power_status.ID))
                                            Power_status.Add(dev_power_status.ID, dev_power_status);
                                    }
                                    else
                                    {

                                    }

                                    break;
                                case "power_off":

                                    #region access power status

                                    {
                                        string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                        sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  power = 'on',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                    }
                                    #endregion
                                    string power_off_sn = string.Empty;
                                    Device_power_status dev_power_off_status = new Device_power_status();
                                    dev_power_off_status.ID = parse_package["source_id"].ToString();
                                    if (CheckIfUidInEquipmentTable(dev_power_off_status.ID))
                                    {

                                    }
                                    else
                                    {
                                        break;
                                    }
                                    #region

                                    Device_power_status find_dev_sn;
                                    if (Power_status.ContainsKey(dev_power_off_status.ID))
                                    {
                                        find_dev_sn = (Device_power_status)Power_status[dev_power_off_status.ID];
                                        power_off_sn = dev_power_off_status.SN = find_dev_sn.SN;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                    #endregion
                                    #region
                                    /*
                            Device_power_status find_dev_sn = Power_status.Find(
                                 delegate(Device_power_status bk)
                                 {
                                     return bk.ID == dev_power_off_status.ID;
                                 }
                                );
                            if (find_dev_sn != null)
                            {
                                power_off_sn = dev_power_off_status.SN = find_dev_sn.SN;
                            }
                            else
                            {
                              Console.WriteLine("power_off:Cannot fine device_id {0}", dev_power_off_status.ID);
                                log.Info("power_off:Cannot fine device_id "+dev_power_off_status.ID);
                                break;
                            }
                            */
                                    #endregion

                                    //string power_off_sn = ConfigurationManager.AppSettings[dev_power_off_status.ID].ToString();
                                    // string[] power_off_sn_sub = power_off_sn.Split(',');
                                    //dev_power_off_status.SN = dev_power_off_status.ID + power_off_sn_sub[0] + uint.Parse(power_off_sn_sub[1]).ToString("D3");
                                    dev_power_off_status.power_off_time = parse_package["timestamp"].ToString();
                                    //parse_package.Add("timestamp", date_time.ToString("yyyyMMddHHmmssffff"));
                                    string device_off_time = dev_power_off_status.power_off_time.Substring(0, 4) + "-" + dev_power_off_status.power_off_time.Substring(4, 2) + "-" +
                                        dev_power_off_status.power_off_time.Substring(6, 2) + " " + dev_power_off_status.power_off_time.Substring(8, 2) + ":" +
                                        dev_power_off_status.power_off_time.Substring(10, 2) + ":" + dev_power_off_status.power_off_time.Substring(12, 2);
                                    dev_power_off_status.power_off_time = find_dev_sn.power_off_time = device_off_time;
                                    dev_power_off_status = find_dev_sn;
                                    {
                                        sql_cmd = @"SELECT 
  custom.turn_onoff_log.serial_no
FROM
  custom.turn_onoff_log
INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.uid = '" + dev_power_off_status.ID + @"' AND 
custom.turn_onoff_log.on_time IS NOT NULL AND 
  custom.turn_onoff_log.off_time IS  NULL
ORDER BY
  custom.turn_onoff_log.create_time DESC
LIMIT 1";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        var dt = sql_client.get_DataTable(sql_cmd);
                                        sql_client.disconnect();
                                        if (dt != null && dt.Rows.Count != 0)
                                        {
                                            InsertPowerOnOffEventToPublicGpsLogAndToAvlsLog(dev_power_off_status, parse_package["result"].ToString(), parse_package);
                                        }
                                        else
                                        {
                                            break;

                                        }
                                    }

                                    sql_cmd = @"SELECT 
  custom.turn_onoff_log.serial_no
FROM
  custom.turn_onoff_log
INNER JOIN sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
WHERE
  custom.turn_onoff_log.serial_no = '" + power_off_sn + @"' AND 
custom.turn_onoff_log.on_time IS NOT NULL AND 
  custom.turn_onoff_log.off_time IS  NULL
ORDER BY
  custom.turn_onoff_log.create_time DESC
LIMIT 1";
                                    while (!sql_client.connect())
                                    {
                                        Thread.Sleep(300);
                                    }
                                    var dt2 = sql_client.get_DataTable(sql_cmd);
                                    sql_client.disconnect();
                                    if (dt2 != null && dt2.Rows.Count != 0)
                                    {
                                        InsertPowerOnOffEventToPublicGpsLogAndToAvlsLog(dev_power_off_status, parse_package["result"].ToString(), parse_package);
                                        sql_table_columns = "custom.turn_onoff_log";
                                        sql_cmd = "UPDATE " + sql_table_columns + " SET off_time=\'" + device_off_time + "\' WHERE serial_no=\'" + dev_power_off_status.SN + "\'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                        // TODO: insert information control present to sql server
                                        /*
                                         * SELECT ((((DATE_PART('day', '2011-12-30 08:56:10'::timestamp - '2011-12-30 08:54:55'::timestamp) * 24 +
                            DATE_PART('hour', '2011-12-30 08:56:10'::timestamp - '2011-12-30 08:54:55'::timestamp)) * 60 +
                            DATE_PART('minute', '2011-12-30 08:56:10'::timestamp - '2011-12-30 08:54:55'::timestamp)) * 60 +
                            DATE_PART('second', '2011-12-30 08:56:10'::timestamp - '2011-12-30 08:54:55'::timestamp))/time_trigger_interval)*100;
                                         */
                                        int time_trigger_interval = int.Parse(ConfigurationManager.AppSettings["time_trigger_interval"]);
                                        string sql_select = @"
                            
                               SELECT
  custom.turn_onoff_log.serial_no,
  custom.turn_onoff_log.uid,
  custom.turn_onoff_log.on_time,
  custom.turn_onoff_log.off_time,
  ((((DATE_PART('day', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp) * 24 +
                DATE_PART('hour', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('minute', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('second', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp))/" + time_trigger_interval + @")  AS all_time_count,
                COUNT(custom.voice_connect.uid) AS location_count,
  (COUNT(custom.voice_connect.uid)/
  ((((DATE_PART('day', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp) * 24 +
                DATE_PART('hour', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('minute', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('second', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp))/" + time_trigger_interval + @"))*100 || '%' AS location_percentage


FROM
  custom.turn_onoff_log 
  INNER JOIN 
  sd.equipment ON (custom.turn_onoff_log.uid = sd.equipment.uid)
  INNER JOIN
  custom.voice_connect
  ON
  custom.voice_connect.uid = " + "\'" + dev_power_off_status.ID + "\'" +//custom.turn_onoff_log.uid
                                           @"
WHERE
  
  custom.voice_connect.end_time > custom.turn_onoff_log.on_time AND
  custom.voice_connect.end_time < custom.turn_onoff_log.off_time
GROUP BY
  custom.turn_onoff_log.serial_no,
  custom.turn_onoff_log.uid,
  custom.turn_onoff_log.on_time,
  custom.turn_onoff_log.off_time,
  (((DATE_PART('day', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp) * 24 +
                DATE_PART('hour', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('minute', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp)) * 60 +
                DATE_PART('second', custom.turn_onoff_log.off_time::timestamp - custom.turn_onoff_log.on_time::timestamp))
limit 1
                                ";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        DataTable data_table = sql_client.get_DataTable(sql_select);
                                        sql_client.disconnect();
                                        Console.WriteLine(sql_select);
                                        log.Info(sql_select);


                                        if ((data_table == null) || (data_table.Rows.Count == 0))
                                        {
                                            Console.WriteLine("data_table == null");
                                        }
                                        else
                                        {
                                            #region

                                            string sn = string.Empty, serial_no = string.Empty;
                                            string power_off_today = DateTime.Now.ToString("yyyyMMdd");
                                            string sqlCmd = @"SELECT 
  custom.location_control_log.serial_no
FROM
  custom.location_control_log
WHERE
  custom.location_control_log.uid = '" + data_table.Rows[0]["uid"].ToString() + @"'
ORDER BY
  custom.location_control_log.serial_no DESC
LIMIT 1";
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            var dt = sql_client.get_DataTable(sqlCmd);
                                            sql_client.disconnect();
                                            if (dt != null && dt.Rows.Count != 0)
                                            {
                                                foreach (DataRow row in dt.Rows)
                                                {
                                                    sn = row[0].ToString();
                                                }


                                                string yyyyMMdd = sn.Substring(data_table.Rows[0]["uid"].ToString().Length, 8);
                                                string count = sn.Substring(data_table.Rows[0]["uid"].ToString().Length + yyyyMMdd.Length, 3);

                                                if (power_off_today.Equals(yyyyMMdd))
                                                {
                                                    uint addCount = (uint.Parse(count) + 1);
                                                    serial_no = data_table.Rows[0]["uid"].ToString() + power_off_today + addCount.ToString("D3");
                                                }
                                                else
                                                {
                                                    int iVal = 0;

                                                    serial_no = data_table.Rows[0]["uid"].ToString() + power_off_today + iVal.ToString("D3");
                                                }


                                            }
                                            else
                                            {
                                                int iVal = 0;

                                                serial_no = data_table.Rows[0]["uid"].ToString() + power_off_today + iVal.ToString("D3");


                                            }
                                            #endregion
                                            Console.WriteLine("data_table != null");
                                            string ssql_insert_value = @"'" + serial_no + @"'" +
                                                "," + @"'" + data_table.Rows[0]["uid"].ToString() + @"'" +
                                                "," + @"'" + data_table.Rows[0]["on_time"].ToString() + @"'" +
                                                "," + @"'" + data_table.Rows[0]["off_time"].ToString() + @"'" +
                                                "," + @"'" + data_table.Rows[0]["all_time_count"].ToString() + @"'" +
                                                "," + @"'" + data_table.Rows[0]["location_count"].ToString() + @"'" +
                                                "," + @"'" + data_table.Rows[0]["location_percentage"].ToString() + @"'";
                                            string sql_insert = @"
INSERT INTO custom.location_control_log 
(serial_no, uid, on_time, off_time, all_time_count, location_count, location_percentage) 
VALUES 
(" + ssql_insert_value + @");
";
                                            Console.WriteLine(sql_insert);
                                            log.Info(sql_insert);
                                            //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"[device]:" + data_table.Rows[0]["device"]);
                                            //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"[longitude]:" + data_table.Rows[0]["longitude"]);
                                            //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"[latitude]:" + data_table.Rows[0]["latitude"]);
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            sql_client.modify(sql_insert);
                                            sql_client.disconnect();
                                        }
                                    }


                                    Power_status.Remove(dev_power_off_status.ID);
                                    break;
                                case "Start_of_Call":
                                case "start_call":
                                    object startCallLock = new object();
                                    Device_call_status devCallStatus = new Device_call_status
                                    {
                                        ID = parse_package["source_id"].ToString(),
                                        start_call_time = parse_package["timestamp"].ToString()
                                    };

                                    if (!CheckIfUidInEquipmentTable(devCallStatus.ID))
                                    {
                                        break;
                                    }
                                    
                                   

                                    string start_call_time = devCallStatus.start_call_time.Substring(0, 4) + "-" + devCallStatus.start_call_time.Substring(4, 2) + "-" +
                                        devCallStatus.start_call_time.Substring(6, 2) + " " + devCallStatus.start_call_time.Substring(8, 2) + ":" +
                                        devCallStatus.start_call_time.Substring(10, 2) + ":" + devCallStatus.start_call_time.Substring(12, 2);
                                    string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                    switch (parse_package["call_type"].ToString())
                                    {

                                        case "Individual_Call":
                                            lock (startCallLock)
                                            {
                                                devCallStatus.call_type = "2";
                                                devCallStatus.targetID = parse_package["target_id"];
                                                if (parse_package.ContainsKey("channel"))
                                                    devCallStatus.channel = parse_package["channel"];
                                                if (parse_package.ContainsKey("site"))
                                                    devCallStatus.site = parse_package["site"];
                                            }
                                            
                                            
                                            if (parse_package.ContainsKey("Radio_Type_Qualifier"))
                                            {
                                                switch (parse_package["Radio_Type_Qualifier"])
                                                {
                                                       // Land_to_Mobile:4
                                                       // Mobile_to_Land:3
                                                    case "Land_to_Mobile":
                                                        lock (startCallLock)
                                                        {
                                                            devCallStatus.call_type = "4";
                                                            parse_package["call_type"] = "Land_to_Mobile";
                                                        }
                                                        
                                                        break;
                                                    case "Mobile_to_Land":
                                                        lock (startCallLock)
                                                        {
                                                            devCallStatus.call_type = "3";
                                                            parse_package["call_type"] = "Mobile_to_Land";
                                                        }
                                                        
                                                        break;
                                                }
                                            }
                                            #region access power status

                                            {
                                                string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                                sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["call_type"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                sql_client.modify(sql_cmd);
                                                sql_client.disconnect();
                                            }
                                            #endregion
                                            break;
                                        case "Group_Call":
                                            lock (startCallLock)
                                            {
                                                devCallStatus.call_type = "1";
                                                devCallStatus.gTarget = parse_package["target_id"];
                                                if (parse_package.ContainsKey("channel"))
                                                    devCallStatus.channel = parse_package["channel"];
                                                if (parse_package.ContainsKey("site"))
                                                    devCallStatus.site = parse_package["site"];
                                            }
                                            
                                            #region access power status

                                            {
                                                string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                                sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["call_type"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                sql_client.modify(sql_cmd);
                                                sql_client.disconnect();
                                            }
                                            #endregion
                                            break;
                                    }
                                    /*SELECT 
          custom.voice_connect.serial_no
        FROM
          custom.voice_connect
        WHERE
          custom.voice_connect.connect_type = '3' AND 
          custom.voice_connect.uid = '930000'
        ORDER BY
          custom.voice_connect.create_time DESC
        LIMIT 1*/
                                    if (true)
                                    {
                                        string sn = string.Empty;
                                        sql_cmd = @"SELECT 
  custom.voice_connect.serial_no
FROM
  custom.voice_connect
WHERE
  custom.voice_connect.uid = '" + devCallStatus.ID + @"'
ORDER BY
  custom.voice_connect.create_time DESC
LIMIT 1";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        var dt = sql_client.get_DataTable(sql_cmd);
                                        sql_client.disconnect();
                                        if (dt != null && dt.Rows.Count != 0)
                                        {
                                            foreach (DataRow row in dt.Rows)
                                            {
                                                sn = row[0].ToString();
                                            }
                                            Console.WriteLine("dev_call_status.ID.Length=" + devCallStatus.ID.Length);
                                            Console.WriteLine("dev_call_status.ID=" + devCallStatus.ID);

                                            string yyyyMMdd = sn.Substring(devCallStatus.ID.Length, 8);
                                            string count = sn.Substring(devCallStatus.ID.Length + yyyyMMdd.Length, 5);

                                            if (start_call_today.Equals(yyyyMMdd))
                                            {
                                                uint addCount = (uint.Parse(count) + 1);
                                                lock (startCallLock)
                                                {
                                                    devCallStatus.SN = devCallStatus.ID + start_call_today + addCount.ToString("D5");
                                                }
                                                
                                            }
                                            else
                                            {
                                                int iVal = 0;
                                                lock (startCallLock)
                                                {
                                                    devCallStatus.SN = devCallStatus.ID + start_call_today + iVal.ToString("D5");
                                                }
                                                
                                            }
                                        }
                                        else
                                        {
                                            int iVal = 0;
                                            lock (startCallLock)
                                            { devCallStatus.SN = devCallStatus.ID + start_call_today + iVal.ToString("D5"); }
                                            
                                        }
                                    }
                                    else
                                    {
                                        if (AddValue(devCallStatus.ID, start_call_today + "," + "00000"))
                                        {
                                            int iVal = 0;

                                            devCallStatus.SN = devCallStatus.ID + start_call_today + iVal.ToString("D5");
                                        }
                                        else
                                        {
                                            string sn = ConfigurationManager.AppSettings[devCallStatus.ID].ToString();
                                            string[] sn_sub = sn.Split(',');
                                            if (sn_sub[0] != start_call_today)
                                            {
                                                int iVal = 0;

                                                devCallStatus.SN = devCallStatus.ID + start_call_today + iVal.ToString("D5");
                                                ModifyValue(devCallStatus.ID, start_call_today + "," + "00000");
                                            }
                                            else
                                            {
                                                uint count = uint.Parse(sn_sub[1]) + 1;
                                                devCallStatus.SN = devCallStatus.ID + start_call_today + count.ToString("D5");
                                                ModifyValue(devCallStatus.ID, start_call_today + "," + count.ToString("D5"));
                                            }
                                        }
                                    }

                                    if (CheckIfUidInEquipmentTable(devCallStatus.ID))
                                    {
                                        if (!Call_status.ContainsKey(devCallStatus.ID + parse_package["call_type"]))
                                        {
                                            if (
                                                devCallStatus.call_type.Equals("2")
                                                && parse_package.ContainsKey("channel") && parse_package.ContainsKey("site"))
                                            {
                                                sql_table_columns = "serial_no,uid,connect_type,start_time,create_user,create_ip,target,channel,site";
                                                sql_table_column_value = "\'" + devCallStatus.SN + "\'" + "," + "\'" + devCallStatus.ID + "\'" + "," + "\'" +
                                                    devCallStatus.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'"
                                                    + "," + "\'" + devCallStatus.targetID + "\'"
                                                    + "," + "\'" + devCallStatus.channel + "\'"
                                                    + "," + "\'" + devCallStatus.site + "\'";
                                                sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";
                                                //test
                                            }
                                            else
                                            {
                                                if (parse_package["call_type"].Equals("Group_Call") && parse_package.ContainsKey("channel") && parse_package.ContainsKey("site"))
                                                {
                                                    sql_table_columns = "serial_no,uid,connect_type,start_time,create_user,create_ip,gtarget,channel,site";
                                                    sql_table_column_value = "\'" + devCallStatus.SN + "\'" + "," + "\'" + devCallStatus.ID + "\'" + "," + "\'" +
                                                        devCallStatus.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'"
                                                        + "," + "\'" + devCallStatus.gTarget + "\'"
                                                        + "," + "\'" + devCallStatus.channel + "\'"
                                                        + "," + "\'" + devCallStatus.site + "\'";
                                                    sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";

                                                }
                                                else
                                                {
                                                    /*
                                                    if (!(parse_package["call_type"].Equals("Group_Call") || devCallStatus.call_type.Equals("2")))
                                                    {
                                                        sql_table_columns = "serial_no,uid,connect_type,start_time,create_user,create_ip,target";
                                                    sql_table_column_value = "\'" + devCallStatus.SN + "\'" + "," + "\'" + devCallStatus.ID + "\'" + "," + "\'" +
                                                        devCallStatus.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'" + "," + "\'" + parse_package["target_id"] + "\'";
                                                    sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";

                                                    }
                                                    */
                                                    if (parse_package.ContainsKey("Radio_Type_Qualifier") && parse_package.ContainsKey("channel") && parse_package.ContainsKey("site"))
                                                    {
                                                        switch (parse_package["Radio_Type_Qualifier"])
                                                        {
                                                            // Land_to_Mobile:4
                                                            // Mobile_to_Land:3
                                                            case "Land_to_Mobile":
                                                                sql_table_columns = "serial_no,uid,connect_type,start_time,create_user,create_ip,target,channel,site";
                                                                sql_table_column_value = "\'" + devCallStatus.SN + "\'" + "," + "\'" + devCallStatus.ID + "\'" + "," + "\'" +
                                                                    devCallStatus.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'" + "," + "\'" + devCallStatus.ID + "\'"
                                                                     + "," + "\'" + devCallStatus.channel + "\'"
                                                        + "," + "\'" + devCallStatus.site + "\'"; ;
                                                                sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";


                                                                break;
                                                            case "Mobile_to_Land":
                                                                sql_table_columns = "serial_no,uid,connect_type,start_time,create_user,create_ip,target,channel,site";
                                                                sql_table_column_value = "\'" + devCallStatus.SN + "\'" + "," + "\'" + devCallStatus.ID + "\'" + "," + "\'" +
                                                                    devCallStatus.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'" + "," + "\'" + "null" + "\'"
                                                                     + "," + "\'" + devCallStatus.channel + "\'"
                                                        + "," + "\'" + devCallStatus.site + "\'"; ;
                                                                sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";


                                                                break;
                                                        }
                                                    }
                                                    
                                                }
                                               
                                            }
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            sql_client.modify(sql_cmd);
                                            sql_client.disconnect();

                                            //Call_status.Add(dev_call_status);
                                            if (!Call_status.ContainsKey(devCallStatus.ID + parse_package["call_type"]))
                                            {
                                                Call_status.Add(devCallStatus.ID + parse_package["call_type"], devCallStatus);
                                            }
                                        }
                                        else
                                        {
                                            var find_dev_call_status = (Device_call_status)Call_status[devCallStatus.ID + parse_package["call_type"]];
                                            if (
                                                (devCallStatus.call_type.Equals("1") ||
                                                devCallStatus.call_type.Equals("2"))
                                                && parse_package.ContainsKey("channel") && parse_package.ContainsKey("site"))
                                            {
                                                sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  channel = "+ "\'" + devCallStatus.channel + "\'"+","+
             "site = " + "\'" + devCallStatus.site + "\'" + @"
WHERE
  custom.voice_connect.serial_no = " + "\'" + find_dev_call_status.SN + "\'";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                sql_client.modify(sql_cmd);
                                                sql_client.disconnect();
                                            }
                                        }
                                        
                                    }
                                    #region


                                    

                                    #endregion
                                    break;
                                case "end_call":
                                    //if (parse_package.ContainsKey("Radio_Type_Qualifier"))
                                    {
                                        #region access power status

                                        {
                                            string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                            sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["result"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            sql_client.modify(sql_cmd);
                                            sql_client.disconnect();
                                        }
                                        #endregion
                                        object endLock = new object();
                                        string end_call_sn = string.Empty;
                                        Device_call_status dev_call_off_status = new Device_call_status();
                                        lock (endLock)
                                        {
                                            dev_call_off_status.ID = parse_package["source_id"].ToString();
                                        }
                                        
                                        if (CheckIfUidInEquipmentTable(dev_call_off_status.ID))
                                        { }
                                        else
                                        {
                                            break;
                                        }
                                        #region

                                        Device_call_status find_dev_call_off_sn;
                                        if (Call_status.ContainsKey(dev_call_off_status.ID + parse_package["call_type"]))
                                        {
                                            find_dev_call_off_sn = (Device_call_status)Call_status[dev_call_off_status.ID + parse_package["call_type"]];
                                            lock (endLock)
                                            {
                                                end_call_sn = dev_call_off_status.SN = find_dev_call_off_sn.SN;
                                            }
                                            
                                        }
                                        else
                                        {
                                            break;
                                        }
                                        #endregion
                                        #region
                                        /*
                            Device_call_status find_dev_call_off_sn = Call_status.Find(
                                 delegate(Device_call_status bk)
                                 {
                                     return bk.ID == dev_call_off_status.ID;
                                 }
                                );
                            if (find_dev_call_off_sn != null)
                            {
                                end_call_sn = dev_call_off_status.SN = find_dev_call_off_sn.SN;
                            }
                            else
                            {
                              Console.WriteLine("end_call:Cannot fine device_id {0}", dev_call_off_status.ID);
                                log.Info("end_call:Cannot fine device_id "+dev_call_off_status.ID);
                                break;
                            }
                            */
                                        #endregion
                                        //string power_off_sn = ConfigurationManager.AppSettings[dev_power_off_status.ID].ToString();
                                        // string[] power_off_sn_sub = power_off_sn.Split(',');
                                        //dev_power_off_status.SN = dev_power_off_status.ID + power_off_sn_sub[0] + uint.Parse(power_off_sn_sub[1]).ToString("D3");
                                        lock (endLock)
                                        {
                                            dev_call_off_status.end_call_time = parse_package["timestamp"].ToString();
                                        }
                                        
                                        //parse_package.Add("timestamp", date_time.ToString("yyyyMMddHHmmssffff"));
                                        string device_end_call_time = dev_call_off_status.end_call_time.Substring(0, 4) + "-" + dev_call_off_status.end_call_time.Substring(4, 2) + "-" +
                                            dev_call_off_status.end_call_time.Substring(6, 2) + " " + dev_call_off_status.end_call_time.Substring(8, 2) + ":" +
                                            dev_call_off_status.end_call_time.Substring(10, 2) + ":" + dev_call_off_status.end_call_time.Substring(12, 2);
                                        sql_table_columns = "custom.voice_connect";
                                        sql_cmd = "UPDATE " + sql_table_columns + " SET end_time=\'" + device_end_call_time + "\' WHERE serial_no=\'" + dev_call_off_status.SN + "\'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();

                                        if (parse_package.ContainsKey("call_type"))
                                        {
                                            switch (parse_package["call_type"].ToString())
                                            {
                                                case "Land_to_Mobile": //L uid = null
                                                    sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  connect_type = '4',
target = NULL,
  gtarget = NULL
WHERE
  custom.voice_connect.serial_no = '"+dev_call_off_status.SN + "\'";
                                                    while (!sql_client.connect())
                                                    {
                                                        Thread.Sleep(300);
                                                    }
                                                    sql_client.modify(sql_cmd);
                                                    sql_client.disconnect();
                                                    if (parse_package.ContainsKey("target_id") && false)
                                                    {
                                                        sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  target = "+"\'"+parse_package["target_id"].ToString()+ "\'"+@"
WHERE
  custom.voice_connect.serial_no = '" + dev_call_off_status.SN + "\'";
                                                        while (!sql_client.connect())
                                                        {
                                                            Thread.Sleep(300);
                                                        }
                                                        sql_client.modify(sql_cmd);
                                                        sql_client.disconnect();
                                                    }
                                                    break;
                                                case "Mobile_to_Land":
                                                    sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  connect_type = '3'
WHERE
  custom.voice_connect.serial_no = '" + dev_call_off_status.SN + "\'";
                                                    while (!sql_client.connect())
                                                    {
                                                        Thread.Sleep(300);
                                                    }
                                                    sql_client.modify(sql_cmd);
                                                    sql_client.disconnect();
                                                    if (parse_package.ContainsKey("target_id"))
                                                    {
                                                        sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  target = " + "\'" + parse_package["target_id"].ToString() + "\'" + @"
WHERE
  custom.voice_connect.serial_no = '" + dev_call_off_status.SN + "\'";
                                                        while (!sql_client.connect())
                                                        {
                                                            Thread.Sleep(300);
                                                        }
                                                        sql_client.modify(sql_cmd);
                                                        sql_client.disconnect();
                                                    }
                                                    break;
                                            }

                                        }
                                        if (!string.IsNullOrEmpty((parse_package["target_id"])) || !parse_package["target_id"].Equals("0"))
                                            Call_status.Remove(dev_call_off_status.ID + parse_package["call_type"]);
                                    }
                                   
                                    break;
                                case "Call_State_Change"://channel/site change
                                    #region Individual_Call
                                    object CSClock = new object();
                                    string callStateChangeSn = string.Empty;
                                    Device_call_status dev_call_state_change = new Device_call_status();
                                    lock (CSClock)
                                    {
                                        dev_call_state_change.ID = parse_package["source_id"].ToString();
                                        if (parse_package.ContainsKey("channel"))
                                            dev_call_state_change.channel = parse_package["channel"];
                                        if (parse_package.ContainsKey("site"))
                                            dev_call_state_change.site = parse_package["site"];
                                    }
                                    
                                    if (CheckIfUidInEquipmentTable(dev_call_state_change.ID))
                                    { }
                                    else
                                    {
                                        break;
                                    }
                                    #region Individual_Call or land<->mobile

                                    Device_call_status find_call_state_change_sn;
                                    if (Call_status.ContainsKey(dev_call_state_change.ID + parse_package["call_type"]))
                                    {
                                        //Individual_Call
                                        find_call_state_change_sn = (Device_call_status)Call_status[dev_call_state_change.ID + parse_package["call_type"]];
                                        callStateChangeSn = dev_call_state_change.SN = find_call_state_change_sn.SN;

                                        sql_table_columns = "custom.voice_connect";
                                        sql_cmd = "UPDATE " + sql_table_columns + " SET channel=\'" + dev_call_state_change.channel + "\'"+","+
                                            "site=\'" + dev_call_state_change.site+"\' WHERE serial_no=\'" + callStateChangeSn + "\'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                    }
                                    else
                                    {
                                        #region land<->mobile
                                        #endregion land<->mobile
                                    }
                                    #endregion
                                    #endregion Individual_Call
                                    break;
                            }
                            //if (parse_package.ContainsKey("Radio_Type_Qualifier"))
                            if (parse_package.ContainsKey("call_type") && parse_package.ContainsKey("start_call_time"))
                            {
                                object LMLock = new object();
                                #region access power status

                                {
                                    string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                    sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["call_type"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                    while (!sql_client.connect())
                                    {
                                        Thread.Sleep(300);
                                    }
                                    sql_client.modify(sql_cmd);
                                    sql_client.disconnect();
                                }
                                #endregion
                                
                                
                                if (CheckIfUidInEquipmentTable(parse_package["source_id"]))
                                { }
                                else
                                {
                                    return;
                                }
                                #region

                                Device_call_status dev_call_status;
                                if (Call_status.ContainsKey(parse_package["source_id"] + parse_package["call_type"]))
                                {
                                    lock (LMLock)
                                    {
                                        dev_call_status = (Device_call_status)Call_status[parse_package["source_id"] + parse_package["call_type"]];
                                    }
                                    
                                    
                                }
                                else
                                {
                                    return;
                                }
                                #endregion

                                string start_call_time = string.Empty, end_call_time = string.Empty;
                                
                                
                                switch (parse_package["call_type"].ToString())
                                {
                                    case "Land_to_Mobile"://L uid = null
                                        lock (LMLock)
                                        {
                                            dev_call_status.start_call_time = parse_package["start_call_time"].ToString();
                                            dev_call_status.end_call_time = parse_package["timestamp"].ToString();
                                            string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                            start_call_time = dev_call_status.start_call_time.Substring(0, 4) + "-" + dev_call_status.start_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.start_call_time.Substring(6, 2) + " " + dev_call_status.start_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.start_call_time.Substring(10, 2) + ":" + dev_call_status.start_call_time.Substring(12, 2);
                                            end_call_time = dev_call_status.end_call_time.Substring(0, 4) + "-" + dev_call_status.end_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.end_call_time.Substring(6, 2) + " " + dev_call_status.end_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.end_call_time.Substring(10, 2) + ":" + dev_call_status.end_call_time.Substring(12, 2);

                                        }
                                        sql_cmd = @"UPDATE 
  custom.voice_connect
SET
  start_time = " +"\'"+start_call_time+"\'"+@",
  end_time = " + "\'" + end_call_time + "\'" + @",
connect_type = '4',
target = NULL,
  gtarget = NULL
WHERE
  custom.voice_connect.serial_no = '" + dev_call_status.SN+@"'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                        break;
                                    case "Mobile_to_Land"://M target = null
                                        lock (LMLock)
                                        {
                                            dev_call_status.start_call_time = parse_package["start_call_time"].ToString();
                                            dev_call_status.end_call_time = parse_package["timestamp"].ToString();
                                            string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                            start_call_time = dev_call_status.start_call_time.Substring(0, 4) + "-" + dev_call_status.start_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.start_call_time.Substring(6, 2) + " " + dev_call_status.start_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.start_call_time.Substring(10, 2) + ":" + dev_call_status.start_call_time.Substring(12, 2);
                                            end_call_time = dev_call_status.end_call_time.Substring(0, 4) + "-" + dev_call_status.end_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.end_call_time.Substring(6, 2) + " " + dev_call_status.end_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.end_call_time.Substring(10, 2) + ":" + dev_call_status.end_call_time.Substring(12, 2);

                                        }
                                        lock (LMLock)
                                        {
                                            dev_call_status.targetID = parse_package["target_id"];
                                        }
                                        sql_cmd = @"UPDATE 
  custom.voice_connect
SET
connect_type = '3',
  start_time = " + "\'" + start_call_time + "\'" + @",
  end_time = " + "\'" + end_call_time + "\'" + @",
  target = " + "\'" + dev_call_status.targetID + "\'" + @"
WHERE
  custom.voice_connect.serial_no = '" + dev_call_status.SN + @"'";
                                        while (!sql_client.connect())
                                        {
                                            Thread.Sleep(300);
                                        }
                                        sql_client.modify(sql_cmd);
                                        sql_client.disconnect();
                                        break;

                                }
                                Call_status.Remove(parse_package["source_id"] + parse_package["call_type"]);
                            }

                            if (parse_package.ContainsKey("call_type") && false)
                            {
                                switch (parse_package["call_type"].ToString())
                                {
                                    case "Land_to_Mobile"://L
                                        {
                                            #region access power status

                                            {
                                                string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                                sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["call_type"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                sql_client.modify(sql_cmd);
                                                sql_client.disconnect();
                                            }
                                            #endregion
                                            Device_call_status dev_call_status = new Device_call_status();
                                            dev_call_status.call_type = "4";
                                            dev_call_status.ID = parse_package["source_id"].ToString();
                                            if (CheckIfUidInEquipmentTable(dev_call_status.ID))
                                            {
                                            }
                                            else
                                            {
                                                break;
                                            }
                                            dev_call_status.start_call_time = parse_package["start_call_time"].ToString();
                                            dev_call_status.end_call_time = parse_package["timestamp"].ToString();
                                            string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                            string start_call_time = dev_call_status.start_call_time.Substring(0, 4) + "-" + dev_call_status.start_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.start_call_time.Substring(6, 2) + " " + dev_call_status.start_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.start_call_time.Substring(10, 2) + ":" + dev_call_status.start_call_time.Substring(12, 2);
                                            string end_call_time = dev_call_status.end_call_time.Substring(0, 4) + "-" + dev_call_status.end_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.end_call_time.Substring(6, 2) + " " + dev_call_status.end_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.end_call_time.Substring(10, 2) + ":" + dev_call_status.end_call_time.Substring(12, 2);
                                            /*SELECT 
        custom.voice_connect.serial_no
        FROM
        custom.voice_connect
        WHERE
        custom.voice_connect.connect_type = '3' AND 
        custom.voice_connect.uid = '930000'
        ORDER BY
        custom.voice_connect.create_time DESC
        LIMIT 1*/
                                            if (true)
                                            {
                                                string sn = string.Empty;
                                                sql_cmd = @"SELECT 
  custom.voice_connect.serial_no
FROM
  custom.voice_connect
WHERE
  custom.voice_connect.uid = '" + dev_call_status.ID + @"'
ORDER BY
  custom.voice_connect.create_time DESC
LIMIT 1";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                var dt = sql_client.get_DataTable(sql_cmd);
                                                sql_client.disconnect();
                                                if (dt != null && dt.Rows.Count != 0)
                                                {
                                                    foreach (DataRow row in dt.Rows)
                                                    {
                                                        sn = row[0].ToString();
                                                    }
                                                    Console.WriteLine("dev_call_status.ID.Length=" + dev_call_status.ID.Length);
                                                    Console.WriteLine("dev_call_status.ID=" + dev_call_status.ID);

                                                    string yyyyMMdd = sn.Substring(dev_call_status.ID.Length, 8);
                                                    string count = sn.Substring(dev_call_status.ID.Length + yyyyMMdd.Length, 5);
                                                    if (start_call_today.Equals(yyyyMMdd))
                                                    {
                                                        uint addCount = (uint.Parse(count) + 1);
                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + addCount.ToString("D5");
                                                    }
                                                    else
                                                    {
                                                        int iVal = 0;

                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                    }
                                                }
                                                else
                                                {
                                                    int iVal = 0;

                                                    dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                }
                                            }
                                            else
                                            {
                                                if (AddValue(dev_call_status.ID, start_call_today + "," + "00000"))
                                                {
                                                    int iVal = 0;

                                                    dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                }
                                                else
                                                {
                                                    string sn = ConfigurationManager.AppSettings[dev_call_status.ID].ToString();
                                                    string[] sn_sub = sn.Split(',');
                                                    if (sn_sub[0] != start_call_today)
                                                    {
                                                        int iVal = 0;

                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                        ModifyValue(dev_call_status.ID, start_call_today + "," + "00000");
                                                    }
                                                    else
                                                    {
                                                        uint count = uint.Parse(sn_sub[1]) + 1;
                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + count.ToString("D5");
                                                        ModifyValue(dev_call_status.ID, start_call_today + "," + count.ToString("D5"));
                                                    }
                                                }
                                            }

                                            sql_table_columns = "serial_no,uid,connect_type,start_time,end_time,create_user,create_ip";
                                            sql_table_column_value = "\'" + dev_call_status.SN + "\'" + "," + "\'" + dev_call_status.ID + "\'" + "," + "\'" +
                                                dev_call_status.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "\'" + end_call_time + "\'" +
                                                "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'";
                                            sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            sql_client.modify(sql_cmd);
                                            sql_client.disconnect();
                                        }
                                        break;
                                    case "Mobile_to_Land"://M
                                        {
                                            #region access power status

                                            {
                                                string unsUpdateTimeStamp = DateTime.Now.ToString("yyyyMMdd HHmmss+8");
                                                sql_cmd = @"UPDATE 
  custom.atia_device_power_status
SET
  ""callStatus"" = '" + parse_package["call_type"].ToString() + @"',
""updateTime"" = '" + unsUpdateTimeStamp + @"'::timestamp
WHERE
  custom.atia_device_power_status.uid = '" + parse_package["source_id"].ToString() + @"'";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                sql_client.modify(sql_cmd);
                                                sql_client.disconnect();
                                            }
                                            #endregion
                                            Device_call_status dev_call_status = new Device_call_status();
                                            dev_call_status.call_type = "3";
                                            dev_call_status.ID = parse_package["source_id"].ToString();
                                            dev_call_status.targetID = parse_package["target_id"];
                                            if (CheckIfUidInEquipmentTable(dev_call_status.ID))
                                            {
                                            }
                                            else
                                            {
                                                break;
                                            }
                                            dev_call_status.start_call_time = parse_package["start_call_time"].ToString();
                                            dev_call_status.end_call_time = parse_package["timestamp"].ToString();
                                            string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                            string start_call_time = dev_call_status.start_call_time.Substring(0, 4) + "-" + dev_call_status.start_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.start_call_time.Substring(6, 2) + " " + dev_call_status.start_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.start_call_time.Substring(10, 2) + ":" + dev_call_status.start_call_time.Substring(12, 2);
                                            string end_call_time = dev_call_status.end_call_time.Substring(0, 4) + "-" + dev_call_status.end_call_time.Substring(4, 2) + "-" +
                                            dev_call_status.end_call_time.Substring(6, 2) + " " + dev_call_status.end_call_time.Substring(8, 2) + ":" +
                                            dev_call_status.end_call_time.Substring(10, 2) + ":" + dev_call_status.end_call_time.Substring(12, 2);
                                            /*SELECT 
        custom.voice_connect.serial_no
        FROM
        custom.voice_connect
        WHERE
        custom.voice_connect.connect_type = '3' AND 
        custom.voice_connect.uid = '930000'
        ORDER BY
        custom.voice_connect.create_time DESC
        LIMIT 1*/
                                            if (true)
                                            {
                                                string sn = string.Empty;
                                                sql_cmd = @"SELECT 
  custom.voice_connect.serial_no
FROM
  custom.voice_connect
WHERE
  custom.voice_connect.uid = '" + dev_call_status.ID + @"'
ORDER BY
  custom.voice_connect.create_time DESC
LIMIT 1";
                                                while (!sql_client.connect())
                                                {
                                                    Thread.Sleep(300);
                                                }
                                                var dt = sql_client.get_DataTable(sql_cmd);
                                                sql_client.disconnect();
                                                if (dt != null && dt.Rows.Count != 0)
                                                {
                                                    foreach (DataRow row in dt.Rows)
                                                    {
                                                        sn = row[0].ToString();
                                                    }
                                                    Console.WriteLine("dev_call_status.ID.Length=" + dev_call_status.ID.Length);
                                                    Console.WriteLine("dev_call_status.ID=" + dev_call_status.ID);

                                                    string yyyyMMdd = sn.Substring(dev_call_status.ID.Length, 8);
                                                    string count = sn.Substring(dev_call_status.ID.Length + yyyyMMdd.Length, 5);
                                                    if (start_call_today.Equals(yyyyMMdd))
                                                    {
                                                        uint addCount = (uint.Parse(count) + 1);
                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + addCount.ToString("D5");
                                                    }
                                                    else
                                                    {
                                                        int iVal = 0;

                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                    }
                                                }
                                                else
                                                {
                                                    int iVal = 0;

                                                    dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                }
                                            }
                                            else
                                            {
                                                if (AddValue(dev_call_status.ID, start_call_today + "," + "00000"))
                                                {
                                                    int iVal = 0;

                                                    dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                }
                                                else
                                                {
                                                    string sn = ConfigurationManager.AppSettings[dev_call_status.ID].ToString();
                                                    string[] sn_sub = sn.Split(',');
                                                    if (sn_sub[0] != start_call_today)
                                                    {
                                                        int iVal = 0;

                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + iVal.ToString("D5");
                                                        ModifyValue(dev_call_status.ID, start_call_today + "," + "00000");
                                                    }
                                                    else
                                                    {
                                                        uint count = uint.Parse(sn_sub[1]) + 1;
                                                        dev_call_status.SN = dev_call_status.ID + start_call_today + count.ToString("D5");
                                                        ModifyValue(dev_call_status.ID, start_call_today + "," + count.ToString("D5"));
                                                    }
                                                }
                                            }

                                            sql_table_columns = "serial_no,uid,connect_type,start_time,end_time,create_user,create_ip,target";
                                            sql_table_column_value = "\'" + dev_call_status.SN + "\'" + "," + "\'" + dev_call_status.ID + "\'" + "," + "\'" +
                                                dev_call_status.call_type + "\'" + "," + "\'" + start_call_time + "\'" + "," + "\'" + end_call_time + "\'" +
                                                "," + "0" + "," + "\'" + GetLocalIPAddress() + "\'" + "," + "\'" + dev_call_status.targetID + "\'";
                                            sql_cmd = "INSERT INTO custom.voice_connect (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";
                                            while (!sql_client.connect())
                                            {
                                                Thread.Sleep(300);
                                            }
                                            sql_client.modify(sql_cmd);
                                            sql_client.disconnect();
                                        }
                                        break;
                                }
                            }
                            Console.WriteLine(sql_cmd);
                            log.Info(sql_cmd);

                        }
                        Console.WriteLine("-AccessSqlServer");
                    }
                }
                
            }

        private static bool CheckIfUidInEquipmentTable(string id)
        {
            string regSqlCmd = @"SELECT
  sd.equipment.uid
  FROM
  sd.equipment
  where
  sd.equipment.uid = '"+id+@"'";
            var sql_client = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
            while (!sql_client.connect())
            {
                Thread.Sleep(300);
            }
                            var dt = sql_client.get_DataTable(regSqlCmd);
                            sql_client.disconnect();
            if (dt != null && dt.Rows.Count != 0)
                return true;
            else
            {
                return false;
            }
        }

        private static void InsertPowerOnOffEventToPublicGpsLogAndToAvlsLog(Device_power_status dev_power_status, string result, SortedDictionary<string, string> parse_package)
            {
                AUTO_SQL_DATA gps_log = new AUTO_SQL_DATA();
                
                        var sqlClient =
                            new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"],
                                ConfigurationManager.AppSettings["SQL_SERVER_PORT"],
                                ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"],
                                ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"],
                                ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"],
                                ConfigurationManager.AppSettings["Pooling"],
                                ConfigurationManager.AppSettings["MinPoolSize"],
                                ConfigurationManager.AppSettings["MaxPoolSize"],
                                ConfigurationManager.AppSettings["ConnectionLifetime"]);
                        string sqlCmd = @"SELECT 
  public._gps_log._lat,
  public._gps_log._lon
FROM
  public._gps_log
WHERE
  public._gps_log._time < now() AND 
  public._gps_log._uid = '" + dev_power_status.ID + @"'
ORDER BY
  public._gps_log._time DESC
LIMIT 1";
                        log.Info("avlsSqlCmd=" + Environment.NewLine + sqlCmd);
                        while (!sqlClient.connect())
                        {
                            Thread.Sleep(300);
                        }
                        var dt = sqlClient.get_DataTable(sqlCmd);
                        sqlClient.disconnect();
                        string lat = string.Empty, lon = string.Empty;
                        if (dt != null && dt.Rows.Count != 0)
                        {
                            
                            foreach (DataRow row in dt.Rows)
                            {
                                lat = row[0].ToString();
                                lon = row[1].ToString();
                            }
                            string zero = "0";
                            if (lat.Equals(zero) || lon.Equals(zero))
                            {
                                GetInitialLocationFromSql(ref lat, ref lon, dev_power_status.ID);
                            }

                        }
                        else
                        {
                           
                            GetInitialLocationFromSql(ref lat, ref lon, dev_power_status.ID);

                        }
                        gps_log._uid = "\'" + dev_power_status.ID + "\'";
                        string now = DateTime.Now.ToString("yyyyMMdd");
            while (now == null || now == string.Empty)
            {
                now = DateTime.Now.ToString("yyyyMMdd");
            }

            while (!sqlClient.connect())
            {
                Thread.Sleep(300);
            }
                        string auto_id_serial_command = sqlClient.get_DataTable("SELECT COUNT(_uid)   FROM public._gps_log").Rows[0].ItemArray[0].ToString();
                        sqlClient.disconnect();

                        gps_log._id = "\'" + dev_power_status.ID + "_" + now + "_" + auto_id_serial_command + "\'";
                        gps_log._lat = gps_log._or_lat =   lat;
                        gps_log._lon = gps_log._or_lon =   lon;
                        gps_log._option3 = "\'" + result + "\'";
                        gps_log._validity = "\'D\'";

                gps_log._satellites = gps_log._temperature = gps_log._voltage = "0";
                string table_columns = "_id,_uid,_option3,_or_lon,_or_lat,_satellites,_temperature,_voltage,_lat,_lon,_validity";
                        string table_column_value = gps_log._id + "," + gps_log._uid +  "," + gps_log._option3 + "," +
                            gps_log._or_lon + "," + gps_log._or_lat + "," + gps_log._satellites + "," +
                                       gps_log._temperature + "," + gps_log._voltage + ","  + gps_log._lat
                                       +
                                       "," + gps_log._lon +
                                       "," + gps_log._validity;
                        string cmd = "INSERT INTO public._gps_log (" + table_columns + ") VALUES (" + table_column_value + ")";
                        while (!sqlClient.connect())
                        {
                            Thread.Sleep(300);
                        }
                sqlClient.modify(cmd);
                sqlClient.disconnect();

                //avls
            
                #region avls
            /*
                string avlsLat = string.Empty, avlsLon = string.Empty;
                log.Info("+AccessAvlsServer:if");
                Console.WriteLine("+AccessAvlsServer:if");
                TcpClient avls_tcpClient;
                string send_string = string.Empty;
                AVLS_UNIT_Report_Packet avls_package = new AVLS_UNIT_Report_Packet();
                //string ipAddress = "127.0.0.1";
                string ipAddress = ConfigurationManager.AppSettings["AVLS_SERVER_IP"];
                //int port = 23;
                int port = int.Parse(ConfigurationManager.AppSettings["AVLS_SERVER_PORT"]);

                avls_tcpClient = new TcpClient();

                //avls_tcpClient.Connect(ipAddress, port);
                connectDone.Reset();
                avls_tcpClient.BeginConnect(ipAddress, port, new AsyncCallback(AvlsConnectCallback), avls_tcpClient);
                connectDone.WaitOne();

                //avls_tcpClient.NoDelay = false;

                //Keeplive.keep(avls_tcpClient.Client);
                NetworkStream netStream = avls_tcpClient.GetStream();

                //if (parse_package.ContainsKey("result") && (parse_package["result"].ToString().Equals("power_on") || parse_package["result"].ToString().Equals("power_off")))
                //{
                switch (result)
                {
                    case "power_on":
                        avls_package.Event = "181,";
                        break;
                    case "power_off":
                        avls_package.Event = "182,";
                        break;
                }


                avls_package.Date_Time = parse_package["timestamp"].ToString().Substring(2, 12);
                //log.Info("avls_package.Date_Time=" + avls_package.Date_Time);
                DateTime tempDatetime = DateTime.ParseExact(avls_package.Date_Time, "yyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                tempDatetime = tempDatetime.ToUniversalTime();
                avls_package.Date_Time = tempDatetime.ToString("yyMMddHHmmss") + ",";

                avls_package.ID = parse_package["source_id"].ToString();
                avls_package.GPS_Valid = "A,";

                //GeoAngle lat_value = GeoAngle.FromDouble(Convert.ToDecimal(lat));
                //GeoAngle long_value = GeoAngle.FromDouble(Convert.ToDecimal(lon));
                //string lat_str = lat_value.Degrees.ToString() + lat_value.Minutes.ToString("D2") + "." + lat_value.Seconds.ToString("D2") + lat_value.Milliseconds.ToString("D3");
                //string long_str = long_value.Degrees.ToString() + long_value.Minutes.ToString("D2") + "." + long_value.Seconds.ToString("D2") + long_value.Milliseconds.ToString("D3");
                //avls_package.Loc = "N" + (Convert.ToDouble(htable["lat_value"])*100).ToString() + "E" + (Convert.ToDouble(htable["long_value"])*100).ToString()+ ",";
                string lat_str = lat, long_str = lon;
                ConvertLocToAvlsLoc(ref lat_str, ref long_str); 
                avls_package.Loc = "N" + lat_str + "E" + long_str + ",";
                //avls_package.Loc = "N00000.0000E00000.0000,";

                avls_package.Speed = "0,";
                avls_package.Dir = "0,";
                avls_package.Temp = "NA,";
                avls_package.Status = "00000000,";
                avls_package.Message = "test";

                //}
                avls_package.ID += ",";
                send_string = "%%" + avls_package.ID + avls_package.GPS_Valid + avls_package.Date_Time + avls_package.Loc + avls_package.Speed + avls_package.Dir + avls_package.Temp + avls_package.Status + avls_package.Event + avls_package.Message + "\r\n";


                sendDone.Reset();
                avls_WriteLine(netStream, System.Text.Encoding.Default.GetBytes(send_string), send_string);
                sendDone.WaitOne();

                //ReadLine(avls_tcpClient, netStream, send_string.Length);
                netStream.Close();
                avls_tcpClient.Close();
                log.Info("-AccessAvlsServer:if");
                Console.WriteLine("-AccessAvlsServer:if");
             * */
                #endregion
            }

            private static void parse_data_section(byte[] p, ref SortedDictionary<string, string> parse_package)
            {
                //get unique id and timestamp(uid,timestamp)
                
                if(parse_package.ContainsKey("cmd"))
                {
                    switch (parse_package["cmd"].ToString())
                    {
                        case "Flexible_Controlling_Zone_Update":
                            {
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS+2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Requester_section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS+6).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Status_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Target_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 8).Take(2).Reverse().ToArray(), 0); 
                                const int offset_to_call_section_Timestamp = 0;
                                const int offset_to_req_section_Primary_ID = 0;
                                const int offset_to_call_section_ucn = 8;
                                const int offset_to_status_section_Overall_Call_Status = 0;
                                const int offset_to_status_section_Reason_for_Busy = 1;
                                const int Offset_to_Target_Section_Secondary_ID = 0;
                                const int offset_to_call_section_call_type = 14;
                                byte[] timestamp = new byte[8];
                                byte[] uid = new byte[4];
                                byte[] ucn = new byte[4];//Universal Call Number
                                byte[] snd_id = new byte[4];
                                byte[] call_type = new byte[1];
                                byte call_status, reason_for_busy;
                                timestamp = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = p.Skip((int)Offset_to_Requester_section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_req_section_Primary_ID).Take(uid.Length).Reverse().ToArray();
                                ucn = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_ucn).Take(ucn.Length).Reverse().ToArray();
                                //call_status = p[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Overall_Call_Status-1];
                                //reason_for_busy = p[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Reason_for_Busy-1];
                                snd_id = p.Skip((int)Offset_to_Target_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Target_Section_Secondary_ID).Take(snd_id.Length).Reverse().ToArray();
                                call_type = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_call_type).Take(call_type.Length).Reverse().ToArray();
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_ucn(ucn,ref  parse_package);
                                //parse_call_status(call_status);
                                //parse_reason_for_busy(reason_for_busy);
                                if (!Offset_to_Target_Section.Equals(0))
                                    parse_snd_id(snd_id,ref  parse_package);
                                else
                                {
                                    parse_package.Add("target_id", "null");
                                }
                                parse_call_type(call_type[0],ref parse_package);
                            }
                            break;
                        case "Flexible_Mobility_Update":
                            {
                                uint Offset_to_Status_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Unit_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Group_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 6).Take(2).Reverse().ToArray(), 0);
                                const int Offset_to_Status_Section_Timestamp = 0;
                                const int Offset_to_Unit_Section_Operating_Unit_ID = 0;
                                const int Offset_to_Group_Section_Operating_Group_ID = 0;
                                byte[] timestamp = new byte[8];
                                byte[] uid = new byte[4];
                                byte[] gid = new byte[4];
                                timestamp = p.Skip((int)Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Status_Section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = p.Skip((int)Offset_to_Unit_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Unit_Section_Operating_Unit_ID).Take(uid.Length).Reverse().ToArray();
                                gid = p.Skip((int)Offset_to_Group_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Group_Section_Operating_Group_ID).Take(gid.Length).Reverse().ToArray();                                
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_gid(gid);
                            }
                            break;
                        case "Flexible_Call_Activity_Update":
                            {
                                //get secondary ID for target device combine with primary id to dicide end call will be raise with limite in the same group
                                //Primary Alias , local radio device id maybe e.g. Zone01-Radio-02
                                //Secondary Alias, group id maybe e.g. Zone01-TG04
                                //Requester’s Affiliated TG Alias, group id maybe the same with Secondary Alias e.g. Zone01-TG04

                                byte[] timestamp = new byte[8];
                                byte[] uid = new byte[4];
                                byte[] ucn = new byte[4];//Universal Call Number
                                byte[] snd_id = new byte[4];
                                byte[] channel = new byte[2];
                                byte[] site = new byte[2];
                                byte[] call_type = new byte[1];
                                byte[] Radio_Type_Qualifier = new byte[4];
                                byte call_status, reason_for_busy;
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Busy_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Requester_section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 8).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Target_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 10).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_RF_Sites_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 18).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Active_RFSites_Channel_Info = BitConverter.ToUInt16(p.Skip((int)Offset_to_RF_Sites_Section+8).Take(2).Reverse().ToArray(), 0);
                                uint Num_Active_RF_Site_Channels = BitConverter.ToUInt16(p.Skip((int)Offset_to_RF_Sites_Section + (int)Offset_to_Active_RFSites_Channel_Info).Take(2).Reverse().ToArray(), 0);
                                const int offset_to_call_section_Timestamp = 0;
                                const int offset_to_call_section_ucn = 8;
                                const int offset_to_call_section_call_status = 31;
                                const int offset_to_busy_section_reason_of_busy = 0;
                                const int offset_to_req_section_Primary_ID = 0;
                                const int Offset_to_Target_Section_Secondary_ID = 0;
                                const int offset_to_call_section_call_type = 16;
                                const int offset_to_call_section_Radio_Type_Qualifier = 18;
                                const int offset_to_Requester_s_Affiliated_Site = 12;
                                timestamp = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = p.Skip((int)Offset_to_Requester_section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_req_section_Primary_ID).Take(uid.Length).Reverse().ToArray();
                                snd_id = p.Skip((int)Offset_to_Target_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Target_Section_Secondary_ID).Take(snd_id.Length).Reverse().ToArray();
                                ucn = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_ucn).Take(ucn.Length).Reverse().ToArray();
                                call_type = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_call_type).Take(call_type.Length).Reverse().ToArray();
                                Radio_Type_Qualifier = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Radio_Type_Qualifier).Take(Radio_Type_Qualifier.Length).ToArray();

                                //parse_package.Add("debug:Radio_Type_Qualifier", ByteToHexBitFiddle(p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Radio_Type_Qualifier).Take(Radio_Type_Qualifier.Length).ToArray()));
                                //call_status = p[Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_call_status-1];
                                //reason_for_busy = p[Offset_to_Busy_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_busy_section_reason_of_busy-1];
                                site = p.Skip((int)Offset_to_Requester_section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_Requester_s_Affiliated_Site).Take(site.Length).Reverse().ToArray();
                                if (!Num_Active_RF_Site_Channels.Equals(0))
                                {
                                    channel = p.Skip((int)Offset_to_RF_Sites_Section + (int)Offset_to_Active_RFSites_Channel_Info + 4).Take(channel.Length).Reverse().ToArray();
                                    parse_channel(channel, ref parse_package);
                                    //site = p.Skip((int)Offset_to_RF_Sites_Section + (int)Offset_to_Active_RFSites_Channel_Info + 2).Take(site.Length).Reverse().ToArray();
                                    //parse_site(site, ref parse_package);
                                }
                                parse_site(site, ref parse_package);    
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                if (!Offset_to_Target_Section.Equals(0))
                                    parse_snd_id(snd_id,ref  parse_package);
                                else
                                {
                                    parse_package.Add("target_id", "null");
                                }
                                parse_ucn(ucn,ref  parse_package);
                                parse_call_type(call_type[0], ref parse_package);
                                parse_Radio_Type_Qualifier(Radio_Type_Qualifier, ref parse_package);
                                //parse_call_status(call_status);
                                //parse_reason_for_busy(reason_for_busy);                                 
                            }
                            break;
                        case "Flexible_End_of_Call":
                            {
                                byte[] timestamp = new byte[8];
                                byte[] ucn = new byte[4];//Universal Call Number
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                const int offset_to_call_section_Timestamp = 0;
                                const int offset_to_call_section_ucn = 8;
                                timestamp = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                ucn = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_ucn).Take(ucn.Length).Reverse().ToArray();
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(ucn,ref  parse_package);
                            }
                            break;
                            
                        case "Flexible_Interconnect_Call_Billing":
                            {
                                //phone call use api:Flexible Interconnect Call Billing Info Packet
                                //field use:Call Section
                                //-Duration in Seconds
                                //-Subscriber ID->uid
                                //-Call Type->call direction
                                /*--L-> Land to Mobile
                                 * --M->Mobile to Land
                                 * --T->Land to Talkgroup
                                 * [09/12/13 00:45:37] Interconnect Call Billing Info Packet - MBX Info Type : CALL {Universal Call # (lower comp) = 128 ; Controlling Zone ID = 1 ; Duration in Seconds = 13 ; Subscriber ID = 1(0x1) "Z1RADIO01" [Security Id=1] ; Type = Mobile to Land} INTERCONNECT {Route # = 1} PHONE NUMBER {Phone Encoding = n/a ; Phone # = 9999906} 
00:45:37    mobile_to_land PHONE INFO - - - - - - - -  radio:  1        00m13 9999906
                                 * mobile_to_land->radio to phone
                                 * radio:1->radio device id=1
                                 * 00m13->duration time 13 sec
                                 * 9999906->phone number
                                 * ------------------------------------
                                 * land-to-mobile PHONE INFO - - - - - - - -  radio:  1        00m12
                                 * phone to radio,radio uid=1,duration time is 12sec, unknow phone number
                                 */
                                byte[] duration_in_sec = new byte[4];
                                byte[] uid = new byte[4];
                                byte[] timestamp = new byte[8];
                                byte[] phone_length = new byte[2];
                                byte[] Call_Type = new byte[1];
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Phone_Number_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 6).Take(2).Reverse().ToArray(), 0);
                                const int offset_to_call_section_timestamp = 0;
                                const int offset_to_call_section_duration_in_sec = 14;
                                const int offset_to_call_uid = 18;
                                const int offset_to_call_Call_Type = 22;
                                const int offset_to_phone_Length_Phone_Number = 2;
                                const int offset_to_phone_Phone_Number = 4;
                                timestamp = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_timestamp).Take(timestamp.Length).Reverse().ToArray();
                                duration_in_sec = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_duration_in_sec).Take(duration_in_sec.Length).Reverse().ToArray();
                                uid = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_uid).Take(uid.Length).Reverse().ToArray();
                                Call_Type = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_Call_Type).Take(Call_Type.Length).Reverse().ToArray();
                                phone_length = p.Skip((int)Offset_to_Phone_Number_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_phone_Length_Phone_Number).Take(phone_length.Length).Reverse().ToArray();
                                uint phone_length_uint = BitConverter.ToUInt16(phone_length.Take(phone_length.Length).ToArray(), 0);
                                
                                byte[] phone = new byte[phone_length_uint];
                                phone = p.Skip((int)Offset_to_Phone_Number_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_phone_Phone_Number).Take(phone.Length).ToArray();
                                if (!(Offset_to_Phone_Number_Section.Equals(0) && phone_length.Equals(0)))
                                    parse_phone(phone, ref parse_package);
                                else
                                {
                                    parse_package.Add("target_id", "null");
                                }
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_duration_in_sec(duration_in_sec,ref  parse_package);
                                parse_call_type(Call_Type[0],ref  parse_package);
                            }
                            break;
                    }
                }
            }

            private static void parse_site(byte[] site, ref SortedDictionary<string, string> parsePackage)
            {
                string siteString = string.Empty;
                uint site_uint = BitConverter.ToUInt16(site.Take(site.Length).ToArray(), 0);
                siteString = site_uint.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(siteString))
                    parsePackage.Add("site", siteString);
                else
                {
                    parsePackage.Add("site", "null");
                }
            }

        private static void parse_Radio_Type_Qualifier(byte[] radioTypeQualifier, ref SortedDictionary<string, string> parsePackage)
        {
            string s = string.Join("",
    radioTypeQualifier.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')).ToArray());
            /*
            Console.WriteLine(ByteToHexBitFiddle(radioTypeQualifier));
            Console.WriteLine(s);
            Console.WriteLine(s.ToCharArray());
            Console.WriteLine(Reverse(s));
            char[] charArray = Reverse(s).ToCharArray();
            foreach (var VARIABLE in charArray)
            {
                Console.WriteLine(VARIABLE);
            }
             */
            parsePackage.Add("debug:radioTypeQualifier",ByteToHexBitFiddle(radioTypeQualifier));
            parsePackage.Add("debug:s", s);
            char[] flag = Reverse(s).ToCharArray();
            //char[] flag = s.ToCharArray();
            parsePackage.Add("debug:flag",new string(flag));
            //log.Info(flag);
            parsePackage.Add("Radio_Type_Qualifier", string.Empty);
            if (flag[27].Equals('1')) //Interconnect
            {
                if (flag[25].Equals('1'))//Landline Call
                {
                    //parsePackage.Add("Radio_Type_Qualifier", "Land_to_Mobile");
                    parsePackage["Radio_Type_Qualifier"] += "Land_to_Mobile";
                }
                else
                {
                    //parsePackage.Add("Radio_Type_Qualifier", "Mobile_to_Land");
                    parsePackage["Radio_Type_Qualifier"] += "Mobile_to_Land";
                }
            }
            if (flag[20].Equals('1') && false)
            {
                parsePackage["Radio_Type_Qualifier"] += "ASTRO call,";
            }

        }
        private static string Reverse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = text.Length - 1; i >= 0; i--)
            {
                builder.Append(text[i]);
            }

            return builder.ToString();
        }
        private static void parse_Radio_Type_Qualifier(byte[] radioTypeQualifier)
        {
            string s = string.Join("",
    radioTypeQualifier.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')).ToArray());
            Console.WriteLine(ByteToHexBitFiddle(radioTypeQualifier));
            Console.WriteLine(s);
            Console.WriteLine(s.ToCharArray());
            Console.WriteLine(Reverse(s));
            char[] charArray = Reverse(s).ToCharArray();
            int count = 0;
            foreach (var VARIABLE in charArray)
            {
                count++;
                Console.WriteLine(count+":"+VARIABLE);
            }
        }
        private static void parse_channel(byte[] channel, ref SortedDictionary<string, string> parsePackage)
        {
            string channelStr = string.Empty;
            uint channel_uint = BitConverter.ToUInt16(channel.Take(channel.Length).ToArray(), 0);
            channelStr = channel_uint.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(channelStr))
                parsePackage.Add("channel",channelStr);
            else
            {
                parsePackage.Add("channel", "null");
            }
        }

        private static void parse_phone(byte[] phone, ref SortedDictionary<string, string> parse_package)
            {
                string result = string.Empty;
                foreach (byte x in phone)
                {
                    result += Convert.ToChar(x);
                }
                //parse_package.Add("phone_number", result);
                parse_package.Add("target_id", result);
            }
            /// <summary>
            /// Call Type Description
            ///L Land to Mobile
            ///M Mobile to Land
            ///T Land to Talkgroup
            /// </summary>
            /// <param name="Call_Type"></param>
            private static void parse_call_type(byte Call_Type, ref SortedDictionary<string, string> parse_package)
            {
                /*
                char result = Convert.ToChar(Call_Type);//L , M or T
                switch (result)
                {
                    case 'L':
                        parse_package.Add("call_type", "Land_to_Mobile");
                        break;
                    case 'M':
                        parse_package.Add("call_type", "Mobile_to_Land");
                        break;
                    case '1':
                        parse_package.Add("call_type", "Individual_Call");
                        break;
                    case '2':
                        parse_package.Add("call_type", "Group_Call");
                        break;
                }
                 * */
                string result = Call_Type.ToString();//L , M or T
                switch (result)
                {
                    case "76"://L
                        //break;
                        parse_package.Add("call_type", "Land_to_Mobile");
                        parse_package.Add("result", "end_call");
                        break;
                    case "77"://M
                        //break;
                        parse_package.Add("call_type", "Mobile_to_Land");
                        parse_package.Add("result", "end_call");
                        break;
                    case "1":
                        parse_package.Add("call_type", "Individual_Call");
                        break;
                    case "2":
                        parse_package.Add("call_type", "Group_Call");
                        break;
                    case "3":
                        parse_package.Add("call_type", "DataCall");
                        break;
                    case "4":
                        parse_package.Add("call_type", "Multislot Data Call");
                        break;
                    case "5":
                        parse_package.Add("call_type", "Analog Conventional Call");
                        break;
                    case "6":
                        parse_package.Add("call_type", "Controlled Channel Access Data Call");
                        break;
                    case "7":
                        parse_package.Add("call_type", "Digital Conventional Call");
                        break;
                }
              Console.WriteLine("+parse_call_type");
                if(parse_package.ContainsKey("call_type"))
                  Console.WriteLine("call type ="+parse_package["call_type"]);
                else
                {
                  Console.WriteLine("call type = unknown");
                }
              Console.WriteLine("-parse_call_type");

            }

            private static void parse_duration_in_sec(byte[] duration_in_sec, ref SortedDictionary<string, string> parse_package)
            {
                uint sec = BitConverter.ToUInt32(duration_in_sec.Take(duration_in_sec.Length).ToArray(), 0);
                parse_package.Add("sec", sec.ToString());
            }

            private static void parse_gid(byte[] gid)
            {
                uint id = BitConverter.ToUInt32(gid.Take(gid.Length).ToArray(), 0);
                //parse_package.Add("gid", id.ToString());
            }

            private static void parse_snd_id(byte[] snd_id, ref SortedDictionary<string, string> parse_package)
            {
                uint id = BitConverter.ToUInt32(snd_id.Take(snd_id.Length).ToArray(), 0);
                parse_package.Add("target_id", id.ToString());
            }

            private static void parse_call_status(byte call_status,ref  SortedDictionary<string, string> parse_package)
            {
                switch ((int)call_status)
                {
                    case (int)Call_Status.Global_Active:
                        parse_package.Add("call_status", "Global_Active");
                        break;
                    case (int)Call_Status.Global_Busy:
                        parse_package.Add("call_status", "Global_Busy");
                        break;
                    case (int)Call_Status.Active_and_Busy:
                        parse_package.Add("call_status", "Active_and_Busy");
                        break;
                    case (int)Call_Status.Not_Active_or_Busy:
                        parse_package.Add("call_status", "Not_Active_or_Busy");
                        break;
                    case (int)Call_Status.Active_and_Busy_from_FastStart:
                        parse_package.Add("call_status", "Active_and_Busy_from_FastStart");
                        break;
                    
                }
            }

            private static void parse_reason_for_busy(byte reason_for_busy, ref SortedDictionary<string, string> parse_package)
            {
                switch ((int)reason_for_busy)
                {
                    case (int)Reason_for_Busy.No_Busy:
                        parse_package.Add("reason_for_busy", "No_Busy");
                        break;
                    case (int)Reason_for_Busy.No_resources:
                        parse_package.Add("reason_for_busy", "No_resources");
                        break;
                    case (int)Reason_for_Busy.Talkgroup_and_Multigroup_Contention:
                        parse_package.Add("reason_for_busy", "Talkgroup_and_Multigroup_Contention");
                        break;
                }
            }

            private static void parse_ucn(byte[] ucn, ref SortedDictionary<string, string> parse_package)
            {
                uint call_number = BitConverter.ToUInt32(ucn.Take(ucn.Length).ToArray(), 0);
                parse_package.Add("universal_call_number", call_number.ToString());
            }

            private static void parse_uid(byte[] uid, ref SortedDictionary<string, string> parse_package)
            {
                //This the individual ID received from the radio unit. It is right justified and padded with leading zeros.
                //For the Type II case, thefield would have the format 0x0000nnnn where the n’s represent the 16-bit
                //individual ID. If this field is unused, it will be set to the hexadecimal value 0.
                uint id= BitConverter.ToUInt32(uid.Take(uid.Length).ToArray(), 0);
                parse_package.Add("source_id", id.ToString());
                
            }

            private static void parse_timestamp(byte[] timestamp, ref SortedDictionary<string, string> parse_package)
            {
                Array.Reverse(timestamp);
                int year = BitConverter.ToUInt16(timestamp.Take(2).Reverse().ToArray(), 0);
                int month = (int)timestamp[2];
                int day = (int)timestamp[3];
                int hour = (int)timestamp[4];
                int minute = (int)timestamp[5];
                int second = (int)timestamp[6];
                int deciSecond = (int)timestamp[7];
                DateTime date_time = new DateTime(year, month, day, hour, minute, second, deciSecond / 100);
                parse_package.Add("timestamp", date_time.ToString("yyyyMMddHHmmssffff"));
            }

            private static void parse_header_and_numoffset_package(ATIA_PACKAGE_Header_and_NumOffset struct_header, ref SortedDictionary<string, string> parse_package)
            {
                string command,opcode;
                switch (struct_header.BlockCommandType)
                {
                    case (ushort)Block_Command_Type_Values.Flexible_Controlling_Zone_Update:
                        command = Block_Command_Type_Values.Flexible_Controlling_Zone_Update.ToString("G");
                        parse_package.Add("cmd", "Flexible_Controlling_Zone_Update");
                        switch (struct_header.BlockOpcode)
                        {
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.Start_of_Call:
                                opcode = Flexible_Controlling_Zone_Update_opcode.Start_of_Call.ToString("G");
//                                parse_package.Add("opcode", "Start_of_Call");
                                //parse_package.Add("result", "start_call");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.End_of_Call:
                                opcode = Flexible_Controlling_Zone_Update_opcode.End_of_Call.ToString("G");
//                                parse_package.Add("opcode", "End_of_Call");
                                parse_package.Add("result", "end_call");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_Control.ToString("G");
//                                parse_package.Add("opcode", "PTT_ID_Active_Control");
                                parse_package.Add("result", "PTT_ID_Active_Control");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_No_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_No_Control.ToString("G");
//                                parse_package.Add("opcode", "PTT_ID_Active_No_Control");
                                parse_package.Add("result", "PTT_ID_Active_No_Control");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Busy_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Busy_Control.ToString("G");
//                                parse_package.Add("opcode", "PTT_ID_Busy_Control");
                                parse_package.Add("result", "PTT_ID_Busy_Control");
                                break;
                        }
                        break;
                    case (ushort)Block_Command_Type_Values.Flexible_Mobility_Update:
                        command = Block_Command_Type_Values.Flexible_Mobility_Update.ToString("G");
                        parse_package.Add("cmd", "Flexible_Mobility_Update");
                        switch (struct_header.BlockOpcode)
                        {
                            case (ushort)Flexible_Mobility_Update_opcode.Unit_Registration:
                                parse_package.Add("comment", "Unit_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Request_for_Registration:
                                parse_package.Add("comment", "Request_for_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Location_Registration:
                                parse_package.Add("comment", "Location_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Console_Registration:
                                parse_package.Add("comment", "Console_Registration");
                                parse_package.Add("result", "power_on");
                                break;

                            case (ushort)Flexible_Mobility_Update_opcode.Deregistration:
                                parse_package.Add("comment", "Deregistration");
                                parse_package.Add("result", "power_off");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.IncorrectSystemDeregistration:
                                parse_package.Add("comment", "IncorrectSystemDeregistration");
                                parse_package.Add("result", "power_off");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.RemoteZoneDeregistration:
                                parse_package.Add("comment", "RemoteZoneDeregistration");
                                parse_package.Add("result", "power_off");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Group_Affiliation:
                                parse_package.Add("comment", "Group_Affiliation");// to get gid/uid
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.RemoteZoneRegistration:
                                parse_package.Add("comment", "RemoteZoneRegistration");// to get gid/uid
                                parse_package.Add("result", "power_on");
                                break;
                        }
                        break;

                    case (ushort)Block_Command_Type_Values.Flexible_Call_Activity_Update:
                        {
                            command = Block_Command_Type_Values.Flexible_Call_Activity_Update.ToString("G");
                            parse_package.Add("cmd", "Flexible_Call_Activity_Update");
                            switch (struct_header.BlockOpcode)
                            {
                                case (ushort)Flexible_Call_Activity_Update_opcode.PTT_ID_Update_Active:
//                                    parse_package.Add("opcode", "PTT_ID_Update_Active");
                                    parse_package.Add("result", "PTT_ID_Update_Active");
                                    break;
                                case (ushort)Flexible_Call_Activity_Update_opcode.PTT_ID_Update_Active_No_Control:
//                                    parse_package.Add("opcode", "PTT_ID_Update_Active_No_Control");
                                    parse_package.Add("result", "PTT_ID_Update_Active_No_Control");
                                    break;
                                case (ushort)Flexible_Call_Activity_Update_opcode.PTT_ID_Update_Busy:
//                                    parse_package.Add("opcode", "PTT_ID_Update_Busy");
                                    parse_package.Add("result", "PTT_ID_Update_Active");
                                    break;
                                case (ushort)Flexible_Call_Activity_Update_opcode.PTT_ID_Update_Busy_No_Control:
//                                    parse_package.Add("opcode", "PTT_ID_Update_Busy_No_Control");
                                    parse_package.Add("result", "PTT_ID_Update_Busy_No_Control");
                                    break;
                                case (ushort)Flexible_Call_Activity_Update_opcode.Start_of_Call:
//                                    parse_package.Add("opcode", "Start_of_Call");
                                    parse_package.Add("result", "Start_of_Call");
                                    break;
                                case (ushort)Flexible_Call_Activity_Update_opcode.Call_State_Change:
                                    parse_package.Add("result", "Call_State_Change");
                                    break;
                            }
                        }
                        break;
                    case (ushort)Block_Command_Type_Values.Flexible_End_of_Call:
                        {
                            command = Block_Command_Type_Values.Flexible_End_of_Call.ToString("G");
                            parse_package.Add("cmd", "Flexible_End_of_Call");
                            switch (struct_header.BlockOpcode)
                            {
                                case (ushort)Flexible_End_of_Call_opcode.End_of_Call:
//                                    parse_package.Add("opcode", "End_of_Call");
                                    //parse_package.Add("result", "end_call");
                                    break;
                                
                            }
                        }
                        break;
                    case (ushort)Block_Command_Type_Values.Flexible_Interconnect_Call_Billing_Info_Packet:
                        {
                            command = Block_Command_Type_Values.Flexible_Interconnect_Call_Billing_Info_Packet.ToString("G");
                            parse_package.Add("cmd", "Flexible_Interconnect_Call_Billing");
                        }
                        break;
                }
            }
            //3. byte 轉成 struct
            static object BytesToStruct(byte[] bytes, Type strcutType)
            {

                int size = Marshal.SizeOf(strcutType);
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.Copy(bytes, 0, buffer, size);
                    return Marshal.PtrToStructure(buffer, strcutType);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            static void print_struct(object obj)
            {
                foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
                {
                    string name = descriptor.Name;
                    object value = descriptor.GetValue(obj);
                  Console.WriteLine("{0}={1}", name, value);
                }
            }
            static void array_reverse_ATIA_PACKAGE_Header_and_NumOffset(ref byte[] receive)
            {
                Array.Reverse(receive, 0, 4);
                Array.Reverse(receive, 6, 2);
                Array.Reverse(receive, 8, 2);
                Array.Reverse(receive, 10, 2);
                Array.Reverse(receive, 12, 2);
                Array.Reverse(receive, 14, 2);
                Array.Reverse(receive, 16, 4);
                Array.Reverse(receive, 20, 2);

            }
            public static void Log(String logMessage, TextWriter w)
            {
                w.Write("\r\nLog Entry : ");
                w.WriteLine("{0} {1}", DateTime.Now.ToString("H:mm:ss.fffffff"),
                    DateTime.Now.ToLongDateString());
                w.WriteLine("  :");
                w.WriteLine("{0}", logMessage);
                w.WriteLine("-------------------------------");
                // Update the underlying file.
                w.Flush();
            }
            static string ByteToHexBitFiddle(byte[] bytes)
            {
                char[] c = new char[bytes.Length * 2];
                int b;
                for (int i = 0; i < bytes.Length; i++)
                {
                    b = bytes[i] >> 4;
                    c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                    b = bytes[i] & 0xF;
                    c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
                }
                string str1 = new string(c);
                string str2 = String.Join(" ",
                    str1.ToCharArray().Aggregate("",
                    (result, x) => result += ((!string.IsNullOrEmpty(result) &&
                        (result.Length + 1) % 3 == 0) ? " " : "") + x.ToString())
                        .Split(' ').ToList().Select(
                    x => x.Length == 1
                        ? String.Format("{0}{1}", Int32.Parse(x) - 1, x)
                        : x).ToArray());
                return str2;
            }
            static bool AddValue(string key, string value)
            {
                ConfigurationManager.RefreshSection("appSettings");

                if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings.Get(key)))
                {
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"not null");
                    return false;
                }
                else
                {
                    //Console.WriteLine(Thread.CurrentThread.Name+"@"+DateTime.Now.ToString("O")+Environment.NewLine+"null");
                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    AppSettingsSection app = config.AppSettings;
                    app.Settings.Add(key, value);
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    return true;
                }
                
            }
            static void ModifyValue(string key, string value)
            {
                ConfigurationManager.RefreshSection("appSettings");
                //Configuration與AppSettingsSection必須引用System.Configuration才可使用！
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                AppSettingsSection app = config.AppSettings;
                //app.Settings.Add("B", "This is B value");
                app.Settings[key].Value = value;
                config.Save(ConfigurationSaveMode.Modified);
            }
            static void DeleteValue(string key)
            {
                ConfigurationManager.RefreshSection("appSettings");
                //Configuration與AppSettingsSection必須引用System.Configuration才可使用！
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                AppSettingsSection app = config.AppSettings;
                //app.Settings.Add("B", "This is B value");
                //app.Settings["A"].Value = "This is not B";
                app.Settings.Remove(key);
                config.Save(ConfigurationSaveMode.Modified);
            }
        
    }
    public class Device_power_status
    {
        public string ID { get; set; }
        public string SN { get; set; }
        public string power_on_time { get; set; }
        public string power_off_time { get; set; }
        
    }
    public class Device_call_status
    {
        public string ID { get; set; }
        public string SN { get; set; }
        public string call_type { get; set; }
        public string start_call_time { get; set; }
        public string end_call_time { get; set; }
        public string targetID { get; set; }
        public string channel { get; set; }
        public string site { get; set; }
        public string gTarget { get; set; }

    }
    public class GeoAngle
    {
        public bool IsNegative { get; set; }
        public int Degrees { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public int Milliseconds { get; set; }



        public static GeoAngle FromDouble(decimal angleInDegrees)
        {
            //ensure the value will fall within the primary range [-180.0..+180.0]
            while (angleInDegrees < -180.0m)
                angleInDegrees += 360.0m;

            while (angleInDegrees > 180.0m)
                angleInDegrees -= 360.0m;

            var result = new GeoAngle();

            //switch the value to positive
            result.IsNegative = angleInDegrees < 0;
            angleInDegrees = Math.Abs(angleInDegrees);

            //gets the degree
            result.Degrees = (int)Math.Floor(angleInDegrees);
            var delta = angleInDegrees - result.Degrees;

            //gets minutes and seconds
            var seconds = (int)Math.Floor(3600.0m * delta);
            result.Seconds = seconds % 60;
            result.Minutes = (int)Math.Floor(seconds / 60.0);
            delta = delta * 3600.0m - seconds;

            //gets fractions
            result.Milliseconds = (int)(1000.0m * delta);

            return result;
        }



        public override string ToString()
        {
            var degrees = this.IsNegative
                ? -this.Degrees
                : this.Degrees;

            return string.Format(
                "{0}° {1:00}' {2:00}\"",
                degrees,
                this.Minutes,
                this.Seconds);
        }



        public string ToString(string format)
        {
            switch (format)
            {
                case "NS":
                    return string.Format(
                        "{0}° {1:00}' {2:00}\".{3:000} {4}",
                        this.Degrees,
                        this.Minutes,
                        this.Seconds,
                        this.Milliseconds,
                        this.IsNegative ? 'S' : 'N');

                case "WE":
                    return string.Format(
                        "{0}° {1:00}' {2:00}\".{3:000} {4}",
                        this.Degrees,
                        this.Minutes,
                        this.Seconds,
                        this.Milliseconds,
                        this.IsNegative ? 'W' : 'E');

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
