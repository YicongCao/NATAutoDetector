using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using CustomNATCommon;

namespace CustomNATClientA
{
    class Program
    {
        static void SendPacket(string content, IPEndPoint ipe, UdpClient udpClient)
        {
            byte[] bytesContent = Encoding.ASCII.GetBytes(content);
            udpClient.Send(bytesContent, bytesContent.Length, ipe);
        }
        static bool ReceivePacket(ref string content, ref IPEndPoint ipFrom, UdpClient udpClient)
        {
            ipFrom = new IPEndPoint(IPAddress.Any, 0);
            byte[] bytesData = udpClient.Receive(ref ipFrom);
            content = Encoding.ASCII.GetString(bytesData);
            return true;
        }
        static bool ReceivePacketTimeout(ref string content, ref IPEndPoint ipFrom, ref UdpClient udpClient, int timeMiliseconds)
        {
            var timeToWait = TimeSpan.FromMilliseconds(timeMiliseconds);
            var asyncResult = udpClient.BeginReceive(null, null);
            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    byte[] receivedData = udpClient.EndReceive(asyncResult, ref ipFrom);
                    content = Encoding.ASCII.GetString(receivedData);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"接收异常: {ex.Message}");
                    return false;
                }
            }
            else
            {
                // 尝试结束上一个没收到包的Receive请求, 否则对下一次会造成干扰
                RenewUdpClient(ref udpClient);
                return false;
            }
        }
        static List<Response> SendRequests(List<Request> listReq, IPEndPoint ipe, UdpClient udpClient)
        {
            string strReq = Actions.PackRequestsIntoXML(listReq);
            byte[] bytesRequest = Encoding.ASCII.GetBytes(strReq);
            int nRet = udpClient.Send(bytesRequest, bytesRequest.Length, ipe);
            IPEndPoint ipFrom = new IPEndPoint(IPAddress.Any, 0);
            byte[] bytesResponse = udpClient.Receive(ref ipFrom);
            string strResp = Encoding.ASCII.GetString(bytesResponse);
            List<Response> listResp = Actions.ParseResponsesFromXML(strResp);
            return listResp;
        }
        static List<Response> SendRequestsTimeout(List<Request> listReq, IPEndPoint ipe, ref UdpClient udpClient, int timeMiliseconds)
        {
            string strReq = Actions.PackRequestsIntoXML(listReq);
            byte[] bytesRequest = Encoding.ASCII.GetBytes(strReq);
            int nRet = udpClient.Send(bytesRequest, bytesRequest.Length, ipe);
            IPEndPoint ipFrom = new IPEndPoint(IPAddress.Any, 0);
            string strResp = "";
            List<Response> listResp = new List<Response>();
            IPEndPoint remoteIP = null;

            var timeToWait = TimeSpan.FromMilliseconds(timeMiliseconds);

            var asyncResult = udpClient.BeginReceive(null, null);
            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    byte[] receivedData = udpClient.EndReceive(asyncResult, ref remoteIP);
                    strResp = Encoding.ASCII.GetString(receivedData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"接收异常: {ex.Message}");
                }
            }
            else
            {
                RenewUdpClient(ref udpClient);
            }

            if (strResp != "")
            {
                listResp = Actions.ParseResponsesFromXML(strResp);
            }

            return listResp;
        }
        static void RenewUdpClient(ref UdpClient udpClient)
        {
            string strSelfAddr = udpClient.Client.LocalEndPoint.ToString();
            IPEndPoint ipSelfAddr = CustomNATCommon.Utils.CreateIPEndPoint(strSelfAddr);
            udpClient.Close();
            udpClient = new UdpClient(ipSelfAddr.Port);
        }
        static void Main(string[] args)
        {
            // For AutoLog
            string strNATResult = "", strLocalIP = "", strOutIP = "", strMyName = "";
            strNATResult = "探测失败";
            Console.WriteLine("NAT 探查程序开始运行");
            MyConfigMgr configMgr = new MyConfigMgr();
            configMgr.Init();
            string myName = Guid.NewGuid().ToString();
            UdpClient udpClient = new UdpClient(0);
            Console.WriteLine($"本机名称: {myName}");
            // 获取一下本地地址
            IPEndPoint ipLocal = udpClient.Client.LocalEndPoint as IPEndPoint;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("1.1.1.1", 1);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                ipLocal = new IPEndPoint(endPoint.Address, ipLocal.Port);
                socket.Close();
            }
            Console.WriteLine($"本地地址: {ipLocal.ToString()}");
            strMyName = myName;
            strLocalIP = ipLocal.ToString();
            Thread.Sleep(configMgr.WaitMiliseconds);

            Func<int> funcEnd =
                () =>
                {
                    List<Request> listReqEnd = new List<Request>();
                    listReqEnd.Add(new Request("erase", myName));
                    SendRequestsTimeout(listReqEnd, configMgr.IPServer, ref udpClient, configMgr.WaitMiliseconds);
                    udpClient.Close();
                    if (configMgr.AutoPilot)
                    {
                        string strLog = configMgr.AutoLogFormat.Replace("date", DateTime.Now.ToString())
                                                               .Replace("result", strNATResult)
                                                               .Replace("name", strMyName)
                                                               .Replace("localip", strLocalIP)
                                                               .Replace("outip", strOutIP);
                        System.IO.File.AppendAllText("NATDetect.log", strLog + Environment.NewLine);
                        Console.WriteLine("\r\n程序即将自动退出");
                    }
                    else
                    {
                        Console.WriteLine("\r\n按任意键退出探测程序");
                        Console.ReadKey(true);
                    }
                    return 0;
                };

            Console.WriteLine("\r\n正在联系户口服务器");
            List<Request> listReq = new List<Request>();
            listReq.Add(new Request("register", myName));
            listReq.Add(new Request("queryuser", myName));
            listReq.Add(new Request("queryuser", "B"));
            listReq.Add(new Request("a2b_r", ""));
            List<Response> listResp = SendRequestsTimeout(listReq, configMgr.IPServer, ref udpClient, configMgr.WaitMiliseconds);
            //List<Response> listResp = SendRequests(listReq, configMgr.IPServer, udpClient);
            // 这里验证出一个坑来，同步模式老LSP没问题，用异步方式（底层IOCP），LSP会给我的包体内容有干扰

            IPEndPoint ipA1 = null;
            IPEndPoint ipA2 = null;
            IPEndPoint ipB = null;

            // 这里分别解析四条应答的内容，写的有点繁琐了
            if (listResp.Count != 4)
            {
                strNATResult = "取得户口服务应答失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }

            if (listResp[0].ResultInteger == 0)
            {
                Console.WriteLine("注册本机名称成功");
            }
            else
            {
                strNATResult = "注册本机名称失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }
            if (listResp[1].ResultInteger == 0)
            {
                Console.WriteLine($"查询本机地址成功: {listResp[1].ResultString}");
                ipA1 = CustomNATCommon.Utils.CreateIPEndPoint(listResp[1].ResultString);
                if (ipA1 == null)
                {
                    strNATResult = "解析IP回显结果异常";
                    Console.WriteLine(strNATResult);
                    funcEnd();
                    return;
                }
                strOutIP = ipA1.ToString();
            }
            else
            {
                strNATResult = "取得IP回显请求失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }
            if (listResp[2].ResultInteger == 0)
            {
                Console.WriteLine($"查询伙伴地址成功: {listResp[2].ResultString}");
                ipB = CustomNATCommon.Utils.CreateIPEndPoint(listResp[2].ResultString);
                if (ipB == null)
                {
                    strNATResult = "解析伙伴IP地址异常";
                    Console.WriteLine(strNATResult);
                    funcEnd();
                    return;
                }
            }
            else
            {
                strNATResult = "取得伙伴地址失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }
            if (listResp[3].ResultInteger == 0)
            {
                Console.WriteLine("命令伙伴给本机发包成功");
            }
            else
            {
                strNATResult = "命令伙伴给本机发包失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }

            // 验证NAT开放
            Console.WriteLine("\r\n尝试接收伙伴回包");
            string strRecvPacket = "";
            IPEndPoint ipRecvFrom = new IPEndPoint(IPAddress.Any, 0);
            bool bRecv = ReceivePacketTimeout(ref strRecvPacket, ref ipRecvFrom, ref udpClient, configMgr.WaitMiliseconds);
            if (bRecv)
            {
                Console.WriteLine($"收到来自伙伴 {ipRecvFrom.ToString()} 的回包, 包体内容是:\r\n{strRecvPacket}");
                Console.WriteLine("\r\n检测到【NAT开放 - 完全圆锥NAT】, 探测完成");
                strNATResult = "完全圆锥NAT";
                funcEnd();
                return;
            }
            else
            {
                Console.WriteLine("未能收到伙伴回包");
            }

            // 验证NAT严格
            Console.WriteLine("\r\n尝试主动联系伙伴");
            listReq.Clear();
            listResp.Clear();
            listReq.Add(new Request("echoip", ""));
            listResp = SendRequestsTimeout(listReq, ipB, ref udpClient, configMgr.WaitMiliseconds);
            if (listResp.Count != 1)
            {
                strNATResult = "主动联系伙伴失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }
            Console.WriteLine("主动联系伙伴成功");
            Console.WriteLine($"伙伴给出的本机地址: {listResp[0].ResultString}");
            ipA2 = CustomNATCommon.Utils.CreateIPEndPoint(listResp[0].ResultString);
            if (ipA2 == null)
            {
                strNATResult = "解析伙伴给出的本机出口IP错误";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }

            if (ipA1.Port != ipA2.Port)
            {
                Console.WriteLine($"NAT网关为本机分配了两个出口: {ipA1.ToString()}, {ipA2.ToString()}");
                Console.WriteLine("\r\n检测到【NAT严格 - 对称NAT】, 探测完成");
                strNATResult = "对称NAT";
                funcEnd();
                return;
            }

            // 验证NAT中等的具体类型
            Console.WriteLine("\r\n尝试命令伙伴在另一端口发包");
            listReq.Clear();
            listResp.Clear();
            listReq.Add(new Request("a2b_r", ""));
            listResp = SendRequestsTimeout(listReq, configMgr.IPServer, ref udpClient, configMgr.WaitMiliseconds);
            if (listResp.Count != 1 || listResp[0].ResultInteger != 0)
            {
                strNATResult = "联系户口服务器，命令伙伴在另一端口发包失败";
                Console.WriteLine(strNATResult);
                funcEnd();
                return;
            }
            Console.WriteLine("命令传达成功, 伙伴已在另一端口发包");
            strRecvPacket = "";
            ipRecvFrom = new IPEndPoint(IPAddress.Any, 0);
            bRecv = ReceivePacketTimeout(ref strRecvPacket, ref ipRecvFrom, ref udpClient, configMgr.WaitMiliseconds);
            if (bRecv)
            {
                Console.WriteLine($"收到来自伙伴 {ipRecvFrom.ToString()} 的回包, 包体内容是:\r\n{strRecvPacket}");
                Console.WriteLine("\r\n检测到【NAT中等 - 受限圆锥NAT】, 探测完成");
                strNATResult = "受限圆锥NAT";
                funcEnd();
                return;
            }
            else
            {
                Console.WriteLine("未能收到伙伴回包");
                Console.WriteLine("\r\n检测到【NAT中等 - 端口受限圆锥NAT】, 探测完成");
                strNATResult = "端口受限圆锥NAT";
                funcEnd();
                return;
            }
        }
    }
}
