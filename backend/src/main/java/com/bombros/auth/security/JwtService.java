package com.bombros.auth.security;

import com.bombros.auth.config.AppProperties;
import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.SignatureAlgorithm;
import io.jsonwebtoken.security.Keys;

import java.security.Key;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.Date;
import java.util.Base64;

public class JwtService {
    private final AppProperties appProperties;

    public JwtService(AppProperties appProperties) {
        this.appProperties = appProperties;
    }

    public String generateToken(String username) {
        Instant now = Instant.now();
        Instant expiration = now.plus(appProperties.getJwt().getExpirationMinutes(), ChronoUnit.MINUTES);

        return Jwts.builder()
            .setSubject(username)
            .setIssuedAt(Date.from(now))
            .setExpiration(Date.from(expiration))
            .signWith(getSigningKey(), SignatureAlgorithm.HS256)
            .compact();
    }

    public String extractUsername(String token) {
        return parseClaims(token).getSubject();
    }

    public boolean isTokenValid(String token) {
        Claims claims = parseClaims(token);
        return claims.getSubject() != null && claims.getExpiration() != null && claims.getExpiration().after(new Date());
    }

    private Claims parseClaims(String token) {
        return Jwts.parserBuilder()
            .setSigningKey(getSigningKey())
            .build()
            .parseClaimsJws(token)
            .getBody();
    }

    private Key getSigningKey() {
        byte[] keyBytes = Base64.getEncoder().encode(appProperties.getJwt().getSecret().getBytes(StandardCharsets.UTF_8));
        return Keys.hmacShaKeyFor(keyBytes);
    }
}