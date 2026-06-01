package com.bombros.auth.dto;

import java.util.List;

public class LeaderboardResponse extends ApiResponse {
    private List<LeaderboardUserDto> leaderboard;

    public LeaderboardResponse() {
    }

    public LeaderboardResponse(boolean success, String message, List<LeaderboardUserDto> leaderboard) {
        super(success, message);
        this.leaderboard = leaderboard;
    }

    public List<LeaderboardUserDto> getLeaderboard() {
        return leaderboard;
    }

    public void setLeaderboard(List<LeaderboardUserDto> leaderboard) {
        this.leaderboard = leaderboard;
    }
}
