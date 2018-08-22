using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using socket.core.Server;
using CustomNATCommon;

namespace CustomNATServer
{
    class Program
    {
        static MyConfigMgr configMgr = new MyConfigMgr();
        static NATManager natMgr = new NATManager();
        static UdpServer udpServer = null;
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
            udpServer = new UdpServer(configMgr.BufferSize);
            udpServer.OnReceive += UdpServer_OnReceive;
            udpServer.OnSend += UdpServer_OnSend;
            udpServer.Start(configMgr.LocalPort);
            Log($"CustomNATEx 服务端正在端口 {configMgr.LocalPort} 上运行");

            //List<Request> listReq = new List<Request>();
            //listReq.Add(new Request("register", "A"));
            //listReq.Add(new Request("queryuser", "A"));
            //string strReq = Actions.PackRequestsIntoXML(listReq);

            Console.ReadKey(true);
            udpServer.OnReceive -= UdpServer_OnReceive;
            udpServer.OnSend -= UdpServer_OnSend;
            Log($"CustomNATEx 服务端已经停止运行");
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
            List<Response> listResp = new List<Response>();
            foreach (Request r in listReq)
            {
                Log($"正在处理该指令: {r.Command}");
                switch (r.Command)
                {
                    case "register":
                        if (r.Param != "B")
                        {
                            natMgr.RegisterClient(r.Param, arg1);
                            Log($"已登记计算机: [name] {r.Param} [addr] {arg1.ToString()}");
                            listResp.Add(new Response("success", 0));
                        }
                        else
                        {
                            Log($"不能注册B的名字");
                            listResp.Add(new Response("do not reg as B", 4));
                        }
                        break;
                    case "queryuser":
                        EndPoint user = null;
                        if (r.Param == "B")
                        {
                            user = configMgr.IPBEcho;
                        }
                        else
                        {
                            user = natMgr.GetClient(r.Param);
                        }
                        if (user != null)
                        {
                            Log($"已找到计算机: [name] {r.Param} [addr] {user.ToString()}");
                            listResp.Add(new Response(user.ToString(), 0));
                        }
                        else
                        {
                            Log($"未找到计算机: [name] {r.Param}");
                            listResp.Add(new Response("not found", 2));
                        }
                        break;
                    case "queryall":
                        string strAllUserName = natMgr.GetAllClientName(";");
                        Log($"已获取所有计算机列表: {strAllUserName}");
                        listResp.Add(new Response(strAllUserName, 0));
                        break;
                    case "echoip":
                        Log($"来自该计算机的IP回显请求: [addr] {arg1.ToString()}");
                        listResp.Add(new Response(arg1.ToString(), 0));
                        break;
                    case "erase":
                        if (r.Param != "B")
                        {
                            natMgr.RemoveClient(r.Param);
                            Log($"已擦除计算机: [name] {r.Param}");
                            listResp.Add(new Response("success", 0));

                        }
                        else
                        {
                            Log($"不能擦除计算机: [name] B");
                            listResp.Add(new Response("do not erase B", 3));
                        }
                        break;
                    case "a2b_r":
                        Log($"来自该计算机的a2b_r请求: [addr] {arg1.ToString()} [param] {r.Param}");
                        string strParamIPA = arg1.ToString();
                        var strIPSlice = r.Param.Split(':', 2);
                        if (strIPSlice.Length == 2)
                        {
                            try
                            {
                                IPEndPoint ipa = new IPEndPoint(IPAddress.Parse(strIPSlice[0]), int.Parse(strIPSlice[1]));
                                strParamIPA = ipa.ToString();
                            }
                            catch
                            {
                                Log($"该请求的 param 非法");
                                strParamIPA = arg1.ToString();
                            }
                        }
                        List<Request> listReqForB = new List<Request>();
                        listReqForB.Add(new Request("a2b_r", strParamIPA));
                        string strReqForB = Actions.PackRequestsIntoXML(listReqForB);
                        byte[] bytesReqForB = Encoding.ASCII.GetBytes(strReqForB);
                        udpServer.Send(configMgr.IPBCommunication, bytesReqForB, 0, bytesReqForB.Length);
                        listResp.Add(new Response("success", 0));
                        break;
                    default:
                        Log($"该指令不支持: {r.Command}");
                        listResp.Add(new Response("command not support", 1));
                        break;
                }
            }
            string strResp = Actions.PackResponsesIntoXML(listResp);
            byte[] bytesResponse;
            bytesResponse = Encoding.ASCII.GetBytes(strResp);
            udpServer.Send(arg1, bytesResponse, 0, bytesResponse.Length);
            Log($"来自 {arg1.ToString()} 的请求已经处理完毕");
            Log($"应答内容如下: \r\n{strResp}");
            if (configMgr.Log)
                Console.WriteLine($"\r\n----------------------------------\r\n");
        }
    }
}
