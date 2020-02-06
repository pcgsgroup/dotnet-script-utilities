#!/usr/bin/env dotnet-script
#r "nuget: CommandLineParser, 2.7.82"
#r "nuget: Dapper, 2.0.30"
#r "nuget: MySql.Data, 6.10.9"

using CommandLine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Mail;
using System.Data;
using MySql.Data;
using MySql.Data.MySqlClient;
using Dapper;

public class Options
{
    [Option('n', "hostname", Required = false, HelpText = "Host name to use on notifications")]
    public string HostName { get; set; }

    [Option('h', "mysqlhost", Required = false, Default="localhost", HelpText = "MySQL Host")]
    public string MySqlHost { get; set; } = "localhost";

    [Option('P', "mysqlport", Required = false, Default=3306, HelpText = "MySQL Port")]
    public int MySqlPort { get; set; } = 3306;

    [Option('u', "mysqluser", Required = false, HelpText = "MySQL User")]
    public string MySqlUser { get; set; }

    [Option('p', "mysqlpassword", Required = false, HelpText = "MySQL Password")]
    public string MySqlPassword { get; set; }

    [Option('l', "maxsecondsbehindmaster", Required = false, Default=600, HelpText = "Max Seconds Behind Master")]
    public int MaxSlaveLagSeconds { get; set; } = 600;

    [Option("smtpuser", Required = false, HelpText = "SMTP user")]
    public string SmtpUser { get; set; }

    [Option("smtppassword", Required = false, HelpText = "SMTP password")]
    public string SmtpPassword { get; set; }

    [Option("smtphost", Required = false, HelpText = "SMTP host")]
    public string SmtpHost { get; set; }

    [Option("smtpport", Required = false, HelpText = "SMTP port")]
    public int SmtpPort { get; set; }

    [Option("smtpfrom", Required = false, HelpText = "SMTP from")]
    public string SmtpFrom { get; set; }

    [Option("smtpto", Required = false, HelpText = "SMTP to")]
    public string SmtpTo { get; set; }

    [Option('s', "notifysuccess", Required = false, Default=false, HelpText = "Send an email notification when the slave is OK")]
    public bool NotifySuccess { get; set; } = false;
}

Parser.Default.ParseArguments<Options>(Args).WithParsed<Options>(o =>
{
    var host = !string.IsNullOrEmpty(o.HostName) ? o.HostName : "SLAVE";
    try{
        string connectionString = $"Server={o.MySqlHost};Uid={o.MySqlUser};Port={o.MySqlPort};Pwd={o.MySqlPassword};Allow User Variables=True;SslMode=none;";
        using(MySqlConnection connection = new MySqlConnection(connectionString)){
            Log($"Checking slave status for {o.MySqlUser}@{o.MySqlHost}:{o.MySqlPort}...");
            connection.Open();
            //Get slave status
            IEnumerable<dynamic> statusRows = connection.Query("show slave status");
            //Check how many rows are there, can be more than one if there are multiple replication channels
            int channels = statusRows.Count();
            if(channels > 0){
                Log($"Found {channels} replication channel(s)...");
                foreach(dynamic statusRow in statusRows){
                    //Check if replication is running
                    if(statusRow.Slave_IO_Running != "Yes" || statusRow.Slave_SQL_Running != "Yes"){
                        //Replication error
                        var errors = new StringBuilder();
                        if(!string.IsNullOrEmpty(statusRow.Last_IO_Error)){
                            errors.AppendLine($"IO Error: {statusRow.Last_IO_Errno} {statusRow.Last_IO_Error}");
                        }
                        if(!string.IsNullOrEmpty(statusRow.Last_SQL_Error)){
                            errors.AppendLine($"SQL Error: {statusRow.Last_SQL_Errno} {statusRow.Last_SQL_Error}");
                        }
                        Log($"ERROR Replication stopped\r\n{errors.ToString()}");
                        SendEmail($"{host} REPLICATION ERROR", $"Errors: \r\n\r\n{errors.ToString()}", o);
                        Environment.Exit(1);
                    }
                    //Check replication lag
                    else if(Convert.ToInt32(statusRow.Seconds_Behind_Master) > o.MaxSlaveLagSeconds){
                        //Too much lag
                        Log($"ERROR Too much lag {statusRow.Seconds_Behind_Master}s behind master");
                        SendEmail($"{host} {statusRow.Seconds_Behind_Master}s LAG", $"Seconds Behind Master: {statusRow.Seconds_Behind_Master}", o);
                        Environment.Exit(2);
                    }
                    else{
                        //Everything OK
                        Log("Channel OK!");
                    }
                }
            }
            else{
                //No replication channels found, something is wrong
                Log($"ERROR No replication channels found");
                SendEmail($"{host} NO REPLICATION CHANNELS FOUND", $"No replication channels found for {host}", o);
                Environment.Exit(2);
            }
        }
        Log("Done.");
        if(o.NotifySuccess){
            SendEmail($"{host} replication ok", $"Great news everyone! All replication channels for {host} are OK.", o);
        }
    }
    catch(Exception exc)
    {
        Log(exc.ToString());
        SendEmail($"MONITOR {host} ERROR", $"Monitor {host} failed with exception:\r\n\r\n{exc.ToString()}", o);
        throw;
    }
});

public void Log(string message){
    Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {message}");
}

public void SendEmail(string subject, string message, Options o){    
    if(!String.IsNullOrEmpty(o.SmtpHost)){
        Log($"Sending notification to {o.SmtpTo}");
        // Credentials
        var credentials = new NetworkCredential(o.SmtpUser, o.SmtpPassword);
        // Mail message
        var mail = new MailMessage()
        {
            From = new MailAddress(o.SmtpFrom),
            Subject = subject,
            Body = message
        };
        mail.IsBodyHtml = false;
        mail.To.Add(new MailAddress(o.SmtpTo));
        // Smtp client
        var client = new SmtpClient()
        {
            Port = o.SmtpPort,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Host = o.SmtpHost,
            EnableSsl = true,
            Credentials = credentials
        };
        client.Send(mail);
    }
}
