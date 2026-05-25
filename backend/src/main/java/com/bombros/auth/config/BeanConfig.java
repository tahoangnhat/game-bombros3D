package com.bombros.auth.config;

import com.bombros.auth.repository.UserRepository;
import com.bombros.auth.security.JwtService;
import com.bombros.auth.service.CustomUserDetailsService;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

@Configuration
@EnableConfigurationProperties(AppProperties.class)
public class BeanConfig {
    @Bean
    public CustomUserDetailsService customUserDetailsService(UserRepository userRepository) {
        return new CustomUserDetailsService(userRepository);
    }

    @Bean
    public JwtService jwtService(AppProperties appProperties) {
        return new JwtService(appProperties);
    }
}