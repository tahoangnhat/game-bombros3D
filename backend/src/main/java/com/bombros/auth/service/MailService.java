package com.bombros.auth.service;

import jakarta.mail.internet.MimeMessage;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.mail.javamail.MimeMessageHelper;
import org.springframework.stereotype.Service;

@Service
public class MailService {
    private static final Logger log = LoggerFactory.getLogger(MailService.class);

    private final JavaMailSender mailSender;
    private final HttpClient httpClient;

    @Value("${bombros.mail.from:no-reply@bombros.local}")
    private String from;

    @Value("${bombros.mail.from-name:Bombros}")
    private String fromName;

    @Value("${bombros.mail.enabled:false}")
    private boolean enabled;

    @Value("${bombros.mail.provider:smtp}")
    private String provider;

    @Value("${bombros.mail.brevo-api-key:}")
    private String brevoApiKey;

    @Value("${bombros.mail.brevo-api-url:https://api.brevo.com/v3/smtp/email}")
    private String brevoApiUrl;

    public MailService(JavaMailSender mailSender) {
        this.mailSender = mailSender;
        this.httpClient = HttpClient.newHttpClient();
    }

    public boolean sendRegistrationSuccessEmail(String to, String username) {
        String safeUsername = displayName(username);
        return send(
            to,
            "Bombros registration successful",
            "Welcome " + safeUsername + ", your account was created successfully.",
            renderMessage(
                "Welcome to Bombros",
                "Hi " + safeUsername + ",",
                "Your account was created successfully. You can now log in and play online."
            )
        );
    }

    public boolean sendPasswordResetOtpEmail(String to, String username, String otp) {
        // Do not log full OTP in production logs. Only log that an OTP was created and will be sent.
        log.info("Preparing password reset email to {} (user={})", to, username);
        String safeUsername = displayName(username);
        return send(
            to,
            "Bombros password reset OTP",
            "Hello " + safeUsername + ", your OTP is: " + otp + " (valid for 5 minutes)",
            renderOtpMessage(safeUsername, otp)
        );
    }

    private boolean send(String to, String subject, String text, String html) {
        if (!enabled) {
            log.info("Email sending is disabled. Skipping email to '{}' with subject '{}'.", to, subject);
            return false;
        }

        if (isBlank(to)) {
            log.warn("Email send skipped because recipient is blank (subject='{}')", subject);
            return false;
        }

        if ("brevo".equalsIgnoreCase(provider)) {
            return sendWithBrevo(to, subject, text, html);
        }

        return sendWithSmtp(to, subject, text, html);
    }

    private boolean sendWithSmtp(String to, String subject, String text, String html) {
        try {
            log.info("Sending email to '{}' with subject '{}' (from={}) using {}", to, subject, from, mailSender.getClass().getSimpleName());
            MimeMessage message = mailSender.createMimeMessage();
            MimeMessageHelper helper = new MimeMessageHelper(message, false, "UTF-8");
            helper.setFrom(from, fromName);
            helper.setTo(to);
            helper.setSubject(subject);
            helper.setText(text, html);
            mailSender.send(message);
            log.info("Email successfully sent to '{}' with subject '{}'", to, subject);
            return true;
        } catch (Exception ex) {
            log.error("Email send failed to {} with subject '{}': {}", to, subject, ex.getMessage(), ex);
            return false;
        }
    }

    private boolean sendWithBrevo(String to, String subject, String text, String html) {
        if (isBlank(brevoApiKey)) {
            log.error("Email send failed to {} with subject '{}': BREVO_API_KEY is not configured", to, subject);
            return false;
        }

        if (isBlank(from)) {
            log.error("Email send failed to {} with subject '{}': MAIL_FROM is not configured", to, subject);
            return false;
        }

        try {
            String payload = "{"
                + "\"sender\":{\"name\":\"" + jsonEscape(fromName) + "\",\"email\":\"" + jsonEscape(from) + "\"},"
                + "\"to\":[{\"email\":\"" + jsonEscape(to) + "\"}],"
                + "\"subject\":\"" + jsonEscape(subject) + "\","
                + "\"textContent\":\"" + jsonEscape(text) + "\","
                + "\"htmlContent\":\"" + jsonEscape(html) + "\""
                + "}";

            HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(brevoApiUrl))
                .header("accept", "application/json")
                .header("api-key", brevoApiKey)
                .header("content-type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(payload))
                .build();

            log.info("Sending email to '{}' with subject '{}' (from={}) using Brevo API", to, subject, from);
            HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() >= 200 && response.statusCode() < 300) {
                log.info("Email successfully sent to '{}' with subject '{}' via Brevo", to, subject);
                return true;
            }

            log.error(
                "Brevo email send failed to {} with subject '{}': HTTP {} {}",
                to,
                subject,
                response.statusCode(),
                response.body()
            );
            return false;
        } catch (IOException ex) {
            log.error("Brevo email send failed to {} with subject '{}': {}", to, subject, ex.getMessage(), ex);
            return false;
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
            log.error("Brevo email send interrupted to {} with subject '{}': {}", to, subject, ex.getMessage(), ex);
            return false;
        } catch (Exception ex) {
            log.error("Brevo email send failed to {} with subject '{}': {}", to, subject, ex.getMessage(), ex);
            return false;
        }
    }

    private String renderOtpMessage(String username, String otp) {
        return "<!doctype html><html><body style=\"margin:0;background:#f4f7fb;font-family:Arial,sans-serif;color:#172033;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f7fb;padding:24px 0;\">"
            + "<tr><td align=\"center\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:520px;background:#ffffff;border:1px solid #dfe6f2;border-radius:8px;overflow:hidden;\">"
            + "<tr><td style=\"padding:24px 28px;background:#172033;color:#ffffff;font-size:22px;font-weight:bold;\">Bombros</td></tr>"
            + "<tr><td style=\"padding:28px;\">"
            + "<h1 style=\"margin:0 0 12px;font-size:22px;line-height:1.3;color:#172033;\">Password reset OTP</h1>"
            + "<p style=\"margin:0 0 16px;font-size:15px;line-height:1.6;\">Hello " + htmlEscape(username) + ", use this code to reset your password:</p>"
            + "<div style=\"margin:18px 0;padding:16px 20px;background:#eef5ff;border:1px solid #bad6ff;border-radius:6px;text-align:center;font-size:30px;letter-spacing:6px;font-weight:bold;color:#0b5ed7;\">"
            + htmlEscape(otp)
            + "</div>"
            + "<p style=\"margin:0;font-size:14px;line-height:1.6;color:#596579;\">This code is valid for 5 minutes. If you did not request it, you can ignore this email.</p>"
            + "</td></tr></table>"
            + "</td></tr></table>"
            + "</body></html>";
    }

    private String renderMessage(String title, String greeting, String body) {
        return "<!doctype html><html><body style=\"margin:0;background:#f4f7fb;font-family:Arial,sans-serif;color:#172033;\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f7fb;padding:24px 0;\">"
            + "<tr><td align=\"center\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:520px;background:#ffffff;border:1px solid #dfe6f2;border-radius:8px;overflow:hidden;\">"
            + "<tr><td style=\"padding:24px 28px;background:#172033;color:#ffffff;font-size:22px;font-weight:bold;\">Bombros</td></tr>"
            + "<tr><td style=\"padding:28px;\">"
            + "<h1 style=\"margin:0 0 12px;font-size:22px;line-height:1.3;color:#172033;\">" + htmlEscape(title) + "</h1>"
            + "<p style=\"margin:0 0 12px;font-size:15px;line-height:1.6;\">" + htmlEscape(greeting) + "</p>"
            + "<p style=\"margin:0;font-size:15px;line-height:1.6;color:#596579;\">" + htmlEscape(body) + "</p>"
            + "</td></tr></table>"
            + "</td></tr></table>"
            + "</body></html>";
    }

    private String displayName(String value) {
        return isBlank(value) ? "player" : value.trim();
    }

    private boolean isBlank(String value) {
        return value == null || value.trim().isEmpty();
    }

    private String jsonEscape(String value) {
        if (value == null) {
            return "";
        }

        StringBuilder builder = new StringBuilder(value.length() + 16);
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '\\':
                    builder.append("\\\\");
                    break;
                case '"':
                    builder.append("\\\"");
                    break;
                case '\b':
                    builder.append("\\b");
                    break;
                case '\f':
                    builder.append("\\f");
                    break;
                case '\n':
                    builder.append("\\n");
                    break;
                case '\r':
                    builder.append("\\r");
                    break;
                case '\t':
                    builder.append("\\t");
                    break;
                default:
                    if (c < 0x20) {
                        builder.append(String.format("\\u%04x", (int) c));
                    } else {
                        builder.append(c);
                    }
                    break;
            }
        }
        return builder.toString();
    }

    private String htmlEscape(String value) {
        if (value == null) {
            return "";
        }

        return value
            .replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;")
            .replace("\"", "&quot;")
            .replace("'", "&#39;");
    }
}
