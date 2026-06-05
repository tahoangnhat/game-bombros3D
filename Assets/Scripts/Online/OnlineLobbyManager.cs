using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class OnlineLobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
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
    public string gameSceneName = "GameScene";

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
    private NetworkRunner runner;
    private NetworkSceneManagerDefault sceneManager;
    private bool servicesReady;
    private bool isHost;
    private float heartbeatTimer;
    private float pollTimer;
    private bool localReady;
    private bool isLoadingGame;
    private bool gameSceneLoaded;
    private bool isLobbyConnectionInProgress;
    private float lobbyRefreshGraceUntil = -1f;
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
    private const float LobbyRefreshGraceSeconds = 3f;
    private const int JoinLobbyRetryAttempts = 3;
    private const int JoinLobbyRetryDelayMs = 1000;

    public bool ServicesReady => servicesReady;
    public bool IsHostLobby => isHost;
    public bool IsSignedIn => (TryGetAuthService(out IAuthenticationService auth) && auth.IsSignedIn) || SpringAuthSession.IsSignedIn;
    public Lobby CurrentLobby => currentLobby;
    public string CurrentLobbyCode => currentLobby != null ? currentLobby.LobbyCode : string.Empty;
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

        AutoAssignNetworkPrefabs();
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
        int displaySlot = slotIndex + 1;

        if (currentLobby == null || currentLobby.Players == null || slotIndex < 0 || slotIndex >= maxPlayers)
        {
            return $"Slot {displaySlot}: Empty";
        }

        if (slotIndex >= currentLobby.Players.Count)
        {
            return $"Slot {displaySlot}: Empty";
        }

        Player player = currentLobby.Players[slotIndex];
        if (player == null)
        {
            return $"Slot {displaySlot}: Empty";
        }

        string name = GetPlayerDisplayName(player);
        return $"Slot {displaySlot}: {name}";
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

        if (!servicesReady || !await EnsureUnityAuthenticationAsync())
        {
            statusMessage = "Please login first";
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            statusMessage = "Creating lobby...";
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { LobbyRankMmrKey, new DataObject(DataObject.VisibilityOptions.Public, CurrentMmr.ToString(), DataObject.IndexOptions.N1) },
                    { LobbyRankBucketKey, new DataObject(DataObject.VisibilityOptions.Public, BuildRankBucket(CurrentMmr), DataObject.IndexOptions.S1) }
                }
            };

            currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            isHost = true;
            await SendHeartbeatAsync();
            await SetLocalReadyAsync(true);

            statusMessage = "Starting Fusion host session...";
            await StartFusionSessionAsync(GameMode.Host, currentLobby.LobbyCode);
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

        isLobbyConnectionInProgress = true;
        try
        {
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

            statusMessage = "Starting Fusion client session...";
            await StartFusionSessionAsync(GameMode.Client, currentLobby.LobbyCode);

            isHost = false;
            await SetLocalReadyAsync(false);

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
                return await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
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
        await InitializeServicesAsync();

        if (!servicesReady || !await EnsureUnityAuthenticationAsync())
        {
            statusMessage = "Please login first";
            return;
        }

        isLobbyConnectionInProgress = true;
        try
        {
            statusMessage = "Quick joining by rank...";
            currentLobby = await FindRankedLobbyAsync();

            if (currentLobby == null)
            {
                statusMessage = "No same-rank lobby, trying random quick join...";
                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            }

            statusMessage = "Starting Fusion client session...";
            await StartFusionSessionAsync(GameMode.Client, currentLobby.LobbyCode);

            isHost = false;
            await SetLocalReadyAsync(false);
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
        gameSceneLoaded = false;
        statusMessage = "Starting game...";

        if (runner != null && runner.IsRunning)
        {
            _ = runner.LoadScene(gameSceneName, LoadSceneMode.Single, LocalPhysicsMode.Physics3D, true);
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

        if (runner != null)
        {
            _ = runner.Shutdown();
        }

        ClearLobbySessionState("Left session");
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
            currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
            localReady = IsLocalPlayerReady(currentLobby);
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

    private void ClearLobbySessionState(string message)
    {
        currentLobby = null;
        isHost = false;
        localReady = false;
        isLoadingGame = false;
        lobbyRefreshGraceUntil = -1f;
        OnlineSessionState.IsOnlineSession = false;
        statusMessage = message;
    }

    private static bool IsFatalLobbyRefreshError(LobbyServiceException ex)
    {
        return ex != null && ex.Reason == LobbyExceptionReason.EntityNotFound;
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

    private async Task StartFusionSessionAsync(GameMode gameMode, string sessionName)
    {
        AutoAssignNetworkPrefabs();

        if (runner == null)
        {
            GameObject networkRoot = new GameObject("FusionRunner");
            runner = networkRoot.AddComponent<NetworkRunner>();
            sceneManager = networkRoot.AddComponent<NetworkSceneManagerDefault>();
            DontDestroyOnLoad(networkRoot);
        }

        if (runner == null)
        {
            statusMessage = "Fusion runner unavailable";
            return;
        }

        runner.ProvideInput = true;
        runner.RemoveCallbacks(this);
        runner.AddCallbacks(this);

        StartGameResult result = await runner.StartGame(new StartGameArgs
        {
            GameMode = gameMode,
            SessionName = sessionName,
            SceneManager = sceneManager,
            PlayerCount = maxPlayers
        });

        if (!result.Ok)
        {
            statusMessage = $"Fusion session failed: {result.ShutdownReason}";
            Debug.LogError(result.ErrorMessage);
            return;
        }

        OnlineSessionState.IsOnlineSession = true;
        SetupNetworkPrefabs();
    }

    private async Task<bool> EnsureUnityAuthenticationAsync()
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
                    EnsureLocalRankInitialized();
                    return auth.IsAuthorized;
                }

                auth.SignOut();
            }
            else if (auth.IsSignedIn)
            {
                auth.SignOut();
            }

            auth.SwitchProfile(profile);
            statusMessage = auth.SessionTokenExists
                ? "Restoring Unity session..."
                : "Signing in to Unity Services...";
            await auth.SignInAnonymouslyAsync();
            EnsureLocalRankInitialized();

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

        if (playerPrefab != null)
        {
            OnlinePlayerController playerController = playerPrefab.GetComponent<OnlinePlayerController>();
            if (playerController != null && playerController.bombPrefab == null && bombPrefab != null)
            {
                playerController.bombPrefab = bombPrefab;
            }

        }

        if (bombPrefab != null)
        {
            OnlineBomb bomb = bombPrefab.GetComponent<OnlineBomb>();
            if (bomb != null && bomb.explosionPrefab == null && explosionPrefab != null)
            {
                bomb.explosionPrefab = explosionPrefab;
            }
        }
    }

    private bool IsNetworkSessionActive()
    {
        return runner != null && runner.IsRunning;
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

    private void SpawnExistingPlayers()
    {
        if (runner == null || !runner.IsServer)
        {
            return;
        }

        Vector3[] spawnPoints = BuildSpawnPoints();
        List<PlayerRef> activePlayers = runner.ActivePlayers
            .OrderBy(player => player.PlayerId)
            .ToList();

        for (int index = 0; index < activePlayers.Count && index < spawnPoints.Length; index++)
        {
            SpawnPlayer(activePlayers[index], spawnPoints[index], index);
        }
    }

    private void SpawnPlayer(PlayerRef playerRef, Vector3 spawnPoint, int index)
    {
        if (runner == null || playerPrefab == null)
        {
            return;
        }

        NetworkObject playerObject = runner.Spawn(playerPrefab, spawnPoint, Quaternion.identity, playerRef);
        if (playerObject == null)
        {
            return;
        }

        runner.SetPlayerObject(playerRef, playerObject);

        OnlinePlayerController controller = playerObject.GetComponent<OnlinePlayerController>();
        if (controller != null)
        {
            controller.name = $"Player{index + 1}";
        }
    }

    private Vector3[] BuildSpawnPoints()
    {
        ThemeManager themeManager = ThemeManager.Instance;
        if (themeManager != null && themeManager.IsLevelReady)
        {
            return themeManager.GetCornerSpawnPoints();
        }

        return new[]
        {
            new Vector3(-2f, 0.5f, -2f),
            new Vector3(2f, 0.5f, -2f),
            new Vector3(-2f, 0.5f, 2f),
            new Vector3(2f, 0.5f, 2f)
        };
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer || !gameSceneLoaded)
        {
            return;
        }

        Vector3[] spawnPoints = BuildSpawnPoints();
        List<PlayerRef> activePlayers = runner.ActivePlayers
            .OrderBy(playerRef => playerRef.PlayerId)
            .ToList();

        int spawnIndex = activePlayers.IndexOf(player);
        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length)
        {
            return;
        }

        SpawnPlayer(player, spawnPoints[spawnIndex], spawnIndex);
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        if (runner == null || !runner.IsServer || !isLoadingGame)
        {
            return;
        }

        gameSceneLoaded = false;
        statusMessage = $"Loading {gameSceneName}...";
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (runner == null || !runner.IsServer || !isLoadingGame)
        {
            return;
        }

        StartCoroutine(SpawnPlayersAfterSceneReady());
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

        SpawnExistingPlayers();
        gameSceneLoaded = true;
        isLoadingGame = false;
        statusMessage = "Match started";
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        OnlinePlayerInput playerInput = new OnlinePlayerInput
        {
            Move = ReadMoveInput(),
            PlaceBomb = ReadBombInput()
        };

        input.Set(playerInput);
    }

    private Vector2 ReadMoveInput()
    {
        if (Gamepad.current != null)
        {
            return Gamepad.current.leftStick.ReadValue();
        }

        if (Keyboard.current != null)
        {
            float h = 0f;
            float v = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            return new Vector2(h, v);
        }

        return Vector2.zero;
    }

    private bool ReadBombInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
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
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
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
