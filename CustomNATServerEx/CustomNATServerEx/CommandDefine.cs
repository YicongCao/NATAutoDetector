using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// 约定

// 请求命令
// Command:
// register -> 向服务端注册自己的名字和地址关联信息
// queryuser -> 从服务器读取另一个名字的机器的地址信息
// erase -> 从服务器删除一个名字的机器的信息

/// <summary>
/// C/S端的公共方法
/// </summary>
namespace CustomNATCommon
{
    /// <summary>
    /// 自定请求
    /// </summary>
    public struct Request
    {
        public string Command;
        public string Param;
        public Request(string command, string param)
        {
            this.Command = command;
            this.Param = param;
        }
    }
    /// <summary>
    /// 自定应答
    /// </summary>
    public struct Response
    {
        public string ResultString;
        public int ResultInteger;
        public Response(string retStr, int retInt)
        {
            this.ResultString = retStr;
            this.ResultInteger = retInt;
        }
    }
    /// <summary>
    /// 拆解数据
    /// </summary>
    public class Actions
    {
        /// <summary>
        /// 客户端将请求打包成XML
        /// </summary>
        /// <param name="listReq"></param>
        /// <returns></returns>
        public static string PackRequestsIntoXML(List<Request> listReq)
        {
            XDocument doc = new XDocument(
                new XElement("root",
                new XElement("requestlist")));
            var nodeList = doc.Root.Element("requestlist");
            foreach (Request r in listReq)
            {
                nodeList.Add(new XElement("request",
                    new XAttribute("command", r.Command),
                    new XAttribute("param", r.Param)));
            }
            return doc.ToString();
        }
        /// <summary>
        /// 服务端从XML解析出请求
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static List<Request> ParseRequestsFromXML(string xml)
        {
            List<Request> listReq = new List<Request>();
            try
            {
                XDocument doc = XDocument.Parse(xml);
                var requests = from r in doc.Descendants("request")
                               select new
                               {
                                   cmd = r.Attribute("command").Value,
                                   param = r.Attribute("param").Value
                               };
                foreach (var r in requests)
                {
                    listReq.Add(new Request(r.cmd, r.param));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"XML请求解析异常: {e.Message}");
            }
            return listReq;
        }
        /// <summary>
        /// 服务端将应答打包成XML
        /// </summary>
        /// <param name="listResp"></param>
        /// <returns></returns>
        public static string PackResponsesIntoXML(List<Response> listResp)
        {
            XDocument doc = new XDocument(
               new XElement("root",
               new XElement("responselist")));
            var nodeList = doc.Root.Element("responselist");
            foreach (Response r in listResp)
            {
                nodeList.Add(new XElement("response",
                    new XAttribute("retstr", r.ResultString),
                    new XAttribute("retint", r.ResultInteger.ToString())));
            }
            return doc.ToString();
        }
        /// <summary>
        /// 客户端从XML解析出应答
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static List<Response> ParseResponsesFromXML(string xml)
        {
            List<Response> listResp = new List<Response>();
            try
            {
                XDocument doc = XDocument.Parse(xml);
                var requests = from r in doc.Descendants("response")
                               select new
                               {
                                   retstr = r.Attribute("retstr").Value,
                                   retint = r.Attribute("retint").Value
                               };
                foreach (var r in requests)
                {
                    listResp.Add(new Response(r.retstr, int.Parse(r.retint)));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"XML请求解析异常: {e.Message}");
            }
            return listResp;
        }
    }
}