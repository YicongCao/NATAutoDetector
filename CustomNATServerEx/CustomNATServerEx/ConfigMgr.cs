using System;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Net;

namespace CustomNATServer
{
    class MyConfigMgr
    {
        private int nLocalPort;
        private int nBufferSize;
        private bool bLog;
        private IPEndPoint ipBCom;
        private IPEndPoint ipBEcho;
        public MyConfigMgr()
        {
            nLocalPort = 7777;
            nBufferSize = 2048;
            bLog = true;
            ipBCom = null;
            ipBEcho = null;
        }
        public void Init()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "NatSvrCfg.xml";
            if (File.Exists(path))
            {
                // 读取文件内容
                StreamReader reader = new StreamReader(path);
                string xmlcontent = reader.ReadToEnd();
                reader.Close();

                // 解析属性值
                var doc = XDocument.Parse(xmlcontent);
                var vLocalPort = doc.Descendants("common").First().Attribute("LocalPort").Value;
                nLocalPort = int.Parse(vLocalPort);
                if (nLocalPort == 0)
                {
                    nLocalPort = 7777;
                }
                var vBufferSize = doc.Descendants("common").First().Attribute("BufferSize").Value;
                nBufferSize = int.Parse(vBufferSize);
                if (nBufferSize == 0)
                {
                    nBufferSize = 2048;
                }
                var vLog = doc.Descendants("common").First().Attribute("Log").Value;
                bLog = bool.Parse(vLog);
                string strIPB = doc.Descendants("common").First().Attribute("ClientBIP").Value.ToString();
                var vPortBCom = doc.Descendants("common").First().Attribute("ClientBPortCom").Value;
                var vPortBEcho = doc.Descendants("common").First().Attribute("ClinetBPortEcho").Value;
                int nPortBCom = int.Parse(vPortBCom);
                int nPortBEcho = int.Parse(vPortBEcho);
                ipBCom = new IPEndPoint(IPAddress.Parse(strIPB), nPortBCom);
                ipBEcho = new IPEndPoint(IPAddress.Parse(strIPB), nPortBEcho);
            }
            else
            {
                Console.WriteLine("配置文件不存在，使用默认值");
            }
            // 打印结果
            Console.WriteLine($"监听本地 {nLocalPort} 端口");
            Console.WriteLine($"接收缓冲区长度为 {nBufferSize} 字节");
            Console.WriteLine($"日志开关: {bLog}\r\n");
            Console.WriteLine($"客户端B的通信地址: {ipBCom.ToString()}");
            Console.WriteLine($"客户端B的IP回显地址: {ipBEcho.ToString()}");
        }

        public int LocalPort
        {
            get
            {
                return nLocalPort;
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
        public IPEndPoint IPBCommunication
        {
            get
            {
                return ipBCom;
            }
        }
        public IPEndPoint IPBEcho
        {
            get
            {
                return ipBEcho;
            }
        }
    }
}