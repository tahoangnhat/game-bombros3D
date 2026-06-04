package com.bombros.auth.repository;

import com.bombros.auth.entity.GameSession;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.List;
import java.util.Optional;

public interface GameSessionRepository extends JpaRepository<GameSession, Long> {
    Optional<GameSession> findBySessionCode(String sessionCode);
    boolean existsBySessionCode(String sessionCode);
    List<GameSession> findByStatus(String status);
}
