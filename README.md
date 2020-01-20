# xtrabackups3.csx

Script to automate the creation and upload to s3 of incremental backups with xtrabackup.

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).
- Percona xtrabackup (https://www.percona.com/doc/percona-xtrabackup/8.0/installation.html) including the xbcloud and xbstream binaries

Installation: 

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/xtrabackups3.csx -O /opt/xbs3/xtrabackups3.csx
chmod +x /opt/xbs3/xtrabackups3.csx
```

Usage:

Create a cron job with one of the following commands

```bash
# full backups only
/opt/xbs3/xtrabackups3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 0 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket 
# daily full backup plus 23 incremental hourly backups
/opt/xbs3/xtrabackups3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket 
# send an email notification when backups are created, or an error occurs
/opt/xbs3/xtrabackups3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket --smtpuser myuser --smtppassword mypassword --smtphost smtp.gmail.com --smtpport 587 --smtpfrom me@gmail.com --smtpto you@gmail.com --notifyfull --notifyincremental
```
