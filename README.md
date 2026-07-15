# BoomBros

BoomBros is a 3D Bomberman-style Unity game with account authentication, lobby-based online multiplayer, and Photon Fusion gameplay networking. Players can register or log in with a real backend account, create or join a lobby, ready up, and start an online match.

## Features

- Account flow: register, login, logout, forgot password, OTP verification, and password reset.
- Spring Boot backend deployed at `https://bombros-backend.onrender.com`.
- JWT session storage in Unity through `PlayerPrefs`.
- Main menu with multiplayer lobby entry and single-player placeholder.
- Unity Lobby integration for create/join lobby, lobby code sharing, ready state, and host start.
- Photon Fusion networking for online player movement, bombs, explosions, health, match result popup, and scene transition.
- Map/theme support with generated level data and online spawning.

## Project Structure

```text
Assets/
  Scenes/                 Unity scenes for login, menu, lobby, game, OTP, and registration
  Scripts/                Core gameplay scripts
  Scripts/Online/         Online auth, lobby, Fusion, and multiplayer scripts
  Resources/              Runtime config such as the backend base URL
  Prefab/                 Player, bomb, explosion, buff, and map prefabs
backend/                  Spring Boot authentication backend
Packages/                 Unity package manifest
ProjectSettings/          Unity project settings
```

## Main Scenes

- `LoginScene`: default entry scene for authentication.
- `RegisterScene`: account registration.
- `ForgotPasswordScene`: request password reset OTP.
- `VerifyOtpScene`: verify OTP and reset password.
- `MainMenuScene`: multiplayer entry and logout.
- `LobbyScene`: lobby code, player slots, ready/start controls.
- `GameScene`: online match gameplay.

## Tech Stack

- Unity `6000.4.6f1`
- Universal Render Pipeline
- TextMeshPro and Unity UI
- Unity Input System
- Unity Services Authentication and Lobby
- Photon Fusion
- Spring Boot `3.3.2`
- Java `17`
- MySQL
- JWT authentication
- Maven

## Backend URL

The Unity client reads the backend root URL from:

```text
Assets/Resources/spring-auth-base-url.txt
```

Current value:

```text
https://bombros-backend.onrender.com
```

`SpringAuthClient` automatically appends endpoint paths such as `/api/auth/login`, so the file should contain only the root URL.

Runtime override priority:

1. Command line: `-bombrosAuthBaseUrl https://your-backend`
2. Environment variable: `BOMBROS_AUTH_BASE_URL`
3. Saved PlayerPrefs override
4. `Assets/Resources/spring-auth-base-url.txt`
5. Scene `baseUrl` field fallback

## Backend API

Base URL:

```text
https://bombros-backend.onrender.com
```

Auth endpoints:

```text
POST /api/auth/register
POST /api/auth/login
GET  /api/auth/me
POST /api/auth/password/forgot/request
POST /api/auth/password/forgot/verify
POST /api/auth/password/forgot/reset
GET  /actuator/health
```

## Run the Unity Game

1. Open this folder in Unity Hub with Unity `6000.4.6f1`.
2. Let Unity restore packages from `Packages/manifest.json`.
3. Open `Assets/Scenes/LoginScene.unity`.
4. Check that `Assets/Resources/spring-auth-base-url.txt` points to `https://bombros-backend.onrender.com`.
5. Press Play.
6. Register or log in.
7. From Main Menu, create a lobby or join by lobby code.
8. Ready up and start the match.

## Run the Backend Locally

From the `backend` folder:

```powershell
mvn spring-boot:run
```

Default local backend URL:

```text
http://localhost:8082
```

To point Unity back to local backend, edit:

```text
Assets/Resources/spring-auth-base-url.txt
```

and set:

```text
http://localhost:8082
```

## Backend Environment Variables

Common deployment variables:

```text
PORT
SPRING_DATASOURCE_URL
SPRING_DATASOURCE_USERNAME
SPRING_DATASOURCE_PASSWORD
JWT_SECRET
JWT_EXPIRATION_MINUTES
MAIL_ENABLED
MAIL_PROVIDER
MAIL_FROM
MAIL_FROM_NAME
BREVO_API_KEY
```

For Render free deployment, Brevo transactional email is recommended for OTP/reset mail:

```text
MAIL_ENABLED=true
MAIL_PROVIDER=brevo
MAIL_FROM=your-verified-sender@example.com
MAIL_FROM_NAME=Bombros
BREVO_API_KEY=your-brevo-api-key
```

## Online Multiplayer Notes

- The player must be signed in through Spring auth before using multiplayer lobby actions.
- `OnlineLobbyManager` initializes Unity Services and signs in to Unity anonymously using a profile derived from the Spring username.
- Unity Lobby handles lobby creation, joining, player list, and ready state.
- Photon Fusion handles real-time gameplay after the lobby starts a match.
- Verify the Photon Fusion App Id in `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`.
- If host and clients are in different regions, set the same Photon fixed region, for example `asia`.

## Troubleshooting

- Login fails with connection error: confirm `Assets/Resources/spring-auth-base-url.txt` contains the deployed backend root URL and that the backend is awake.
- Render free service is slow on first request: wait for the backend to wake up, then retry login.
- OTP email does not arrive: check backend mail environment variables and Brevo sender verification.
- Lobby buttons are disabled: make sure the player is logged in and Unity Services initialized successfully.
- Fusion `Game does not exist`: host must create/start the Fusion session first, and Photon App Id must be valid.
- Build errors from `.NET CLI` against Unity packages can differ from Unity Editor compilation; use the Unity Console or Editor log for the authoritative Unity compile result.

## Useful Files

- `Assets/Scripts/Online/SpringAuthClient.cs`: Unity HTTP client for Spring auth.
- `Assets/Scripts/Online/SpringAuthSession.cs`: saved auth session state.
- `Assets/Scripts/Online/OnlineLobbyManager.cs`: Unity Lobby and Photon Fusion coordination.
- `Assets/Scripts/Online/OnlineAuthCanvasUI.cs`: auth scene UI wiring.
- `Assets/Scripts/Online/MainMenuCanvasUI.cs`: main menu multiplayer entry.
- `backend/src/main/java/com/bombros/auth/controller/AuthController.java`: auth API routes.
- `backend/src/main/resources/application.yml`: backend configuration defaults.
