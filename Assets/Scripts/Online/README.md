# Online Lobby Setup

1. Install the multiplayer packages in `Packages/manifest.json`.
2. Create a lobby/menu scene and add an empty GameObject with `OnlineLobbyManager`.
3. Assign these prefabs on `OnlineLobbyManager`:
   - `playerPrefab` -> network player prefab with `NetworkObject`, `ClientNetworkTransform`, `OnlinePlayerController`, `OnlinePlayerHealth`
   - `bombPrefab` -> network bomb prefab with `NetworkObject`, `OnlineBomb`
   - `explosionPrefab` -> network explosion prefab with `NetworkObject`, `OnlineExplosion`
4. Set `gameSceneName` to the map scene name, for example `SampleScene`.
5. Add `OnlineGameSpawner` to the game scene so the host places players at the four corners after load.
6. Keep `ThemeManager` in the map scene. It will skip local player spawning while `OnlineSessionState.IsOnlineSession` is true.

Default flow:
- Host clicks `Create Lobby & Host`
- Share the lobby code with friends
- 2 to 4 players join
- Host clicks `Start Game`

Notes:
- `OnlinePlayerController` uses owner-authoritative movement and places bombs through a server RPC.
- `OnlineBomb` and `OnlineExplosion` are server-authoritative and will work only when the prefabs are registered in the network manager.
