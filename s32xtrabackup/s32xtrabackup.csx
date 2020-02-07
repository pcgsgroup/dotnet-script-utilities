#!/usr/bin/env dotnet-script
#r "nuget: CommandLineParser, 2.7.82"
#r "nuget: Minio, 3.1.9"
#r "nuget: Flurl.Http, 2.4.2"

using CommandLine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Mail;
using System.Reactive.Linq;
using Minio;
using Minio.DataModel;
using Flurl;
using Flurl.Http;
using System.Text.RegularExpressions;

public class Options
{
    [Option('d', "backupdirectory", Required = true, HelpText = "Directory to store the backups")]
    public string BackupDirectory { get; set; }

    [Option('b', "partialbackups", Required = false, Default=99, HelpText = "Number of partial backups to restore after the full backup")]
    public int PartialBackupNumber { get; set; } = 99;

    [Option('u', "mysqluser", Required = false, HelpText = "MySQL User")]
    public string MySqlUser { get; set; }

    [Option('p', "mysqlpassword", Required = false, HelpText = "MySQL Password")]
    public string MySqlPassword { get; set; }

    [Option('D', "mysqldatadir", Required = false, Default="/var/lib/mysql", HelpText = "MySQL Data Directory")]
    public string MySqlDataDir { get; set; } = "/var/lib/mysql";

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

    [Option("s3folder", Required = false, HelpText = "S3 folder of the full backup to restore")]
    public string S3Folder { get; set; }

    [Option("s3paralleldownloads", Required = false, Default = 8, HelpText = "S3 number of parallel downloads")]
    public int S3ParallelDownloads { get; set; } = 8;

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

    [Option('n', "notifysuccess", Required = false, Default=false, HelpText = "Send an email notification when the backup restore is completed")]
    public bool NotifySuccess { get; set; } = false;

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

        //Get the existing backups
        var minio = new MinioClient(o.S3Endpoint, o.S3AccessKey, o.S3SecretKey).WithSSL();
        var observable = minio.ListObjectsAsync(o.S3Bucket, $"{o.S3Folder}/", false);
        //To sort the backups by folder name
        var backups = new SortedList<string, string>();
        observable.ForEachAsync(entry => {
            // Log(entry.Key);
            backups.Add(entry.Key, entry.Key);}
            ).Wait();

        //Get the last full and incremental backups
        string fullBackup = "";
        var differentialBackups = new List<string>();
        var toRestore = new List<string>();
        foreach(var kv in backups){
            var key =  kv.Key.TrimEnd('/');
            //Check if it's a full backup
            if(key.EndsWith("full")){
                //Clear the previous partial backups
                toRestore.Clear();
                differentialBackups.Clear();
                fullBackup = key;
            }
            //Check if it's a differential backup
            else if(Regex.IsMatch(key, ".+diff[0-9]+$")){
                //Store the most recent differential backup
                differentialBackups.Add(key);
            }
            else{
                //It's an incremental backup
                toRestore.Add(key);
            }
        }

        //Restore the full backup plus the specified number of partial backups
        var incrementalToRestore = toRestore.Take(o.PartialBackupNumber);
        var differentialBackup = differentialBackups.Take(o.PartialBackupNumber).LastOrDefault();

        if(!String.IsNullOrEmpty(fullBackup)){
            //backups found, confirm restoration
            Console.WriteLine("The following backups will be restored:");
            char userResponse;
            Console.WriteLine(fullBackup);
            if(String.IsNullOrEmpty(differentialBackup)){
                foreach(var backup in incrementalToRestore){
                    Console.WriteLine(backup);
                }
            }
            else{
                Console.WriteLine(differentialBackup);
            }
            do{
                Console.WriteLine($"WARNING: MySql data dir {o.MySqlDataDir} will be emptied and replaced");
                Console.WriteLine("Do you want to restore those backups? Y=yes, N=no");
                userResponse = Console.ReadKey().KeyChar;
                Console.WriteLine();
            } while(userResponse != 'Y' && userResponse != 'N');
            if(userResponse == 'Y'){
                //Prepare the folders
                //Create full backup folder
                var fullBackupPath = Path.Combine(o.BackupDirectory, "full");
                if(!Directory.Exists(fullBackupPath)){
                    Log($"Creating {fullBackupPath} directory...");
                    Directory.CreateDirectory(fullBackupPath);
                }

                //Create incremental backup folder
                var incrementalBackupPath = Path.Combine(o.BackupDirectory, "partial");
                if(!Directory.Exists(incrementalBackupPath)){
                    Log($"Creating {incrementalBackupPath} directory...");
                    Directory.CreateDirectory(incrementalBackupPath);
                }

                //Download and prepare the full backup
                Log($"Downloading full backup {fullBackup} and storing it at {fullBackupPath}...");
                Bash($"set -o pipefail && xbcloud get --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelDownloads} {fullBackup} | xbstream -xv -C {fullBackupPath}", o);

                Log($"Preparing {fullBackupPath}...");
                Bash($"xtrabackup --prepare --apply-log-only --target-dir={fullBackupPath}", o);

                //Download and prepare the differential backup
                if(!String.IsNullOrEmpty(differentialBackup)){
                    Log($"Downloading differential backup {differentialBackup} and storing it at {incrementalBackupPath}...");
                    Bash($"set -o pipefail && xbcloud get --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelDownloads} {differentialBackup} | xbstream -xv -C {incrementalBackupPath}", o);

                    Log($"Preparing {incrementalBackupPath}...");
                    Bash($"xtrabackup --prepare --apply-log-only --target-dir={fullBackupPath} --incremental-dir={incrementalBackupPath}", o);

                    Log($"Preparing {fullBackupPath}...");
                    Bash($"xtrabackup --prepare --target-dir={fullBackupPath}", o);
                }
                else{
                    //Download and prepare the incremental backups
                    Log("Not implemented! Use differential backups instead or restore them manually.");
                    Environment.Exit(5);
                }

                //Stop mysql
                Log($"Stopping MySql...");
                Bash($"service mysql stop", o);

                //Emptying mysql data dir
                if(Directory.Exists(o.MySqlDataDir)){
                    Log($"Emptying Mysql data dir...");
                    Bash($"rm -R {o.MySqlDataDir}", o);
                }

                //Move back the full backup
                Log($"Moving back {fullBackupPath}...");
                Bash($"xtrabackup --move-back --target-dir={fullBackupPath}", o);

                Log($"Changing mysql data dir owner...");
                Bash($"chown -R mysql:mysql {o.MySqlDataDir}", o);
                
                Log($"Starting MySql...");
                Bash($"service mysql start", o);

                Log($"Done.");
            }
            else{
                //Exit
                Environment.Exit(0);
            }

        }
        else{
            Log("No backups found in the specified S3 folder. The folder should contain subfolders ending with full, inc1, inc2, etc");
        }
        
        Log("Done");
    }
    catch(Exception exc){
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

    // Check exit code
    if(process.ExitCode != 0){
        Console.WriteLine($"ERROR: {cmd} exited with code {process.ExitCode}");
        
        //Notify
        NotifyError($"Backup {o.S3Bucket}/{o.S3Folder} failed",$"Backup {o.S3Bucket}/{o.S3Folder} creation failed", o);
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