# EvtxToESSvc - Windows evtx to logstash exporter service
## Quick Info:
This little program is intended to provide easy to use and versatile way to export Windows Evtx Logs to ElasticSearch via Logstash. Created to provide logging configuration for SIEM usage, without using tools to big for this goal like beats or elastic agent.

## Features:
1. Granular configuration of log filtering using XPATH syntax, the same you use in Powershell
2. Config file may be provided with local text file, or via getting JSON (For easy deployment in corporate environments)
3. Uses HTTP or HTTPS, also with  self-signed certificates 
4. Reports bad queries to ES
5. Refreshes configuration when the service is restarted

## Installation:
Just compile the project and copy the bin output folder somewhere. You can use the compiled version too. Then use sc.exe to install the service in the system.
Compiled version (x64) --> [release.zip](https://github.com/lucasgeras/EvtxToESSvc/files/10826427/release.zip)

1. type : (file) for reading a config file, (url) for getting json from url 
2. url/path : (url) if you wish to get config via json, (file) to read local file
3. selfsigned : optional argument to skip ssl validation when getting the json
## Installation Examples:
	sc create EvtxToESSvc_file binPath= "C:\Users\lucas\source\repos\EvtxToES\EvtxToESSvc\bin\Debug\EvtxToESSvc.exe file C:\Users\lucas\Desktop\rules.json"
	sc create EvtxToESSvc_ssl binPath= "C:\Users\lucas\source\repos\EvtxToES\EvtxToESSvc\bin\Debug\EvtxToESSvc.exe url https://api.npoint.io/30a32e7f740af0a18b43 selfsigned"

## Configuration file Example (simple JSON, both for file and url option):
Address is your logstash parser listening address, refresh is a number of miliseconds between each search for evtx on local drive.
The xpath filters are the same like in Powershell/cmd, misconfiguration info will be also sent to logstash.
```
{
  "rules": [
    {
      "xpath": "*[System[(EventID=4624) and TimeCreated[timediff(@SystemTime) <= 60000]]]",
      "source": "Security"
    },
    {
      "xpath": "*[System[(EventID=4672) and TimeCreated[timediff(@SystemTime) <= 60000]]]",
      "source": "Security"
    },
    {
      "xpath": "*[System[(EventID=5379) and TimeCreated[timediff(@SystemTime) <= 60000]]]",
      "source": "Security"
    }
  ],
  "address": "https://192.168.56.101:5000",
  "refresh_freq": 60000,
  "ssl_validation": false
}
```


## Example Logstash parser:
Recommended to use parser like this, used some ruby to parse the event data properly. it works for both Windows and Sysmon Logs, also all timestamps and datafields are parsed. Set your server and ssl certs ofc.
```
input{
  http{
    port => 5000
    codec => json {ecs_compatibility => "disabled"}
    ssl => true
    ssl_certificate => "/etc/logstash/ssl/logstash.crt"
    ssl_key => "/etc/logstash/ssl/logstash.key"
    ssl_verify_mode => "none"
  }
}
filter {
#For Windows Event Logs
  if ![Exception]
  {
    mutate{
      remove_field => ["[event][original]","http","url","path","port","domain","@version","user_agent","original","[Event][@xmlns]","[http][method]","[http][version]","[http][request]"]
	  }
    mutate{
      gsub => ["[Event][System][TimeCreated][@SystemTime]","\.\d+Z",""]
    }
    date{
      match => ["[Event][System][TimeCreated][@SystemTime]","yyyy-MM-dd'T'HH:mm:ss"]
      timezone => "UTC"
    }
    ruby{
      code =>'
        c = event.get("[Event][EventData][Data]")
        for value in c
          i = value["@Name"]
          j = value["#text"]
          event.set("[Event][EventData][DataParsed][#{i}]",j)
        end'
    }
    mutate{
      remove_field => ["[Event][EventData][Data]"]
    }
  }
#For Exceptions
  else{
    date{
      match => ["[ExceptionData][Timestamp]","yyyy-MM-dd'T'HH:mm:ss"]
      timezone => "UTC"
    }
    mutate{
       remove_field => ["http","request","?xml","@version","event","url","user_agent"]

    }
  }
}
output{
  stdout{
    codec => rubydebug{metadata=>false}
  }
}
```
## Logstash output example:
```
{
    "@timestamp" => 2023-02-24T00:40:44.000Z,
         "Event" => {
        "EventData" => {
            "DataParsed" => {
                          "UtcTime" => "2023-02-24 00:40:44.622",
                "ParentCommandLine" => "\"C:\\Windows\\system32\\cmd.exe\" ",
                             "User" => "REGNUM\\lucas",
                "ParentProcessGuid" => "{c4d2b41e-078c-63f8-1d44-000000002c00}",
                      "FileVersion" => "10.0.22000.1516 (WinBuild.160101.0800)",
                      "ParentImage" => "C:\\Windows\\System32\\cmd.exe",
                        "LogonGuid" => "{c4d2b41e-914f-63ec-126b-0c0000000000}",
                        "ProcessId" => "28752",
                            "Image" => "C:\\Windows\\System32\\conhost.exe",
                          "Company" => "Microsoft Corporation",
                      "CommandLine" => "\\??\\C:\\Windows\\system32\\conhost.exe 0xffffffff -ForceV1",
                         "RuleName" => "-",
                          "Product" => "Microsoft® Windows® Operating System",
                "TerminalSessionId" => "1",
                      "Description" => "Console Window Host",
                 "CurrentDirectory" => "C:\\Windows",
                  "ParentProcessId" => "7760",
                       "ParentUser" => "REGNUM\\lucas",
                 "OriginalFileName" => "CONHOST.EXE",
                          "LogonId" => "0xc6b12",
                   "IntegrityLevel" => "Medium",
                           "Hashes" => "SHA256=714ACCD1698AF5347F5493B86732CB58CB815E6D8F8A638F5A0891E559E727D6",
                      "ProcessGuid" => "{c4d2b41e-078c-63f8-1e44-000000002c00}"
            }
        },
           "System" => {
                  "Version" => "5",
                 "Computer" => "REGNUM",
                 "Provider" => {
                "@Name" => "Microsoft-Windows-Sysmon",
                "@Guid" => "{5770385f-c22a-43e0-bf4c-06f5698ffbd9}"
            },
            "EventRecordID" => "539295",
                   "Opcode" => "0",
                    "Level" => "4",
                  "EventID" => "1",
                 "Keywords" => "0x8000000000000000",
                  "Channel" => "Microsoft-Windows-Sysmon/Operational",
                     "Task" => "1",
              "Correlation" => nil,
              "TimeCreated" => {
                "@SystemTime" => "2023-02-24T00:40:44"
            },
                "Execution" => {
                 "@ThreadID" => "5296",
                "@ProcessID" => "4156"
            },
                 "Security" => {
                "@UserID" => "S-1-5-18"
            }
        }
    },
          "host" => {
        "ip" => "192.168.56.1"
    }
}
```
## Final thoughts
1. At best, send logs to logstash via Kafka, no logs will be dropped by overloaded logstash instance
2. Create various configs for various systems
