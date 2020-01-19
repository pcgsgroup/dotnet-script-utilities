# xtrabackups3.csx

Script to automate the creation and upload to s3 of incremental backups with xtrabackup.

Requirements:

.Net Core SDK (https://dotnet.microsoft.com/download) and dotnet script (https://github.com/filipw/dotnet-script) must be installed.

Installation: 

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/xtrabackups3.csx
chmod +x xtrabackups3.csx
```

Usage:

Create a cron job with one of the following commands

```bash
# full backups only
./xtrabackups3.csx --backupdirectory /var/opt/mysql/backup --incrementalbackups 0 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket 
# daily full backup plus 23 incremental hourly backups
./xtrabackups3.csx --backupdirectory /var/opt/mysql/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket 
# with email notifications
./xtrabackups3.csx --backupdirectory /var/opt/mysql/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket --smtpuser myuser --smtppassword mypassword --smtphost smtp.gmail.com --smtpport 587 --smtpfrom me@gmail.com --smtpto you@gmail.com --notifysuccess
```
