using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Net;
using System.Xml;
using System.IO;
using Newtonsoft.Json;
using RestSharp;


#pragma warning disable IDE1006

namespace EvtxToES
{
    public static class EventManager
    {
        public enum Conf
        {
            File,
            Url
        }
        public class Rule
        {
            public string xpath { get; set; }
            public string source { get; set; }
        }
        public class Config
        {
            public Rule[] rules { get; set; }
            public string address { get; set; }
            public int refresh_freq { get; set; }
            public bool ssl_validation { get; set; }

        }
        public static string GetEvents(string logName, string query)
        {
            List<XmlDocument> events = new List<XmlDocument>();
            EventLogQuery eventQuery = new EventLogQuery(logName, PathType.LogName, query);
            try
            {
                EventLogReader eventReader = new EventLogReader(eventQuery);
                for (EventRecord eventDetails = eventReader.ReadEvent(); eventDetails != null; eventDetails = eventReader.ReadEvent())
                {
                    var eventdata = eventDetails.ToXml();
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(eventdata);
                    events.Add(doc);
                }
                string jsonstring = JsonConvert.SerializeObject(events, Newtonsoft.Json.Formatting.Indented);
                return jsonstring;
            }
            catch (Exception e)
            {
                var hostname = Environment.MachineName;
                var message = e.Message;
                var domainname = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
                string timestamp = GetTimestamp(DateTime.UtcNow);
                string errormessage = String.Format("<?xml version=\"1.0\"?><Exception><ExceptionData><Source>EvtxToES</Source><Message>{0}</Message><Timestamp>{1}</Timestamp><Host>{2}</Host><Domain>{3}</Domain></ExceptionData></Exception>", message, timestamp, hostname, domainname);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(errormessage);
                string jsonstring = JsonConvert.SerializeXmlNode(doc, Newtonsoft.Json.Formatting.Indented);
                return jsonstring;

            }
        }
        public static string GetTimestamp(DateTime value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ss");
        }
        public static void SendToLogstash(string logs, string url, bool selfsigned)
        {
            var options = new RestClientOptions(url);
            if (selfsigned == false)
            {
                options.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            }
            var client = new RestSharp.RestClient(options);
            var request = new RestRequest("/", Method.Post)
                .AddBody(logs);
            request.RequestFormat = DataFormat.Json;
            client.Execute(request);
        }
    }
}