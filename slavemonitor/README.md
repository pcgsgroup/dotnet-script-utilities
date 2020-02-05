# slavemonitor.csx

Script to automate the monitoring of mysql / percona / mariadb replication slaves with email notifications

Requirements:

- .Net Core SDK (https://dotnet.microsoft.com/download)
- dotnet script (https://github.com/filipw/dotnet-script).

Installation: 

```bash
wget https://raw.githubusercontent.com/rubenmch/dotnet-script-utilities/master/slavemonitor/slavemonitor.csx -O /opt/dsutils/slavemonitor.csx
chmod +x /opt/dsutils/slavemonitor.csx
```

Usage:

Monitor every hour
```bash
WHICHDOTNETSCRIPT=`which dotnet-script`
sed -i '1c#!'"$WHICHDOTNETSCRIPT" /opt/dsutils/slavemonitor.csx
cat << EOF > /etc/cron.hourly/slavemonitor
#!/bin/bash
/opt/dsutils/slavemonitor.csx -n myslavename -h localhost -P 3306 --mysqluser myuser --mysqlpassword mypwd --smtpuser myuser --smtppassword mypassword --smtphost smtp.gmail.com --smtpport 587 --smtpfrom me@gmail.com --smtpto you@gmail.com
EOF
chmod +x /etc/cron.hourly/slavemonitor
```