# xtrabackup2s3.csx

Script to automate the creation of mysql / percona / mariadb backups with xtrabackup and upload them to an Amazon S3 compatible service

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).
- Percona xtrabackup (https://www.percona.com/doc/percona-xtrabackup/8.0/installation.html) including the xbcloud and xbstream binaries

Installation: 

Note: The script will backup the local installation of mysql, percona or mariadb, remote servers are not supported

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/xtrabackup2s3/xtrabackup2s3.csx -O /opt/xbs3/xtrabackup2s3.csx
chmod +x /opt/xbs3/xtrabackup2s3.csx
```

Usage:

Daily full backups
```bash
WHICHDOTNETSCRIPT=`which dotnet-script`
sed -i '1c#!'"$WHICHDOTNETSCRIPT" /opt/xbs3/xtrabackup2s3.csx
cat << EOF > /etc/cron.daily/xbs3
#!/bin/bash
/opt/xbs3/xtrabackup2s3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 0 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
EOF
chmod +x /etc/cron.daily/xbs3
```

Daily full backup plus 23 hourly incremental backups everyday
```bash
WHICHDOTNETSCRIPT=`which dotnet-script`
sed -i '1c#!'"$WHICHDOTNETSCRIPT" /opt/xbs3/xtrabackup2s3.csx
cat << EOF > /etc/cron.hourly/xbs3
#!/bin/bash
/opt/xbs3/xtrabackup2s3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
EOF
chmod +x /etc/cron.hourly/xbs3
```

Daily full backup plus 23 hourly incremental backups everyday with email notification of errors and successful completion
```bash 
WHICHDOTNETSCRIPT=`which dotnet-script`
sed -i '1c#!'"$WHICHDOTNETSCRIPT" /opt/xbs3/xtrabackup2s3.csx
cat << EOF > /etc/cron.hourly/xbs3
#!/bin/bash
/opt/xbs3/xtrabackup2s3.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 23 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket --smtpuser myuser --smtppassword mypassword --smtphost smtp.gmail.com --smtpport 587 --smtpfrom me@gmail.com --smtpto you@gmail.com --notifyfull --notifyincremental
EOF
chmod +x /etc/cron.hourly/xbs3
```
