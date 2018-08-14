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
        static bool ReceivePacketTimeout(ref string content, ref IPEndPoint ipFrom, UdpClient udpClient, int timeMiliseconds)
        {
            var timeToWait = TimeSpan.FromMilliseconds(timeMiliseconds);
            var asyncResult = udpClient.BeginReceive(null, null);
            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    IPEndPoint remoteEP = null;
                    byte[] receivedData = udpClient.EndReceive(asyncResult, ref remoteEP);
                    content = Encoding.ASCII.GetString(receivedData);
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            else
            {
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
        static List<Response> SendRequestsTimeout(List<Request> listReq, IPEndPoint ipe, UdpClient udpClient, int timeMiliseconds)
        {
            string strReq = Actions.PackRequestsIntoXML(listReq);
            byte[] bytesRequest = Encoding.ASCII.GetBytes(strReq);
            int nRet = udpClient.Send(bytesRequest, bytesRequest.Length, ipe);
            IPEndPoint ipFrom = new IPEndPoint(IPAddress.Any, 0);
            string strResp = "";
            List<Response> listResp = new List<Response>();

            var timeToWait = TimeSpan.FromMilliseconds(timeMiliseconds);
            var asyncResult = udpClient.BeginReceive(null, null);
            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    IPEndPoint remoteEP = null;
                    byte[] receivedData = udpClient.EndReceive(asyncResult, ref remoteEP);
                    strResp = Encoding.ASCII.GetString(receivedData);
                }
                catch (Exception ex)
                {
                    
                }
            }
            
            if (strResp != "")
            {
                listResp = Actions.ParseResponsesFromXML(strResp);
            }
            
            return listResp;
        }
        static void Main(string[] args)
        {
            MyConfigMgr configMgr = new MyConfigMgr();
            configMgr.Init();
            string myName = Guid.NewGuid().ToString();
            UdpClient udpClient = new UdpClient(0);
            Console.WriteLine($"本机名称: {myName}");
            Console.WriteLine($"本地地址: {udpClient.Client.LocalEndPoint.ToString()}");
            Thread.Sleep(configMgr.WaitMiliseconds);

            Func<int> funcEnd =
                () =>
                {
                    List<Request> listReqEnd = new List<Request>();
                    listReqEnd.Add(new Request("erase", myName));
                    SendRequestsTimeout(listReqEnd, configMgr.IPServer, udpClient, configMgr.WaitMiliseconds);
                    return 0;
                };

            List<Request> listReq = new List<Request>();
            listReq.Add(new Request("register", myName));
            listReq.Add(new Request("queryuser", myName));
            listReq.Add(new Request("queryuser", "B"));
            listReq.Add(new Request("a2b_r", ""));
            List<Response> listResp = SendRequestsTimeout(listReq, configMgr.IPServer, udpClient, configMgr.WaitMiliseconds);

            IPEndPoint ipA1 = null;
            IPEndPoint ipA2 = null;
            IPEndPoint ipB = null;

            if (listResp.Count != 4)
            {
                Console.WriteLine("首次与户口服务器通信失败");
                return;
            }

            if (listResp[0].ResultInteger == 0)
            {
                Console.WriteLine("注册本机名称成功");
            }
            else
            {
                Console.WriteLine("注册本机名称失败");
                funcEnd();
                return;
            }
            if (listResp[1].ResultInteger == 0)
            {
                Console.WriteLine($"查询本机地址成功: {listResp[1].ResultString}");
                var strIPSlice = listResp[1].ResultString.Split(':');
                if (strIPSlice.Length == 2)
                {
                    try
                    {
                        ipA1 = new IPEndPoint(IPAddress.Parse(strIPSlice[0]), int.Parse(strIPSlice[1]));
                    }
                    catch
                    {
                        Console.WriteLine("解析本机IP地址异常");
                        funcEnd();
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("解析本机IP地址失败");
                    funcEnd();
                    return;
                }
            }
            else
            {
                Console.WriteLine("查询本机地址失败");
                funcEnd();
                return;
            }
            if (listResp[2].ResultInteger == 0)
            {
                Console.WriteLine($"查询伙伴地址成功: {listResp[2].ResultString}");
                string[] ep = listResp[2].ResultString.Split(':');
                if (ep.Length != 2)
                {
                    Console.WriteLine("伙伴地址格式错误");
                    funcEnd();
                    return;
                }
                ipB = new IPEndPoint(IPAddress.Parse(ep[0]), int.Parse(ep[1]));
            }
            else
            {
                Console.WriteLine("查询伙伴地址失败");
                funcEnd();
                return;
            }
            if (listResp[3].ResultInteger == 0)
            {
                Console.WriteLine("命令B给本机发包成功");
            }
            else
            {
                Console.WriteLine("命令B给本机发包失败");
                funcEnd();
                return;
            }

            // 验证NAT开放
            string strRecvPacket = "";
            IPEndPoint ipRecvFrom = new IPEndPoint(IPAddress.Any, 0);
            bool bRecv = ReceivePacketTimeout(ref strRecvPacket, ref ipRecvFrom, udpClient, configMgr.WaitMiliseconds);
            if (bRecv)
            {
                Console.WriteLine($"\r\n收到来自伙伴 {ipRecvFrom.ToString()} 的回包, 包体内容是:\r\n{strRecvPacket}");
                Console.WriteLine("\r\n检测到【NAT开放】, 程序结束");
                funcEnd();
                return;
            }

            // 验证NAT严格
            listReq.Clear();
            listResp.Clear();
            listReq.Add(new Request("echoip", ""));
            listResp = SendRequestsTimeout(listReq, ipB, udpClient, configMgr.WaitMiliseconds);
            if (listResp.Count != 1)
            {
                Console.WriteLine("首次与户口服务器通信失败");
                funcEnd();
                return;
            }
            Console.WriteLine($"B给出的本机地址: {listResp[0].ResultString}");
            string[] ep2 = listResp[0].ResultString.Split(':');
            if (ep2.Length != 2)
            {
                Console.WriteLine("B给出的本机地址格式错误");
                funcEnd();
                return;
            }
            ipA2 = new IPEndPoint(IPAddress.Parse(ep2[0]), int.Parse(ep2[1]));

            string strPortFromS = ipA1.ToString().Split(':')[1];
            string strPortFromB = ipA2.ToString().Split(':')[1];
            if (int.Parse(strPortFromS) != int.Parse(strPortFromB))
            {
                Console.WriteLine($"\r\nNAT网关为本机分配了两个出口: {ipA1.ToString()}, {ipA2.ToString()}");
                Console.WriteLine("\r\n检测到【NAT严格】, 程序结束");
                funcEnd();
                return;
            }

            // 验证NAT中等的具体类型
            listReq.Clear();
            listResp.Clear();
            listReq.Add(new Request("echoip", ""));
            listResp = SendRequestsTimeout(listReq, ipB, udpClient, configMgr.WaitMiliseconds);
            if (listResp.Count != 1 || listResp[0].ResultInteger != 0)
            {
                Console.WriteLine("让S命令B发送包给我失败");
                funcEnd();
                return;
            }
            strRecvPacket = "";
            ipRecvFrom = new IPEndPoint(IPAddress.Any, 0);
            bRecv = ReceivePacketTimeout(ref strRecvPacket, ref ipRecvFrom, udpClient, configMgr.WaitMiliseconds);
            if (bRecv)
            {
                Console.WriteLine($"\r\n收到来自伙伴 {ipRecvFrom.ToString()} 的回包, 包体内容是:\r\n{strRecvPacket}");
                Console.WriteLine("\r\n检测到【NAT中等 - 受限圆锥NAT】, 程序结束");
                funcEnd();
                return;
            }
            else
            {
                Console.WriteLine("\r\n检测到【NAT中等 - 端口受限圆锥NAT】, 程序结束");
                funcEnd();
                return;
            }
        }
    }
}
