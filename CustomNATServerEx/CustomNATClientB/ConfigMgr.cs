using System;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Net;

namespace CustomNATClientB
{
    class MyConfigMgr
    {
        private int nLocalPortCom;
        private int nLocalPortEcho;
        private int nBufferSize;
        private bool bLog;
        private IPEndPoint ipServer;
        public MyConfigMgr()
        {
            nLocalPortCom = 12777;
            nLocalPortEcho = 12779;
            nBufferSize = 2048;
            bLog = true;
            ipServer = null;
        }
        public void Init()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "NatBCfg.xml";
            if (File.Exists(path))
            {
                // 读取文件内容
                StreamReader reader = new StreamReader(path);
                string xmlcontent = reader.ReadToEnd();
                reader.Close();

                // 解析属性值
                var doc = XDocument.Parse(xmlcontent);
                var vLocalPortCom = doc.Descendants("common").First().Attribute("ClientBPortCom").Value;
                nLocalPortCom = int.Parse(vLocalPortCom);
                if (nLocalPortCom == 0)
                {
                    nLocalPortCom = 12777;
                }
                var vLocalPortEcho = doc.Descendants("common").First().Attribute("ClinetBPortEcho").Value;
                nLocalPortEcho = int.Parse(vLocalPortEcho);
                if (nLocalPortEcho == 0)
                {
                    nLocalPortEcho = 12779;
                }
                var vBufferSize = doc.Descendants("common").First().Attribute("BufferSize").Value;
                nBufferSize = int.Parse(vBufferSize);
                if (nBufferSize == 0)
                {
                    nBufferSize = 2048;
                }
                var vLog = doc.Descendants("common").First().Attribute("Log").Value;
                bLog = bool.Parse(vLog);
                string strIPServer = doc.Descendants("common").First().Attribute("ServerIP").Value.ToString();
                var vPortServer = doc.Descendants("common").First().Attribute("ServerPort").Value;
                int nPortServer = int.Parse(vPortServer);
                ipServer = new IPEndPoint(IPAddress.Parse(strIPServer), nPortServer);
            }
            else
            {
                Console.WriteLine("配置文件不存在，使用默认值");
            }
            // 打印结果
            Console.WriteLine($"通信端口: {nLocalPortCom}, 回显端口: {nLocalPortEcho}");
            Console.WriteLine($"户口服务器: {ipServer.ToString()}");
            Console.WriteLine($"接收缓冲区长度为 {nBufferSize} 字节");
            Console.WriteLine($"日志开关: {bLog}\r\n");
        }

        public int LocalPortCommunication
        {
            get
            {
                return nLocalPortCom;
            }
        }
        public int LocalPortEcho
        {
            get
            {
                return nLocalPortEcho;
            }
        }
        public int BufferSize
        {
            get
            {
                return nBufferSize;
            }
        }
        public bool Log
        {
            get
            {
                return bLog;
            }
        }
        public IPEndPoint IPServer
        {
            get
            {
                return ipServer;
            }
        }
    }
}