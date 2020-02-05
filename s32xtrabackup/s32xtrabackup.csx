#!/usr/bin/env dotnet-script
#r "nuget: CommandLineParser, 2.7.82"
#r "nuget: Minio, 3.1.9"

using CommandLine;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Mail;
using System.Reactive.Linq;
using Minio;
using Minio.DataModel;

public class Options
{
    [Option('d', "backupdirectory", Required = true, HelpText = "Directory to store the backups")]
    public string BackupDirectory { get; set; }

    [Option('i', "incrementalbackups", Required = false, Default=99, HelpText = "Number of incremental backups to restore after the full backup")]
    public int IncrementalBackupNumber { get; set; } = 99;

    [Option('u', "mysqluser", Required = false, HelpText = "MySQL User")]
    public string MySqlUser { get; set; }

    [Option('p', "mysqlpassword", Required = false, HelpText = "MySQL Password")]
    public string MySqlPassword { get; set; }

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
        var subscription = observable.Subscribe(entry => {
            Log(entry.Key);
            // backups.Add(entry.Key, entry.Key);
            
            //Get the folder
            // var folder = !String.IsNullOrEmpty(o.S3Folder) ? entry.Key.Replace(o.S3Folder, "").TrimStart('/') : entry.Key;
            // folder = folder.Split('/')[0];
            // //Don't duplicate folders
            // //if(!backups.ContainsKey(folder)){
            //     backups.Add(folder, folder);
            //}
        });
        observable.Wait();

        //Get the last full and incremental backups
        string fullBackup = "";
        var toRestore = new List<string>();
        foreach(var kv in backups){
            var key =  kv.Key.TrimEnd('/');
            if(key.EndsWith("full")){
                toRestore.Clear();
                fullBackup = key;
            }
            else{
                toRestore.Add(key);
            }
        }

        //Restore the full backup plus the specified number of incremental backups
        var incrementalToRestore = toRestore.Take(o.IncrementalBackupNumber);

        if(toRestore.Count > 0 && toRestore[0].EndsWith("full")){
            //backups found, confirm restoration
            Console.WriteLine("The following backups will be restored:");
            char userResponse;
            Console.WriteLine(fullBackup);
            foreach(var backup in incrementalToRestore){
                Console.WriteLine(backup);
            }
            do{
                Console.WriteLine("Do you want to restore those backups? Y=yes, N=no");
                userResponse = Console.ReadKey().KeyChar;
                Console.WriteLine();
            } while(userResponse != 'Y' && userResponse != 'N');
            if(userResponse == 'Y'){
                //Restore the backups
                foreach(var backup in incrementalToRestore){

                }
            }
            else{
                //Exit
                Environment.Exit(0);
            }

        }
        else{
            Log("No backups found in the specified S3 folder. The folder should contain subfolders ending with full, inc1, inc2, etc");
        }

        // //Check if a full backup exists
        // var fullBackupPath = Path.Combine(o.BackupDirectory, "full");

        // //Check if incremental backup path exists
        // var incrementalBackupPath = Path.Combine(o.BackupDirectory, "inc");
        // if(!Directory.Exists(incrementalBackupPath)){
        //     Log($"Creating {incrementalBackupPath} directory");
        //     Directory.CreateDirectory(incrementalBackupPath);
        // }

        // //Get incremental backups
        // var incrementalBackupDirInfo = new DirectoryInfo(incrementalBackupPath);
        // var incrementalBackups = incrementalBackupDirInfo.EnumerateDirectories().OrderBy(d => d.Name);
        // var incrementalBackupCount = incrementalBackups.Count();

        // //Check if the full backup needs to be cleaned
        // if(Directory.Exists(fullBackupPath) && (o.IncrementalBackupNumber == 0 || incrementalBackupCount >= o.IncrementalBackupNumber)){
        //     Log($"Cleaning {o.BackupDirectory} directory");
        //     Directory.Delete(o.BackupDirectory, true);
        //     Directory.CreateDirectory(o.BackupDirectory);
        //     incrementalBackupCount = 0;
        // }

        // var mysqlUser = !String.IsNullOrEmpty(o.MySqlUser) ? $"--user={o.MySqlUser}" : "";
        // var mysqlPassword = !String.IsNullOrEmpty(o.MySqlPassword) ? $"--password={o.MySqlPassword}" : "";

        // if(!Directory.Exists(fullBackupPath)){
        //     //Create full backup
        //     Directory.CreateDirectory(fullBackupPath);
        //     var news3Name = $"{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}full";
        //     var s3folder = !String.IsNullOrEmpty(o.S3Folder) ? $"{o.S3Folder}/{news3Name}" : news3Name;
        //     Log($"Creating a full backup {fullBackupPath} and storing it at {s3folder}");
        //     Bash($"set -o pipefail && xtrabackup {mysqlUser} {mysqlPassword} --backup --stream=xbstream --extra-lsndir={fullBackupPath} --target-dir={fullBackupPath} | xbcloud put --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelUploads} {s3folder}", o);

        //     //Notify
        //     if(o.NotifyFull){
        //         SendEmail($"Backup {o.S3Bucket}/{s3folder} created", $"Great news everyone! The backup {o.S3Bucket}/{s3folder} was successfully created", o);
        //     }
        // }
        // else{
        //     if(o.IncrementalBackupNumber > 0){
        //         //Create next incremental backup
        //         var baseDir = incrementalBackupCount > 0 ? incrementalBackups.ElementAt(incrementalBackupCount - 1).FullName : fullBackupPath;
        //         var nextBackupNumber = ++incrementalBackupCount;
        //         var nextIncrementalBackupPath = Path.Combine(incrementalBackupPath, $"inc{nextBackupNumber}");
        //         Directory.CreateDirectory(nextIncrementalBackupPath);
        //         var news3Name = $"{DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss")}inc{nextBackupNumber}";
        //         var s3folder = !String.IsNullOrEmpty(o.S3Folder) ? $"{o.S3Folder}/{news3Name}" : news3Name;
        //         Log($"Creating incremental backup number {nextBackupNumber} at {nextIncrementalBackupPath} from {baseDir} and storing it at {s3folder}");
        //         Bash($"set -o pipefail && xtrabackup {mysqlUser} {mysqlPassword} --backup --stream=xbstream --extra-lsndir={nextIncrementalBackupPath} --incremental-basedir={baseDir} --target-dir={nextIncrementalBackupPath} | xbcloud put --storage=s3 --s3-endpoint='{o.S3Endpoint}' --s3-access-key='{o.S3AccessKey}' --s3-secret-key='{o.S3SecretKey}' --s3-bucket='{o.S3Bucket}' --s3-region='{o.S3Region}' --parallel={o.S3ParallelUploads} {s3folder}", o);
                
        //         //Notify
        //         if(o.NotifyIncremental || (o.NotifyLastIncremental && nextBackupNumber == o.IncrementalBackupNumber)){
        //             SendEmail($"Backup {o.S3Bucket}/{s3folder} created", $"Great news everyone! The backup {o.S3Bucket}/{s3folder} was successfully created", o);
        //         }
        //     }
        // }
        
        Log("Done");
    }
    catch(Exception exc){
        SendEmail($"Backup {o.S3Bucket}/{o.S3Folder} failed", $"Backup {o.S3Bucket}/{o.S3Folder} creation failed with exception: {exc.ToString()}", o);
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
        SendEmail($"Backup {o.S3Bucket}/{o.S3Folder} failed",$"Backup {o.S3Bucket}/{o.S3Folder} creation failed", o);
        Environment.Exit(1);
    }
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
