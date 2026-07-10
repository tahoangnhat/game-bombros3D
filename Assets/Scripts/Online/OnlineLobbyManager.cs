using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OnlineLobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    private const string ReadyKey = "ready";
    private const string DisplayNameKey = "displayName";
    private const string ClientInstanceKey = "clientInstanceId";
    private const string MatchStartKey = "matchStart";
    private const string MatchMapIndexKey = "matchMapIndex";

    public static OnlineLobbyManager Instance { get; private set; }

    [Header("Lobby")]
    public string lobbyName = "BoomBros Online";
    [Range(1, 4)] public int maxPlayers = 4;
    [Range(1, 4)] public int minPlayersToStart = 1;
    public string joinLobbyCode = "";
    public string statusMessage = "Initializing...";

    [Header("Scenes")]
    public string gameSceneName = "GameScene";
    public string lobbySceneName = "LobbyScene";

    [Header("Map Random")]
    [SerializeField] private bool randomizeMapOnStart = true;
    [SerializeField, Min(1)] private int onlineRandomMapCount = 4;

    [Header("Network Prefabs")]
    public NetworkObject playerPrefab;
    public NetworkObject bombPrefab;
    public NetworkObject explosionPrefab;
    public NetworkObject buffPrefab;

    [Header("Runtime")]
    public bool autoInitialize = true;
    public bool allowAnonymousFallbackIfUsernamePasswordDisabled = true;

    [Header("Email / OTP")]
    public bool useMockEmailOtp = true;
    public string emailServiceBaseUrl = "";
    public float otpExpireSeconds = 300f;
    public float otpResendCooldownSeconds = 60f;

    private Lobby currentLobby;
    private string currentLobbyPlayerId = string.Empty;
    private readonly string localClientInstanceId = System.Guid.NewGuid().ToString("N");
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private bool servicesReady;
    private bool isHost;
    private float heartbeatTimer;
    private float pollTimer;
    private bool localReady;
    private bool isLoadingGame;
    private bool gameSceneLoaded;
    private Coroutine matchSpawnCoroutine;
    private bool isLobbyConnectionInProgress;
    private bool isFusionRoleTransitionInProgress;
    private bool isConnectingToMatch;
    private bool isStoppingFusionForLobby;
    private bool isReturningToLobby;
    private bool isReadyUpdateInProgress;
    private bool isUnityAuthenticationInProgress;
    private Task<bool> unityAuthenticationTask;
    private float lobbyRefreshGraceUntil = -1f;
    private float lastOtpRequestTime = -999f;
    private string pendingOtpEmail = string.Empty;
    private string pendingOtpCode = string.Empty;
    private float pendingOtpExpireAt = -1f;
    private readonly OnlinePlayerInputReader inputReader = new OnlinePlayerInputReader();

    private const float HeartbeatInterval = 15f;
    private const float PollInterval = 2f;
    private const float LobbyRefreshGraceSeconds = 3f;
    private const int JoinLobbyRetryAttempts = 3;
    private const int JoinLobbyRetryDelayMs = 1000;
    private const int FusionJoinRetryAttempts = 6;
    private const int FusionJoinRetryDelayMs = 1500;

    public bool ServicesReady => servicesReady;
    public bool IsHostLobby => isHost;
    public bool IsSignedIn => (TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn) || SpringAuthSession.IsSignedIn;
    public Lobby CurrentLobby => currentLobby;
    public string CurrentLobbyCode => currentLobby != null ? currentLobby.LobbyCode : string.Empty;
    public float OtpResendRemainingSeconds => Mathf.Max(0f, otpResendCooldownSeconds - (Time.unscaledTime - lastOtpRequestTime));
    public bool CanResendOtp => !string.IsNullOrEmpty(pendingOtpEmail) && OtpResendRemainingSeconds <= 0f;
    public bool CanUseLobbyActions => servicesReady
        && IsSignedIn
        && !isLobbyConnectionInProgress
        && !isUnityAuthenticationInProgress
        && !IsNetworkSessionActive();
    public bool IsLobbyActionInProgress => isLobbyConnectionInProgress || isUnityAuthenticationInProgress;
    public bool IsMatchActive => gameSceneLoaded;

    public bool CanToggleReady => servicesReady
        && currentLobby != null
        && !string.IsNullOrEmpty(GetCurrentLobbyPlayerId())
        && !isReadyUpdateInProgress
        && !isUnityAuthenticationInProgress;

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

    private string GetCurrentLobbyPlayerId()
    {
        if (!string.IsNullOrEmpty(currentLobbyPlayerId))
        {
            return currentLobbyPlayerId;
        }

        CaptureCurrentLobbyPlayerId();
        return currentLobbyPlayerId;
    }

    private void CaptureCurrentLobbyPlayerId()
    {
        if (currentLobby == null || currentLobby.Players == null)
        {
            currentLobbyPlayerId = string.Empty;
            return;
        }

        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            Player player = currentLobby.Players[i];
            if (player?.Data == null
                || !player.Data.TryGetValue(ClientInstanceKey, out PlayerDataObject instanceData)
                || instanceData == null
                || instanceData.Value != localClientInstanceId)
            {
                continue;
            }

            currentLobbyPlayerId = player.Id;
            return;
        }

        string authenticatedPlayerId = GetPlayerIdSafe();
        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            Player player = currentLobby.Players[i];
            if (player != null && player.Id == authenticatedPlayerId)
            {
                currentLobbyPlayerId = player.Id;
                return;
            }
        }

        if (isHost && !string.IsNullOrEmpty(currentLobby.HostId))
        {
            currentLobbyPlayerId = currentLobby.HostId;
            return;
        }

        currentLobbyPlayerId = string.Empty;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == lobbySceneName)
        {
            OnlineScenePresentation.FinalizeLobbySceneTransition(lobbySceneName, gameSceneName);
            // Tự động reset trạng thái sẵn sàng của Client khi quay trở lại sảnh
            if (currentLobby != null && !isHost)
            {
                _ = SetLocalReadyAsync(false);
            }
        }
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

        AutoAssignNetworkPrefabs();
        OnlineSessionState.IsOnlineSession = false;
        OnlineSessionState.ClearMatchMap();

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
        if (pollTimer >= PollInterval && Time.unscaledTime >= lobbyRefreshGraceUntil)
        {
            pollTimer = 0f;
            _ = RefreshLobbyAsync();
        }
    }

    public void SetJoinCodeInput(string value)
    {
        joinLobbyCode = NormalizeLobbyCodeInput(value);
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

    public string GetLobbyPlayerSlotText(int slotIndex)
    {
        if (currentLobby == null || currentLobby.Players == null || slotIndex < 0 || slotIndex >= maxPlayers)
        {
            return "Empty";
        }

        if (slotIndex >= currentLobby.Players.Count)
        {
            return "Empty";
        }

        Player player = currentLobby.Players[slotIndex];
        if (player == null)
        {
            return "Empty";
        }

        string name = GetPlayerDisplayName(player);
        bool isPlayerHost = player.Id == currentLobby.HostId;
        bool isPlayerReady = IsPlayerReady(player);

        string status = "";
        if (isPlayerHost)
        {
            status = "[Host]";
        }
        else if (isPlayerReady)
        {
            status = "[Ready]";
        }
        else
        {
            status = "[Not Ready]";
        }

        return $"{name} {status}";
    }

    public void CreateLobbyAndHost()
    {
        if (isLobbyConnectionInProgress || isUnityAuthenticationInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

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

    public void LoginAsGuest()
    {
        string guestName = SpringAuthSession.StartGuestSession();
        statusMessage = $"Logged in as {guestName}";
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

    public void JoinLobbyByInputCode()
    {
        if (isLobbyConnectionInProgress || isUnityAuthenticationInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

        _ = JoinLobbyAndConnectAsync(joinLobbyCode);
    }

    public void QuickJoin()
    {
        if (isLobbyConnectionInProgress || isUnityAuthenticationInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

        _ = QuickJoinAndConnectAsync();
    }

    public void ToggleReady()
    {
        if (!CanToggleReady)
        {
            return;
        }

        _ = ToggleReadyAsync();
    }

    public void StartGame()
    {
        if (isLoadingGame)
        {
            return;
        }

        _ = StartGameAsync();
    }

    public void LeaveSession()
    {
        _ = LeaveSessionAsync();
    }

    public void LeaveSessionAndGoToMainMenu(string mainMenuSceneName)
    {
        _ = LeaveSessionAndGoToMainMenuAsync(mainMenuSceneName);
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
            statusMessage = $"Registered & logged in as {BuildLocalDisplayName()}";

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
            statusMessage = $"Logged in as {BuildLocalDisplayName()}";
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
            auth.SignOut(clearCredentials: true);
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
        if (isLobbyConnectionInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            await InitializeServicesAsync();

            if (!servicesReady || !await EnsureUnityAuthenticationAsync())
            {
                statusMessage = "Please login first";
                return;
            }

            statusMessage = "Creating lobby...";
            currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayers,
                new CreateLobbyOptions
                {
                    Player = BuildLobbyPlayer(true),
                    Data = BuildMatchStartData(false, -1)
                });
            CaptureCurrentLobbyPlayerId();
            isHost = true;
            await SendHeartbeatAsync();
            await SetLocalReadyAsync(true);

            statusMessage = $"Lobby created. Code: {currentLobby.LobbyCode}";
            MarkLobbyRefreshGracePeriod();
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Host failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            isLobbyConnectionInProgress = false;
        }
    }

    private async Task JoinLobbyAndConnectAsync(string lobbyCode)
    {
        if (isLobbyConnectionInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            await InitializeServicesAsync();

            if (!servicesReady || !await EnsureUnityAuthenticationAsync())
            {
                statusMessage = "Please login first";
                return;
            }

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                statusMessage = "Enter a lobby code first";
                return;
            }

            statusMessage = "Joining lobby...";
            string trimmedLobbyCode = NormalizeLobbyCodeInput(lobbyCode);
            if (string.IsNullOrWhiteSpace(trimmedLobbyCode))
            {
                statusMessage = "Enter a valid lobby code";
                return;
            }

            await PruneStaleJoinedLobbiesAsync();

            currentLobby = await TryGetJoinedLobbyByCodeAsync(trimmedLobbyCode);
            if (currentLobby == null)
            {
                await LeaveAnyJoinedLobbiesAsync();
                currentLobby = await JoinLobbyByCodeWithRecoveryAsync(trimmedLobbyCode);
            }

            if (currentLobby == null)
            {
                return;
            }

            CaptureCurrentLobbyPlayerId();
            isHost = currentLobby.HostId == GetCurrentLobbyPlayerId();
            await SetLocalReadyAsync(isHost);

            if (currentLobby == null)
            {
                statusMessage = "Join failed: lobby session was lost during connect";
                return;
            }

            statusMessage = $"Joined lobby {currentLobby.LobbyCode}";
            MarkLobbyRefreshGracePeriod();
        }
        catch (LobbyServiceException ex) when (IsLobbyNotFoundError(ex))
        {
            statusMessage = BuildLobbyNotFoundMessage(NormalizeLobbyCodeInput(lobbyCode));
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Join failed: {ex.Message}";
            Debug.LogWarning($"Join lobby failed: {ex.Message}");
        }
        finally
        {
            isLobbyConnectionInProgress = false;
        }
    }

    private async Task<Lobby> JoinLobbyByCodeWithRecoveryAsync(string lobbyCode)
    {
        for (int attempt = 0; attempt < JoinLobbyRetryAttempts; attempt++)
        {
            try
            {
                return await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, new JoinLobbyByCodeOptions
                {
                    Player = BuildLobbyPlayer(false)
                });
            }
            catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyConflict)
            {
                await LeaveAnyJoinedLobbiesAsync();
            }
            catch (LobbyServiceException ex) when (IsLobbyNotFoundError(ex))
            {
                if (attempt < JoinLobbyRetryAttempts - 1)
                {
                    statusMessage = $"Lobby not ready, retrying ({attempt + 2}/{JoinLobbyRetryAttempts})...";
                    await Task.Delay(JoinLobbyRetryDelayMs);
                    continue;
                }

                statusMessage = BuildLobbyNotFoundMessage(lobbyCode);
                return null;
            }
            catch (LobbyServiceException ex) when (IsTransientLobbyServiceError(ex))
            {
                if (attempt < JoinLobbyRetryAttempts - 1 && await EnsureUnityAuthenticationAsync())
                {
                    statusMessage = $"Lobby service busy, retrying ({attempt + 2}/{JoinLobbyRetryAttempts})...";
                    await Task.Delay(JoinLobbyRetryDelayMs);
                    continue;
                }

                statusMessage = "Lobby service unavailable. Please try again.";
                Debug.LogWarning($"Join lobby transient error ({ex.Reason}): {ex.Message}");
                return null;
            }
        }

        statusMessage = BuildLobbyNotFoundMessage(lobbyCode);
        return null;
    }

    private async Task<Lobby> TryGetJoinedLobbyByCodeAsync(string lobbyCode)
    {
        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            return null;
        }

        List<string> joinedLobbyIds = await GetJoinedLobbyIdsSafeAsync();
        if (joinedLobbyIds.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < joinedLobbyIds.Count; i++)
        {
            string joinedLobbyId = joinedLobbyIds[i];
            if (string.IsNullOrWhiteSpace(joinedLobbyId))
            {
                continue;
            }

            try
            {
                Lobby joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobbyId);
                if (joinedLobby != null && string.Equals(joinedLobby.LobbyCode, lobbyCode, System.StringComparison.OrdinalIgnoreCase))
                {
                    return joinedLobby;
                }
            }
            catch (LobbyServiceException ex) when (IsStaleLobbyLookupError(ex))
            {
                await TryRemovePlayerFromLobbyAsync(joinedLobbyId);
            }
            catch (System.Exception ex) when (IsStaleLobbyLookupError(ex))
            {
                await TryRemovePlayerFromLobbyAsync(joinedLobbyId);
            }
        }

        return null;
    }

    private async Task PruneStaleJoinedLobbiesAsync()
    {
        try
        {
            List<string> joinedLobbyIds = await GetJoinedLobbyIdsSafeAsync();
            if (joinedLobbyIds.Count == 0)
            {
                return;
            }

            for (int i = 0; i < joinedLobbyIds.Count; i++)
            {
                string joinedLobbyId = joinedLobbyIds[i];
                if (string.IsNullOrWhiteSpace(joinedLobbyId))
                {
                    continue;
                }

                try
                {
                    await LobbyService.Instance.GetLobbyAsync(joinedLobbyId);
                }
                catch (System.Exception ex) when (IsStaleLobbyLookupError(ex))
                {
                    await TryRemovePlayerFromLobbyAsync(joinedLobbyId);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Prune stale lobbies skipped: {ex.Message}");
        }
    }

    private async Task<List<string>> GetJoinedLobbyIdsSafeAsync()
    {
        if (!await EnsureUnityAuthenticationAsync())
        {
            return new List<string>();
        }

        try
        {
            List<string> joinedLobbyIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
            return joinedLobbyIds ?? new List<string>();
        }
        catch (LobbyServiceException ex)
        {
            Debug.LogWarning($"GetJoinedLobbies skipped ({ex.Reason}): {ex.Message}");
            return new List<string>();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"GetJoinedLobbies skipped: {ex.Message}");
            return new List<string>();
        }
    }

    private async Task TryRemovePlayerFromLobbyAsync(string lobbyId)
    {
        string playerId = GetPlayerIdSafe();
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrWhiteSpace(lobbyId))
        {
            return;
        }

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);
        }
        catch (LobbyServiceException ex) when (IsStaleLobbyLookupError(ex))
        {
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Could not remove stale lobby membership ({lobbyId}): {ex.Message}");
        }
    }

    private async Task LeaveAnyJoinedLobbiesAsync()
    {
        string playerId = GetPlayerIdSafe();
        if (string.IsNullOrEmpty(playerId))
        {
            return;
        }

        List<string> joinedLobbyIds = await GetJoinedLobbyIdsSafeAsync();
        for (int i = 0; i < joinedLobbyIds.Count; i++)
        {
            string joinedLobbyId = joinedLobbyIds[i];
            if (string.IsNullOrWhiteSpace(joinedLobbyId))
            {
                continue;
            }

            await TryRemovePlayerFromLobbyAsync(joinedLobbyId);
        }

        if (currentLobby != null)
        {
            currentLobby = null;
        }
    }

    private static bool IsTransientLobbyServiceError(LobbyServiceException ex)
    {
        if (ex == null)
        {
            return false;
        }

        return ex.Reason == LobbyExceptionReason.Unknown
            || ex.Reason == LobbyExceptionReason.NetworkError
            || ex.Reason == LobbyExceptionReason.ServiceUnavailable;
    }

    private static bool IsLobbyNotFoundError(LobbyServiceException ex)
    {
        return ex != null && ex.Reason == LobbyExceptionReason.EntityNotFound;
    }

    private static bool IsStaleLobbyLookupError(System.Exception ex)
    {
        if (ex is LobbyServiceException lobbyEx)
        {
            return lobbyEx.Reason == LobbyExceptionReason.EntityNotFound
                || lobbyEx.Reason == LobbyExceptionReason.Unknown;
        }

        return ex != null && ex.Message != null
            && (ex.Message.Contains("404") || ex.Message.Contains("not found", System.StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildLobbyNotFoundMessage(string trimmedLobbyCode)
    {
        return string.IsNullOrWhiteSpace(trimmedLobbyCode)
            ? "Lobby not found. Ask the host to create a new room."
            : $"Lobby '{trimmedLobbyCode}' not found. Check the code or ask the host to create a new room.";
    }

    private static string NormalizeLobbyCodeInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmedValue = value.Trim();
        string lastToken = string.Empty;
        int tokenStart = -1;

        for (int i = 0; i < trimmedValue.Length; i++)
        {
            char current = trimmedValue[i];
            bool isCodeChar = char.IsLetterOrDigit(current);

            if (isCodeChar)
            {
                if (tokenStart < 0)
                {
                    tokenStart = i;
                }

                continue;
            }

            if (tokenStart >= 0)
            {
                lastToken = trimmedValue.Substring(tokenStart, i - tokenStart);
                tokenStart = -1;
            }
        }

        if (tokenStart >= 0)
        {
            lastToken = trimmedValue.Substring(tokenStart);
        }

        return !string.IsNullOrWhiteSpace(lastToken) ? lastToken : trimmedValue;
    }

    private async Task QuickJoinAndConnectAsync()
    {
        if (isLobbyConnectionInProgress)
        {
            statusMessage = "Lobby action already in progress...";
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            await InitializeServicesAsync();

            if (!servicesReady || !await EnsureUnityAuthenticationAsync())
            {
                statusMessage = "Please login first";
                return;
            }

            statusMessage = "Quick joining...";
            currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(new QuickJoinLobbyOptions
            {
                Player = BuildLobbyPlayer(false)
            });
            CaptureCurrentLobbyPlayerId();

            isHost = currentLobby.HostId == GetCurrentLobbyPlayerId();
            await SetLocalReadyAsync(isHost);
            statusMessage = $"Quick joined lobby {currentLobby.LobbyCode}";
            MarkLobbyRefreshGracePeriod();
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Quick join failed: {ex.Message}";
            Debug.LogException(ex);
        }
        finally
        {
            isLobbyConnectionInProgress = false;
        }
    }

    private async Task StartGameAsync()
    {
        if (isLoadingGame || !isHost || currentLobby == null)
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

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            statusMessage = "Game scene name is empty";
            return;
        }

        isLoadingGame = true;
        gameSceneLoaded = false;
        statusMessage = "Starting Fusion host session...";
        int selectedThemeIndex = SelectMatchThemeIndex();
        OnlineSessionState.SelectedThemeIndex = selectedThemeIndex;

        if (!await StartFusionSessionAsync(GameMode.Host, currentLobby.LobbyCode))
        {
            isLoadingGame = false;
            OnlineSessionState.ClearMatchMap();
            return;
        }

        currentLobby = await LobbyService.Instance.UpdateLobbyAsync(
            currentLobby.Id,
            new UpdateLobbyOptions { Data = BuildMatchStartData(true, selectedThemeIndex) });

        statusMessage = "Waiting for players to connect...";
        float connectDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < connectDeadline
            && CountActiveFusionPlayers() < playerCount)
        {
            await Task.Delay(200);
        }

        OnlineSessionState.IsOnlineSession = true;
        statusMessage = $"Starting {gameSceneName}...";

        // Multiple peer mode (ParrelSync / Fusion PeerMode.Multiple) does not support LocalPhysicsMode.Physics3D.
        await runner.LoadScene(gameSceneName, LoadSceneMode.Single, LocalPhysicsMode.None, true);
    }

    private int CountActiveFusionPlayers()
    {
        if (runner == null || !runner.IsRunning)
        {
            return 0;
        }

        int count = 0;
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            count++;
        }

        return count;
    }

    public void ReturnToLobby()
    {
        _ = ReturnToLobbyAsync();
    }

    private async Task ReturnToLobbyAsync()
    {
        if (isReturningToLobby || !isHost || runner == null || !runner.IsRunning)
        {
            return;
        }

        isReturningToLobby = true;
        OnlineSessionState.IsOnlineSession = false;
        OnlineSessionState.ClearMatchMap();
        isLoadingGame = false;
        gameSceneLoaded = false;
        statusMessage = $"Returning to {lobbySceneName}...";

        await runner.LoadScene(lobbySceneName, LoadSceneMode.Single, LocalPhysicsMode.None, true);

        if (currentLobby != null)
        {
            currentLobby = await LobbyService.Instance.UpdateLobbyAsync(
                currentLobby.Id,
                new UpdateLobbyOptions { Data = BuildMatchStartData(false, -1) });
        }

        // Clients close their runners after their lobby scene finishes loading.
        // Wait for them to leave before the host closes the room.
        float disconnectDeadline = Time.realtimeSinceStartup + 5f;
        while (CountActiveFusionPlayers() > 1
            && Time.realtimeSinceStartup < disconnectDeadline)
        {
            await Task.Delay(100);
        }
        Debug.Log($"[Fusion Return] Host closing runner with {CountActiveFusionPlayers()} active player(s).");
        await ShutdownAndDisposeRunnerAsync();
        await LoadLobbySceneLocallyAsync();
        statusMessage = "Returned to lobby";
        isReturningToLobby = false;
    }

    private async Task LeaveSessionAsync()
    {
        if (isLobbyConnectionInProgress)
        {
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            if (currentLobby != null)
            {
                if (isHost)
                {
                    Player nextHost = FindNextLobbyHost();
                    if (nextHost != null)
                    {
                        currentLobby = await LobbyService.Instance.UpdateLobbyAsync(
                            currentLobby.Id,
                            new UpdateLobbyOptions { HostId = nextHost.Id });

                        string playerId = GetCurrentLobbyPlayerId();
                        if (!string.IsNullOrEmpty(playerId))
                        {
                            await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, playerId);
                        }
                    }
                    else
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                    }
                }
                else
                {
                    string playerId = GetCurrentLobbyPlayerId();
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

        await ShutdownAndDisposeRunnerAsync();

        ClearLobbySessionState("Left session");
    }

    private Player FindNextLobbyHost()
    {
        string localPlayerId = GetCurrentLobbyPlayerId();
        if (currentLobby == null || currentLobby.Players == null)
        {
            return null;
        }

        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            Player player = currentLobby.Players[i];
            if (player != null
                && !string.IsNullOrEmpty(player.Id)
                && player.Id != localPlayerId)
            {
                return player;
            }
        }

        return null;
    }

    private async Task LeaveSessionAndGoToMainMenuAsync(string mainMenuSceneName)
    {
        await LeaveSessionAsync();

        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private async Task SendHeartbeatAsync()
    {
        if (currentLobby == null || !isHost || string.IsNullOrWhiteSpace(currentLobby.Id))
        {
            return;
        }

        if (!await EnsureUnityAuthenticationAsync())
        {
            return;
        }

        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
        }
        catch (LobbyServiceException ex) when (IsFatalLobbyRefreshError(ex))
        {
            ClearLobbySessionState("Host lobby expired. Please create a new room.");
            Debug.LogWarning($"Host lobby heartbeat failed ({ex.Reason}): {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Host lobby heartbeat skipped: {ex.Message}");
        }
    }

    private async Task RefreshLobbyAsync()
    {
        if (currentLobby == null || isLobbyConnectionInProgress)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentLobby.Id))
        {
            return;
        }

        if (!await EnsureUnityAuthenticationAsync())
        {
            return;
        }

        try
        {
            string previousHostId = currentLobby.HostId;
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            CaptureCurrentLobbyPlayerId();
            localReady = IsLocalPlayerReady(currentLobby);

            string localPlayerId = GetCurrentLobbyPlayerId();
            bool localPlayerIsHost = !string.IsNullOrEmpty(localPlayerId)
                && localPlayerId == currentLobby.HostId;
            isHost = localPlayerIsHost;

            if (previousHostId != currentLobby.HostId)
            {
                statusMessage = localPlayerIsHost
                    ? "You are now the lobby host."
                    : "Lobby host changed.";

                if (localPlayerIsHost && !localReady)
                {
                    await SetLocalReadyAsync(true);
                }
            }

            if (!localPlayerIsHost
                && IsMatchStartRequested(currentLobby)
                && (runner == null || !runner.IsRunning))
            {
                ApplyMatchMapFromLobby(currentLobby);
                _ = ConnectToStartedMatchAsync();
            }
            else if (!localPlayerIsHost
                && !IsMatchStartRequested(currentLobby)
                && runner != null
                && runner.IsRunning
                && SceneManager.GetActiveScene().name == lobbySceneName)
            {
                _ = StopFusionForLobbyAsync();
            }
        }
        catch (LobbyServiceException ex)
        {
            if (IsFatalLobbyRefreshError(ex))
            {
                ClearLobbySessionState(ex.Reason == LobbyExceptionReason.EntityNotFound
                    ? "Lobby no longer exists"
                    : "Lobby refresh failed");
                Debug.LogWarning($"Lobby refresh ended session ({ex.Reason}): {ex.Message}");
                return;
            }

            Debug.LogWarning($"Lobby refresh skipped ({ex.Reason}): {ex.Message}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Lobby refresh skipped: {ex.Message}");
        }
    }

    private void MarkLobbyRefreshGracePeriod()
    {
        lobbyRefreshGraceUntil = Time.unscaledTime + LobbyRefreshGraceSeconds;
    }

    private async Task ConnectToStartedMatchAsync()
    {
        if (isConnectingToMatch
            || isLobbyConnectionInProgress
            || currentLobby == null
            || gameSceneLoaded)
        {
            return;
        }

        isConnectingToMatch = true;
        try
        {
            ApplyMatchMapFromLobby(currentLobby);
            isLoadingGame = true;
            statusMessage = "Connecting to the match...";
            string lobbyCode = currentLobby.LobbyCode;
            if (await StartFusionSessionAsync(GameMode.Client, lobbyCode))
            {
                statusMessage = "Connected. Waiting for host to load the game...";
            }
            else
            {
                isLoadingGame = false;
            }
        }
        finally
        {
            isConnectingToMatch = false;
        }
    }

    private async Task StopFusionForLobbyAsync()
    {
        if (isStoppingFusionForLobby)
        {
            return;
        }

        isStoppingFusionForLobby = true;
        try
        {
            OnlineSessionState.IsOnlineSession = false;
            OnlineSessionState.ClearMatchMap();
            await ShutdownAndDisposeRunnerAsync();
            await LoadLobbySceneLocallyAsync();
        }
        finally
        {
            isStoppingFusionForLobby = false;
        }
    }

    private async Task LoadLobbySceneLocallyAsync()
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            return;
        }

        // Fusion unloads its managed scene when the runner shuts down and leaves
        // FusionSceneManager_TempEmptyScene active. Load a normal Unity scene
        // after shutdown so the lobby keeps its camera, canvas, and EventSystem.
        await Task.Yield();

        Scene activeScene = SceneManager.GetActiveScene();
        bool hasLocalLobbyScene = activeScene.IsValid()
            && activeScene.isLoaded
            && string.Equals(
                activeScene.name,
                lobbySceneName,
                System.StringComparison.Ordinal);

        if (!hasLocalLobbyScene)
        {
            AsyncOperation loadOperation =
                SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogError($"[Fusion Return] Could not load {lobbySceneName} locally.");
                return;
            }

            while (!loadOperation.isDone)
            {
                await Task.Yield();
            }
        }

        OnlineScenePresentation.FinalizeLobbySceneTransition(lobbySceneName, gameSceneName);
        Debug.Log($"[Fusion Return] Local {lobbySceneName} is ready.");
    }

    private void ClearLobbySessionState(string message)
    {
        currentLobby = null;
        currentLobbyPlayerId = string.Empty;
        isHost = false;
        localReady = false;
        isLoadingGame = false;
        gameSceneLoaded = false;
        lobbyRefreshGraceUntil = -1f;
        isLobbyConnectionInProgress = false;
        isFusionRoleTransitionInProgress = false;
        isConnectingToMatch = false;
        isStoppingFusionForLobby = false;
        isReturningToLobby = false;
        OnlineSessionState.IsOnlineSession = false;
        OnlineSessionState.ClearMatchMap();
        statusMessage = message;

        if (matchSpawnCoroutine != null)
        {
            StopCoroutine(matchSpawnCoroutine);
            matchSpawnCoroutine = null;
        }
    }

    private static bool IsFatalLobbyRefreshError(LobbyServiceException ex)
    {
        return ex != null && ex.Reason == LobbyExceptionReason.EntityNotFound;
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
        if (isReadyUpdateInProgress)
        {
            return;
        }

        isReadyUpdateInProgress = true;
        try
        {
            if (!await EnsureUnityAuthenticationAsync())
            {
                statusMessage = "Please login first";
                return;
            }

            string playerId = GetCurrentLobbyPlayerId();
            if (currentLobby == null || string.IsNullOrEmpty(playerId))
            {
                return;
            }

            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = BuildLobbyPlayerData(ready)
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
        finally
        {
            isReadyUpdateInProgress = false;
        }
    }

    private Player BuildLobbyPlayer(bool ready)
    {
        return new Player(
            // Lobby resolves this from the access token. A cached explicit ID can
            // mismatch immediately after switching Authentication profiles.
            id: null,
            data: BuildLobbyPlayerData(ready));
    }

    private Dictionary<string, PlayerDataObject> BuildLobbyPlayerData(bool ready)
    {
        return new Dictionary<string, PlayerDataObject>
        {
            { ReadyKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0") },
            { DisplayNameKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, BuildLocalDisplayName()) },
            { ClientInstanceKey, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, localClientInstanceId) }
        };
    }

    private static Dictionary<string, DataObject> BuildMatchStartData(bool start, int themeIndex)
    {
        return new Dictionary<string, DataObject>
        {
            {
                MatchStartKey,
                new DataObject(DataObject.VisibilityOptions.Member, start ? "1" : "0")
            },
            {
                MatchMapIndexKey,
                new DataObject(DataObject.VisibilityOptions.Member, themeIndex.ToString())
            }
        };
    }

    private static bool IsMatchStartRequested(Lobby lobby)
    {
        return lobby != null
            && lobby.Data != null
            && lobby.Data.TryGetValue(MatchStartKey, out DataObject startData)
            && startData != null
            && startData.Value == "1";
    }

    private int SelectMatchThemeIndex()
    {
        if (!randomizeMapOnStart || onlineRandomMapCount <= 1)
        {
            return 0;
        }

        return Random.Range(0, onlineRandomMapCount);
    }

    private static void ApplyMatchMapFromLobby(Lobby lobby)
    {
        OnlineSessionState.SelectedThemeIndex = GetMatchMapIndex(lobby);
    }

    private static int GetMatchMapIndex(Lobby lobby)
    {
        if (lobby == null
            || lobby.Data == null
            || !lobby.Data.TryGetValue(MatchMapIndexKey, out DataObject mapData)
            || mapData == null
            || string.IsNullOrWhiteSpace(mapData.Value))
        {
            return -1;
        }

        return int.TryParse(mapData.Value, out int themeIndex) ? themeIndex : -1;
    }

    private bool AreAllPlayersReady()
    {
        if (currentLobby == null || currentLobby.Players == null || currentLobby.Players.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < currentLobby.Players.Count; i++)
        {
            Player player = currentLobby.Players[i];
            if (player != null && player.Id == currentLobby.HostId)
            {
                continue;
            }

            if (!IsPlayerReady(player))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsLocalPlayerReady(Lobby lobby)
    {
        string localPlayerId = GetCurrentLobbyPlayerId();
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

    public string BuildLocalDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(SpringAuthSession.Username))
        {
            return SpringAuthSession.Username.Trim();
        }

        if (SpringAuthSession.Profile != null && !string.IsNullOrWhiteSpace(SpringAuthSession.Profile.username))
        {
            return SpringAuthSession.Profile.username.Trim();
        }

        string playerId = GetPlayerIdSafe();
        if (string.IsNullOrEmpty(playerId))
        {
            return "Player";
        }

        int shortLength = Mathf.Min(6, playerId.Length);
        return $"P-{playerId.Substring(0, shortLength)}";
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

    private async Task<bool> StartFusionSessionAsync(GameMode gameMode, string sessionName)
    {
        AutoAssignNetworkPrefabs();

        string normalizedSessionName = NormalizeLobbyCodeInput(sessionName);
        if (string.IsNullOrWhiteSpace(normalizedSessionName))
        {
            statusMessage = "Fusion session name is invalid";
            return false;
        }

        bool canActAsJoiningPeer = gameMode == GameMode.Client
            || gameMode == GameMode.AutoHostOrClient;
        int maxAttempts = canActAsJoiningPeer ? FusionJoinRetryAttempts : 1;
        GameMode attemptGameMode = gameMode;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (attempt > 0)
            {
                statusMessage = attemptGameMode == GameMode.Host
                    ? $"Taking over as Fusion host ({attempt + 1}/{maxAttempts})..."
                    : $"Waiting for host Fusion session ({attempt + 1}/{maxAttempts})...";
                await Task.Delay(FusionJoinRetryDelayMs);
            }

            if (!await RecreateFusionRunnerAsync())
            {
                statusMessage = "Could not reset Fusion runner";
                return false;
            }

            runner.ProvideInput = true;
            runner.RemoveCallbacks(this);
            runner.AddCallbacks(this);

            StartGameResult result = await runner.StartGame(new StartGameArgs
            {
                GameMode = attemptGameMode,
                SessionName = normalizedSessionName,
                SceneManager = sceneManager,
                PlayerCount = maxPlayers
            });

            if (result.Ok)
            {
                OnlineSessionState.IsOnlineSession = true;
                if (attemptGameMode == GameMode.Host
                    || (attemptGameMode == GameMode.AutoHostOrClient && runner.IsServer))
                {
                    isHost = true;
                }

                SetupNetworkPrefabs();
                return true;
            }

            bool fusionRoomMissing = result.ShutdownReason == ShutdownReason.GameNotFound;
            if (gameMode == GameMode.Client && fusionRoomMissing)
            {
                bool localPlayerBecameLobbyHost = await RefreshLocalLobbyHostRoleAsync();
                attemptGameMode = localPlayerBecameLobbyHost ? GameMode.Host : GameMode.Client;
            }
            else if (gameMode == GameMode.Client
                && attemptGameMode == GameMode.Host
                && (result.ShutdownReason == ShutdownReason.GameIdAlreadyExists
                    || result.ShutdownReason == ShutdownReason.ServerInRoom))
            {
                // Another peer completed Fusion migration first. Join that peer as a client.
                attemptGameMode = GameMode.Client;
            }

            bool transientMigrationFailure = fusionRoomMissing
                || result.ShutdownReason == ShutdownReason.GameIdAlreadyExists
                || result.ShutdownReason == ShutdownReason.ServerInRoom
                || result.ShutdownReason == ShutdownReason.DisconnectedByPluginLogic
                || result.ShutdownReason == ShutdownReason.ConnectionTimeout
                || result.ShutdownReason == ShutdownReason.OperationTimeout
                || result.ShutdownReason == ShutdownReason.Error;
            bool canRetry = canActAsJoiningPeer
                && transientMigrationFailure
                && attempt < maxAttempts - 1;
            if (canRetry)
            {
                continue;
            }

            statusMessage = BuildFusionStartFailureMessage(gameMode, result);
            Debug.LogError(
                $"Fusion start failed. Requested={gameMode}, Attempted={attemptGameMode}, " +
                $"Reason={result.ShutdownReason}, Error={result.ErrorMessage}");
            return false;
        }

        statusMessage = "Fusion session failed: host room not found";
        return false;
    }

    private async Task<bool> RefreshLocalLobbyHostRoleAsync()
    {
        if (currentLobby == null
            || string.IsNullOrWhiteSpace(currentLobby.Id)
            || !await EnsureUnityAuthenticationAsync())
        {
            return false;
        }

        try
        {
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            CaptureCurrentLobbyPlayerId();
            string localPlayerId = GetCurrentLobbyPlayerId();
            isHost = !string.IsNullOrEmpty(localPlayerId)
                && localPlayerId == currentLobby.HostId;
            return isHost;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Could not refresh lobby host during Fusion reconnect: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RecreateFusionRunnerAsync()
    {
        if (!await ShutdownAndDisposeRunnerAsync())
        {
            return false;
        }

        GameObject networkRoot = new GameObject("FusionRunner");
        runner = networkRoot.AddComponent<NetworkRunner>();
        sceneManager = networkRoot.AddComponent<NetworkSceneManagerDefault>();
        networkRoot.AddComponent<NetworkObjectProviderDefault>();
        DontDestroyOnLoad(networkRoot);

        return runner != null && sceneManager != null;
    }

    private async Task<bool> ShutdownAndDisposeRunnerAsync()
    {
        if (runner == null)
        {
            return true;
        }

        NetworkRunner oldRunner = runner;
        GameObject oldRunnerObject = oldRunner.gameObject;

        oldRunner.RemoveCallbacks(this);

        if (oldRunner.IsRunning)
        {
            await oldRunner.Shutdown();

            for (int i = 0; i < 40; i++)
            {
                if (!oldRunner.IsRunning)
                {
                    break;
                }

                await Task.Delay(50);
            }
        }

        bool shutdownComplete = !oldRunner.IsRunning;
        if (runner == oldRunner)
        {
            runner = null;
            sceneManager = null;
        }

        if (oldRunnerObject != null)
        {
            Destroy(oldRunnerObject);
        }

        return shutdownComplete;
    }

    private static string BuildFusionStartFailureMessage(GameMode gameMode, StartGameResult result)
    {
        if (result.ShutdownReason == ShutdownReason.GameNotFound)
        {
            if (gameMode == GameMode.Client)
            {
                return "Fusion room not found. Make sure the host clicked Create Lobby & Host and wait a few seconds before joining.";
            }

            return "Fusion cloud session failed. Open Tools > Fusion > Fusion Hub and verify App Id Fusion in PhotonAppSettings.";
        }

        return $"Fusion session failed: {result.ShutdownReason}";
    }

    private async Task<bool> EnsureUnityAuthenticationAsync()
    {
        if (unityAuthenticationTask != null && !unityAuthenticationTask.IsCompleted)
        {
            statusMessage = "Unity sign-in already in progress...";
            return await unityAuthenticationTask;
        }

        unityAuthenticationTask = EnsureUnityAuthenticationCoreAsync();
        try
        {
            return await unityAuthenticationTask;
        }
        finally
        {
            if (unityAuthenticationTask != null && unityAuthenticationTask.IsCompleted)
            {
                unityAuthenticationTask = null;
            }
        }
    }

    private async Task<bool> EnsureUnityAuthenticationCoreAsync()
    {
        if (!servicesReady)
        {
            return false;
        }

        if (!SpringAuthSession.IsSignedIn || !TryGetAuthService(out IAuthenticationService auth))
        {
            return false;
        }

        string springUser = GetSpringUsername();
        if (string.IsNullOrEmpty(springUser))
        {
            return false;
        }

        string profile = BuildUnityAuthProfile(springUser);

        isUnityAuthenticationInProgress = true;
        try
        {
            if (auth.IsSignedIn && string.Equals(auth.Profile, profile, System.StringComparison.Ordinal))
            {
                if (auth.IsAuthorized)
                {
                    return true;
                }

                if (auth.SessionTokenExists)
                {
                    statusMessage = "Refreshing Unity session...";
                    await auth.SignInAnonymouslyAsync();
                    return auth.IsAuthorized;
                }

                auth.SignOut(clearCredentials: true);
            }
            else if (auth.IsSignedIn)
            {
                auth.SignOut(clearCredentials: true);
            }

            auth.SwitchProfile(profile);
            statusMessage = auth.SessionTokenExists
                ? "Restoring Unity session..."
                : "Signing in to Unity Services...";
            await auth.SignInAnonymouslyAsync();

            if (!auth.IsAuthorized)
            {
                await Task.Delay(250);
                if (!auth.IsAuthorized && auth.SessionTokenExists)
                {
                    await auth.SignInAnonymouslyAsync();
                }
            }

            return auth.IsAuthorized;
        }
        catch (System.Exception ex)
        {
            statusMessage = $"Unity sign-in failed: {ex.Message}";
            Debug.LogException(ex);
            return false;
        }
        finally
        {
            isUnityAuthenticationInProgress = false;
        }
    }

    private static string GetSpringUsername()
    {
        if (!string.IsNullOrWhiteSpace(SpringAuthSession.Username))
        {
            return SpringAuthSession.Username.Trim().ToLowerInvariant();
        }

        if (SpringAuthSession.Profile != null && !string.IsNullOrWhiteSpace(SpringAuthSession.Profile.username))
        {
            return SpringAuthSession.Profile.username.Trim().ToLowerInvariant();
        }

        return string.Empty;
    }

    private static string BuildUnityAuthProfile(string springUser)
    {
        StringBuilder builder = new StringBuilder("s_");
        for (int i = 0; i < springUser.Length && builder.Length < 30; i++)
        {
            char current = springUser[i];
            if (char.IsLetterOrDigit(current) || current == '-' || current == '_')
            {
                builder.Append(current);
            }
            else
            {
                builder.Append('_');
            }
        }

        return builder.ToString();
    }

    private void SetupNetworkPrefabs()
    {
        AutoAssignNetworkPrefabs();
        OnlineNetworkPrefabBinder.SetupPrefabLinks(playerPrefab, bombPrefab, explosionPrefab);
    }

    private bool IsNetworkSessionActive()
    {
        return runner != null && runner.IsRunning;
    }

    private void AutoAssignNetworkPrefabs()
    {
        OnlineNetworkPrefabBinder.AutoAssign(ref playerPrefab, ref bombPrefab, ref explosionPrefab, ref buffPrefab);
    }

    private void SpawnActivePlayersFromGameScene()
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        OnlineGameSpawner spawner = FindAnyObjectByType<OnlineGameSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            statusMessage = "Game spawner is missing";
            Debug.LogError("Cannot spawn players: OnlineGameSpawner was not found in the game scene.");
            return;
        }

        spawner.SpawnActivePlayers(runner);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (runner == null || string.IsNullOrWhiteSpace(gameSceneName))
        {
            return;
        }

        if (gameSceneLoaded)
        {
            isReturningToLobby = true;
            Debug.Log("[Fusion Return] Scene load started from an active match.");
        }

        gameSceneLoaded = false;

        if (matchSpawnCoroutine != null)
        {
            StopCoroutine(matchSpawnCoroutine);
            matchSpawnCoroutine = null;
        }

        if (isLoadingGame)
        {
            OnlineSessionState.IsOnlineSession = true;
        }

        statusMessage = isReturningToLobby
            ? $"Returning to {lobbySceneName}..."
            : $"Loading {gameSceneName}...";
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner == null)
        {
            return;
        }

        if (isReturningToLobby
            && OnlineScenePresentation.IsLobbySceneLoaded(lobbySceneName))
        {
            Debug.Log($"[Fusion Return] Lobby scene loaded. Server={runner.IsServer}.");
            OnlineScenePresentation.FinalizeLobbySceneTransition(lobbySceneName, gameSceneName);
            OnlineSessionState.IsOnlineSession = false;
            isLoadingGame = false;
            gameSceneLoaded = false;
            statusMessage = "Returned to lobby";

            bool shouldStopClientRunner = !runner.IsServer;
            if (shouldStopClientRunner)
            {
                isReturningToLobby = false;
                Debug.Log("[Fusion Return] Client is shutting down its runner.");
                _ = StopFusionForLobbyAsync();
            }
            return;
        }

        if (!OnlineScenePresentation.IsGameSceneLoaded(gameSceneName))
        {
            return;
        }

        MatchGridState.Reset();
        inputReader.ResetBombState();
        OnlineScenePresentation.FinalizeGameSceneTransition(gameSceneName, lobbySceneName);

        if (runner.IsServer && isLoadingGame && matchSpawnCoroutine == null)
        {
            matchSpawnCoroutine = StartCoroutine(SpawnPlayersAfterSceneReady());
        }
        else if (!runner.IsServer)
        {
            EnsureMatchResultPopup();
            gameSceneLoaded = true;
            isLoadingGame = false;
            isReturningToLobby = false;
            statusMessage = "Match started";
        }
    }

    private IEnumerator SpawnPlayersAfterSceneReady()
    {
        const float timeoutSeconds = 5f;
        float elapsed = 0f;

        while (elapsed < timeoutSeconds)
        {
            ThemeManager themeManager = ThemeManager.Instance;
            if (themeManager != null && themeManager.IsLevelReady)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        OnlineScenePresentation.FinalizeGameSceneTransition(gameSceneName, lobbySceneName);
        SpawnActivePlayersFromGameScene();
        EnsureMatchResultPopup();
        gameSceneLoaded = true;
        isLoadingGame = false;
        isReturningToLobby = false;
        statusMessage = "Match started";
        matchSpawnCoroutine = null;
    }

    private void EnsureMatchResultPopup()
    {
        OnlineMatchResultPopup popup = FindAnyObjectByType<OnlineMatchResultPopup>(FindObjectsInactive.Include);
        if (popup == null)
        {
            Debug.LogWarning("[Match Result] OnlineMatchResultPopup was not found in GameScene.");
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        inputReader.SetInput(input);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        // Lobby recovery is coordinated through Unity Lobby HostId so only one
        // peer can recreate the Fusion host. Do not start a second runner here.
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) => OnInput(runner, input);
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner) => OnSceneLoadDone(runner);
    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) => OnObjectExitAOI(runner, obj, player);
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) => OnObjectEnterAOI(runner, obj, player);
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) => OnPlayerJoined(runner, player);
    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) => OnInputMissing(runner, player, input);
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) => OnShutdown(runner, shutdownReason);
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) => OnConnectedToServer(runner);
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) => OnDisconnectedFromServer(runner, reason);
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) => OnConnectRequest(runner, request, token);
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) => OnConnectFailed(runner, remoteAddress, reason);
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) => OnUserSimulationMessage(runner, message);
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) => OnSessionListUpdated(runner, sessionList);
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) => OnCustomAuthenticationResponse(runner, data);
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) => OnHostMigration(runner, hostMigrationToken);
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) => OnReliableDataReceived(runner, player, key, data);
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) => OnReliableDataProgress(runner, player, key, progress);
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) => OnSceneLoadStart(runner);

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxPlayers = Mathf.Clamp(maxPlayers, 1, 4);
        minPlayersToStart = Mathf.Clamp(minPlayersToStart, 1, maxPlayers);
        AutoAssignNetworkPrefabs();
    }

    private void Reset()
    {
        AutoAssignNetworkPrefabs();
    }
#endif
}
