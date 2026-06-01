package com.bombros.auth.controller;

import com.bombros.auth.dto.*;
import com.bombros.auth.service.AuthService;
import jakarta.validation.Valid;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/auth")
public class AuthController {
    private final AuthService authService;

    public AuthController(AuthService authService) {
        this.authService = authService;
    }

    @PostMapping("/register")
    public AuthResponse register(@Valid @RequestBody RegisterRequest request) {
        return authService.register(request);
    }

    @PostMapping("/login")
    public AuthResponse login(@Valid @RequestBody LoginRequest request) {
        return authService.login(request);
    }

    @PostMapping("/password/forgot/request")
    public ApiResponse requestForgotPassword(@Valid @RequestBody ForgotPasswordRequest request) {
        return authService.requestPasswordResetOtp(request);
    }

    @PostMapping("/password/forgot/verify")
    public ApiResponse verifyForgotPasswordOtp(@Valid @RequestBody VerifyOtpRequest request) {
        return authService.verifyPasswordResetOtp(request);
    }

    @PostMapping("/password/forgot/reset")
    public ApiResponse resetPassword(@Valid @RequestBody ResetPasswordRequest request) {
        return authService.resetPassword(request);
    }

    @PostMapping("/logout")
    public ApiResponse logout(@RequestHeader("Authorization") String authorization) {
        return authService.logout(authorization);
    }

    @GetMapping("/me")
    public UserProfileResponse me(@RequestHeader("Authorization") String authorization) {
        return authService.getProfile(authorization);
    }

    @GetMapping("/search")
    public UserSearchResponse search(@RequestParam(name = "id", required = false) Long id,
            @RequestParam(name = "username", required = false) String username) {
        return authService.searchUser(id, username);
    }
}