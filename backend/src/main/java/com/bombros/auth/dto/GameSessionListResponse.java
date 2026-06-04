package com.bombros.auth.dto;

import java.util.List;

public class GameSessionListResponse extends ApiResponse {
    private List<GameSessionResponse> sessions;

    public GameSessionListResponse() {
    }

    public GameSessionListResponse(boolean success, String message, List<GameSessionResponse> sessions) {
        super(success, message);
        this.sessions = sessions;
    }

    public List<GameSessionResponse> getSessions() {
        return sessions;
    }

    public void setSessions(List<GameSessionResponse> sessions) {
        this.sessions = sessions;
    }
}
