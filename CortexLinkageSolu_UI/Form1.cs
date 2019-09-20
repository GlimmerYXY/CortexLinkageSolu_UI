using System;
using System.IO;
using System.IO.Pipes;
using System.Xml;
using System.Net;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Globalization;
using System.Configuration;
using System.Threading.Tasks;
using System.Collections.Generic;
using CortexAPILib;
using CortexAPILib.CrtxApiTypes;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace CortexLinkageSolu_UI
{
    struct Operate
    {
        public int flag;       //操作类型：0-禁用规则；1-激活规则；2-调整预置位；3-延时
        public int camera;     //操作对象：（相机编号）1-相机1；2-相机2
        public string detail;  //操作参数：规则名称、预置位编号、延时时长(秒)
    }

    public partial class Form1 : Form
    {
        #region 全局变量

        /**********************************  CORTEX  **********************************/
        private CortexAPI cortexAPI;
        private string serverIP;     // cortex服务器IP
        private int scannerID;       // 分析引擎ID，也就是BehaviourWatch的ID

        /**********************************  HIKVISION（没用到）  **********************************/
        //private string DVRIPAddress;    //门禁管理机 IP地址或者域名
        //private Int16 DVRPortNumber;    //门禁管理机 服务端口号
        //private string DVRUserName;     //门禁管理机 登录用户名
        //private string DVRPassword;     //门禁管理机 登录密码
        //private string SDKPath;         //海康SDK 所在路径

        //private uint iLastErr = 0;
        //private Int32 m_lUserID = -1;
        //private Int32 m_lAlarmHandle = -1;
        //private byte m_byDoorStatus;
        //private CHCNetSDK.NET_DVR_DEVICEINFO_V30 DeviceInfo = new CHCNetSDK.NET_DVR_DEVICEINFO_V30();
        //private CHCNetSDK.MSGCallBack_V31 m_falarmData_V31 = null;

        private int preDoor = -2;
        private int curDoor = -1;

        /**********************************  声波盾  **********************************/
        private string soundWaveShieldIP;   //声波盾控制器IP
        private int soundWaveShieldPort;    //声波盾控制器端口号
        
        private int modeForRuqinJin;
        private int modeForRuqinYuan;

        private int frequencyForRuqinJin;      // 入侵检测近-声波盾频率
        private int frequencyForRuqinYuan;     // 入侵检测远-声波盾频率
        private int frequencyForXXX;    // 待定

        private int soundTime1;      //频率1下每次发声时长
        private int silenceTime1;    //频率1下每次静默时长
        private int soundTime2;      //频率2下每次发声时长
        private int silenceTime2;    //频率2下每次静默时长
        private int soundTime3;      //频率3下每次发声时长
        private int silenceTime3;    //频率3下每次静默时长

        private int intrudeDelay;    // 停止声波盾

        private string mp3path;      //mp3路径
        private int mp3delay;        //mp3播放完毕，延时启动声波盾

        /**********************************  联动  **********************************/
        private bool isRun = false;
        private bool isAuto = false;
        private bool isRuqin = false;
        private bool allowSound = false;    //允许声波盾工作
        private bool isIdentify = false;
        private bool aCircle = true;

        //private AlarmInfo alarmInfo = new AlarmInfo();
        private Dictionary<string, AlarmInfo> dicAlarmInfo = new Dictionary<string, AlarmInfo>();
        private int lastMode = -1;

        private int jinNum = 0;
        private int yuanNum = 0;
        private string latestAlarmId;       //最新报警的ID
        private DateTime alarmStopTime;     //报警字典清空的时间 赋什么初值？？

        private object runObj = new object();       //锁 isRun
        private object autoObj = new object();      //锁 isAuto
        private object latestAlarmObj = new object(); //锁 tempAlarm
        private object dicAlarmsObj = new object(); //锁 dicAlarmInfo
        private object ruqinObj = new object();     //锁 isRuqin
        private object fileObj = new object();      //锁 写日志
        private object lockObj = new object();      //锁 isIdentify、单个报警、报警集合
        private object alarmStopObj = new object(); //锁
        private object alarmNumObj = new object(); //锁

        private int closeOpeNumber; //关门流程下操作数量
        private Queue<Operate> closeOperate = new Queue<Operate>();
        private int openOpeNumber; //开门流程下操作数量
        private Queue<Operate> openOperate = new Queue<Operate>();

        private string genboxIP;
        private string connStr;
        private LinkageObj initializeObj;
        private LinkageObj openObj;
        private LinkageObj closeObj;
        private int opa4tNum = 0;
        private List<string> opa4tList = new List<string>();

        #endregion

        public Form1()
        {
            InitializeComponent();
            //AccessAppConfig();
            AccessLinkageJson();

            GetDbUuid();
        }

        //方案一：获取配置参数
        private void AccessAppConfig()
        {
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                serverIP = config.AppSettings.Settings["serverIP"].Value;
                scannerID = int.Parse(config.AppSettings.Settings["scannerID"].Value);

                openOpeNumber = int.Parse(config.AppSettings.Settings["openOpeNumber"].Value);
                for (int i = 1; i <= openOpeNumber; i++)
                {
                    Operate tmp = new Operate();
                    tmp.flag = int.Parse(config.AppSettings.Settings["open" + i.ToString() + "flag"].Value);
                    tmp.camera = int.Parse(config.AppSettings.Settings["open" + i.ToString() + "camera"].Value);
                    tmp.detail = config.AppSettings.Settings["open" + i.ToString() + "detail"].Value;
                    openOperate.Enqueue(tmp);
                }

                closeOpeNumber = int.Parse(config.AppSettings.Settings["closeOpeNumber"].Value);
                for (int i = 1; i <= closeOpeNumber; i++)
                {
                    Operate tmp = new Operate();
                    tmp.flag = int.Parse(config.AppSettings.Settings["close" + i.ToString() + "flag"].Value);
                    tmp.camera = int.Parse(config.AppSettings.Settings["close" + i.ToString() + "camera"].Value);
                    tmp.detail = config.AppSettings.Settings["close" + i.ToString() + "detail"].Value;
                    closeOperate.Enqueue(tmp);
                }

                //DVRIPAddress = config.AppSettings.Settings["DVRIPAddress"].Value;
                //DVRPortNumber = Int16.Parse(config.AppSettings.Settings["DVRPortNumber"].Value);
                //DVRUserName = config.AppSettings.Settings["DVRUserName"].Value;
                //DVRPassword = config.AppSettings.Settings["DVRPassword"].Value;
                //SDKPath = config.AppSettings.Settings["SDKPath"].Value;

                soundWaveShieldIP = config.AppSettings.Settings["soundWaveShieldIP"].Value;
                soundWaveShieldPort = int.Parse(config.AppSettings.Settings["soundWaveShieldPort"].Value);

                modeForRuqinJin = int.Parse(config.AppSettings.Settings["modeForRuqinJin"].Value);
                modeForRuqinYuan = int.Parse(config.AppSettings.Settings["modeForRuqinYuan"].Value);

                frequencyForRuqinJin = int.Parse(config.AppSettings.Settings["frequencyForRuqinJin"].Value);
                frequencyForRuqinYuan = int.Parse(config.AppSettings.Settings["frequencyForRuqinYuan"].Value);
                frequencyForXXX = int.Parse(config.AppSettings.Settings["frequencyForXXX"].Value);

                soundTime1 = int.Parse(config.AppSettings.Settings["soundTime1"].Value);
                silenceTime1 = int.Parse(config.AppSettings.Settings["silenceTime1"].Value);
                soundTime2 = int.Parse(config.AppSettings.Settings["soundTime2"].Value);
                silenceTime2 = int.Parse(config.AppSettings.Settings["silenceTime2"].Value);
                soundTime3 = int.Parse(config.AppSettings.Settings["soundTime3"].Value);
                silenceTime3 = int.Parse(config.AppSettings.Settings["silenceTime3"].Value);

                intrudeDelay = int.Parse(config.AppSettings.Settings["intrudeDelay"].Value);

                mp3path = config.AppSettings.Settings["mp3path"].Value;
                mp3delay = int.Parse(config.AppSettings.Settings["mp3delay"].Value);

                genboxIP = config.AppSettings.Settings["genboxIP"].Value;
                connStr = config.AppSettings.Settings["connStr"].Value;
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }
        
        //方案二：获取联动信息
        private void AccessLinkageJson()
        {
            try
            {
                string jsonFile = "D://initialize_linkage.json";
                using (StreamReader file = new StreamReader(jsonFile))
                {
                    string json = file.ReadToEnd();
                    LinkageObj initializeObj = JsonConvert.DeserializeObject<LinkageObj>(json);
                }
            }
            catch (Exception ex)
            {
                WriteLog("读取D://initialize_linkage.json失败！" + ex.Message + ex.StackTrace);
            }

            try
            {
                string jsonFile = "D://open_linkage.json";
                using (StreamReader file = new StreamReader(jsonFile))
                {
                    string json = file.ReadToEnd();
                    LinkageObj openObj = JsonConvert.DeserializeObject<LinkageObj>(json);
                }
            }
            catch (Exception ex)
            {
                WriteLog("读取D://open_linkage.json失败！" + ex.Message + ex.StackTrace);
            }

            try
            {
                string jsonFile = "D://close_linkage.json";
                using (StreamReader file = new StreamReader(jsonFile))
                {
                    string json = file.ReadToEnd();
                    LinkageObj closeObj = JsonConvert.DeserializeObject<LinkageObj>(json);
                }
            }
            catch (Exception ex)
            {
                WriteLog("读取D://close_linkage.json失败！" + ex.Message + ex.StackTrace);
            }

        }

        //管道通信（没用）
        private void ServerPipe()
        {
            try
            {
                NamedPipeServerStream pipeServer = new NamedPipeServerStream("Hik&Cortex", PipeDirection.InOut);

                pipeServer.WaitForConnection();
                Console.WriteLine("Hik&Cortex管道 已建立连接");

                // Read the request from the client. Once the client has written to the pipe its security token will be available.
                StreamString ss = new StreamString(pipeServer);

                //发送签名
                ss.WriteString("I am Hik!");

                ss.WriteString("");

                pipeServer.Close();
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //写txt日志
        private void WriteLog(string msg)
        {
            StreamWriter sw = null;
            try
            {
                string logPath = Path.GetDirectoryName(Application.ExecutablePath);
                lock (fileObj)
                {
                    sw = File.AppendText(logPath + "/LinkLog.txt");
                    sw.WriteLine(DateTime.Now.ToString("HH:mm:ss   ") + msg);
                    sw.Close();
                    sw.Dispose();
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //显示UI日志
        private void PrintLog(string msg)
        {
            while (!this.IsHandleCreated)
            {
            }
            this.BeginInvoke(new ThreadStart(delegate ()
            {
                printLog.AppendText(msg);
            }));
        }

        /**********************************  联动  **********************************/
        #region 联动

        //恢复全局变量
        private void ResetGlobalVar()
        {
            isAuto = false;
            aCircle = true;
            isRuqin = false;
            allowSound = false;
            isIdentify = false;
        }

        //进入初始状态
        private void Initialize()
        {
            try
            {
                //OnControlPTZ(scannerID, 1, 1);
                //OnActiveMSF(scannerID, 1, "ruqinyanhuozhiliu1.msf", "ruqinyanhuozhiliu1.ims");
                //OnControlPTZ(scannerID, 2, 1);
                //OnActiveMSF(scannerID, 2, "ruqinyanhuozhiliu2.msf", "ruqinyanhuozhiliu2.ims");
                //OnActiveMSF(scannerID, 2, "renlian2.msf", "renlian2.ims");
                ProcessLinkageObj(initializeObj);
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //获取门禁状态
        private void GetGateStatus()
        {
            try
            {
                FileStream stream = new FileStream("D:\\GateStatus.txt", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                StreamReader sr = new StreamReader(stream);

                while (isRun && isAuto)
                {
                    string stmp = sr.ReadLine();
                    sr.ReadToEnd();                            //在这里空读一下，这样就把剩余内容释放了。然后再重新读取。
                    sr.BaseStream.Seek(0, SeekOrigin.Begin);

                    if (stmp != null)
                    {
                        int tmp = int.Parse(stmp);

                        preDoor = curDoor;
                        curDoor = tmp;
                        if (curDoor != preDoor)
                        {
                            WriteLog("门状态：" + stmp);

                            if (preDoor == 0 && curDoor == 1 || preDoor == 1 && curDoor == 0)
                            {
                                while (!this.IsHandleCreated)
                                {
                                }
                                this.BeginInvoke(new ThreadStart(delegate ()
                                {
                                    this.button3.Enabled = false;
                                    if (curDoor == 1)
                                    {
                                        gateLabel.Text = "开";
                                        gateLabel.BackColor = System.Drawing.Color.Green;
                                    }
                                    else if (curDoor == 0)
                                    {
                                        gateLabel.Text = "关";
                                        gateLabel.BackColor = System.Drawing.Color.Red;
                                    }
                                }));

                                ProcessGate(curDoor);

                                while (!this.IsHandleCreated)
                                {
                                }
                                this.BeginInvoke(new ThreadStart(delegate ()
                                {
                                    this.button3.Enabled = true;
                                }));
                            }
                        }
                        else
                        {
                            //InformRemoteControl("0");
                            Thread.Sleep(4000); //待定
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //配置文件驱动流程，type：0-关门流程，1-开门流程
        private void ProcessGate(int type)
        {
            try
            {
                //Queue<Operate> oq;
                if (type == 0)
                {
                    WriteLog("/-------------------- 关门 --------------------/\n");
                    PrintLog("/-------------------- 关门 --------------------/\n");
                    //oq = new Queue<Operate>(closeOperate.ToArray());
                    ProcessLinkageObj(closeObj);
                    allowSound = true;
                }
                else
                {
                    WriteLog("/-------------------- 开门 --------------------/\n");
                    PrintLog("/-------------------- 开门 --------------------/\n");
                    //oq = new Queue<Operate>(openOperate.ToArray());
                    ProcessLinkageObj(openObj);
                    allowSound = false;
                }
                
                //while (oq.Count > 0)
                //{
                //    Operate cur = oq.Dequeue();
                //    switch (cur.flag)
                //    {
                //        case 0:
                //            OnDeactivateMSF(scannerID, cur.camera, cur.detail + ".msf");
                //            break;
                //        case 1:
                //            OnActiveMSF(scannerID, cur.camera, cur.detail + ".msf", cur.detail + ".ims");
                //            break;
                //        case 2:
                //            OnControlPTZ(scannerID, cur.camera, int.Parse(cur.detail));
                //            break;
                //        case 3:
                //            WriteLog("延时" + cur.detail + "s……\n");
                //            PrintLog("延时" + cur.detail + "s……\n");
                //            Thread.Sleep(int.Parse(cur.detail) * 1000);
                //            break;
                //    }
                //}
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //通知海康门口机开门
        private void InformRemoteControl(string msg)
        {
            try
            {
                //lock (fileObj)
                //{
                //    StreamWriter sw = new StreamWriter("D:\\ControlGate.txt");
                //    sw.Write(msg);
                //    sw.Close();
                //    sw.Dispose();
                //    WriteLog("远程开锁：" + msg);
                //}

                IPAddress ip = IPAddress.Parse("127.0.0.1");
                Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(new IPEndPoint(ip, 65432));
                WriteLog("连接门禁成功！\n");
                PrintLog("连接门禁成功！\n");

                byte[] recByte = new byte[1024];
                int bytes;
                string recStr;

                clientSocket.Send(Encoding.ASCII.GetBytes(msg));
                bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                WriteLog("发送" + msg + "\t接收" + recStr);
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //控制声波盾
        private void ControlSoundWaveShield()
        {
            try
            {
                new MCIPlayer().Play(mp3path, 1);
            }
            catch (Exception e)
            {
                WriteLog("播放本地mp3失败！\t" + e.Message + e.StackTrace);
            }

            Thread.Sleep(mp3delay * 1000);  //延时启动声波盾

            try
            {
                IPAddress ip = IPAddress.Parse(soundWaveShieldIP);
                byte[] recByte = new byte[1024];
                int bytes;
                string recStr;
                int flag = 1;

                while (isRuqin)
                {
                    Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    clientSocket.Connect(new IPEndPoint(ip, soundWaveShieldPort));
                    WriteLog("连接声波盾成功！\n");
                    //PrintLog("连接声波盾成功！\n");

                    #region sound
                    if (allowSound)
                    {
                        if(jinNum > 0 && flag == 1)  //按近的模式-持续响：1、只有入侵检测近，2、远近都有
                        {
                            flag = 0;
                            //WriteLog("报警ID：" + alarmInfo.alarmID + "\t声波盾持续响……\n");
                            //PrintLog("报警ID：" + alarmInfo.alarmID + "\t声波盾持续响……\n");
                            WriteLog("声波盾持续响……报警ID：\n" + dicAlarmInfo.Keys);

                            clientSocket.Send(Encoding.ASCII.GetBytes("11"));
                            bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                            recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                            WriteLog("发送11\t接收" + recStr);
                            //Thread.Sleep(1000);
                        }
                        else if(yuanNum > 0)    //只有入侵检测远，按远的模式-断续响
                        {
                            switch (frequencyForRuqinYuan)
                            {
                                case 1:
                                    WriteLog("声波盾频率1……报警ID：" + dicAlarmInfo.Keys + "\n");
                                    //PrintLog("报警ID：" + alarmInfo.alarmID + "\t声波盾频率1……\n");

                                    clientSocket.Send(Encoding.ASCII.GetBytes("11"));   //响
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送11\t接收" + recStr);
                                    Thread.Sleep(soundTime1 * 1000);

                                    clientSocket.Send(Encoding.ASCII.GetBytes("21"));   //停
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送21\t接收" + recStr);
                                    Thread.Sleep(silenceTime1 * 1000);
                                    break;
                                case 2:
                                    WriteLog("声波盾频率2……报警ID：" + dicAlarmInfo.Keys + "\n");
                                    //PrintLog("报警ID：" + alarmInfo.alarmID + "\t声波盾频率2……\n");

                                    clientSocket.Send(Encoding.ASCII.GetBytes("11"));   //响
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送11\t接收" + recStr);
                                    Thread.Sleep(soundTime2 * 1000);

                                    clientSocket.Send(Encoding.ASCII.GetBytes("21"));   //停
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送21\t接收" + recStr);
                                    Thread.Sleep(silenceTime2 * 1000);
                                    break;
                                case 3:
                                    WriteLog("声波盾频率3……报警ID：" + dicAlarmInfo.Keys + "\n");
                                    //PrintLog("报警ID：" + alarmInfo.alarmID + "\t声波盾频率3……\n");

                                    clientSocket.Send(Encoding.ASCII.GetBytes("11"));   //响
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送11\t接收" + recStr);
                                    Thread.Sleep(soundTime3 * 1000);

                                    clientSocket.Send(Encoding.ASCII.GetBytes("21"));   //停
                                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                    WriteLog("发送21\t接收" + recStr);
                                    Thread.Sleep(silenceTime3 * 1000);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                    #endregion

                    //所有报警都已结束，则关闭声波盾
                    if (dicAlarmInfo.Count <= 0)
                    {
                        DateTime curTime = DateTime.Now;
                        TimeSpan ts = curTime.Subtract(alarmStopTime);
                        if (ts.TotalSeconds > intrudeDelay)
                        {
                            lock (ruqinObj)
                                isRuqin = false;

                            if (lastMode == 0)//持续响 停止
                            {
                                clientSocket.Send(Encoding.ASCII.GetBytes("21"));
                                bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                                recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                                WriteLog("停止持续响模式……发送21\t接收" + recStr);
                            }
                            
                            WriteLog("【声波盾停止】" + ts.TotalSeconds + "s内未收到报警\n");
                            PrintLog("【声波盾停止】" + ts.TotalSeconds + "s内未收到报警\n");
                        }
                    }
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    WriteLog("关闭声波盾！\n");
                    //PrintLog("关闭声波盾！\n");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
                WriteLog("连接声波盾失败！\n");
                PrintLog("连接声波盾失败！\n");
            }
        }
        //简易版
        private void SimpleSoundWaveShield()
        {
            try
            {
                new MCIPlayer().Play(mp3path, 1);
            }
            catch (Exception e)
            {
                WriteLog("播放本地mp3失败！\t" + e.Message + e.StackTrace);
            }

            Thread.Sleep(mp3delay * 1000);  //延时启动声波盾

            try
            {
                IPAddress ip = IPAddress.Parse(soundWaveShieldIP);
                byte[] recByte = new byte[1024];
                int bytes;
                string recStr;

                while (isRuqin)
                {
                    Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    clientSocket.Connect(new IPEndPoint(ip, soundWaveShieldPort));
                    WriteLog("连接声波盾成功！\n");
                    //PrintLog("连接声波盾成功！\n");

                    clientSocket.Send(Encoding.ASCII.GetBytes("11"));   //响
                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                    WriteLog("发送11\t接收" + recStr);
                    Thread.Sleep(soundTime1 * 1000);

                    clientSocket.Send(Encoding.ASCII.GetBytes("21"));   //停
                    bytes = clientSocket.Receive(recByte, recByte.Length, 0);
                    recStr = Encoding.ASCII.GetString(recByte, 0, bytes);
                    WriteLog("发送21\t接收" + recStr);
                    Thread.Sleep(silenceTime1 * 1000);

                    if (opa4tList.Count <= 0)
                    {
                        DateTime curTime = DateTime.Now;
                        TimeSpan ts = curTime.Subtract(alarmStopTime);
                        if (ts.TotalSeconds > intrudeDelay)
                        {
                            lock (ruqinObj)
                                isRuqin = false;

                            WriteLog("【声波盾停止】" + ts.TotalSeconds + "s内未收到报警\n");
                            PrintLog("【声波盾停止】" + ts.TotalSeconds + "s内未收到报警\n");
                        }
                    }
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                    WriteLog("关闭声波盾！\n");
                    //PrintLog("关闭声波盾！\n");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
                WriteLog("连接声波盾失败！\n");
                PrintLog("连接声波盾失败！\n");
            }
        }

        //处理联动对象
        private void ProcessLinkageObj(LinkageObj linkageObj)
        {
            try
            {
                foreach (var item in linkageObj.sequence.instruction)
                {
                    switch (item.operation)
                    {
                        case "active":
                            GenboxHelper.algo_enable_set(item.uuid, item.detail, "1");
                            break;
                        case "deactive":
                            GenboxHelper.algo_enable_set(item.uuid, item.detail, "0");
                            break;
                        case "position":
                            //等袁福星查相机SDK
                            break;
                        case "delay":
                            WriteLog("延时" + item.detail + "s……\n");
                            PrintLog("延时" + item.detail + "s……\n");
                            Thread.Sleep(int.Parse(item.detail) * 1000);
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                WriteLog(e.Message + e.StackTrace);
            }
        }

        //轮询告警状态
        private void PollAlarmStatus()
        {
            try
            {
                List<string> uuidList = GetDbUuid();

                foreach(string uuid in uuidList)
                {
                    ParameterizedThreadStart threadStart = new ParameterizedThreadStart(IpcamAlarmGet);
                    Thread thread = new Thread(threadStart);
                    thread.Start(uuid);
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //获取数据库uuid
        private List<string> GetDbUuid()
        {
            List<string> uuidList = new List<string>();
            string connStr = "server=localhost;user id=root;password=566711;database=gensys;Charset=utf8";
            MySqlConnection conn = new MySqlConnection(connStr);

            try
            {
                conn.Open();

                string sql = "select uuid from ipcam";
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                    uuidList.Add(reader.GetString("uuid"));
            }
            catch (MySqlException ex)
            {
                WriteLog(ex.Message + ex.Number);
            }
            finally
            {
                conn.Close();
            }

            return uuidList;
        }

        //线程处理各ipcam的告警状态
        private void IpcamAlarmGet(object obj)
        {
            string uuid = obj.ToString();
            while (true)
            {
                string result = GenboxHelper.ipcam_alarm_get(uuid);
                JObject jo = (JObject)JsonConvert.DeserializeObject(result);

                try
                {
                    //刷脸开门
                    int pfr = int.Parse(jo["opa4t"].ToString());
                    if (pfr == 1)
                    {
                        PrintLog("刷脸通过");
                        WriteLog("刷脸通过");
                        InformRemoteControl("1");   //去开门
                    }

                    //入侵报警
                    int opa4t = int.Parse(jo["opa4t"].ToString());
                    if (opa4t == 1)
                    {
                        //添加进报警字典
                        lock (dicAlarmsObj)
                            opa4tList.Add(uuid);

                        //UI更新
                        WriteLog("【入侵开始】报警相机：" + uuid + "\n");
                        while (!this.IsHandleCreated)
                        {
                        }
                        this.BeginInvoke(new ThreadStart(delegate ()
                        {
                            alarmLabel.Text = "是";
                            alarmLabel.BackColor = System.Drawing.Color.Red;
                            printLog.AppendText("【入侵开始】报警相机：" + uuid + "\n");
                        }));

                        //启动声波盾
                        if (!isRuqin)
                        {
                            lock (ruqinObj)
                                isRuqin = true;
                            ThreadStart threadStart = new ThreadStart(SimpleSoundWaveShield);
                            Thread thread = new Thread(threadStart);
                            thread.Start();
                        }
                    }
                    else
                    {
                        if (opa4tList.Contains(uuid))
                        {
                            //从字典中删除该结束的报警记录
                            lock (dicAlarmsObj)
                                opa4tList.Remove(uuid);

                            //所有报警都结束了
                            if (dicAlarmInfo.Count <= 0)
                                lock (alarmStopObj)
                                    alarmStopTime = DateTime.Now; 

                            //更新UI
                            WriteLog("【入侵结束】报警相机：" + uuid + "\t结束时间：" + alarmStopTime + "\n");
                            PrintLog("【入侵结束】报警相机：" + uuid + "\t结束时间：" + alarmStopTime + "\n");

                            while (!this.IsHandleCreated)
                            {
                            }
                            this.BeginInvoke(new ThreadStart(delegate ()
                            {
                                alarmLabel.Text = "否";
                                alarmLabel.BackColor = System.Drawing.Color.Green;
                            }));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

        }

        #endregion

        /**********************************  CORTEX  **********************************/
        #region 算法服务器

        //连接注册到cortex服务器
        private void RegisterToServer()
        {
            try
            {
                /* 如果不接受视频数据，仅接收报警元数据信息，则必须以metadata开头
                 * 否则一台主机的IP上，只能成功实例化第一个CortexAPI对象
                 * 
                 * MetadataCortexViewer 使用 
                 * cortexAPI = new CortexAPI("MetadataCortexViewer", MateUtil.MU.enDeviceType.Viewer);
                 * cortexAPI.CortexApiStart()
                 * 
                 * Main Cortex Viewer 使用 
                 * cortexAPI = new CortexAPI("Main Cortex Viewer", MateUtil.MU.enDeviceType.Viewer);
                 * cortexAPI.CortexApiStartMetaData()
                 */
                cortexAPI = new CortexAPI("Metadata Cortex Viewer", MateUtil.MU.enDeviceType.Viewer);
                string trueIP = cortexAPI.SetServerIP(serverIP);
                WriteLog("当前服务器IP为[" + trueIP + "]\n");
                PrintLog("当前服务器IP为[" + trueIP + "]\n");

                #region Register Event
                delegateOnConnect oc = new delegateOnConnect(OnConnected);
                cortexAPI.Events.RegisterOnConnect(oc, true);

                delegateOnDisconnect odc = new delegateOnDisconnect(OnDisconnected);
                cortexAPI.Events.RegisterOnDisconnect(odc, true);

                delegateOnAlarmEvent oae = new delegateOnAlarmEvent(OnAlarmStart);
                cortexAPI.Events.RegisterOnAlarmEvent(oae, true);

                delegateOnAlarmStopEvent event4 = new delegateOnAlarmStopEvent(OnALarmStop);
                cortexAPI.Events.RegisterOnAlarmStopEvent(event4, true);

                delegateOnFaceSpotEvent ofse = new delegateOnFaceSpotEvent(OnFaceSpotEvent);
                cortexAPI.Events.RegisterOnFaceSpotEvent(ofse, true);//设置人脸检测回调 
                #endregion

                int result = cortexAPI.CortexApiStart();  //启用单实例，接收视频
                //int result = cortexAPI.CortexApiStartMetaData(); //多实例，启用元数据接收
                if (result != 0)
                {
                    WriteLog("连接服务器[" + GetCortexServerIP() + "]出错：[" + result.ToString() + "]" + GetErrorName(result) + "\n");
                    PrintLog("连接服务器[" + GetCortexServerIP() + "]出错：[" + result.ToString() + "]" + GetErrorName(result) + "\n");
                }
                else
                {
                    WriteLog("正在连接服务器[" + GetCortexServerIP() + "]，请稍后......\n");
                    PrintLog("正在连接服务器[" + GetCortexServerIP() + "]，请稍后......\n");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //断开服务器
        private void LogOutServer()
        {
            delegateOnConnect oc = new delegateOnConnect(OnConnected);
            cortexAPI.Events.RegisterOnConnect(oc, false);

            delegateOnDisconnect odc = new delegateOnDisconnect(OnDisconnected);
            cortexAPI.Events.RegisterOnDisconnect(odc, false);

            delegateOnAlarmEvent oae = new delegateOnAlarmEvent(OnAlarmStart);
            cortexAPI.Events.RegisterOnAlarmEvent(oae, false);

            delegateOnAlarmStopEvent event4 = new delegateOnAlarmStopEvent(OnALarmStop);
            cortexAPI.Events.RegisterOnAlarmStopEvent(event4, false);

            int stopResult = cortexAPI.CrtxApiStop();
            WriteLog("停止cortexAPI：" + stopResult + "\n");
            PrintLog("停止cortexAPI：" + stopResult + "\n");
        }

        //连接回调
        private void OnConnected(String strDescription)
        {
            int loginResult;
            loginResult = cortexAPI.UserLogin("DefaultUser", "123");

            if (loginResult == 0)
            {
                WriteLog("与服务器[" + GetCortexServerIP() + "]连接成功\n");

                Initialize();   //初始状态

                while (!this.IsHandleCreated)
                {
                }
                this.BeginInvoke(new ThreadStart(delegate ()
                {
                    printLog.AppendText("与服务器[" + GetCortexServerIP() + "]连接成功\n");
                    button3.Enabled = true;
                    button4.Enabled = true;
                    button5.Enabled = false;
                    button6.Enabled = false;
                    button5.Visible = true;
                    button6.Visible = true;
                }));
            }
        }

        //断开回调
        private void OnDisconnected(String strDescription)
        {
            WriteLog("与服务器[" + GetCortexServerIP() + "]的连接断开\n");

            while (!this.IsHandleCreated)
            {
            }
            this.BeginInvoke(new ThreadStart(delegate ()
            {
                this.printLog.AppendText("与服务器[" + GetCortexServerIP() + "]的连接断开\n");
                button3.Enabled = false;
                button4.Enabled = false;
                button5.Enabled = false;
                button6.Enabled = false;
                button5.Visible = true;
                button6.Visible = true;
            }));
        }

        //报警开始回调
        private void OnAlarmStart(string eventXml, byte[] thumbnail)
        {
            if (allowSound == true)
            {
                AlarmInfo tempAlarm = new AlarmInfo();

                XmlDocument document = new XmlDocument();
                document.LoadXml(eventXml);
                XmlNodeList elementsByTagName;

                if (eventXml.Contains("AlarmEventStartMsg"))    //是报警开始报文
                {
                    elementsByTagName = document.GetElementsByTagName("AlarmName"); //获取报警名称
                    if (elementsByTagName.Count > 0)
                    {
                        string alarmName = string.Empty;
                        string str2 = elementsByTagName[0].InnerText.Trim();
                        try
                        {
                            byte[] bytes = new byte[str2.Length / 2];
                            for (int i = 0; i < str2.Length; i += 2)
                                bytes[i / 2] = byte.Parse(str2.Substring(i, 2), NumberStyles.HexNumber);
                            alarmName = Encoding.Unicode.GetString(bytes);
                        }
                        catch (Exception ex)
                        {
                            WriteLog(ex.Message + ex.StackTrace);
                        }

                        if (alarmName == "入侵检测近" || alarmName == "入侵检测远")   //只处理入侵报警
                        {
                            elementsByTagName = document.GetElementsByTagName("CustomerNumber");
                            if (elementsByTagName.Count > 0)
                            {
                                tempAlarm.customerNumber = elementsByTagName[0].InnerText.Trim();

                                elementsByTagName = document.GetElementsByTagName("FeedNumber");
                                if (elementsByTagName.Count > 0)
                                {
                                    tempAlarm.feedNumber = elementsByTagName[0].InnerText.Trim();

                                    elementsByTagName = document.GetElementsByTagName("DeviceAlarmID"); //获取报警ID
                                    if (elementsByTagName.Count > 0)
                                    {
                                        tempAlarm.alarmID = elementsByTagName[0].InnerText.Trim();

                                        tempAlarm.isAlarm = true;
                                        tempAlarm.alarmName = alarmName;
                                        tempAlarm.alarmStartTime = DateTime.Now;
                                        tempAlarm.alarmStopTime = DateTime.Now;

                                        if (tempAlarm.alarmName == "入侵检测近")
                                        {
                                            lastMode = 0;
                                            tempAlarm.mode = modeForRuqinJin;
                                            tempAlarm.frequency = frequencyForRuqinJin;
                                            lock (alarmNumObj)
                                                jinNum++;
                                        }
                                        else
                                        {
                                            lastMode = 1;
                                            tempAlarm.mode = modeForRuqinYuan;
                                            tempAlarm.frequency = frequencyForRuqinYuan;
                                            lock (alarmNumObj)
                                                yuanNum++;
                                        }

                                        //添加进报警字典
                                        lock (dicAlarmsObj)
                                            dicAlarmInfo.Add(tempAlarm.alarmID, tempAlarm);

                                        //UI更新
                                        WriteLog("【入侵开始】报警ID：" + tempAlarm.alarmID + "\t类型：" + tempAlarm.alarmName + "\t开始时间：" + tempAlarm.alarmStartTime + "\n");
                                        while (!this.IsHandleCreated)
                                        {
                                        }
                                        this.BeginInvoke(new ThreadStart(delegate ()
                                        {
                                            alarmLabel.Text = "是";
                                            alarmLabel.BackColor = System.Drawing.Color.Red;
                                            printLog.AppendText("【入侵开始】报警ID：" + tempAlarm.alarmID + "\t类型：" + tempAlarm.alarmName + "\t开始时间：" + tempAlarm.alarmStartTime + "\n");
                                        }));

                                        //启动声波盾
                                        if (!isRuqin)
                                        {
                                            lock (ruqinObj)
                                                isRuqin = true;
                                            ThreadStart threadStart = new ThreadStart(ControlSoundWaveShield);
                                            Thread thread = new Thread(threadStart);
                                            thread.Start();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        //报警停止回调
        private void OnALarmStop(string eventXml)
        {
            if (allowSound == true)
            {
                AlarmInfo tempAlarm = new AlarmInfo();

                XmlDocument document = new XmlDocument();
                document.LoadXml(eventXml);
                XmlNodeList elementsByTagName;

                if (eventXml.Contains("AlarmEventStopMsg"))
                {
                    elementsByTagName = document.GetElementsByTagName("DeviceAlarmID"); //获取报警ID
                    if (elementsByTagName.Count > 0)
                    {
                        string alarmID = elementsByTagName[0].InnerText.Trim();

                        if (dicAlarmInfo.ContainsKey(alarmID))
                        {
                            //从字典中删除该结束的报警记录
                            lock (alarmNumObj)
                                if (dicAlarmInfo[alarmID].alarmName == "入侵检测近")
                                    jinNum--;
                                else
                                    yuanNum--;
                            lock (dicAlarmsObj)
                                dicAlarmInfo.Remove(alarmID);

                            //所有报警都结束了
                            if (dicAlarmInfo.Count <= 0)
                                lock (alarmStopObj)
                                    alarmStopTime = DateTime.Now;

                            //更新UI
                            WriteLog("【入侵结束】报警ID：" + alarmID + "\t结束时间：" + DateTime.Now + "\n");
                            PrintLog("【入侵结束】报警ID：" + alarmID + "\t结束时间：" + DateTime.Now + "\n");

                            while (!this.IsHandleCreated)
                            {
                            }
                            this.BeginInvoke(new ThreadStart(delegate ()
                            {
                                alarmLabel.Text = "否";
                                alarmLabel.BackColor = System.Drawing.Color.Green;
                            }));
                        }
                    }
                }
            }
        }

        //人脸检测回调
        private void OnFaceSpotEvent(string eventXml, byte[] thumbnail)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(eventXml);
            XmlNodeList elementsByTagName;

            if (eventXml.Contains("FaceSpotMsg"))
            {
                //WriteLog(eventXml);

                elementsByTagName = document.GetElementsByTagName("IsIdentify");
                if (elementsByTagName.Count > 0)
                {
                    string tmp = elementsByTagName[0].InnerText.Trim();
                    WriteLog("刷脸结果：" + tmp);

                    if (tmp == "True")
                    {
                        //PrintLog("刷脸回调True");
                        //WriteLog("刷脸回调True");

                        //lock (lockObj)
                        //{
                        //    isIdentify = true;
                        //}

                        InformRemoteControl("1");   //去开门
                    }
                    //else
                    //{
                    //    PrintLog("刷脸回调false");
                    //    WriteLog("刷脸回调false");

                    //    lock (lockObj)
                    //    {
                    //        isIdentify = false;
                    //    }
                    //}
                }
            }
        }

        //获取百特服务器IP
        private string GetCortexServerIP()
        {
            string ip = string.Empty;
            ip = cortexAPI.GetServerIP();
            return ip;
        }

        //获取错误名称
        private string GetErrorName(int code)
        {
            string name = string.Empty;
            switch (code)
            {
                case MateUtil.MU.API_ERR_SUCCESS:
                    name = "成功";
                    break;
                case MateUtil.MU.API_ERR_ALLREADY_RUNNING:
                    name = "已经运行";
                    break;
                case MateUtil.MU.API_ERR_CONNECTION_FAILED:
                    name = "连接服务器失败";
                    break;
                case MateUtil.MU.API_ERR_DISCONNECTED:
                    name = "未连接服务器";
                    break;
                case MateUtil.MU.API_ERR_SEND_FAILED:
                    name = "请求失败";
                    break;
                case MateUtil.MU.API_ERR_TIMEOUT:
                    name = "请求超时";
                    break;
                default:
                    name = "未定义";
                    break;
            }
            return name;
        }

        //禁用规则，并不需要具体的规则文件
        private void OnDeactivateMSF(int bwid, int feedid, string msfName)
        {
            try
            {
                int rtn = cortexAPI.DeactivateMSF(bwid, feedid, MateUtil.MU.enMsfCategory.SinglePreset, msfName);
                if (rtn == 0)
                {
                    WriteLog("禁用 规则" + msfName + " 成功！\n");
                    PrintLog("禁用 规则" + msfName + " 成功！\n");
                }
                else
                {
                    WriteLog("禁用 规则" + msfName + " 失败，返回：" + rtn + "\n");
                    PrintLog("禁用 规则" + msfName + " 失败，返回：" + rtn + "\n");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //激活规则，考虑修改读取路径
        private void OnActiveMSF(int bwid, int feedid, string msfName, string imsName)
        {
            try
            {
                FileStream fsMSF = new FileStream("./jingyingMSF/" + msfName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                int msfLen = (int)fsMSF.Length;
                byte[] msfByte = new byte[msfLen];
                int r1 = fsMSF.Read(msfByte, 0, msfByte.Length);
                string msfStr = System.Text.Encoding.UTF8.GetString(msfByte);
                fsMSF.Close();
                //
                FileStream fsIMS = new FileStream("./jingyingMSF/" + imsName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                int imsLen = (int)fsIMS.Length;
                byte[] imsByte = new byte[imsLen];
                int r2 = fsIMS.Read(imsByte, 0, imsByte.Length);
                fsIMS.Close();

                int rtn = cortexAPI.SetMSF(msfStr, imsByte, imsLen, true);
                if (rtn == 0)
                {
                    WriteLog("激活 规则" + msfName + " 成功！\n");
                    PrintLog("激活 规则" + msfName + " 成功！\n");
                }
                else
                {
                    WriteLog("激活 规则" + msfName + " 失败，返回：" + rtn + "\n");
                    PrintLog("激活 规则" + msfName + " 失败，返回：" + rtn + "\n");
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.Message + ex.StackTrace);
            }
        }

        //设置预置位
        private void OnControlPTZ(int bwid, int feedid, int pos)
        {
            string rtn = cortexAPI.PTZControl(bwid, feedid, "PRESET", pos, 0);
            if (rtn == "0")
            {
                WriteLog("切换cam" + feedid + " 到预置位" + pos + " 成功！\n");
                PrintLog("切换cam" + feedid + " 到预置位" + pos + " 成功！\n");
            }
            else
            {
                WriteLog("切换cam" + feedid + " 到预置位" + pos + " 失败，返回：" + rtn + "\n");
                PrintLog("切换cam" + feedid + " 到预置位" + pos + " 失败，返回：" + rtn + "\n");
            }
        }

        #endregion

        /**********************************  UI交互  **********************************/
        #region UI交互

        //运行停止
        private void button1_Click(object sender, EventArgs e)
        {
            ResetGlobalVar();   //恢复所有全局变量到初始值，以免声波盾一直响

            lock (runObj)
                isRun = !isRun;

            if (isRun)  //运行
            {
                WriteLog("/-------------------- 开始运行 --------------------/\n");
                printLog.AppendText("/-------------------- 开始运行 --------------------/\n");
                button1.Text = "停止";
                button2.Enabled = true;

                //RegisterToServer(); //连接注册到cortex服务器
                //InformRemoteControl("0");
            }
            else  //停止
            {
                WriteLog("/-------------------- 准备停止 --------------------/\n");
                printLog.AppendText("/-------------------- 准备停止 --------------------/\n");
                button1.Text = "运行";
                
                isAuto = false;
                button2.Enabled = false;

                //LogOutServer();
            }
        }

        //远程开锁
        private void button2_Click(object sender, EventArgs e)
        {
            gateLabel.Text = "关";
            gateLabel.BackColor = System.Drawing.Color.Red;

            InformRemoteControl("1");
        }

        //自动
        private void button3_Click(object sender, EventArgs e)
        {
            lock (autoObj)
                isAuto = true;

            WriteLog("【自动模式】\n");
            printLog.AppendText("【自动模式】\n");
            button5.Visible = false;
            button6.Visible = false;
            button5.Enabled = false;
            button6.Enabled = false;

            //自动获取门状态
            ThreadStart threadStart = new ThreadStart(GetGateStatus);
            Thread thread = new Thread(threadStart);
            thread.Start();
        }

        //手动
        private void button4_Click(object sender, EventArgs e)
        {
            lock (autoObj)
                isAuto = false;

            WriteLog("【手动模式】\n");
            printLog.AppendText("【手动模式】\n");
            button5.Visible = true;
            button6.Visible = true;
            button5.Enabled = true;
            button6.Enabled = true;

            //恢复命令0
            //ThreadStart threadStart = new ThreadStart(test);
            //Thread thread = new Thread(threadStart);
            //thread.Start();
        }

        private void test()
        {
            while (!isAuto)
            {
                InformRemoteControl("0");
                Thread.Sleep(4000);
            }
        }
        
        //开门流程
        private void button5_Click(object sender, EventArgs e)
        {
            gateLabel.Text = "开";
            gateLabel.BackColor = System.Drawing.Color.Green;

            ProcessGate(1);
        }

        //关门流程
        private void button6_Click(object sender, EventArgs e)
        {
            gateLabel.Text = "关";
            gateLabel.BackColor = System.Drawing.Color.Red;

            ProcessGate(0);
        }
        
        //退出
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("是否退出程序？", "退出", MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                Dispose(true);
                Application.Exit();
                Environment.Exit(0);
            }
            else
                e.Cancel = true;
        }

        #endregion
    }
}
