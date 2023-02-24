using System;
using System.Diagnostics;
using System.ServiceProcess;
using EvtxToES;
using System.Timers;
using RestSharp;
using Newtonsoft.Json;
using System.IO;

#pragma warning disable IDE0044
#pragma warning disable IDE0079

namespace EvtxToESSvc
{
    
    public partial class EvtxToEsSvc : ServiceBase
    {
        
        private EvtxToES.EventManager.Config currentConfig;
        private EventLog ServiceEvtx;
        public EvtxToEsSvc(string[] args)
        {
            InitializeComponent(); 
            this.ServiceEvtx = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("EvtxToESSvc"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "EvtxToESSvc", "EvtxToESSvcLog");
            }
            this.ServiceEvtx.Source = "EvtxToESSvc";
            this.ServiceEvtx.Log = "EvtxToESSvcLog";
            this.currentConfig = new EvtxToES.EventManager.Config();
        }
        protected override void OnStart(string[] args)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string argholder = "";
            foreach(string arg in arguments)
            {
                argholder = argholder + arg + '\r';
            }
            argholder += arguments.Length.ToString();
            this.ServiceEvtx.WriteEntry(argholder);


            /*Check the arguments*/
            if (arguments.Length < 3 || arguments.Length > 4)
            {
                this.ServiceEvtx.WriteEntry("Service started with wrong number of parameters. " +
                    "Provide paramaters like: type(url|file), path(http://example.com:5000|C:\\Users\\JohnDoe\\config.json), ssl_validation(selfisnged|)");
                this.Stop();
            }
            else if(arguments.Length == 4)
            {
                if (arguments[3] == "selfsigned")
                {
                    //ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
                }
            }
            if (arguments[1] != "url" && arguments[1] != "file")
            {
                this.ServiceEvtx.WriteEntry("You have provided wrong config source type. Enter (file|url) as a first argument.");
                this.Stop();
            }
            
            /*Logging start with parameters*/
            string arguments_string = "";
            foreach(string arg in arguments)
            {
                arguments_string += arg;
                arguments_string += '\r';
            }
            string url = arguments[2];
            this.ServiceEvtx.WriteEntry("Service started with parameters: \r" + arguments_string);



            try
            {
                /*Strings for logging*/
                string logtext = "Application Loaded with rules:\r";
                string configString;

                /*Getting config from url*/
                if (arguments[1] == "url")
                {
                    var options = new RestClientOptions(url);
                    if (arguments.Length == 4)
                    {
                        if (arguments[3] == "selfsigned")
                        {
                            options.RemoteCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                        }
                    }
                    var client = new RestClient(options);
                    var request = new RestRequest("/", Method.Get);
                    RestResponse response = client.Execute(request);
                    configString = response.Content;
                }
                
            
                

                /*Getting config from file*/
                else
                {
                    configString = File.ReadAllText(arguments[2]);
                }

                /*Logging the rules to the evtx*/
                ServiceEvtx.WriteEntry("Service loaded logging policy: \r" + configString);
                var currentConfig = JsonConvert.DeserializeObject<EventManager.Config>(configString);
                foreach (EventManager.Rule rule in currentConfig.rules)
                {
                    logtext = logtext + rule.xpath + "\r";
                }
                ServiceEvtx.WriteEntry("Service downloaded logging policy: \r" + logtext);

                /*Setting the timer*/
                Timer timer = new Timer() { Interval = Convert.ToDouble(currentConfig.refresh_freq) };
                timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
                timer.Start();
                ServiceEvtx.WriteEntry("Service sets it's timer to: \r" + timer.Interval);
                
                /*Saving the config for global use*/
                this.currentConfig = currentConfig;
                }
                /*Catching general WTF's to Event Log*/
                catch (Exception e)
                {
                    ServiceEvtx.WriteEntry(e.Message + e.StackTrace);
                }
        }

        protected override void OnStop()
        {
            //Stop the Service and Log the poweroff
            ServiceEvtx.WriteEntry("Service is about to stop.");
        }
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            
            foreach(EvtxToES.EventManager.Rule rules in currentConfig.rules)
            {
                var ruleString = rules.xpath;
                var rulesource = rules.source;
                string outputJson = EvtxToES.EventManager.GetEvents(rulesource, ruleString);
                EventManager.SendToLogstash(outputJson, currentConfig.address, currentConfig.ssl_validation);
            }
        }
    }
}
