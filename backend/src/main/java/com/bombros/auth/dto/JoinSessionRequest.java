package com.bombros.auth.dto;

import jakarta.validation.constraints.NotBlank;

public class JoinSessionRequest {
    @NotBlank(message = "Session code is required")
    private String sessionCode;

    public JoinSessionRequest() {
    }

    public JoinSessionRequest(String sessionCode) {
        this.sessionCode = sessionCode;
    }

    public String getSessionCode() {
        return sessionCode;
    }

    public void setSessionCode(String sessionCode) {
        this.sessionCode = sessionCode;
    }
}
