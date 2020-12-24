using MQTTnet;
using MQTTnet.Core.Adapter;
using MQTTnet.Core.Protocol;
using MQTTnet.Core.Server;
using System;
using System.Text;
using System.Threading;
using System.IO;
using System.Collections.Generic;

namespace MqttServerTest
{
    class Program
    {
        private static MqttServer mqttServer = null;
        private static float data_frame_time_long;
        private static string file_name="", can_next="", can_next_part="";
        private static Boolean pause = false,is_end=false;
        private static int sam,bit,data_frame_long,total_time,id3_l,now_play_s=1, total_data_package;//採樣率,比特率,幀長,總時常,id3標籤長度
        static  Boolean bplay = true;
        static void Main(string[] args)
        {
            
            new Thread(StartMqttServer).Start();
            while (true)
            {
                var inputString = Console.ReadLine().ToLower().Trim();
                switch (inputString)
                {
                    case "exit":
                        {
                            mqttServer?.StopAsync();
                            Console.WriteLine("MQTT服務已停止！");
                            Thread.Sleep(1000);
                            System.Environment.Exit(0);
                            break;
                        }
                    case "clients":
                        {
                            foreach (var item in mqttServer.GetConnectedClients())
                            {
                                Console.WriteLine($"客戶端標識：{item.ClientId}，協議版本：{item.ProtocolVersion}");
                            }
                            break;
                        }
                    case "publish":
                        {
                            string paylaod = Console.ReadLine().ToLower().Trim(); ;
                            var applicationMessage = 
                                new MQTTnet.Core.MqttApplicationMessage(
                                    "mynowplay", Encoding.UTF8.GetBytes(paylaod), 0, false);
                            applicationMessage = new MQTTnet.Core.MqttApplicationMessage("can_next_part", Encoding.UTF8.GetBytes(paylaod), 0, false);
                            mqttServer.Publish(applicationMessage);
                            Console.WriteLine("發布成功");
                            break;
                        }
                    case "play":
                        {
                            Thread play = null;
                            play = new Thread(playing);
                            what_file_load();
                            play.Start();
                            break;
                        }
                    case "pause":
                        {
                            pause = !pause;
                            break;
                        }
                    case "next":
                        {
                            var applicationMessage = new MQTTnet.Core.MqttApplicationMessage("can_next", Encoding.UTF8.GetBytes(""), 0, false);
                            mqttServer.Publish(applicationMessage);
                            break;
                        }
                    /*case "":
                        {
                            break;
                        }*/
                    default:
                        {
                            Console.WriteLine($"命令[{inputString}]無效！");
                            break;
                        }

                }
            }
        }
        private static void what_file_load()
        {
            file_name = Console.ReadLine().ToLower().Trim();
            file_name += ".mp3";
            if (file_name == ".mp3")
            {
                Console.WriteLine("檔案名不能為空");
                file_name = "";
                what_file_load();
            }
            else if (file_name == "exit.mp3")
            {
                Console.WriteLine("取消");
                file_name = "";
            }
        }
        private static void playing()
        {
            List<int> Frame_position = mp3fileread();
            string filepath = @"D:\hfs\" + file_name;
            FileStream fs = new FileStream(filepath, FileMode.Open);
            if (Frame_position.Count != 0)
            {
                if (bit > 192)
                {
                    int package = 1;//之後用來控制傳輸量的 每次傳輸幾秒
                    int one_second_need_frame = (int)((float)1 / data_frame_time_long);
                    for (; now_play_s <= total_time; now_play_s++)
                    {
                        while (pause)
                            Thread.Sleep(50);
                        var applicationMessage = new MQTTnet.Core.MqttApplicationMessage("mp3_frame_byte", transfer_data(Frame_position, fs, now_play_s, one_second_need_frame), 0, false);
                        mqttServer.Publish(applicationMessage);
                        while (now_play_s % package == 0)
                        {
                            if (can_next == "can_next")
                            {
                                can_next = "";
                                Console.WriteLine("下一個");
                                break;
                            }
                        }
                    }
                    var end_message = new MQTTnet.Core.MqttApplicationMessage("play", Encoding.UTF8.GetBytes("end_play"), 0, false);
                    mqttServer.Publish(end_message);
                }
                else
                {
                    int package = 1;//之後用來控制傳輸量的 每次傳輸幾秒
                    int one_second_need_frame = 20;
                    if (Frame_position.Count % one_second_need_frame != 0)
                    {
                        total_data_package = Frame_position.Count / one_second_need_frame + 1;
                    }
                    else
                        total_data_package = Frame_position.Count / one_second_need_frame;
                    for (; now_play_s <= total_data_package; now_play_s++)
                    {
                        while (pause)
                            Thread.Sleep(50);
                        var applicationMessage = new MQTTnet.Core.MqttApplicationMessage("mp3_frame_byte", transfer_data(Frame_position, fs, now_play_s, one_second_need_frame), 0, false);
                        mqttServer.Publish(applicationMessage);
                        while ((now_play_s % package) == 0)
                        {
                            if (can_next == "can_next")
                            {
                                can_next = "";
                                Console.WriteLine("下一個");
                                break;
                            }
                        }
                    }
                    /*int package = 1;//之後用來控制傳輸量的 每次傳輸幾秒
                    int one_second_need_frame = (int)((float)1 / data_frame_time_long);
                    for (; now_play_s <= total_time; now_play_s++)
                    {
                        while (pause)
                            Thread.Sleep(50);
                        var applicationMessage = new MQTTnet.Core.MqttApplicationMessage("mp3_frame_byte", transfer_data(Frame_position, fs, now_play_s, one_second_need_frame), 0, false);
                        mqttServer.Publish(applicationMessage);
                        while ((now_play_s % package) == 0)
                        {
                            if (can_next == "can_next")
                            {
                                can_next = "";
                                Console.WriteLine("下一個");
                                break;
                            }
                        }
                    }*/



                }
            }
            Console.WriteLine("end");
            fs.Dispose();
            Console.WriteLine("結束選擇");
        }
        private static List<int> mp3fileread()
        {
            List<int> Frame_position = new List<int>();
            //所有的幀判斷部分都在這
            string filepath = @"D:\hfs\" + file_name;
            FileStream fs = new FileStream(filepath, FileMode.Open);
            byte[] byDataValue = new byte[10];//id3標籤判斷用陣列
            byte[] h_byDataValue = new byte[4];//數據幀頭
            fs.Seek(0, SeekOrigin.Begin);
            fs.Read(byDataValue, 0, 10);
            fs.Seek(0, SeekOrigin.Begin);
            int dataframe_head_position = id3_is_alive(byDataValue);//找到幀頭
                                                                    //Console.WriteLine(ByteArrayToString(byDataValue));
            fs.Seek(dataframe_head_position, SeekOrigin.Begin);
            fs.Read(h_byDataValue, 0, 4);
            int[] dateframelong = dataframelong(h_byDataValue);
            //Console.WriteLine(ByteArrayToString(h_byDataValue));
            Console.WriteLine("數據幀大小:" + dateframelong[0]);
            data_frame_long = dateframelong[0];
            //Console.WriteLine(ByteArrayToString(dataframe(fs, dataframe_head_position, dateframelong)));
            //建立一個以時間為主的跳耀方法，及傳輸檔案為mp3給8266 每幀為26ms
            //以下為對檔案的帧進行定位存在一個
            byte[] all_file_load = new byte[fs.Length];//讀取整個檔案
            byte[] byte_check = new byte[4];//讀幀頭前4個byte
            string[] Frame_head_1 = new string[] {//-----------------第1 2byte
            "11111111111",//同步信息 0
            "11",//版本 1
            "01",//Layer 2
            "0","1",//crc 3~4 
            };
            string[] Frame_head_2 = new string[] {//-----------------第3byte
            //採樣與位率為恆定
            "0","1",//幀常調整
            "0",//保留字
            };
            string[] Frame_head_3 = new string[]
            {
            //-----------------第4byte
            "00","01","10","11",//聲道模式0~3
            "00","01","10","11",//擴充模式 當聲道模式為 01 時才使用。 所以如不是為00 4~7
            "0","1",//版權8 9
            "0","1",//原版10 11
            "00"//強調方式12
            };
            fs.Seek(0, SeekOrigin.Begin);//從頭開始/
            fs.Read(all_file_load, 0, (int)fs.Length);//讀取整個檔案
            //Console.WriteLine(ByteArrayToString(all_file_load));
            for (int a = dataframe_head_position; a < all_file_load.Length; a++)//從第一個開始，例讀1 2 3 4|2 3 4 5
            {
                for (int b = 0; b < 4; b++)

                {
                    if (a < all_file_load.Length - 4)
                    {
                        if (b == 0)
                            byte_check[b] = all_file_load[a];
                        else if (b == 1)
                            byte_check[b] = all_file_load[a + 1];
                        else if (b == 2)
                            byte_check[b] = all_file_load[a + 2];
                        else if (b == 3)
                            byte_check[b] = all_file_load[a + 3];
                    }//不要讓它超出範圍
                }//讀取頭
                //----------------------------------------------------
                string ByteString = "";
                for (int b = 0; b < 4; b++)
                {
                    char[] yourByteString = Convert.ToString(byte_check[b], 2).PadLeft(8, '0').ToCharArray();
                    foreach (char item in yourByteString)
                    {
                        ByteString += item;
                    }
                }//從byte轉 0 1 的string
                if (ByteString.Substring(0, 11) == Frame_head_1[0])//同步
                    if (ByteString.Substring(11, 2) == Frame_head_1[1])//版本
                        if (ByteString.Substring(13, 2) == Frame_head_1[2])//Layer  
                            if (ByteString.Substring(15, 1) == Frame_head_1[3]//crc
                                || ByteString.Substring(15, 1) == Frame_head_1[4])//crc
                                if (ByteString.Substring(16, 4) == dateframelong[1].ToString())//取樣率
                                    if (ByteString.Substring(20, 2) == dateframelong[2].ToString().PadLeft(2, '0'))//採樣率
                                        if (ByteString.Substring(22, 1) == Frame_head_2[0] ||//幀長調整
                                            ByteString.Substring(22, 1) == Frame_head_2[1])//幀長調整
                                            if (ByteString.Substring(23, 1) == Frame_head_2[2])//保留字
                                            {
                                                if (ByteString.Substring(24, 2) == Frame_head_3[1])//聲道模式是Joint Stereo
                                                {
                                                    if (!(ByteString.Substring(26, 2) == Frame_head_3[4]))//當聲道模式是Joint Stereo必不為 00
                                                        if (ByteString.Substring(28, 1) == Frame_head_3[8]//版權
                                                            || ByteString.Substring(28, 1) == Frame_head_3[9])//版權
                                                            if (ByteString.Substring(29, 1) == Frame_head_3[10]//是否原版
                                                                || ByteString.Substring(29, 1) == Frame_head_3[11])//是否原版
                                                                if (ByteString.Substring(30, 2) == Frame_head_3[12])//強調方式 沒再用 00
                                                                {
                                                                    Frame_position.Add(a);
                                                                    a += dateframelong[0] - 1;
                                                                }
                                                }
                                                else //聲道模式非Joint Stereo
                                                {
                                                    if (ByteString.Substring(26, 2) == Frame_head_3[4])//當聲道模式是Joint Stereo必為 00
                                                        if (ByteString.Substring(28, 1) == Frame_head_3[8]//版權
                                                            || ByteString.Substring(28, 1) == Frame_head_3[9])//版權
                                                            if (ByteString.Substring(29, 1) == Frame_head_3[10]//是否原版
                                                                || ByteString.Substring(29, 1) == Frame_head_3[11])//是否原版
                                                                if (ByteString.Substring(30, 2) == Frame_head_3[12])//強調方式 沒再用 00
                                                                {
                                                                    Frame_position.Add(a);
                                                                    a += dateframelong[0] - 1;
                                                                }
                                                }
                                            }
                }
                Console.WriteLine("全幀數" + Frame_position.Count);
                data_frame_time_long = (float)1152 / (float)sam;
                Console.WriteLine("幀長" + data_frame_time_long);
                /*
                    CBR播放時間
                     檔案大小（byte）× （ 4 + 幀長 × 125 ） ）×（1152 ÷ 每一幀的採樣頻率）
                     我用的mp3為MPEG-1，Layer III MPEG-1的採樣數為1152 頻率看表
                    */
                total_time = (int)((float)Frame_position.Count * ((float)1152 / (float)sam)) + 1;
                Console.WriteLine("總長度為:" + total_time);
                /*while (true)
                    {
                        Console.WriteLine("第幾幀");
                        string Frame_p = Console.ReadLine().ToLower().Trim();
                        fs.Seek(Frame_position[int.Parse(Frame_p)], SeekOrigin.Begin);//這裡還沒加入超出陣列處理的部分
                        byte[] t = new byte[Frame_position[int.Parse(Frame_p)+1] - Frame_position[int.Parse(Frame_p)]];                    
                        fs.Read(t, 0, t.GetUpperBound(0));
                        Console.WriteLine(ByteArrayToString(t));
                    }測試用看帧長*/
                //現在用的是把整個檔案都先讀取至一個陣列裡
                //但在遇到真的很大的音樂檔的時候會產生內存不足的問題
                //把存取方式通通改為固定值用seek方式取值
                fs.Dispose();
                return Frame_position;
        }
        public static byte[] transfer_data(List<int> Frame_position, FileStream fs, int now_play_s, int one_second_need_frame)
        {     
            fs.Seek(Frame_position[now_play_s * one_second_need_frame - one_second_need_frame], SeekOrigin.Begin);
            if (now_play_s * one_second_need_frame< Frame_position.Count)
            {
                byte[] tansfer_datapage=new byte[Frame_position[now_play_s * one_second_need_frame]-Frame_position[now_play_s * one_second_need_frame-one_second_need_frame]];//取1秒的長度 例如一秒為38幀就第38幀的頭減第0個幀
                fs.Read(tansfer_datapage,0, Frame_position[now_play_s * one_second_need_frame] - Frame_position[now_play_s * one_second_need_frame - one_second_need_frame]);//一秒的byte陣列
                return tansfer_datapage;
            }
            byte[] no_data = { 0 };
            return no_data;
        }
        public static byte[] dataframe(FileStream fs, int dataframebegin_position,int dateframelong)
        {
            byte[] dataframe=new byte[dateframelong];
            fs.Seek(dataframebegin_position, SeekOrigin.Begin);
            fs.Read(dataframe, 0, dateframelong);
            return dataframe;
        }//讀頭
        public static byte[] dataframe_head(FileStream fs, int dataframebegin_position, int dateframelong)
        {
            byte[] dataframe = new byte[2];
            fs.Seek(dataframebegin_position, SeekOrigin.Begin);
            fs.Read(dataframe, 0, 2);
            return dataframe;
        }//讀頭兩位
        public static int[] dataframelong(byte[] byDataValue)//數據幀大小
        {   
            string bit_rate="", sample_rate="";
            char[] yourByteString = Convert.ToString(byDataValue[2], 2).PadLeft(8, '0').ToCharArray();
            for (int i = 0; i < 4; i++)
                bit_rate +=yourByteString[i];
            for (int i = 4; i < 6; i++)
                sample_rate += yourByteString[i];
            //Console.WriteLine(yourByteString);
            int[] datalong = new int[3];
            datalong [0]= b_s_rate(bit_rate,sample_rate);
            datalong[1] = Int32.Parse(bit_rate);
            datalong[2]= Int32.Parse(sample_rate);
            return datalong;
        }
        public static int id3_is_alive(byte[] byDataValue)//標籤大小計算與判斷
        {
            string id3 = Encoding.Default.GetString(byDataValue, 0, 3);
            if (id3 == "ID3") {
                int total_size = (byDataValue[6] & 0x7F) * 0x200000 + (byDataValue[7] & 0x7F)
                * 0x400 + (byDataValue[8] & 0x7F) * 0x80 + (byDataValue[9] & 0x7F);
                //Console.WriteLine("id3存在");
                id3_l =total_size+10;
                Console.WriteLine("id3標籤大小:" + id3_l);

                return id3_l;
            }
            else {
                Console.WriteLine("id3不存在");
                return 0;
            }
        }
        public static int b_s_rate(string bit_rate,string sample_rate)//比特率與採樣率判斷
        {
            int datalong;
            if (sample_rate == "00")
                sam = 44100;
            else if (sample_rate == "01")
                sam = 48000;
            else if (sample_rate == "10")
                sam = 32000;
            switch (bit_rate)
            {
                case "0001":
                    bit = 32;
                    break;
                case "0010":
                    bit = 40;
                    break;
                case "0011":
                    bit = 48;
                    break;
                case "0100":
                    bit = 56;
                    break;
                case "0101":
                    bit = 64;
                    break;
                case "0110":
                    bit = 80;
                    break;
                case "0111":
                    bit = 96;
                    break;
                case "1000":
                    bit = 112;
                    break;
                case "1001":
                    bit = 128;
                    break;
                case "1010":
                    bit = 160;
                    break;
                case "1011":
                    bit = 192;
                    break;
                case "1100":
                    bit = 224;
                    break;
                case "1101":
                    bit = 256;
                    break;
                case "1110":
                    bit = 320;
                    break;
                default:
                    Console.WriteLine("比特率錯誤");
                    return 0;
            }
            switch (sample_rate)
            {
                case "00"://44.1kHz
                    switch (bit_rate)
                    {
                        case "0001":
                            datalong = 104;
                            return datalong;
                        case "0010":
                            datalong = 130;
                            return datalong;
                        case "0011":
                            datalong = 156;
                            return datalong;
                        case "0100":
                            datalong = 182;
                            return datalong;
                        case "0101":
                            datalong = 208;
                            return datalong;
                        case "0110":
                            datalong = 261;
                            return datalong;
                        case "0111":
                            datalong = 313;
                            return datalong;
                        case "1000":
                            datalong = 365;
                            return datalong;
                        case "1001":
                            datalong = 417;
                            return datalong;
                        case "1010":
                            datalong = 522;
                            return datalong;
                        case "1011":
                            datalong = 626;
                            return datalong;
                        case "1100":
                            datalong = 731;
                            return datalong;
                        case "1101":
                            datalong = 835;
                            return datalong;
                        case "1110":
                            datalong = 1044;
                            return datalong;
                        default:
                            Console.WriteLine("比特率錯誤");
                            return 0;
                    }
                case "01"://48kHz 
                    switch (bit_rate)
                    {
                        case "0001":
                            datalong = 96;
                            return datalong;
                        case "0010":
                            datalong = 120;
                            return datalong;
                        case "0011":
                            datalong = 144;
                            return datalong;
                        case "0100":
                            datalong = 168;
                            return datalong;
                        case "0101":
                            datalong = 192;
                            return datalong;
                        case "0110":
                            datalong = 240;
                            return datalong;
                        case "0111":
                            datalong = 288;
                            return datalong;
                        case "1000":
                            datalong = 336;
                            return datalong;
                        case "1001":
                            datalong = 384;
                            return datalong;
                        case "1010":
                            datalong = 480;
                            return datalong;
                        case "1011":
                            datalong = 576;
                            return datalong;
                        case "1100":
                            datalong = 672;
                            return datalong;
                        case "1101":
                            datalong = 768;
                            return datalong;
                        case "1110":
                            datalong = 960;
                            return datalong;
                        default:
                            Console.WriteLine("比特率錯誤");
                            return 0;
                    }
                case "10"://32kHz
                    switch (bit_rate)
                    {
                        case "0001":
                            datalong = 144;
                            return datalong;
                        case "0010":
                            datalong = 180;
                            return datalong;
                        case "0011":
                            datalong = 216;
                            return datalong;
                        case "0100":
                            datalong = 252;
                            return datalong;
                        case "0101":
                            datalong = 288;
                            return datalong;
                        case "0110":
                            datalong = 360;
                            return datalong;
                        case "0111":
                            datalong = 432;
                            return datalong;
                        case "1000":
                            datalong = 504;
                            return datalong;
                        case "1001":
                            datalong = 576;
                            return datalong;
                        case "1010":
                            datalong = 720;
                            return datalong;
                        case "1011":
                            datalong = 864;
                            return datalong;
                        case "1100":
                            datalong = 1008;
                            return datalong;
                        case "1101":
                            datalong = 1152;
                            return datalong;
                        case "1110":
                            datalong = 1440;
                            return datalong;
                        default:
                            Console.WriteLine("比特率錯誤");
                            return 0;
                    }
                default:
                    Console.WriteLine("採樣率錯誤");
                    return 0;
            }
        }
        public static string ByteArrayToString(byte[] ba)//bit轉string
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        public static byte[] StringToByteArray(string hex)//string轉bit
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }
        private static void StartMqttServer()
        {
            if (mqttServer == null)
            {
                try
                {
                    var options = new MqttServerOptions
                    {
                        ConnectionValidator = p =>
                        {
                            if (p.ClientId == "c001")
                            {
                                if (p.Username != "u001" || p.Password != "p001")
                                {
                                    return MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
                                }
                            }
                            return MqttConnectReturnCode.ConnectionAccepted;
                        }
                    };
                    mqttServer = new MqttServerFactory().CreateMqttServer(options) as MqttServer;
                    mqttServer.ApplicationMessageReceived += MqttServer_ApplicationMessageReceived;
                    mqttServer.ClientConnected += MqttServer_ClientConnected;
                    mqttServer.ClientDisconnected += MqttServer_ClientDisconnected;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return;
                }
            }
            mqttServer.StartAsync();
            Console.WriteLine("MQTT服務啟動成功！");
        }
        private static void MqttServer_ClientConnected(object sender, MqttClientConnectedEventArgs e)
        {
            Console.WriteLine($"客戶端[{e.Client.ClientId}]已連接，協議版本：{e.Client.ProtocolVersion}");
        }
        private static void MqttServer_ClientDisconnected(object sender, MqttClientDisconnectedEventArgs e)
        {
            Console.WriteLine($"客戶端[{e.Client.ClientId}]已斷開連接！");
        }
        private static void MqttServer_ApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            //if(e.ApplicationMessage.Topic != "mp3_frame_byte")
                //Console.WriteLine($"客戶端[{e.ClientId}]>> 主題：{e.ApplicationMessage.Topic} 負荷：{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)} Qos：{e.ApplicationMessage.QualityOfServiceLevel } 保留：{e.ApplicationMessage.Retain}");
            if (e.ApplicationMessage.Topic == "can_next")
                can_next = "can_next";
            if(e.ApplicationMessage.Topic != "mp3_frame_byte")
                Console.WriteLine($"客戶端[{e.ClientId}]>> 主題：{e.ApplicationMessage.Topic} 負荷：{Encoding.UTF8.GetString(e.ApplicationMessage.Payload)} Qos：{e.ApplicationMessage.QualityOfServiceLevel } 保留：{e.ApplicationMessage.Retain}");
        }
    }
}