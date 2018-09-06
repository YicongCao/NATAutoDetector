using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using socket.core.Server;
using CustomNATCommon;
using System.Net.Sockets;

namespace CustomNATClientB
{
    class Program
    {
        static MyConfigMgr configMgr = new MyConfigMgr();
        static UdpServer udpServerCom = null;
        static UdpServer udpServerEcho = null;
        public static void Log(string format, params object[] paramList)
        {
            if (configMgr.Log)
            {
                Console.WriteLine("[{0}] {1}", DateTime.Now, string.Format(format, paramList));
            }
        }
        static void Main(string[] args)
        {
            configMgr.Init();
            udpServerCom = new UdpServer(configMgr.BufferSize);
            udpServerCom.OnReceive += UdpServer_OnReceive;
            udpServerCom.OnSend += UdpServer_OnSend;
            udpServerCom.Start(configMgr.LocalPortCommunication);

            udpServerEcho = new UdpServer(configMgr.BufferSize);
            udpServerEcho.OnReceive += UdpServer_OnReceive;
            udpServerEcho.OnSend += UdpServer_OnSend;
            udpServerEcho.Start(configMgr.LocalPortEcho);

            Log($"CustomNATEx 客户端B 的通信服务在端口 {configMgr.LocalPortCommunication} 上运行");
            Log($"CustomNATEx 客户端B 的IP回显服务在端口 {configMgr.LocalPortEcho} 上运行");

            Console.ReadKey(true);
            udpServerCom.OnReceive -= UdpServer_OnReceive;
            udpServerCom.OnSend -= UdpServer_OnSend;
            udpServerEcho.OnReceive -= UdpServer_OnReceive;
            udpServerEcho.OnSend -= UdpServer_OnSend;
            Log($"CustomNATEx 客户端B 已经停止运行");
        }
        private static void UdpServer_OnSend(EndPoint arg1, int arg2)
        {

        }
        private static void UdpServer_OnReceive(EndPoint arg1, byte[] arg2, int arg3, int arg4)
        {
            Log($"收到来自 {arg1.ToString()} 的请求");
            string strData = Encoding.ASCII.GetString(arg2, arg3, arg4);
            Log($"请求内容如下: \r\n{strData}");
            List<Request> listReq = Actions.ParseRequestsFromXML(strData);
            foreach (Request r in listReq)
            {
                Log($"正在处理该指令: {r.Command}");
                switch (r.Command)
                {
                    case "echoip":
                        Log($"来自该计算机的IP回显请求: [addr] {arg1.ToString()}");
                        List<Response> listEchoIPResp = new List<Response>();
                        listEchoIPResp.Add(new Response(arg1.ToString(), 0));
                        string strEchoIPResp = Actions.PackResponsesIntoXML(listEchoIPResp);
                        byte[] bytesEchoIPResponse = Encoding.ASCII.GetBytes(strEchoIPResp);
                        udpServerEcho.Send(arg1, bytesEchoIPResponse, 0, bytesEchoIPResponse.Length);
                        Console.WriteLine($"应答内容:\r\n{strEchoIPResp}");
                        break;
                    
                    case "a2b_r":
                        Log($"来自该计算机的a2b_r请求: [addr] {arg1.ToString()} [param] {r.Param}");
                        IPEndPoint ipa = CustomNATCommon.Utils.CreateIPEndPoint(r.Param);
                        List<Response> listRespForA = new List<Response>();
                        listRespForA.Add(new Response("a2b_r_confirm", 0));
                        string strRespForA = Actions.PackResponsesIntoXML(listRespForA);
                        byte[] bytesRespForA = Encoding.ASCII.GetBytes(strRespForA);
                        if (ipa != null)
                        {
                            try
                            {
                                //Thread.Sleep(100);
                                UdpClient udpClient = new UdpClient(0);
                                udpClient.Send(bytesRespForA, bytesRespForA.Length, ipa);
                                Log($"已发送a2b_r_confirm确认包给: [addr] {ipa.ToString()} [localaddr] {udpClient.Client.LocalEndPoint}");
                                Log($"应答内容:\r\n{strRespForA}");
                                udpClient.Close();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"a2b_r请求处理异常: {e.ToString()}");
                            }
                        }
                        else
                        {
                            Log($"a2b_r请求处理失败, ipa == null");
                        }
                        break;
                    default:
                        Log($"该指令不支持: {r.Command}");
                        break;
                }
            }
            Log($"来自 {arg1.ToString()} 的请求已经处理完毕");
            if (configMgr.Log)
                Console.WriteLine($"\r\n----------------------------------\r\n");
        }
    }
}
