# s32xtrabackup.csx

Script to download and restore full and optionally a differential xtrabackup backup from S3 compatible cloud storage

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).
- Percona xtrabackup (https://www.percona.com/doc/percona-xtrabackup/8.0/installation.html) including the xbcloud and xbstream binaries

Installation: 

Note: The script will restore the local installation of mysql, percona or mariadb. Remote servers are not supported

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/s32xtrabackup/s32xtrabackup.csx -O /opt/xbs3/s32xtrabackup.csx
chmod +x /opt/xbs3/s32xtrabackup.csx
```

Usage:

Restore full backup
```bash
/opt/xbs3/s32xtrabackup.csx --backupdirectory /var/opt/xbs3/backup --partialbackups 0 --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
```

Restore full backup plus the latest differential backup
```bash
/opt/xbs3/s32xtrabackup.csx --backupdirectory /var/opt/xbs3/backup --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
```

Restore full backup plus the fifth differential backup created after the last full backup
```bash
/opt/xbs3/s32xtrabackup.csx --backupdirectory /var/opt/xbs3/backup --partialbackups 5 --s3accesskey mykey --s3secretkey mysecret --s3bucket bucket
```