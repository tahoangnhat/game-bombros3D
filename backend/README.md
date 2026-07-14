# Bombros authentication backend

## Email configuration

The registration endpoint creates the account first, then sends a success
notification to the registered email address. If sending fails, registration
still succeeds and the backend writes a warning to its log.

### Render free deployment

For Render free, use the Brevo transactional email API instead of Gmail SMTP:

```text
MAIL_ENABLED=true
MAIL_PROVIDER=brevo
MAIL_FROM=your-verified-sender@example.com
MAIL_FROM_NAME=Bombros
BREVO_API_KEY=your-brevo-api-key
```

`MAIL_FROM` must be a sender that is verified in Brevo.


If Maven Wrapper is unavailable, run:

```powershell
mvn spring-boot:run
```

Use a Google App Password, not the Gmail account password. Never commit the app
password to `application.yml` or another tracked file.
