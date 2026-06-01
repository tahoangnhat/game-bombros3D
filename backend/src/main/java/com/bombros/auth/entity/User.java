package com.bombros.auth.entity;

import jakarta.persistence.*;

import java.time.LocalDateTime;

@Entity
@Table(name = "users")
public class User {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false, unique = true, length = 32)
    private String username;

    @Column(nullable = false, unique = true, length = 128)
    private String email;

    @Column(nullable = false, length = 255)
    private String passwordHash;

    @Column(nullable = false, length = 32)
    private String role = "PLAYER";

    @Column(nullable = false)
    private boolean emailVerified = true;

    @Column(length = 255)
    private String passwordResetOtpHash;

    private LocalDateTime passwordResetOtpExpiresAt;

    private LocalDateTime passwordResetOtpRequestedAt;

    @Column(nullable = false, updatable = false)
    private LocalDateTime createdAt;

    @Column(nullable = false)
    private LocalDateTime updatedAt;

    @Column(nullable = false)
    private int wins = 0;

    @PrePersist
    public void onCreate() {
        LocalDateTime now = LocalDateTime.now();
        createdAt = now;
        updatedAt = now;
    }

    @PreUpdate
    public void onUpdate() {
        updatedAt = LocalDateTime.now();
    }

    public Long getId() {
        return id;
    }

    public void setId(Long id) {
        this.id = id;
    }

    public int getWins() {
        return wins;
    }

    public void setWins(int wins) {
        this.wins = wins;
    }

    public String getUsername() {
        return username;
    }

    public void setUsername(String username) {
        this.username = username;
    }

    public String getEmail() {
        return email;
    }

    public void setEmail(String email) {
        this.email = email;
    }

    public String getPasswordHash() {
        return passwordHash;
    }

    public void setPasswordHash(String passwordHash) {
        this.passwordHash = passwordHash;
    }

    public String getRole() {
        return role;
    }

    public void setRole(String role) {
        this.role = role;
    }

    public boolean isEmailVerified() {
        return emailVerified;
    }

    public void setEmailVerified(boolean emailVerified) {
        this.emailVerified = emailVerified;
    }

    public String getPasswordResetOtpHash() {
        return passwordResetOtpHash;
    }

    public void setPasswordResetOtpHash(String passwordResetOtpHash) {
        this.passwordResetOtpHash = passwordResetOtpHash;
    }

    public LocalDateTime getPasswordResetOtpExpiresAt() {
        return passwordResetOtpExpiresAt;
    }

    public void setPasswordResetOtpExpiresAt(LocalDateTime passwordResetOtpExpiresAt) {
        this.passwordResetOtpExpiresAt = passwordResetOtpExpiresAt;
    }

    public LocalDateTime getPasswordResetOtpRequestedAt() {
        return passwordResetOtpRequestedAt;
    }

    public void setPasswordResetOtpRequestedAt(LocalDateTime passwordResetOtpRequestedAt) {
        this.passwordResetOtpRequestedAt = passwordResetOtpRequestedAt;
    }

    public LocalDateTime getCreatedAt() {
        return createdAt;
    }

    public LocalDateTime getUpdatedAt() {
        return updatedAt;
    }
}