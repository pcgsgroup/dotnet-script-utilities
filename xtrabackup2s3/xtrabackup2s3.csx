#!/usr/bin/env dotnet-script
#r "nuget: CommandLineParser, 2.7.82"
#r "nuget: Flurl.Http, 2.4.2"

using CommandLine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Mail;
using Flurl;
using Flurl.Http;

public class Options
{
    [Option('d', "backupdirectory", Required = true, HelpText = "Directory to store the backups")]
    public string BackupDirectory { get; set; }

    [Option('b', "partialbackups", Required = false, Default=0, HelpText = "Number of partial backups to create after the last full backup")]
    public int PartialBackupNumber { get; set; } = 0;

    [Option('u', "mysqluser", Required = false, HelpText = "MySQL User")]
    public string MySqlUser { get; set; }

    [Option('p', "mysqlpassword", Required = false, HelpText = "MySQL Password")]
    public string MySqlPassword { get; set; }

    [Option('i', "incremental", Required = false, Default=false, HelpText = "If true creates incremental partial backups, if false creates differential partial backups")]
    public bool Incremental { get; set; } = false;

    [Option("s3endpoint", Required = false, Default="s3.amazonaws.com", HelpText = "S3 endpoint")]
    public string S3Endpoint { get; set; } = "s3.amazonaws.com";

    [Option("s3region", Required = false, Default="us-east-1", HelpText = "S3 region")]
    public string S3Region { get; set; } = "us-east-1";

    [Option("s3accesskey", Required = true, HelpText = "S3 access key")]
    public string S3AccessKey { get; set; }

    [Option("s3secretkey", Required = true, HelpText = "S3 secret access key")]
    public string S3SecretKey { get; set; }

    [Option("s3bucket", Required = true, HelpText = "S3 bucket name")]
    public string S3Bucket { get; set; }

    [Option("s3folder", Required = false, HelpText = "S3 folder name")]
    public string S3Folder { get; set; }

    [Option("s3paralleluploads", Required = false, Default = 8, HelpText = "S3 number of parallel uploads")]
    public int S3ParallelUploads { get; set; } = 8;

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

    [Option('n', "notifyfull", Required = false, Default=false, HelpText = "Send an email notification when a full backup is created")]
    public bool NotifyFull { get; set; } = false;

    [Option("notifypartial", Required = false, Default=false, HelpText = "Send an email notification when a partial backup is created")]
    public bool NotifyPartial { get; set; } = false;

    [Option("notifylastpartial", Required = false, Default=false, HelpText = "Send an email notification when the last partial backup is created")]
    public bool NotifyLastPartial { get; set; } = false;

    [Option("pushoveruser", Required = false, HelpText = "Pushover user to send pushover notifications")]
    public string PushoverUser { get; set; }

    [Option("pushovertoken", Required = false, HelpText = "Pushover token to send pushover notifications")]
    public string PushoverToken { get; set; }
}

Parser.Default.ParseArguments<Options>(Args).WithParsed<Options>(o =>
{
    try{
        //Create backup directory if it doesn't exist
        if(!Directory.Exists(o.BackupDirectory)){
            Log($"Creating {o.BackupDirectory}");
            Directory.CreateDirectory(o.BackupDirectory);
        }

        //Check if a full backup exists
        var fullBackupPath = Path.Combine(o.BackupDirectory, "full");

        //Check if incremental backup path exists
        var incrementalBackupPath = Path.Combine(o.BackupDirectory, "partial");
        if(!Directory.Exists(incrementalBackupPath)){
            Log($"Creating {incrementalBackupPath} directory");
            Directory.CreateDirectory(incrementalBackupPath);
        }

        //Get incremental backups
        var incrementalBackupDirInfo = new DirectoryInfo(incrementalBackupPath);
        var incrementalBackups = incrementalBackupDirInfo.EnumerateDirectories().OrderBy(d => d.Name);
        var incrementalBackupCount = incrementalBackups.Count();

        //Check if the full backup needs to be cleaned
        if(Directory.Exists(fullBackupPath) && (o.PartialBackupNumber == 0 || incrementalBackupCount >= o.PartialBackupNumber)){
            Log($"Cleaning {o.BackupDirectory} directory");
            Directory.Delete(o.BackupDirectory, true);
            Directory.CreateDirectory(o.BackupDirectory);
            incrementalBackupCount = 0;
        }

        var mysqlUser = !String.IsNullOrEmpty(o.MySqlUser) ? $"--user={o.MySqlUser}" : "";
        var mysqlPassword = !String.IsNullOrEmpty(o.MySqlPassword) ? $"--password={o.MySqlPassword}" : "";

        if(!Directory.Exists(fullBackupPath)){
            //Create full backup
            Directory.CreateDirectory(fullBackupPath);
            var news3Name = $"{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}full";
            var s3folder = !String.IsNullOrEmpty(o.S3Folder) ? $"{o.S3Folder}/{news3Name}" : news3Name;
            Log($"Creating a full backup {fullBackupPath} and storing it at {s3folder}");
            Bash($"set -o pipefail && xtrabackup {mysqlUser} {mysqlPassword} --backup --stream=xbstream --extra-lsndir={fullBackupPath} --target-dir={fullBackupPath} | xbcloud put --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelUploads} {s3folder}", o);

            //Notify
            if(o.NotifyFull){
                Notify($"Backup {o.S3Bucket}/{s3folder} created", $"{o.S3Bucket}/{s3folder} created", o);
            }
        }
        else{
            if(o.PartialBackupNumber > 0){
                //Create next incremental backup
                var partialBackupPrefix = o.Incremental ? "inc" : "diff";
                var baseDir = (incrementalBackupCount > 0 && o.Incremental) ? incrementalBackups.ElementAt(incrementalBackupCount - 1).FullName : fullBackupPath;
                var nextBackupNumber = ++incrementalBackupCount;
                var nextIncrementalBackupPath = Path.Combine(incrementalBackupPath, String.Format("{1}{0:D4}", nextBackupNumber, partialBackupPrefix));
                Directory.CreateDirectory(nextIncrementalBackupPath);
                var news3Name = $"{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}{partialBackupPrefix}{nextBackupNumber}";
                var s3folder = !String.IsNullOrEmpty(o.S3Folder) ? $"{o.S3Folder}/{news3Name}" : news3Name;
                Log($"Creating partial backup number {nextBackupNumber} at {nextIncrementalBackupPath} from {baseDir} and storing it at {s3folder}");
                Bash($"set -o pipefail && xtrabackup {mysqlUser} {mysqlPassword} --backup --stream=xbstream --extra-lsndir={nextIncrementalBackupPath} --incremental-basedir={baseDir} --target-dir={nextIncrementalBackupPath} | xbcloud put --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelUploads} {s3folder}", o);
                
                //Notify
                if(o.NotifyPartial || (o.NotifyLastPartial && nextBackupNumber == o.PartialBackupNumber)){
                    Notify($"Backup {o.S3Bucket}/{s3folder} created", $"{o.S3Bucket}/{s3folder} created", o);
                }
            }
        }
        
        Log("Done");
    }
    catch(Exception exc){
        Log(exc.ToString());
        NotifyError($"Backup {o.S3Bucket}/{o.S3Folder} failed", $"Backup {o.S3Bucket}/{o.S3Folder} creation failed with exception: {exc.ToString()}", o);
        throw;
    }
});

public void Log(string message){
    Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} {message}");
}

public void Bash(string cmd, Options o)
{
    Console.WriteLine($"> {cmd}");
    var escapedArgs = cmd.Replace("\"", "\\\"");
    bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    var process = new Process()
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = isLinux ? "/bin/bash" : "cmd.exe",
            Arguments = isLinux ? $"-c \"{escapedArgs}\"" : $"/C \"{escapedArgs}\"",
            RedirectStandardOutput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = false
        }
    };

    process.Start();
    process.WaitForExit();

    // // Read standard output
    // string result = process.StandardOutput.ReadToEnd();
    // if(!String.IsNullOrEmpty(result)){
    //     Console.WriteLine(result);
    // }

    // //Read standard error
    // string error = process.StandardError.ReadToEnd();
    // if(!String.IsNullOrEmpty(error)){
    //     Console.WriteLine(error);
    // }

    // Check exit code
    if(process.ExitCode != 0){
        Console.WriteLine($"ERROR: {cmd} exited with code {process.ExitCode}");
        
        //Notify
        NotifyError($"Backup {o.S3Bucket}/{o.S3Folder} failed",$"Backup {o.S3Bucket}/{o.S3Folder} bash statement failed with exit code {process.ExitCode}", o);
        Environment.Exit(1);
    }
}

public void Notify(string subject, string message, Options o){
    NotifyEmail(subject, message, o);
    NotifyPushover(message, -1, o);
}

public void NotifyError(string subject, string message, Options o){
    NotifyEmail(subject, message, o);
    NotifyPushover(message, 2, o);
}

public void NotifyEmail(string subject, string message, Options o){    
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

public void NotifyPushover(string message, short priority, Options o){    
    if(!String.IsNullOrEmpty(o.PushoverToken)){
        Log($"Sending Pushover Notification");
        
        "https://api.pushover.net/1/messages.json".PostJsonAsync(new {
            token = o.PushoverToken,
            user = o.PushoverUser,
            message,
            priority,
            expire = 1200,
            retry = 120
        }).Wait();
    }
}
