package com.bombros.auth.service;

import com.bombros.auth.dto.*;
import com.bombros.auth.entity.User;
import com.bombros.auth.repository.UserRepository;
import com.bombros.auth.security.JwtService;
import org.springframework.security.authentication.AuthenticationManager;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.util.Locale;
import java.util.Optional;
import java.util.Random;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

@Service
public class AuthService {
    private static final Logger log = LoggerFactory.getLogger(AuthService.class);
    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final AuthenticationManager authenticationManager;
    private final JwtService jwtService;
    private final MailService mailService;

    public AuthService(UserRepository userRepository,
            PasswordEncoder passwordEncoder,
            AuthenticationManager authenticationManager,
            JwtService jwtService,
            MailService mailService) {
        this.userRepository = userRepository;
        this.passwordEncoder = passwordEncoder;
        this.authenticationManager = authenticationManager;
        this.jwtService = jwtService;
        this.mailService = mailService;
    }

    public AuthResponse register(RegisterRequest request) {
        validatePasswords(request.getPassword(), request.getConfirmPassword());

        String username = request.getUsername().trim();
        String email = normalizeEmail(request.getEmail());

        if (userRepository.existsByUsernameIgnoreCase(username)) {
            throw new IllegalArgumentException("Username already exists");
        }
        if (userRepository.existsByEmailIgnoreCase(email)) {
            throw new IllegalArgumentException("Email already exists");
        }

        User user = new User();
        user.setUsername(username);
        user.setEmail(email);
        user.setPasswordHash(passwordEncoder.encode(request.getPassword()));
        user.setRole("PLAYER");
        user.setEmailVerified(true);
        userRepository.save(user);

        mailService.sendRegistrationSuccessEmail(user.getEmail(), user.getUsername());

        String token = jwtService.generateToken(user.getUsername());
        return new AuthResponse(true, "Registered successfully", token, user.getUsername(), user.getEmail(),
                user.getRole());
    }

    public AuthResponse login(LoginRequest request) {
        String identifier = request.getIdentifier().trim();
        User user = findByIdentifier(identifier)
                .orElseThrow(() -> new IllegalArgumentException("Account not found"));

        authenticationManager.authenticate(
                new UsernamePasswordAuthenticationToken(user.getUsername(), request.getPassword()));

        String token = jwtService.generateToken(user.getUsername());
        return new AuthResponse(true, "Login successful", token, user.getUsername(), user.getEmail(), user.getRole());
    }

    public ApiResponse requestPasswordResetOtp(ForgotPasswordRequest request) {
        String email = normalizeEmail(request.getEmail());
        log.info("Password reset requested for email='{}'", email);

        Optional<User> userOpt = userRepository.findByEmailIgnoreCase(email);
        if (userOpt.isEmpty()) {
            log.info("No account found for email='{}'. Returning generic success response.", email);
            return new ApiResponse(true, "If the email exists, OTP has been sent");
        }

        User user = userOpt.get();
        log.info("Found user id={} username='{}'. Enforcing cooldown...", user.getId(), user.getUsername());
        enforceCooldown(user);

        String otp = generateOtp();
        user.setPasswordResetOtpHash(passwordEncoder.encode(otp));
        user.setPasswordResetOtpExpiresAt(LocalDateTime.now().plusMinutes(5));
        user.setPasswordResetOtpRequestedAt(LocalDateTime.now());
        userRepository.save(user);
        log.info("Generated OTP for user id={} expiresAt={}", user.getId(), user.getPasswordResetOtpExpiresAt());

        boolean emailSent = mailService.sendPasswordResetOtpEmail(user.getEmail(), user.getUsername(), otp);
        if (!emailSent) {
            // Dev fallback: keep OTP flow usable even when SMTP is misconfigured.
            log.error("Failed to send OTP email to '{}' for user id={}. Continuing with dev fallback OTP flow.",
                    user.getEmail(), user.getId());
            log.warn("DEV OTP for email='{}' user='{}' is {} (valid 5 minutes)", user.getEmail(), user.getUsername(),
                    otp);
            return new ApiResponse(true, "OTP generated (dev fallback). Check backend logs for the OTP.", 60);
        }

        log.info("OTP email sent to '{}' for user id={}", user.getEmail(), user.getId());
        return new ApiResponse(true, "OTP sent to email", 60);
    }

    public ApiResponse verifyPasswordResetOtp(VerifyOtpRequest request) {
        String email = normalizeEmail(request.getEmail());
        log.info("Verify OTP requested for email='{}'", email);

        User user = userRepository.findByEmailIgnoreCase(email)
                .orElseThrow(() -> new IllegalArgumentException("Account not found"));

        validateOtp(user, request.getOtp());
        log.info("OTP verified for user id={}", user.getId());
        return new ApiResponse(true, "OTP verified");
    }

    public ApiResponse resetPassword(ResetPasswordRequest request) {
        validatePasswords(request.getNewPassword(), request.getConfirmPassword());

        String email = normalizeEmail(request.getEmail());
        log.info("Reset password requested for email='{}'", email);

        User user = userRepository.findByEmailIgnoreCase(email)
                .orElseThrow(() -> new IllegalArgumentException("Account not found"));

        validateOtp(user, request.getOtp());

        user.setPasswordHash(passwordEncoder.encode(request.getNewPassword()));
        user.setPasswordResetOtpHash(null);
        user.setPasswordResetOtpExpiresAt(null);
        user.setPasswordResetOtpRequestedAt(null);
        userRepository.save(user);

        log.info("Password successfully reset for user id={}", user.getId());
        return new ApiResponse(true, "Password reset successful");
    }

    public UserProfileResponse getProfile(String authorizationHeader) {
        String token = extractToken(authorizationHeader);
        String username = jwtService.extractUsername(token);
        User user = userRepository.findByUsernameIgnoreCase(username)
                .orElseThrow(() -> new IllegalArgumentException("Account not found"));

        return new UserProfileResponse(true, "Profile loaded", user.getUsername(), user.getEmail(), user.getRole());
    }

    public UserSearchResponse searchUser(Long id, String username) {
        if (id == null && (username == null || username.trim().isEmpty())) {
            throw new IllegalArgumentException("Either id or username must be provided");
        }

        Optional<User> userOpt;
        if (id != null && username != null && !username.trim().isEmpty()) {
            userOpt = userRepository.findByIdAndUsernameIgnoreCase(id, username.trim());
        } else if (id != null) {
            userOpt = userRepository.findById(id);
        } else {
            userOpt = userRepository.findByUsernameIgnoreCase(username.trim());
        }

        User user = userOpt.orElseThrow(() -> new IllegalArgumentException("User not found"));
        return new UserSearchResponse(true, "User found", user.getId(), user.getUsername(), user.getEmail(),
                user.getRole());
    }

    public ApiResponse logout(String authorizationHeader) {
        String token = extractToken(authorizationHeader);
        String username = jwtService.extractUsername(token);

        if (username == null || !jwtService.isTokenValid(token)) {
            throw new IllegalArgumentException("Invalid token");
        }

        log.info("User '{}' successfully logged out.", username);
        return new ApiResponse(true, "Logout successful");
    }

    private Optional<User> findByIdentifier(String identifier) {
        return userRepository.findByUsernameIgnoreCaseOrEmailIgnoreCase(identifier, normalizeEmail(identifier));
    }

    private void validatePasswords(String password, String confirmPassword) {
        if (password == null || confirmPassword == null || !password.equals(confirmPassword)) {
            throw new IllegalArgumentException("Passwords do not match");
        }
    }

    private void validateOtp(User user, String otp) {
        if (user.getPasswordResetOtpHash() == null || user.getPasswordResetOtpExpiresAt() == null) {
            throw new IllegalArgumentException("OTP not requested");
        }
        if (LocalDateTime.now().isAfter(user.getPasswordResetOtpExpiresAt())) {
            throw new IllegalArgumentException("OTP expired");
        }
        if (otp == null || !passwordEncoder.matches(otp.trim(), user.getPasswordResetOtpHash())) {
            throw new IllegalArgumentException("Invalid OTP");
        }
    }

    private void enforceCooldown(User user) {
        if (user.getPasswordResetOtpRequestedAt() == null) {
            return;
        }

        LocalDateTime nextAllowed = user.getPasswordResetOtpRequestedAt().plusSeconds(60);
        if (LocalDateTime.now().isBefore(nextAllowed)) {
            long remaining = java.time.Duration.between(LocalDateTime.now(), nextAllowed).getSeconds();
            throw new IllegalArgumentException(
                    "Please wait " + Math.max(1, remaining) + " seconds before requesting another OTP");
        }
    }

    private String generateOtp() {
        return String.format(Locale.ROOT, "%06d", new Random().nextInt(1_000_000));
    }

    private String normalizeEmail(String email) {
        return email == null ? "" : email.trim().toLowerCase(Locale.ROOT);
    }

    private String extractToken(String authorizationHeader) {
        if (authorizationHeader == null || !authorizationHeader.startsWith("Bearer ")) {
            throw new IllegalArgumentException("Missing bearer token");
        }
        return authorizationHeader.substring(7);
    }
}