package com.bombros.auth.controller;

import com.bombros.auth.dto.*;
import com.bombros.auth.service.AuthService;
import jakarta.validation.Valid;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/auth")
public class AuthController {
    private static final Logger log = LoggerFactory.getLogger(AuthController.class);
    private final AuthService authService;

    public AuthController(AuthService authService) {
        this.authService = authService;
    }

    @PostMapping("/register")
    public AuthResponse register(@Valid @RequestBody RegisterRequest request) {
        log.info("Register attempt username='{}' email='{}'", request.getUsername(), request.getEmail());
        AuthResponse resp = authService.register(request);
        log.info("Register result success={} username='{}' email='{}'", resp.isSuccess(), resp.getUsername(), resp.getEmail());
        return resp;
    }

    @PostMapping("/login")
    public AuthResponse login(@Valid @RequestBody LoginRequest request) {
        log.info("Login attempt identifier='{}'", request.getIdentifier());
        AuthResponse resp = authService.login(request);
        log.info("Login result success={} username='{}'", resp.isSuccess(), resp.getUsername());
        return resp;
    }

    @PostMapping("/password/forgot/request")
    public ApiResponse requestForgotPassword(@Valid @RequestBody ForgotPasswordRequest request) {
        log.info("Forgot-password request for email='{}'", request.getEmail());
        ApiResponse resp = authService.requestPasswordResetOtp(request);
        log.info("Forgot-password response success={}", resp.isSuccess());
        return resp;
    }

    @PostMapping("/password/forgot/verify")
    public ApiResponse verifyForgotPasswordOtp(@Valid @RequestBody VerifyOtpRequest request) {
        log.info("Verify OTP for email='{}'", request.getEmail());
        ApiResponse resp = authService.verifyPasswordResetOtp(request);
        log.info("Verify OTP result success={}", resp.isSuccess());
        return resp;
    }

    @PostMapping("/password/forgot/reset")
    public ApiResponse resetPassword(@Valid @RequestBody ResetPasswordRequest request) {
        log.info("Reset password attempt for email='{}'", request.getEmail());
        ApiResponse resp = authService.resetPassword(request);
        log.info("Reset password result success={}", resp.isSuccess());
        return resp;
    }

    @GetMapping("/me")
    public UserProfileResponse me(@RequestHeader("Authorization") String authorization) {
        return authService.getProfile(authorization);
    }
}