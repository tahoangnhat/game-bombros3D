package com.bombros.auth.controller;

import com.bombros.auth.dto.*;
import com.bombros.auth.service.GameSessionService;
import jakarta.validation.Valid;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/api/auth/game/sessions")
public class GameSessionController {
    private final GameSessionService gameSessionService;

    public GameSessionController(GameSessionService gameSessionService) {
        this.gameSessionService = gameSessionService;
    }

    @PostMapping("/host")
    public GameSessionResponse hostSession(@RequestParam(name = "token") String token) {
        return gameSessionService.hostSession(token);
    }

    @PostMapping("/join")
    public GameSessionResponse joinSession(
            @RequestParam(name = "token") String token,
            @Valid @RequestBody JoinSessionRequest request) {
        return gameSessionService.joinSession(token, request);
    }

    @PostMapping("/end")
    public GameSessionResponse endSession(
            @RequestParam(name = "token") String token,
            @Valid @RequestBody EndSessionRequest request) {
        return gameSessionService.endSession(token, request);
    }

    @GetMapping("/search")
    public GameSessionListResponse searchSessions(@RequestParam(name = "sessionCode", required = false) String sessionCode) {
        return gameSessionService.searchSessions(sessionCode);
    }
}
