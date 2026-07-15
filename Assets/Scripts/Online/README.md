# Online Lobby Setup

1. Install the multiplayer packages in `Packages/manifest.json`.
2. Create a Photon Fusion 2 App Id in the [Photon Dashboard](https://dashboard.photonengine.com/) and paste it into `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (`App Id Fusion`). You can also open **Tools > Fusion > Fusion Hub** in Unity.
3. Create a lobby/menu scene and add an empty GameObject with `OnlineLobbyManager`.
4. Assign these prefabs on `OnlineLobbyManager`:
   - `playerPrefab` -> network player prefab with `NetworkObject`, `ClientNetworkTransform`, `OnlinePlayerController`, `OnlinePlayerHealth`
   - `bombPrefab` -> network bomb prefab with `NetworkObject`, `OnlineBomb`
   - `explosionPrefab` -> network explosion prefab with `NetworkObject`, `OnlineExplosion`
5. Set `gameSceneName` to the map scene name, for example `SampleScene`.
6. Add `OnlineGameSpawner` to the game scene so the host places players at the four corners after load.
7. Keep `ThemeManager` in the map scene. It will skip local player spawning while `OnlineSessionState.IsOnlineSession` is true.

Default flow:
- Host clicks `Create Lobby & Host` and waits until the status shows the lobby code
- Share the lobby code with friends
- Clients join only after the host lobby is ready
- 2 to 4 players mark READY
- Host clicks `Start Game`

Troubleshooting:
- `Game does not exist (ErrorCode: 32758)` usually means the Fusion cloud room does not exist yet. The host must create the lobby first, or the Photon App Id is missing/invalid.
- If host and client are in different regions, set the same `Fixed Region` in `PhotonAppSettings.asset` (for Vietnam, try `asia`).

Spring backend URL:
- Current deployed backend root URL: `https://bombros-backend.onrender.com`.
- Edit `Assets/Resources/spring-auth-base-url.txt` if you need to switch between deployed and local backend.
- `SpringAuthClient` uses this order: command line `-bombrosAuthBaseUrl`, environment variable `BOMBROS_AUTH_BASE_URL`, saved PlayerPrefs override, `Resources/spring-auth-base-url.txt`, then the scene `baseUrl`.
- Keep only the root URL in the file. The game appends paths like `/api/auth/login` automatically.

Notes:
- `OnlinePlayerController` uses owner-authoritative movement and places bombs through a server RPC.
- `OnlineBomb` and `OnlineExplosion` are server-authoritative and will work only when the prefabs are registered in the network manager.
