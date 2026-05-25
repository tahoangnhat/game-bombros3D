using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OnlineLobbyManager : MonoBehaviour
{
    private const string RelayJoinCodeKey = "relayJoinCode";
    private const string LobbyRankMmrKey = "rankMmr";
    private const string LobbyRankBucketKey = "rankBucket";
    private const string ReadyKey = "ready";
    private const string DisplayNameKey = "displayName";

    public static OnlineLobbyManager Instance { get; private set; }

    [Header("Lobby")]
    public string lobbyName = "BoomBros Online";
    [Range(2, 4)] public int maxPlayers = 4;
    [Range(2, 4)] public int minPlayersToStart = 2;
    public string joinLobbyCode = "";
    public string statusMessage = "Initializing...";

    [Header("Scenes")]
    public string gameSceneName = "SampleScene";

    [Header("Network Prefabs")]
    public NetworkObject playerPrefab;
    public NetworkObject bombPrefab;
    public NetworkObject explosionPrefab;

    [Header("Runtime")]
    public bool autoInitialize = true;
    public bool allowAnonymousFallbackIfUsernamePasswordDisabled = true;

    [Header("Rank")]
    public int defaultMmr = 0;
    public int rankWindow = 300;

    [Header("Email / OTP")]
    public bool useMockEmailOtp = true;
    public string emailServiceBaseUrl = "";
    public float otpExpireSeconds = 300f;
    public float otpResendCooldownSeconds = 60f;

    private Lobby currentLobby;
    private UnityTransport transport;
    private NetworkManager networkManager;
    private bool servicesReady;
    private bool isHost;
    private float heartbeatTimer;
    private float pollTimer;
    private string relayJoinCode;
    private bool playerPrefabRegistered;
    private bool bombPrefabRegistered;
    private bool explosionPrefabRegistered;
    private bool localReady;
    private bool isLoadingGame;
    private float lastOtpRequestTime = -999f;
    private string pendingOtpEmail = string.Empty;
    private string pendingOtpCode = string.Empty;
    private float pendingOtpExpireAt = -1f;

    private static readonly string[] RankTiers =
    {
        "Bronze",
        "Silver",
        "Gold",
        "Platinum",
        "Diamond",
        "Master"
    };

    private const int RankStep = 100;
    private const int WinPoints = 25;
    private const int LosePoints = -10;

    private const float HeartbeatInterval = 15f;
    private const float PollInterval = 2f;

    public bool ServicesReady => servicesReady;
    public bool IsHostLobby => isHost;
    public bool IsSignedIn => TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn;
    public Lobby CurrentLobby => currentLobby;
    public string RelayJoinCode => relayJoinCode;
    public int CurrentMmr => GetLocalMmr();
    public string CurrentRankTier => GetRankTier(CurrentMmr);
    public float OtpResendRemainingSeconds => Mathf.Max(0f, otpResendCooldownSeconds - (Time.unscaledTime - lastOtpRequestTime));
    public bool CanResendOtp => !string.IsNullOrEmpty(pendingOtpEmail) && OtpResendRemainingSeconds <= 0f;
    public bool CanUseLobbyActions => servicesReady && IsSignedIn && !IsNetworkSessionActive();

    public bool CanToggleReady => servicesReady && currentLobby != null && !string.IsNullOrEmpty(GetPlayerIdSafe());

    public bool CanStartGame
    {
        get
        {
            if (!isHost || currentLobby == null || isLoadingGame)
            {
                return false;
            }

            int playerCount = currentLobby.Players != null ? currentLobby.Players.Count : 0;
            if (playerCount < minPlayersToStart || playerCount > maxPlayers)
            {
                return false;
            }

            return AreAllPlayersReady();
        }
    }

    private bool TryGetAuthService(out IAuthenticationService auth)
    {
        auth = null;

        if (!servicesReady)
        {
            return false;
        }

        try
        {
            auth = AuthenticationService.Instance;
            return auth != null;
        }
        catch
        {
            return false;
        }
    }

    private string GetPlayerIdSafe()
    {
        if (!TryGetAuthService(out IAuthenticationService auth) || !auth.IsSignedIn)
        {
            return string.Empty;
        }

        return auth.PlayerId;
    }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureNetworkStack();
        OnlineSessionState.IsOnlineSession = false;

        if (autoInitialize)
        {
            await InitializeServicesAsync();
        }
    }

    private void Update()
    {
        if (!servicesReady || currentLobby == null)
        {
            return;
        }

        if (isHost)
        {
            heartbeatTimer += Time.deltaTime;
            if (heartbeatTimer >= HeartbeatInterval)
            {
                heartbeatTimer = 0f;
                _ = SendHeartbeatAsync();
            }
        }

        pollTimer += Time.deltaTime;
        if (pollTimer >= PollInterval)
        {
            pollTimer = 0f;
            _ = RefreshLobbyAsync();
        }
    }

    public void SetJoinCodeInput(string value)
    {
        joinLobbyCode = value != null ? value.Trim() : string.Empty;
    }

    public bool IsLocalPlayerReady()
    {
        return localReady;
    }

    public string GetLobbyPlayersSummary()
    {
        if (currentLobby == null || currentLobby.Players == null || currentLobby.Players.Count == 0)
        {
            return "No players in lobby yet.";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            Player player = currentLobby.Players[i];
            bool ready = IsPlayerReady(player);
            string name = GetPlayerDisplayName(player);
            builder.Append(i + 1)
                .Append(". ")
                .Append(name)
                .Append(ready ? " [READY]" : " [NOT READY]");

            if (i < currentLobby.Players.Count - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    public void CreateLobbyAndHost()
    {
        _ = CreateLobbyAndHostAsync();
    }

    public void Register(string username, string password)
    {
        _ = RegisterAsync(username, string.Empty, password, password);
    }

    public void Register(string username, string email, string password, string confirmPassword)
    {
        _ = RegisterAsync(username, email, password, confirmPassword);
    }

    public void Login(string username, string password)
    {
        _ = LoginAsync(username, password);
    }

    public void RequestPasswordResetOtp(string email)
    {
        _ = RequestPasswordResetOtpAsync(email);
    }

    public void ResendPasswordResetOtp()
    {
        _ = ResendPasswordResetOtpAsync();
    }

    public void ConfirmPasswordResetOtp(string email, string otp, string newPassword, string confirmPassword)
    {
        _ = ConfirmPasswordResetOtpAsync(email, otp, newPassword, confirmPassword);
    }

    public void Logout()
    {
        _ = LogoutAsync();
    }

    public void ApplyRankDelta(int delta)
    {
        if (!IsSignedIn)
        {
            return;
        }

        int next = Mathf.Clamp(GetLocalMmr() + delta, 0, 9999);
        SaveLocalMmr(next);
        statusMessage = $"Rank updated: {CurrentRankTier} ({CurrentMmr})";
    }

    public void ApplyMatchResult(bool won)
    {
        ApplyRankDelta(won ? WinPoints : LosePoints);
    }

    public void JoinLobbyByInputCode()
    {
        _ = JoinLobbyAndConnectAsync(joinLobbyCode);
    }

    public void QuickJoin()
    {
        _ = QuickJoinAndConnectAsync();
    }

    public void ToggleReady()
    {
        _ = ToggleReadyAsync();
    }

    public void StartGame()
    {
        _ = StartGameAsync();
    }

    public void LeaveSession()
    {
        _ = LeaveSessionAsync();
    }

    private async Task InitializeServicesAsync()
    {
        if (servicesReady)
        {
            return;
        }

        try
        {
            statusMessage = "Initializing Unity Services...";
            await UnityServices.InitializeAsync();

            servicesReady = true;
            statusMessage = IsSignedIn ? "Services ready" : "Services ready. Please login.";
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Init failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task RegisterAsync(string username, string email, string password, string confirmPassword)
    {
        await InitializeServicesAsync();

        if (!servicesReady)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            statusMessage = "Username/password cannot be empty";
            return;
        }

        if (password != confirmPassword)
        {
            statusMessage = "Confirm password does not match";
            return;
        }

        try
        {
            if (TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn)
            {
                auth.SignOut();
            }

            statusMessage = "Registering account...";
            if (!TryGetAuthService(out auth))
            {
                statusMessage = "Auth service unavailable";
                return;
            }

            string trimmedUsername = username.Trim();
            await auth.SignUpWithUsernamePasswordAsync(trimmedUsername, password);
            SaveEmailUsernameMapping(email, trimmedUsername);
            EnsureLocalRankInitialized();
            statusMessage = $"Registered & logged in as {BuildLocalDisplayName()} | Rank {CurrentRankTier} ({CurrentMmr})";

            if (!string.IsNullOrWhiteSpace(email))
            {
                await SendRegistrationSuccessEmailAsync(email.Trim(), trimmedUsername);
            }
        }
        catch (System.Exception ex)
        {
            if (IsUsernamePasswordProviderDisabledError(ex))
            {
                await HandleUsernamePasswordProviderDisabledAsync("register");
                return;
            }

            statusMessage = $"Register failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task LoginAsync(string usernameOrEmail, string password)
    {
        await InitializeServicesAsync();

        if (!servicesReady)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            statusMessage = "Username/password cannot be empty";
            return;
        }

        try
        {
            if (TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn)
            {
                auth.SignOut();
            }

            statusMessage = "Signing in...";
            if (!TryGetAuthService(out auth))
            {
                statusMessage = "Auth service unavailable";
                return;
            }

            string username = ResolveUsernameForLogin(usernameOrEmail.Trim());
            await auth.SignInWithUsernamePasswordAsync(username, password);
            EnsureLocalRankInitialized();
            statusMessage = $"Logged in as {BuildLocalDisplayName()} | Rank {CurrentRankTier} ({CurrentMmr})";
        }
        catch (System.Exception ex)
        {
            if (IsUsernamePasswordProviderDisabledError(ex))
            {
                await HandleUsernamePasswordProviderDisabledAsync("login");
                return;
            }

            statusMessage = $"Login failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private bool IsUsernamePasswordProviderDisabledError(System.Exception ex)
    {
        if (ex == null)
        {
            return false;
        }

        string message = ex.Message != null ? ex.Message.ToLowerInvariant() : string.Empty;
        return message.Contains("usernamepassword") && message.Contains("not available") && message.Contains("permission_denied");
    }

    private async Task HandleUsernamePasswordProviderDisabledAsync(string flow)
    {
        if (allowAnonymousFallbackIfUsernamePasswordDisabled && TryGetAuthService(out IAuthenticationService auth))
        {
            try
            {
                await auth.SignInAnonymouslyAsync();
                EnsureLocalRankInitialized();
                statusMessage = $"Username/Password is OFF in Unity Dashboard. Signed in as guest for now ({flow} fallback).";
                return;
            }
            catch (System.Exception fallbackEx)
            {
                Debug.LogException(fallbackEx);
            }
        }

        statusMessage = "Username/Password provider is disabled in Unity Authentication Dashboard. Enable it in Services > Authentication > Identity Providers.";
    }

    private async Task LogoutAsync()
    {
        await LeaveSessionAsync();

        if (TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn)
        {
            auth.SignOut();
        }

        statusMessage = "Logged out";
    }

    private async Task RequestPasswordResetOtpAsync(string email)
    {
        await InitializeServicesAsync();

        if (!servicesReady)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            statusMessage = "Email cannot be empty";
            return;
        }

        if (!CanRequestOtpNow())
        {
            statusMessage = $"Please wait {Mathf.CeilToInt(OtpResendRemainingSeconds)}s to resend OTP";
            return;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        bool sent = await SendOtpEmailAsync(normalizedEmail);
        if (!sent)
        {
            return;
        }

        pendingOtpEmail = normalizedEmail;
        lastOtpRequestTime = Time.unscaledTime;
        statusMessage = "OTP sent to your email";
    }

    private async Task ResendPasswordResetOtpAsync()
    {
        if (string.IsNullOrEmpty(pendingOtpEmail))
        {
            statusMessage = "Request OTP first";
            return;
        }

        await RequestPasswordResetOtpAsync(pendingOtpEmail);
    }

    private async Task ConfirmPasswordResetOtpAsync(string email, string otp, string newPassword, string confirmPassword)
    {
        await InitializeServicesAsync();

        if (!servicesReady)
        {
            return;
        }

        string normalizedEmail = string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail) || string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(newPassword))
        {
            statusMessage = "Email, OTP and password are required";
            return;
        }

        if (newPassword != confirmPassword)
        {
            statusMessage = "Confirm password does not match";
            return;
        }

        bool valid = await VerifyOtpAsync(normalizedEmail, otp.Trim());
        if (!valid)
        {
            statusMessage = "Invalid or expired OTP";
            return;
        }

        string mappedUsername = LoadMappedUsernameByEmail(normalizedEmail);
        if (string.IsNullOrEmpty(mappedUsername))
        {
            statusMessage = "Cannot resolve username from email on this device";
            return;
        }

        // Unity Authentication does not currently provide password reset by OTP directly in this client script.
        // We route through the configured email service for real environments.
        if (useMockEmailOtp)
        {
            statusMessage = "OTP verified. Configure backend API to finalize password reset.";
            ClearPendingOtp();
            return;
        }

        if (string.IsNullOrWhiteSpace(emailServiceBaseUrl))
        {
            statusMessage = "Email service URL is empty";
            return;
        }

        PasswordResetFinalizeRequest body = new PasswordResetFinalizeRequest
        {
            email = normalizedEmail,
            username = mappedUsername,
            otp = otp.Trim(),
            newPassword = newPassword
        };

        bool done = await PostJsonAsync(BuildEmailApiUrl("/password-reset/confirm"), body);
        if (!done)
        {
            return;
        }

        statusMessage = "Password reset successful. Please login again.";
        ClearPendingOtp();
    }

    private async Task CreateLobbyAndHostAsync()
    {
        await InitializeServicesAsync();

        if (!servicesReady || !IsSignedIn)
        {
            statusMessage = "Please login first";
            return;
        }

        try
        {
            statusMessage = "Creating relay allocation...";
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            EnsureNetworkStack();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!networkManager.IsListening)
            {
                SetupNetworkPrefabs();
                if (!networkManager.StartHost())
                {
                    statusMessage = "Failed to start host";
                    return;
                }
            }

            statusMessage = "Creating lobby...";
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { LobbyRankMmrKey, new DataObject(DataObject.VisibilityOptions.Public, CurrentMmr.ToString(), DataObject.IndexOptions.N1) },
                    { LobbyRankBucketKey, new DataObject(DataObject.VisibilityOptions.Public, BuildRankBucket(CurrentMmr), DataObject.IndexOptions.S1) }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            isHost = true;
            OnlineSessionState.IsOnlineSession = true;
            await SetLocalReadyAsync(false);
            statusMessage = $"Lobby created. Code: {currentLobby.LobbyCode}";
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Host failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task JoinLobbyAndConnectAsync(string lobbyCode)
    {
        await InitializeServicesAsync();

        if (!servicesReady || !IsSignedIn)
        {
            statusMessage = "Please login first";
            return;
        }

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            statusMessage = "Enter a lobby code first";
            return;
        }

        try
        {
            statusMessage = "Joining lobby...";
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim());

            if (!currentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayJoinCodeData))
            {
                statusMessage = "Lobby has no relay code";
                return;
            }

            relayJoinCode = relayJoinCodeData.Value;

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            EnsureNetworkStack();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            SetupNetworkPrefabs();
            if (!networkManager.StartClient())
            {
                statusMessage = "Failed to start client";
                return;
            }

            isHost = false;
            OnlineSessionState.IsOnlineSession = true;
            await SetLocalReadyAsync(false);
            statusMessage = $"Joined lobby {currentLobby.LobbyCode}";
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Join failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task QuickJoinAndConnectAsync()
    {
        await InitializeServicesAsync();

        if (!servicesReady || !IsSignedIn)
        {
            statusMessage = "Please login first";
            return;
        }

        try
        {
            statusMessage = "Quick joining by rank...";
            currentLobby = await FindRankedLobbyAsync();

            if (currentLobby == null)
            {
                statusMessage = "No same-rank lobby, trying random quick join...";
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            }

            if (!currentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayJoinCodeData))
            {
                statusMessage = "Lobby has no relay code";
                return;
            }

            relayJoinCode = relayJoinCodeData.Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            EnsureNetworkStack();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            SetupNetworkPrefabs();
            if (!networkManager.StartClient())
            {
                statusMessage = "Failed to start client";
                return;
            }

            isHost = false;
            OnlineSessionState.IsOnlineSession = true;
            await SetLocalReadyAsync(false);
            statusMessage = $"Quick joined lobby {currentLobby.LobbyCode}";
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Quick join failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private async Task StartGameAsync()
    {
        if (!isHost || currentLobby == null)
        {
            return;
        }

        int playerCount = currentLobby.Players != null ? currentLobby.Players.Count : 0;
        if (playerCount < minPlayersToStart)
        {
            statusMessage = $"Need at least {minPlayersToStart} players";
            return;
        }

        if (playerCount > maxPlayers)
        {
            statusMessage = $"Max {maxPlayers} players allowed";
            return;
        }

        if (!AreAllPlayersReady())
        {
            statusMessage = "All players must be READY";
            return;
        }

        OnlineSessionState.IsOnlineSession = true;
        isLoadingGame = true;
        statusMessage = "Starting game...";

        if (networkManager.SceneManager != null)
        {
            networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        await Task.CompletedTask;
    }

    private async Task LeaveSessionAsync()
    {
        try
        {
            if (currentLobby != null)
            {
                if (isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                }
                else
                {
                    string playerId = GetPlayerIdSafe();
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        currentLobby = null;
        isHost = false;
        localReady = false;
        isLoadingGame = false;
        OnlineSessionState.IsOnlineSession = false;
        statusMessage = "Left session";
    }

    private async Task SendHeartbeatAsync()
    {
        if (currentLobby == null || !isHost)
        {
            return;
        }

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task RefreshLobbyAsync()
    {
        if (currentLobby == null)
        {
            return;
        }

        try
        {
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            localReady = IsLocalPlayerReady(currentLobby);
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private async Task<Lobby> FindRankedLobbyAsync()
    {
        int mmr = CurrentMmr;
        int minMmr = mmr - Mathf.Max(50, rankWindow);
        int maxMmr = mmr + Mathf.Max(50, rankWindow);

        QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
        {
            Count = 25,
            SampleResults = true,
            Filters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                new QueryFilter(QueryFilter.FieldOptions.MaxPlayers, "2", QueryFilter.OpOptions.GE),
                new QueryFilter(QueryFilter.FieldOptions.N1, minMmr.ToString(), QueryFilter.OpOptions.GE),
                new QueryFilter(QueryFilter.FieldOptions.N1, maxMmr.ToString(), QueryFilter.OpOptions.LE)
            }
        };

        QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
        if (response?.Results == null || response.Results.Count == 0)
        {
            return null;
        }

        int pick = Random.Range(0, response.Results.Count);
        return await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[pick].Id);
    }

    private async Task ToggleReadyAsync()
    {
        if (!CanToggleReady)
        {
            return;
        }

        await SetLocalReadyAsync(!localReady);
    }

    private async Task SetLocalReadyAsync(bool ready)
    {
        string playerId = GetPlayerIdSafe();
        if (currentLobby == null || string.IsNullOrEmpty(playerId))
        {
            return;
        }

        try
        {
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { ReadyKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0") },
                    { DisplayNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, BuildLocalDisplayName()) }
                }
            };

            currentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                currentLobby.Id,
                playerId,
                options);

            localReady = ready;
            statusMessage = ready ? "You are READY" : "You are NOT READY";
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Ready update failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private bool AreAllPlayersReady()
    {
        if (currentLobby == null || currentLobby.Players == null || currentLobby.Players.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            if (!IsPlayerReady(currentLobby.Players[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsLocalPlayerReady(Lobby lobby)
    {
        string localPlayerId = GetPlayerIdSafe();
        if (lobby == null || lobby.Players == null || string.IsNullOrEmpty(localPlayerId))
        {
            return false;
        }

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            Player player = lobby.Players[i];
            if (player != null && player.Id == localPlayerId)
            {
                return IsPlayerReady(player);
            }
        }

        return false;
    }

    private static bool IsPlayerReady(Player player)
    {
        if (player == null || player.Data == null)
        {
            return false;
        }

        if (!player.Data.TryGetValue(ReadyKey, out PlayerDataObject readyData) || readyData == null)
        {
            return false;
        }

        return readyData.Value == "1";
    }

    private static string GetPlayerDisplayName(Player player)
    {
        if (player == null)
        {
            return "Unknown";
        }

        if (player.Data != null && player.Data.TryGetValue(DisplayNameKey, out PlayerDataObject displayNameData) && !string.IsNullOrEmpty(displayNameData.Value))
        {
            return displayNameData.Value;
        }

        if (!string.IsNullOrEmpty(player.Id))
        {
            int shortLength = Mathf.Min(6, player.Id.Length);
            return $"P-{player.Id.Substring(0, shortLength)}";
        }

        return "Player";
    }

    private string BuildLocalDisplayName()
    {
        string playerId = GetPlayerIdSafe();
        if (string.IsNullOrEmpty(playerId))
        {
            return "Player";
        }

        int shortLength = Mathf.Min(6, playerId.Length);
        return $"P-{playerId.Substring(0, shortLength)}";
    }

    private void EnsureLocalRankInitialized()
    {
        string key = BuildMmrStorageKey();
        if (!PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.SetInt(key, defaultMmr);
            PlayerPrefs.Save();
        }
    }

    private int GetLocalMmr()
    {
        EnsureLocalRankInitialized();
        return PlayerPrefs.GetInt(BuildMmrStorageKey(), defaultMmr);
    }

    private void SaveLocalMmr(int value)
    {
        PlayerPrefs.SetInt(BuildMmrStorageKey(), value);
        PlayerPrefs.Save();
    }

    private string BuildMmrStorageKey()
    {
        string playerId = GetPlayerIdSafe();
        if (string.IsNullOrEmpty(playerId))
        {
            return "boom_rank_guest";
        }

        return "boom_rank_" + playerId;
    }

    private static string BuildRankBucket(int mmr)
    {
        string tier = GetRankTier(mmr);
        return tier.ToLowerInvariant();
    }

    private static string GetRankTier(int mmr)
    {
        int clampedMmr = Mathf.Max(0, mmr);
        int tierIndex = Mathf.Clamp(clampedMmr / RankStep, 0, RankTiers.Length - 1);
        return RankTiers[tierIndex];
    }

    private static string BuildEmailMappingKey(string email)
    {
        return "boom_email_map_" + email;
    }

    private static string ResolveUsernameForLogin(string usernameOrEmail)
    {
        if (!usernameOrEmail.Contains("@"))
        {
            return usernameOrEmail;
        }

        string mapped = LoadMappedUsernameByEmail(usernameOrEmail.ToLowerInvariant());
        return string.IsNullOrEmpty(mapped) ? usernameOrEmail : mapped;
    }

    private static string LoadMappedUsernameByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        string normalized = email.Trim().ToLowerInvariant();
        return PlayerPrefs.GetString(BuildEmailMappingKey(normalized), string.Empty);
    }

    private static void SaveEmailUsernameMapping(string email, string username)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        string normalized = email.Trim().ToLowerInvariant();
        PlayerPrefs.SetString(BuildEmailMappingKey(normalized), username.Trim());
        PlayerPrefs.Save();
    }

    private bool CanRequestOtpNow()
    {
        if (string.IsNullOrEmpty(pendingOtpEmail))
        {
            return true;
        }

        return OtpResendRemainingSeconds <= 0f;
    }

    private void ClearPendingOtp()
    {
        pendingOtpEmail = string.Empty;
        pendingOtpCode = string.Empty;
        pendingOtpExpireAt = -1f;
        lastOtpRequestTime = -999f;
    }

    private async Task<bool> SendOtpEmailAsync(string email)
    {
        if (useMockEmailOtp)
        {
            pendingOtpCode = Random.Range(100000, 999999).ToString();
            pendingOtpExpireAt = Time.unscaledTime + Mathf.Max(30f, otpExpireSeconds);
            Debug.Log($"[MOCK OTP] {email}: {pendingOtpCode}");
            return await Task.FromResult(true);
        }

        if (string.IsNullOrWhiteSpace(emailServiceBaseUrl))
        {
            statusMessage = "Email service URL is empty";
            return false;
        }

        PasswordResetRequest body = new PasswordResetRequest { email = email };
        bool ok = await PostJsonAsync(BuildEmailApiUrl("/password-reset/request"), body);
        if (ok)
        {
            pendingOtpExpireAt = Time.unscaledTime + Mathf.Max(30f, otpExpireSeconds);
        }

        return ok;
    }

    private async Task<bool> VerifyOtpAsync(string email, string otp)
    {
        if (useMockEmailOtp)
        {
            bool notExpired = Time.unscaledTime <= pendingOtpExpireAt;
            return await Task.FromResult(
                !string.IsNullOrEmpty(pendingOtpCode) &&
                pendingOtpEmail == email &&
                pendingOtpCode == otp &&
                notExpired);
        }

        if (string.IsNullOrWhiteSpace(emailServiceBaseUrl))
        {
            statusMessage = "Email service URL is empty";
            return false;
        }

        PasswordResetVerifyRequest body = new PasswordResetVerifyRequest
        {
            email = email,
            otp = otp
        };

        return await PostJsonAsync(BuildEmailApiUrl("/password-reset/verify"), body);
    }

    private async Task SendRegistrationSuccessEmailAsync(string email, string username)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if (useMockEmailOtp)
        {
            Debug.Log($"[MOCK EMAIL] Registration success -> {email} (username: {username})");
            return;
        }

        if (string.IsNullOrWhiteSpace(emailServiceBaseUrl))
        {
            return;
        }

        RegistrationMailRequest body = new RegistrationMailRequest
        {
            email = email,
            username = username
        };

        await PostJsonAsync(BuildEmailApiUrl("/registration/success"), body);
    }

    private string BuildEmailApiUrl(string path)
    {
        string baseUrl = emailServiceBaseUrl.Trim().TrimEnd('/');
        string suffix = path.StartsWith("/") ? path : "/" + path;
        return baseUrl + suffix;
    }

    private async Task<bool> PostJsonAsync<T>(string url, T payload)
    {
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
        using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        var op = request.SendWebRequest();
        while (!op.isDone)
        {
            await Task.Yield();
        }

        if (request.result == UnityWebRequest.Result.Success)
        {
            return true;
        }

        statusMessage = $"Email API failed: {request.responseCode}";
        Debug.LogError($"Email API error ({request.responseCode}): {request.error}");
        return false;
    }

    [System.Serializable]
    private class RegistrationMailRequest
    {
        public string email;
        public string username;
    }

    [System.Serializable]
    private class PasswordResetRequest
    {
        public string email;
    }

    [System.Serializable]
    private class PasswordResetVerifyRequest
    {
        public string email;
        public string otp;
    }

    [System.Serializable]
    private class PasswordResetFinalizeRequest
    {
        public string email;
        public string username;
        public string otp;
        public string newPassword;
    }

    private void EnsureNetworkStack()
    {
        AutoAssignNetworkPrefabs();

        if (networkManager == null)
        {
            networkManager = FindAnyObjectByType<NetworkManager>();
        }

        if (networkManager == null)
        {
            GameObject networkRoot = new GameObject("NetworkManager");
            networkManager = networkRoot.AddComponent<NetworkManager>();
            transport = networkRoot.AddComponent<UnityTransport>();
            DontDestroyOnLoad(networkRoot);
        }
        else
        {
            transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
            }
        }

        if (networkManager != null)
        {
            EnsureNetworkConfigInitialized();
            networkManager.NetworkConfig.ConnectionApproval = false;
        }

        SetupNetworkPrefabs();
    }

    private void SetupNetworkPrefabs()
    {
        AutoAssignNetworkPrefabs();

        EnsureNetworkConfigInitialized();

        if (networkManager == null || networkManager.NetworkConfig == null)
        {
            return;
        }

        if (playerPrefab != null)
        {
            OnlinePlayerController playerController = playerPrefab.GetComponent<OnlinePlayerController>();
            if (playerController != null && playerController.bombPrefab == null && bombPrefab != null)
            {
                playerController.bombPrefab = bombPrefab;
            }

            networkManager.NetworkConfig.PlayerPrefab = playerPrefab.gameObject;
            if (!playerPrefabRegistered)
            {
                networkManager.AddNetworkPrefab(playerPrefab.gameObject);
                playerPrefabRegistered = true;
            }
        }

        if (bombPrefab != null && !bombPrefabRegistered)
        {
            OnlineBomb bomb = bombPrefab.GetComponent<OnlineBomb>();
            if (bomb != null && bomb.explosionPrefab == null && explosionPrefab != null)
            {
                bomb.explosionPrefab = explosionPrefab;
            }

            networkManager.AddNetworkPrefab(bombPrefab.gameObject);
            bombPrefabRegistered = true;
        }

        if (explosionPrefab != null && !explosionPrefabRegistered)
        {
            networkManager.AddNetworkPrefab(explosionPrefab.gameObject);
            explosionPrefabRegistered = true;
        }
    }

    private bool IsNetworkSessionActive()
    {
        return networkManager != null && networkManager.IsListening;
    }

    private void EnsureNetworkConfigInitialized()
    {
        if (networkManager == null)
        {
            return;
        }

        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }
    }

    private void AutoAssignNetworkPrefabs()
    {
#if UNITY_EDITOR
        if (playerPrefab == null)
        {
            playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefab/Player.prefab");
        }

        if (bombPrefab == null)
        {
            bombPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefab/Bomb.prefab");
        }

        if (explosionPrefab == null)
        {
            explosionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Prefab/Explosion.prefab");
        }
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxPlayers = Mathf.Clamp(maxPlayers, 2, 4);
        minPlayersToStart = Mathf.Clamp(minPlayersToStart, 2, maxPlayers);
        AutoAssignNetworkPrefabs();
    }

    private void Reset()
    {
        AutoAssignNetworkPrefabs();
    }
#endif
}
