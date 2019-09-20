using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Web;

namespace CortexLinkageSolu_UI
{
    public class LinkageObj
    {
        public string repeat { get; set; }
        public string trigger { get; set; }
        public string delay { get; set; }
        public Sequence sequence;
    }

    public class Sequence
    {
        public int num { get; set; }
        public List<Instruction> instruction;
    }

    public class Instruction
    {
        public string operation { get; set; }
        public string uuid { get; set; }
        public string detail { get; set; }
    }

    public static class GenboxHelper
    {
        //HTTP POST 请求
        public static string ToPost(string url, string content, string contentType)
        {
            //配置Http协议头
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = contentType;

            //发送数据
            byte[] data = Encoding.UTF8.GetBytes(content);  //在转换字节时指定编码格式
            req.ContentLength = data.Length;
            using (Stream reqStream = req.GetRequestStream())
            {
                reqStream.Write(data, 0, data.Length);
                reqStream.Close();
            }

            string result = "";
            //获取响应内容
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }

        //HTTP GET 请求
        public static string ToGet(string url)
        {
            //配置Http协议头
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";

            string result = "";
            //获取响应内容
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            Stream stream = resp.GetResponseStream();
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                result = reader.ReadToEnd();
            }
            return result;
        }

        //算法运行查询
        public static string algo_enable_get(string uuid, string algo)
        {
            string result = "";
            try
            {
                while ((result = ToGet("http://192.168.1.105:9999/cgi-bin/algo_enable_get.cgi?uuid=" + uuid + "&algo=" + algo)) == "fail")
                {
                    Thread.Sleep(1000);
                }
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message + ex.StackTrace;
            }
        }

        //算法运行设定
        public static string algo_enable_set(string uuid, string algo, string enable)
        {
            string result = "";
            try
            {
                while ((ToGet("http://192.168.1.105:9999/cgi-bin/algo_enable_set.cgi?uuid=" + uuid + "&algo=" + algo + "&enable=" + enable)) == "fail")
                {
                    Thread.Sleep(1000);
                }
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message + ex.StackTrace;
            }
        }

        //算法告警状态查询
        public static string ipcam_alarm_get(string uuid)
        {
            string result = "";
            try
            {
                while ((ToGet("http://192.168.1.105:9999/cgi-bin/ipcam_alarm_get.cgi?uuid=" + uuid)) == "fail")
                {
                    Thread.Sleep(1000);
                }
                return result;
            }
            catch (Exception ex)
            {
                return ex.Message + ex.StackTrace;
            }
        }

    }
}