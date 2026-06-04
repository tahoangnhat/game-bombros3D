package com.bombros.auth.service;

import com.bombros.auth.dto.*;
import com.bombros.auth.entity.GameSession;
import com.bombros.auth.entity.User;
import com.bombros.auth.repository.GameSessionRepository;
import com.bombros.auth.repository.UserRepository;
import com.bombros.auth.security.JwtService;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Random;
import java.util.stream.Collectors;

@Service
public class GameSessionService {
    private final GameSessionRepository gameSessionRepository;
    private final UserRepository userRepository;
    private final JwtService jwtService;

    public GameSessionService(GameSessionRepository gameSessionRepository,
                              UserRepository userRepository,
                              JwtService jwtService) {
        this.gameSessionRepository = gameSessionRepository;
        this.userRepository = userRepository;
        this.jwtService = jwtService;
    }

    @Transactional
    public GameSessionResponse hostSession(String token) {
        User host = getUserFromToken(token);

        GameSession session = new GameSession();
        session.setSessionCode(generateUniqueSessionCode());
        session.setHost(host);
        session.getPlayers().add(host);
        session.setStatus("WAITING");

        gameSessionRepository.save(session);
        return mapToResponse(session, true, "Session hosted successfully");
    }

    @Transactional
    public GameSessionResponse joinSession(String token, JoinSessionRequest request) {
        User user = getUserFromToken(token);
        String code = request.getSessionCode().trim().toUpperCase(Locale.ROOT);

        GameSession session = gameSessionRepository.findBySessionCode(code)
                .orElseThrow(() -> new IllegalArgumentException("Session not found"));

        if (!"WAITING".equals(session.getStatus())) {
            throw new IllegalArgumentException("Session is not in WAITING status");
        }

        // Check if already in the session
        boolean isAlreadyPlayer = session.getPlayers().stream()
                .anyMatch(p -> p.getId().equals(user.getId()));
        if (isAlreadyPlayer) {
            return mapToResponse(session, true, "User is already in this session");
        }

        // Check player counts. Max is 4 (1 host + 3 others)
        if (session.getPlayers().size() >= 4) {
            throw new IllegalArgumentException("Session is full (max 4 players)");
        }

        session.getPlayers().add(user);
        gameSessionRepository.save(session);

        return mapToResponse(session, true, "Joined session successfully");
    }

    @Transactional
    public GameSessionResponse endSession(String token, EndSessionRequest request) {
        User caller = getUserFromToken(token);
        String code = request.getSessionCode().trim().toUpperCase(Locale.ROOT);
        GameSession session = gameSessionRepository.findBySessionCode(code)
                .orElseThrow(() -> new IllegalArgumentException("Session not found"));

        if (!session.getHost().getId().equals(caller.getId())) {
            throw new IllegalArgumentException("Only the session host can end the session");
        }

        if (!"WAITING".equals(session.getStatus())) {
            throw new IllegalArgumentException("Session has already ended");
        }

        // Validate player counts: 1 host + 1 to 3 others means total players must be between 2 and 4
        int playerCount = session.getPlayers().size();
        if (playerCount < 2 || playerCount > 4) {
            throw new IllegalArgumentException("Session must have between 2 and 4 players to end (currently " + playerCount + ")");
        }

        // Validate winner is one of the players
        Long winnerId = request.getWinnerId();
        User winner = session.getPlayers().stream()
                .filter(p -> p.getId().equals(winnerId))
                .findFirst()
                .orElseThrow(() -> new IllegalArgumentException("Winner must be a player in the session"));

        session.setWinner(winner);
        session.setStatus("ENDED");
        winner.setWins(winner.getWins() + 1);

        userRepository.save(winner);
        gameSessionRepository.delete(session);

        return mapToResponse(session, true, "Session ended successfully. Winner: " + winner.getUsername());
    }

    @Transactional(readOnly = true)
    public GameSessionListResponse searchSessions(String sessionCode) {
        List<GameSession> sessions;
        if (sessionCode != null && !sessionCode.trim().isEmpty()) {
            String code = sessionCode.trim().toUpperCase(Locale.ROOT);
            Optional<GameSession> sessionOpt = gameSessionRepository.findBySessionCode(code);
            if (sessionOpt.isPresent() && "WAITING".equals(sessionOpt.get().getStatus())) {
                sessions = List.of(sessionOpt.get());
            } else {
                sessions = List.of();
            }
        } else {
            sessions = gameSessionRepository.findByStatus("WAITING");
        }

        List<GameSessionResponse> mapped = sessions.stream()
                .map(s -> mapToResponse(s, true, "Session found"))
                .collect(Collectors.toList());

        return new GameSessionListResponse(true, "Sessions retrieved", mapped);
    }

    private User getUserFromToken(String token) {
        if (token == null || token.trim().isEmpty()) {
            throw new IllegalArgumentException("Missing authentication token");
        }
        String cleanToken = token.trim();
        if (cleanToken.startsWith("Bearer ")) {
            cleanToken = cleanToken.substring(7).trim();
        }
        String username = jwtService.extractUsername(cleanToken);
        return userRepository.findByUsernameIgnoreCase(username)
                .orElseThrow(() -> new IllegalArgumentException("User not found"));
    }

    private String generateUniqueSessionCode() {
        String chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Random random = new Random();
        String code;
        do {
            StringBuilder sb = new StringBuilder(5);
            for (int i = 0; i < 5; i++) {
                sb.append(chars.charAt(random.nextInt(chars.length())));
            }
            code = sb.toString();
        } while (gameSessionRepository.existsBySessionCode(code));
        return code;
    }

    private GameSessionResponse mapToResponse(GameSession session, boolean success, String message) {
        List<String> playerUsernames = session.getPlayers().stream()
                .map(User::getUsername)
                .collect(Collectors.toList());
        String winnerUsername = session.getWinner() != null ? session.getWinner().getUsername() : null;

        return new GameSessionResponse(
                success,
                message,
                session.getSessionCode(),
                session.getHost().getUsername(),
                playerUsernames,
                session.getStatus(),
                winnerUsername
        );
    }
}
