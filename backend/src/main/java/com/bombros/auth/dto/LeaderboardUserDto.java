package com.bombros.auth.dto;

public class LeaderboardUserDto {
    private Long id;
    private String username;
    private int wins;

    public LeaderboardUserDto() {
    }

    public LeaderboardUserDto(Long id, String username, int wins) {
        this.id = id;
        this.username = username;
        this.wins = wins;
    }

    public Long getId() {
        return id;
    }

    public void setId(Long id) {
        this.id = id;
    }

    public String getUsername() {
        return username;
    }

    public void setUsername(String username) {
        this.username = username;
    }

    public int getWins() {
        return wins;
    }

    public void setWins(int wins) {
        this.wins = wins;
    }
}
