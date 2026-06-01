package com.bombros.auth.config;

import jakarta.annotation.PostConstruct;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Configuration;
import org.springframework.mail.javamail.JavaMailSenderImpl;

@Configuration
public class MailConfiguration {
    private static final Logger log = LoggerFactory.getLogger(MailConfiguration.class);

    @Autowired
    private JavaMailSenderImpl mailSender;

    @Value("${spring.mail.password:}")
    private String rawPassword;

    @Value("${spring.mail.username:}")
    private String username;

    @PostConstruct
    public void sanitizeMailPassword() {
        if (rawPassword == null)
            rawPassword = "";
        String sanitized = rawPassword.replaceAll("\\s+", "");
        if (!sanitized.equals(rawPassword)) {
            log.info("Whitespace removed from configured mail password");
        }
        // Avoid logging the password itself. Only set it on the mail sender.
        mailSender.setPassword(sanitized);

        boolean passwordPresent = mailSender.getPassword() != null && !mailSender.getPassword().isEmpty();
        log.info("Mail sender configured for user '{}' (password present: {})", username, passwordPresent);
    }
}
