package com.bombros.auth.service;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.mail.SimpleMailMessage;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.stereotype.Service;

@Service
public class MailService {
    private static final Logger log = LoggerFactory.getLogger(MailService.class);

    private final JavaMailSender mailSender;

    @Value("${bombros.mail.from:no-reply@bombros.local}")
    private String from;

    public MailService(JavaMailSender mailSender) {
        this.mailSender = mailSender;
    }

    public void sendRegistrationSuccessEmail(String to, String username) {
        send(to, "Bombros registration successful", "Welcome " + username + ", your account was created successfully.");
    }

    public boolean sendPasswordResetOtpEmail(String to, String username, String otp) {
        // Do not log full OTP in production logs. Only log that an OTP was created and will be sent.
        log.info("Preparing password reset email to {} (user={})", to, username);
        return send(to, "Bombros password reset OTP", "Hello " + username + ", your OTP is: " + otp + " (valid for 5 minutes)");
    }

    private boolean send(String to, String subject, String text) {
        try {
            log.info("Sending email to '{}' with subject '{}' (from={}) using {}", to, subject, from, mailSender.getClass().getSimpleName());
            SimpleMailMessage message = new SimpleMailMessage();
            message.setFrom(from);
            message.setTo(to);
            message.setSubject(subject);
            message.setText(text);
            mailSender.send(message);
            log.info("Email successfully sent to '{}' with subject '{}'", to, subject);
            return true;
        } catch (Exception ex) {
            log.error("Email send failed to {} with subject '{}': {}", to, subject, ex.getMessage(), ex);
            return false;
        }
    }
}