package com.bombros.auth.dto;

import java.util.List;

public class GameSessionResponse extends ApiResponse {
    private String sessionCode;
    private String hostUsername;
    private List<String> playerUsernames;
    private String status;
    private String winnerUsername;

    public GameSessionResponse() {
    }

    public GameSessionResponse(boolean success, String message, String sessionCode, String hostUsername, List<String> playerUsernames, String status, String winnerUsername) {
        super(success, message);
        this.sessionCode = sessionCode;
        this.hostUsername = hostUsername;
        this.playerUsernames = playerUsernames;
        this.status = status;
        this.winnerUsername = winnerUsername;
    }

    public String getSessionCode() {
        return sessionCode;
    }

    public void setSessionCode(String sessionCode) {
        this.sessionCode = sessionCode;
    }

    public String getHostUsername() {
        return hostUsername;
    }

    public void setHostUsername(String hostUsername) {
        this.hostUsername = hostUsername;
    }

    public List<String> getPlayerUsernames() {
        return playerUsernames;
    }

    public void setPlayerUsernames(List<String> playerUsernames) {
        this.playerUsernames = playerUsernames;
    }

    public String getStatus() {
        return status;
    }

    public void setStatus(String status) {
        this.status = status;
    }

    public String getWinnerUsername() {
        return winnerUsername;
    }

    public void setWinnerUsername(String winnerUsername) {
        this.winnerUsername = winnerUsername;
    }
}
