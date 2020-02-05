# Dotnet Script Utilities

Miscellaneous scripts to automate server tasks

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).

# xtrabackup2s3.csx 

Script to automate the creation of full and incremental xtrabackup backups and stream them to s3 compatible cloud storage

# s32xtrabackup.csx 

Script to download and restore full and incremental xtrabackup backups from S3 compatible cloud storage

# slavemonitor.csx

Script to automate the monitoring of mysql / percona / mariadb replication slaves with email notifications 