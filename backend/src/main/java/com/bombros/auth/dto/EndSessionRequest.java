package com.bombros.auth.dto;

import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public class EndSessionRequest {
    @NotBlank(message = "Session code is required")
    private String sessionCode;

    @NotNull(message = "Winner ID is required")
    private Long winnerId;

    public EndSessionRequest() {
    }

    public EndSessionRequest(String sessionCode, Long winnerId) {
        this.sessionCode = sessionCode;
        this.winnerId = winnerId;
    }

    public String getSessionCode() {
        return sessionCode;
    }

    public void setSessionCode(String sessionCode) {
        this.sessionCode = sessionCode;
    }

    public Long getWinnerId() {
        return winnerId;
    }

    public void setWinnerId(Long winnerId) {
        this.winnerId = winnerId;
    }
}
