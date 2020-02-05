# s32xtrabackup.csx

Script to download and restore full and incremental xtrabackup backups from S3 compatible cloud storage

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).
- Percona xtrabackup (https://www.percona.com/doc/percona-xtrabackup/8.0/installation.html) including the xbcloud and xbstream binaries

Installation: 

Note: The script will restore the local installation of mysql, percona or mariadb, remote servers are not supported

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/s32xtrabackup/s32xtrabackup.csx -O /opt/xbs3/s32xtrabackup.csx
chmod +x /opt/xbs3/s32xtrabackup.csx
```

Usage:

Restore full backup
```bash
/opt/xbs3/s32xtrabackup.csx --backupdirectory /var/opt/xbs3/backup --incrementalbackups 0 --mysqluser myuser --mysqlpassword mypwd --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
```

Restore full backup plus incremental backups
Pending...