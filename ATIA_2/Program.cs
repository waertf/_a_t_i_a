using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Net.Sockets;
using System.Configuration;
using System.Net;
using System.ComponentModel;
using System.IO;
using System.Collections;
using log4net;
using log4net.Config;
using System.Globalization;


namespace ATIA_2
{
    class Program
    {
        //every device has only one uid , so to use uid just fine without gid or snd_id

        
            //static Byte[] receiveBytes;
            //static ATIA_PACKAGE_Header_and_NumOffset struct_header = new ATIA_PACKAGE_Header_and_NumOffset();
            //static string returnData;
            //static Hashtable parse_package = new Hashtable();//cmd,opcode,result,uid,timestamp
            //static SortedDictionary<string, string> parse_package = new SortedDictionary<string, string>();
            const int OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS = 18;
            const int DEVIATION_OF_OFFSET_FIELDS_OF_VALUES = 0;
            private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
            private static List<Device_power_status> Power_status = new List<Device_power_status>();
            private static List<Device_call_status> Call_status = new List<Device_call_status>();
            private static string sql_table_columns = string.Empty, sql_table_column_value = string.Empty, sql_cmd = string.Empty, sql_condition = string.Empty;
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
                Deregistration=7
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
                //End_of_Call = 7
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
            static void Main(string[] args)
            {
                //Thread read_thread = new Thread(() => read_thread_method(tcpClient, netStream, sql_client));
                Thread udp_server_8671 = new Thread(() => udp_server_t(int.Parse(ConfigurationManager.AppSettings["ATIA_SERVER_PORT_8671"]))); //(new ThreadStart(udp_server_t));
                Thread udp_server_8601 = new Thread(() => udp_server_t(int.Parse(ConfigurationManager.AppSettings["ATIA_SERVER_PORT_8601"])));
                udp_server_8671.Start();
                udp_server_8601.Start();

            }
            static void udp_server_t(int port)
            {
                UdpClient udpClient = new UdpClient(port);
                //IPEndPoint object will allow us to read datagrams sent from any source.
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                //int c = int.Parse(ConfigurationManager.AppSettings["raw_log_counter"].ToString());

                while (true)
                {
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    byte[] receiveBytes_original = new byte[receiveBytes.Length];
                    SortedDictionary<string, string> parse_package = new SortedDictionary<string, string>();
                    Array.Copy(receiveBytes, receiveBytes_original, receiveBytes_original.Length);
                    if (bool.Parse(ConfigurationManager.AppSettings["log_raw_data"]))
                    {
                        //using (var stream = new FileStream("raw" + c + ".txt", FileMode.Append))
                        using (var stream = new FileStream("raw" + DateTime.Now.ToString("yyyy-MM-dd_H.mm.ss.fffffff") + ".atia", FileMode.Append))
                        {
                            stream.Write(receiveBytes, 0, receiveBytes.Length);
                            stream.Close();
                        }
                    }

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
                    Console.WriteLine("receive_length=" + receiveBytes.Length);
                    array_reverse_ATIA_PACKAGE_Header_and_NumOffset(ref receiveBytes);
                    ATIA_PACKAGE_Header_and_NumOffset struct_header = new ATIA_PACKAGE_Header_and_NumOffset();
                    struct_header = (ATIA_PACKAGE_Header_and_NumOffset)BytesToStruct(receiveBytes, struct_header.GetType());
                    //Console.WriteLine("package lenght exclude first 4 byte :"+BitConverter.ToUInt32(receiveBytes.Skip(0).Take(4).Reverse().ToArray(), 0)); 

                    // Uses the IPEndPoint object to determine which of these two hosts responded.
                    Console.WriteLine("This is the message you received :" +
                                                 returnData.ToString());
                    parse_header_and_numoffset_package(struct_header, ref parse_package);

                    parse_data_section(receiveBytes.Skip(4).ToArray(), ref parse_package);//skip first 4 package_length byte

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
                    if (parse_package.ContainsKey("result") && (parse_package["result"].ToString().Equals("power_on") || parse_package["result"].ToString().Equals("power_off") || parse_package["result"].ToString().Equals("start_call") || parse_package["result"].ToString().Equals("end_call")))
                    {
                        SqlClient sql_client = new SqlClient(ConfigurationManager.AppSettings["SQL_SERVER_IP"], ConfigurationManager.AppSettings["SQL_SERVER_PORT"], ConfigurationManager.AppSettings["SQL_SERVER_USER_ID"], ConfigurationManager.AppSettings["SQL_SERVER_PASSWORD"], ConfigurationManager.AppSettings["SQL_SERVER_DATABASE"], ConfigurationManager.AppSettings["Pooling"], ConfigurationManager.AppSettings["MinPoolSize"], ConfigurationManager.AppSettings["MaxPoolSize"], ConfigurationManager.AppSettings["ConnectionLifetime"]);
                        sql_client.connect();
                        switch (parse_package["result"].ToString())
                        {
                            case "power_on":
                                Device_power_status dev_power_status = new Device_power_status();
                                dev_power_status.ID = parse_package["source_id"].ToString();
                                dev_power_status.power_on_time = parse_package["timestamp"].ToString();
                                string device_on_time = dev_power_status.power_on_time.Substring(0, 4) + "-" + dev_power_status.power_on_time.Substring(4, 2) + "-" +
                                    dev_power_status.power_on_time.Substring(6, 2) + " " + dev_power_status.power_on_time.Substring(8, 2) + ":" +
                                    dev_power_status.power_on_time.Substring(10, 2) + ":" + dev_power_status.power_on_time.Substring(12, 2);
                                string power_on_today = DateTime.Now.ToString("yyyyMMdd");
                                if (AddValue(dev_power_status.ID, power_on_today+","+"0"))
                                {
                                    int iVal = 0;

                                    dev_power_status.SN = dev_power_status.ID + power_on_today + iVal.ToString("D3");
                                }
                                else
                                {
                                    string sn = ConfigurationManager.AppSettings[dev_power_status.ID].ToString();
                                    string[] sn_sub = sn.Split(',');
                                    if (sn_sub[0] != power_on_today)
                                    {
                                        int iVal = 0;

                                        dev_power_status.SN = dev_power_status.ID + power_on_today + iVal.ToString("D3");
                                        ModifyValue(dev_power_status.ID, power_on_today + "," + "0");
                                    }
                                    else
                                    {
                                        uint count = uint.Parse(sn_sub[1])+1;
                                        dev_power_status.SN = dev_power_status.ID + power_on_today + count.ToString("D3");
                                        ModifyValue(dev_power_status.ID, power_on_today + "," + count.ToString());
                                    }
                                }
                                sql_table_columns = "serial_no,uid,on_time";
                                sql_table_column_value = "\'" + dev_power_status.SN + "\'" + "," + "\'" + dev_power_status.ID + "\'" + "," + "\'" + device_on_time + "\'";
                                sql_cmd = "INSERT INTO custom.turn_onoff_log (" + sql_table_columns + ") VALUES (" + sql_table_column_value + ")";
                                sql_client.modify(sql_cmd);
                                /*
                                int iVal = 1;

                                iVal.ToString("D3"); // = "001"
                                 * */
                                Power_status.Add(dev_power_status);
                                break;
                            case "power_off":
                                string power_off_sn = string.Empty;
                                Device_power_status dev_power_off_status = new Device_power_status();
                                dev_power_off_status.ID = parse_package["source_id"].ToString();
                                Device_power_status find_dev_sn = Power_status.Find(
                                     delegate(Device_power_status bk)
                                     {
                                         return bk.ID == dev_power_off_status.ID;
                                     }
                                    );
                                if (find_dev_sn != null)
                                {
                                     power_off_sn =dev_power_off_status.SN= find_dev_sn.SN;
                                }
                                else
                                {
                                    Console.WriteLine("Cannot fine device_id {0}", dev_power_off_status.ID);
                                    break;
                                }
                                //string power_off_sn = ConfigurationManager.AppSettings[dev_power_off_status.ID].ToString();
                               // string[] power_off_sn_sub = power_off_sn.Split(',');
                                //dev_power_off_status.SN = dev_power_off_status.ID + power_off_sn_sub[0] + uint.Parse(power_off_sn_sub[1]).ToString("D3");
                                dev_power_off_status.power_off_time = parse_package["timestamp"].ToString();
                                //parse_package.Add("timestamp", date_time.ToString("yyyyMMddHHmmssffff"));
                                string device_off_time = dev_power_off_status.power_off_time.Substring(0, 4) + "-" + dev_power_off_status.power_off_time.Substring(4, 2) + "-" +
                                    dev_power_off_status.power_off_time.Substring(6, 2) + " " + dev_power_off_status.power_off_time.Substring(8, 2) + ":" +
                                    dev_power_off_status.power_off_time.Substring(10, 2) + ":" + dev_power_off_status.power_off_time.Substring(12, 2);
                                    sql_table_columns = "custom.turn_onoff_log";
                                    sql_cmd = "UPDATE " + sql_table_columns + " SET off_time=\'" + device_off_time + "\' WHERE serial_no=\'" + dev_power_off_status.SN + "\'";
                                    sql_client.modify(sql_cmd);
                                    Power_status.Remove(find_dev_sn);
                                break;
                            case "start_call":
                                Device_call_status dev_call_status = new Device_call_status();
                                dev_call_status.ID = parse_package["source_id"].ToString();
                                string start_call_today = DateTime.Now.ToString("yyyyMMdd");
                                Call_status.Add(dev_call_status);
                                break;
                            case "end_call":
                                break;
                        }
                        sql_client.disconnect();
                    }
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
                        log.Info(s.ToString());
                        Console.WriteLine(s.ToString());
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                    Console.WriteLine("####################################################");

                    

                    parse_package.Clear();
                    
                    Thread.Sleep(300);
                }

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
                                call_status = p[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Overall_Call_Status-1];
                                reason_for_busy = p[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Reason_for_Busy-1];
                                snd_id = p.Skip((int)Offset_to_Target_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Target_Section_Secondary_ID).Take(snd_id.Length).Reverse().ToArray();
                                call_type = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_call_type).Take(call_type.Length).Reverse().ToArray();
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_ucn(ucn,ref  parse_package);
                                //parse_call_status(call_status);
                                //parse_reason_for_busy(reason_for_busy);
                                parse_snd_id(snd_id,ref  parse_package);
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
                                byte call_status, reason_for_busy;
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Busy_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Requester_section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 8).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Target_Section = BitConverter.ToUInt16(p.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 10).Take(2).Reverse().ToArray(), 0);
                                const int offset_to_call_section_Timestamp = 0;
                                const int offset_to_call_section_ucn = 8;
                                const int offset_to_call_section_call_status = 31;
                                const int offset_to_busy_section_reason_of_busy = 0;
                                const int offset_to_req_section_Primary_ID = 0;
                                const int Offset_to_Target_Section_Secondary_ID = 0;
                                timestamp = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = p.Skip((int)Offset_to_Requester_section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_req_section_Primary_ID).Take(uid.Length).Reverse().ToArray();
                                snd_id = p.Skip((int)Offset_to_Target_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Target_Section_Secondary_ID).Take(snd_id.Length).Reverse().ToArray();
                                ucn = p.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_ucn).Take(ucn.Length).Reverse().ToArray();
                                call_status = p[Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_call_status-1];
                                reason_for_busy = p[Offset_to_Busy_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_busy_section_reason_of_busy-1];
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_snd_id(snd_id,ref  parse_package);
                                parse_ucn(ucn,ref  parse_package);
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
                                parse_phone(phone, ref parse_package);
                                parse_timestamp(timestamp,ref  parse_package);
                                parse_uid(uid,ref  parse_package);
                                parse_duration_in_sec(duration_in_sec,ref  parse_package);
                                parse_call_type(Call_Type[0],ref  parse_package);
                            }
                            break;
                    }
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
                        parse_package.Add("call_type", "Land_to_Mobile");
                        break;
                    case "77"://M
                        parse_package.Add("call_type", "Mobile_to_Land");
                        break;
                    case "1":
                        parse_package.Add("call_type", "Individual_Call");
                        break;
                    case "2":
                        parse_package.Add("call_type", "Group_Call");
                        break;
                }
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
                                parse_package.Add("result", "start_call");
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
                            case (ushort)Flexible_Mobility_Update_opcode.Group_Affiliation:
                                parse_package.Add("comment", "Group_Affiliation");// to get gid/uid
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
                                    parse_package.Add("result", "end_call");
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
                    //Console.WriteLine("not null");
                    return false;
                }
                else
                {
                    //Console.WriteLine("null");
                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    AppSettingsSection app = config.AppSettings;
                    app.Settings.Add(key, value);
                    config.Save(ConfigurationSaveMode.Modified);
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

    }
}
