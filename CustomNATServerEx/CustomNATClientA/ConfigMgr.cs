using System;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using System.Net;

namespace CustomNATClientA
{
    class MyConfigMgr
    {
        private int nWaitMS;
        private IPEndPoint ipServer;
        public MyConfigMgr()
        {
            nWaitMS = 2000;
            ipServer = null;
        }
        public void Init()
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "NatACfg.xml";
            if (File.Exists(path))
            {
                // 读取文件内容
                StreamReader reader = new StreamReader(path);
                string xmlcontent = reader.ReadToEnd();
                reader.Close();

                // 解析属性值
                var doc = XDocument.Parse(xmlcontent);
                var vWaitMS = doc.Descendants("common").First().Attribute("Wait").Value;
                nWaitMS = int.Parse(vWaitMS);
                if (nWaitMS == 0)
                {
                    nWaitMS = 2000;
                }
                string strServerIP = doc.Descendants("common").First().Attribute("ServerIP").Value.ToString();

                string strServerPort = doc.Descendants("common").First().Attribute("ServerPort").Value;
                int nServerPort = int.Parse(strServerPort);
                ipServer = new IPEndPoint(IPAddress.Parse(strServerIP), nServerPort);
            }
            else
            {
                Console.WriteLine("配置文件不存在，使用默认值");
            }
            // 打印结果
            Console.WriteLine($"户口服务器: {ipServer.ToString()}");
        }

        public int WaitMiliseconds
        {
            get
            {
                return nWaitMS;
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