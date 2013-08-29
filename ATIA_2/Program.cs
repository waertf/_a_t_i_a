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

namespace ATIA_2
{
    class Program
    {

            static Byte[] receiveBytes;
            static ATIA_PACKAGE_Header_and_NumOffset struct_header = new ATIA_PACKAGE_Header_and_NumOffset();
            static string returnData;
            static Hashtable parse_package = new Hashtable();//cmd,opcode,result,uid,timestamp
            const int OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS = 18;
            const int DEVIATION_OF_OFFSET_FIELDS_OF_VALUES = -1;

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
                Flexible_Site_Monitor_Update=142

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
                Thread udp_server = new Thread(new ThreadStart(udp_server_t));
                udp_server.Start();

            }
            static void udp_server_t()
            {
                UdpClient udpClient = new UdpClient(int.Parse(ConfigurationManager.AppSettings["ATIA_SERVER_PORT"]));
                //IPEndPoint object will allow us to read datagrams sent from any source.
                IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
                //int c = int.Parse(ConfigurationManager.AppSettings["raw_log_counter"].ToString());

                while (true)
                {
                    // Blocks until a message returns on this socket from a remote host.
                    receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    if (bool.Parse(ConfigurationManager.AppSettings["log_raw_data"]))
                    {
                        //using (var stream = new FileStream("raw" + c + ".txt", FileMode.Append))
                        using (var stream = new FileStream("raw" + DateTime.Now.ToString("yyyy-MM-dd_H.mm.ss.fffffff") + ".txt", FileMode.Append))
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

                    returnData = Encoding.ASCII.GetString(receiveBytes);
                    Console.WriteLine("receive_length=" + receiveBytes.Length);
                    array_reverse_ATIA_PACKAGE_Header_and_NumOffset(ref receiveBytes);
                    struct_header = (ATIA_PACKAGE_Header_and_NumOffset)BytesToStruct(receiveBytes, struct_header.GetType());
                    //Console.WriteLine("package lenght exclude first 4 byte :"+BitConverter.ToUInt32(receiveBytes.Skip(0).Take(4).Reverse().ToArray(), 0)); 

                    // Uses the IPEndPoint object to determine which of these two hosts responded.
                    Console.WriteLine("This is the message you received :" +
                                                 returnData.ToString());
                    parse_header_and_numoffset_package(struct_header);
                    parse_data_section(receiveBytes.Skip(4).ToArray());//skip first 4 package_length byte

                    StringBuilder s = new StringBuilder();
                    foreach (DictionaryEntry e in parse_package)
                        s.Append(e.Key + ":" + e.Value + "|");
                    s.Append(Environment.NewLine);
                    using (StreamWriter w = File.AppendText("log.txt"))
                    {
                        Log(s.ToString(), w);
                    }

                    parse_package.Clear();
                    Thread.Sleep(300);
                }

            }

            private static void parse_data_section(byte[] p)
            {
                //get unique id and timestamp(uid,timestamp)
                if(parse_package.ContainsKey("cmd"))
                {
                    switch (parse_package["cmd"].ToString())
                    {
                        case "Flexible_Controlling_Zone_Update":
                            {
                                uint Offset_to_Call_Section = BitConverter.ToUInt16(receiveBytes.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS+2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Requester_section = BitConverter.ToUInt16(receiveBytes.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS+6).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Status_Section = BitConverter.ToUInt16(receiveBytes.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                const int offset_to_call_section_Timestamp = 0;
                                const int offset_to_req_section_Primary_ID = 0;
                                const int offset_to_call_section_ucn = 8;
                                const int offset_to_status_section_Overall_Call_Status = 0;
                                const int offset_to_status_section_Reason_for_Busy = 1;
                                byte[] timestamp = new byte[8];
                                byte[] uid = new byte[4];
                                byte[] ucn = new byte[4];//Universal Call Number
                                byte call_status, reason_for_busy;
                                timestamp = receiveBytes.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = receiveBytes.Skip((int)Offset_to_Requester_section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_req_section_Primary_ID).Take(uid.Length).Reverse().ToArray();
                                ucn = receiveBytes.Skip((int)Offset_to_Call_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_call_section_ucn).Take(ucn.Length).Reverse().ToArray();
                                call_status = receiveBytes[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Overall_Call_Status];
                                reason_for_busy = receiveBytes[Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + offset_to_status_section_Reason_for_Busy];
                                parse_timestamp(timestamp);
                                parse_uid(uid);
                                parse_ucn(ucn);
                                parse_call_status(call_status);
                                parse_reason_for_busy(reason_for_busy);
                            }
                            break;
                        case "Flexible_Mobility_Update":
                            {
                                uint Offset_to_Status_Section = BitConverter.ToUInt16(receiveBytes.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 2).Take(2).Reverse().ToArray(), 0);
                                uint Offset_to_Unit_Section = BitConverter.ToUInt16(receiveBytes.Skip(OFFSET_TO_THE_FILE_NEXT_TO_NUM_OFFSETS + 4).Take(2).Reverse().ToArray(), 0);
                                const int Offset_to_Status_Section_Timestamp = 0;
                                const int Offset_to_Unit_Section_Operating_Unit_ID = 0;
                                byte[] timestamp = new byte[8];
                                byte[] uid = new byte[4];
                                timestamp = receiveBytes.Skip((int)Offset_to_Status_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Status_Section_Timestamp).Take(timestamp.Length).Reverse().ToArray();
                                uid = receiveBytes.Skip((int)Offset_to_Unit_Section + DEVIATION_OF_OFFSET_FIELDS_OF_VALUES + Offset_to_Unit_Section_Operating_Unit_ID).Take(uid.Length).Reverse().ToArray();
                                parse_timestamp(timestamp);
                                parse_uid(uid);
                            }
                            break;
     
                    }
                }
            }

            private static void parse_call_status(byte call_status)
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

            private static void parse_reason_for_busy(byte reason_for_busy)
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

            private static void parse_ucn(byte[] ucn)
            {
                uint call_number = BitConverter.ToUInt32(ucn.Take(ucn.Length).Reverse().ToArray(), 0);
                parse_package.Add("call_number", call_number.ToString());
            }

            private static void parse_uid(byte[] uid)
            {
                //This the individual ID received from the radio unit. It is right justified and padded with leading zeros.
                //For the Type II case, thefield would have the format 0x0000nnnn where the n’s represent the 16-bit
                //individual ID. If this field is unused, it will be set to the hexadecimal value 0.
                uint id= BitConverter.ToUInt32(uid.Take(uid.Length).Reverse().ToArray(), 0);
                parse_package.Add("uid", id.ToString());
                
            }

            private static void parse_timestamp(byte[] timestamp)
            {
                int year = BitConverter.ToInt32(timestamp.Take(2).Reverse().ToArray(), 0);
                int month = (int)timestamp[2];
                int day = (int)timestamp[3];
                int hour = (int)timestamp[4];
                int minute = (int)timestamp[5];
                int second = (int)timestamp[6];
                int deciSecond = (int)timestamp[7];
                DateTime date_time = new DateTime(year, month, day, hour, minute, second, deciSecond / 100);
                parse_package.Add("timestamp", date_time.ToString("yyyy/MM/dd H:mm:ss.ffff"));
            }

            private static void parse_header_and_numoffset_package(ATIA_PACKAGE_Header_and_NumOffset struct_header)
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
                                parse_package.Add("opcode", "Start_of_Call");
                                parse_package.Add("result", "start_call");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.End_of_Call:
                                opcode = Flexible_Controlling_Zone_Update_opcode.End_of_Call.ToString("G");
                                parse_package.Add("opcode", "End_of_Call");
                                parse_package.Add("result", "end_call");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_Control.ToString("G");
                                parse_package.Add("opcode", "PTT_ID_Active_Control");
                                parse_package.Add("result", "PTT_ID_Active_Control");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_No_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Active_No_Control.ToString("G");
                                parse_package.Add("opcode", "PTT_ID_Active_No_Control");
                                parse_package.Add("result", "PTT_ID_Active_No_Control");
                                break;
                            case (ushort)Flexible_Controlling_Zone_Update_opcode.PTT_ID_Busy_Control:
                                opcode = Flexible_Controlling_Zone_Update_opcode.PTT_ID_Busy_Control.ToString("G");
                                parse_package.Add("opcode", "PTT_ID_Busy_Control");
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
                                parse_package.Add("opcode", "Unit_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Request_for_Registration:
                                parse_package.Add("opcode", "Request_for_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Location_Registration:
                                parse_package.Add("opcode", "Location_Registration");
                                parse_package.Add("result", "power_on");
                                break;
                            case (ushort)Flexible_Mobility_Update_opcode.Console_Registration:
                                parse_package.Add("opcode", "Console_Registration");
                                parse_package.Add("result", "power_on");
                                break;

                            case (ushort)Flexible_Mobility_Update_opcode.Deregistration:
                                parse_package.Add("opcode", "Deregistration");
                                parse_package.Add("result", "power_off");
                                break;
                        }
                        break;

                    case (ushort)Block_Command_Type_Values.Flexible_Call_Activity_Update:
                        {
                            command = Block_Command_Type_Values.Flexible_Call_Activity_Update.ToString("G");
                            parse_package.Add("cmd", "Flexible_Call_Activity_Update");
                        }
                        break;
                    case (ushort)Block_Command_Type_Values.Flexible_End_of_Call:
                        {
                            command = Block_Command_Type_Values.Flexible_End_of_Call.ToString("G");
                            parse_package.Add("cmd", "Flexible_End_of_Call");
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
                w.WriteLine("  :{0}", logMessage);
                w.WriteLine("-------------------------------");
                // Update the underlying file.
                w.Flush();
            }
            
        
    }
}
