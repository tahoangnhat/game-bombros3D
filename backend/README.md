# Bombros authentication backend

## Gmail configuration

The registration endpoint creates the account first, then sends a success
notification to the registered email address. If sending fails, registration
still succeeds and the backend writes a warning to its log.

Before starting the backend in PowerShell, configure the Gmail app password:

```powershell
$env:MAIL_USERNAME = "nhatdeveloper04@gmail.com"
$env:MAIL_PASSWORD = "your-new-gmail-app-password"
$env:MAIL_FROM = "nhatdeveloper04@gmail.com"
.\mvnw.cmd spring-boot:run
```

If Maven Wrapper is unavailable, run:

```powershell
mvn spring-boot:run
```

Use a Google App Password, not the Gmail account password. Never commit the app
password to `application.yml` or another tracked file.
