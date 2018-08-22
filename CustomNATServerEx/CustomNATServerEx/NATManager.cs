using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace CustomNATServer
{
    class NATManager
    {
        private Dictionary<string, EndPoint> mapClients;
        public NATManager()
        {
            mapClients = new Dictionary<string, EndPoint>();
        }
        public void RegisterClient(string name, EndPoint addr)
        {
            mapClients[name] = addr;
        }
        public void RemoveClient(string name)
        {
            if (mapClients.ContainsKey(name))
            {
                mapClients.Remove(name);
            }
        }
        public EndPoint GetClient(string name)
        {
            if (mapClients.ContainsKey(name))
            {
                return mapClients[name];
            }
            else
            {
                return null;
            }
        }
        public void PrintStatus()
        {
            Console.WriteLine("当前NAT表内容: ");
            foreach (var kv in mapClients)
            {
                Console.WriteLine($"[name] {kv.Key.ToString()} [addr] {kv.Value.ToString()}");
            }
        }
        public string GetAllClientName(string strSeparator)
        {
            StringBuilder strBuilder = new StringBuilder();
            foreach (var kv in mapClients)
            {
                strBuilder.Append(kv.Key.ToString() + strSeparator);
            }
            string strResult = strBuilder.ToString();
            if (strResult.EndsWith(strSeparator))
            {
                strResult = strResult.Substring(0, strResult.Length - strSeparator.Length);
            }
            return strResult;
        }
    }
}
