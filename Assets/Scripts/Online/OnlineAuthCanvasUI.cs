using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineAuthCanvasUI : MonoBehaviour
{
    public enum AuthSceneMode
    {
        Auto,
        Login,
        Register,
        ForgotPassword,
        VerifyOtp
    }

    [Header("References")]
    [SerializeField] private OnlineLobbyManager lobbyManager;
    [SerializeField] private SpringAuthClient authClient;
    [SerializeField] private GameObject root;
    [SerializeField] private AuthSceneMode sceneMode = AuthSceneMode.Auto;

    [Header("Scene Names")]
    [SerializeField] private string loginSceneName = "LoginScene";
    [SerializeField] private string registerSceneName = "RegisterScene";
    [SerializeField] private string forgotPasswordSceneName = "ForgotPasswordScene";
    [SerializeField] private string verifyOtpSceneName = "VerifyOtpScene";
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("Auto Navigation")]
    [SerializeField] private bool autoGoToMainMenuOnSignIn = true;

    [Header("Login Scene")]
    [SerializeField] private TMP_InputField loginIdentifierField;
    [SerializeField] private TMP_InputField loginPasswordField;
    [SerializeField] private Button loginSubmitButton;
    [SerializeField] private Button goToRegisterButton;
    [SerializeField] private Button goToForgotPasswordButton;
    [SerializeField] private Button loginPasswordEyeButton;

    [Header("Register Scene")]
    [SerializeField] private TMP_InputField registerUsernameField;
    [SerializeField] private TMP_InputField registerEmailField;
    [SerializeField] private TMP_InputField registerPasswordField;
    [SerializeField] private TMP_InputField registerConfirmPasswordField;
    [SerializeField] private Button registerSubmitButton;
    [SerializeField] private Button goToLoginButtonFromRegister;
    [SerializeField] private Button registerPasswordEyeButton;
    [SerializeField] private Button registerConfirmPasswordEyeButton;

    [Header("Forgot Password Scene")]
    [SerializeField] private TMP_InputField forgotEmailField;
    [SerializeField] private Button sendOtpButton;
    [SerializeField] private Button goToLoginButtonFromForgot;
    [SerializeField] private Button goToRegisterButtonFromForgot;

    [Header("Verify OTP Scene")]
    [SerializeField] private TMP_InputField verifyEmailField;
    [SerializeField] private TMP_InputField otpField;
    [SerializeField] private TMP_InputField resetPasswordField;
    [SerializeField] private TMP_InputField resetConfirmPasswordField;
    [SerializeField] private Button verifySubmitButton;
    [SerializeField] private Button resendOtpButton;
    [SerializeField] private Button goToLoginButtonFromVerify;
    [SerializeField] private Button resetPasswordEyeButton;
    [SerializeField] private Button resetConfirmPasswordEyeButton;

    [Header("Common UI")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text resendOtpButtonText;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.2f;

    private float refreshTimer;
    private bool showLoginPassword;
    private bool showRegisterPassword;
    private bool showRegisterConfirmPassword;
    private bool showResetPassword;
    private bool showResetConfirmPassword;
    private bool pendingVerifyRedirect;
    private bool pendingResetRedirect;
    private bool lastVerifySubmitInteractable;
    private Coroutine otpNavigationCoroutine;

    private void Awake()
    {
        ResolveSceneMode();
        BindEvents();
    }

    private void Start()
    {
        ResolveManager();
        ResolveAuthClient();
        ApplyPasswordFieldTypes();
        SyncVerifyEmailFieldFromPendingOtp();
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void Update()
    {
        ResolveManager();
        ResolveAuthClient();
        ResolveSceneMode();

        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer >= refreshInterval)
        {
            refreshTimer = 0f;
            RefreshUI();
        }

        TryHandleAutoNavigation();
    }

    private void ResolveManager()
    {
        if (lobbyManager == null)
        {
            lobbyManager = FindAnyObjectByType<OnlineLobbyManager>(FindObjectsInactive.Include);
        }
    }

    private void ResolveAuthClient()
    {
        if (authClient == null)
        {
            authClient = FindAnyObjectByType<SpringAuthClient>(FindObjectsInactive.Include);
        }
    }

    private void ResolveSceneMode()
    {
        if (sceneMode != AuthSceneMode.Auto)
        {
            return;
        }

        string currentScene = SceneManager.GetActiveScene().name;
        if (currentScene == registerSceneName)
        {
            sceneMode = AuthSceneMode.Register;
        }
        else if (currentScene == forgotPasswordSceneName)
        {
            sceneMode = AuthSceneMode.ForgotPassword;
        }
        else if (currentScene == verifyOtpSceneName)
        {
            sceneMode = AuthSceneMode.VerifyOtp;
        }
        else
        {
            sceneMode = AuthSceneMode.Login;
        }
    }

    private void BindEvents()
    {
        if (loginSubmitButton != null) loginSubmitButton.onClick.AddListener(OnLoginClicked);
        if (goToRegisterButton != null) goToRegisterButton.onClick.AddListener(() => LoadScene(registerSceneName));
        if (goToForgotPasswordButton != null) goToForgotPasswordButton.onClick.AddListener(() => LoadScene(forgotPasswordSceneName));
        if (loginPasswordEyeButton != null) loginPasswordEyeButton.onClick.AddListener(OnLoginPasswordEyeClicked);

        if (registerSubmitButton != null) registerSubmitButton.onClick.AddListener(OnRegisterClicked);
        if (goToLoginButtonFromRegister != null) goToLoginButtonFromRegister.onClick.AddListener(() => LoadScene(loginSceneName));
        if (registerPasswordEyeButton != null) registerPasswordEyeButton.onClick.AddListener(OnRegisterPasswordEyeClicked);
        if (registerConfirmPasswordEyeButton != null) registerConfirmPasswordEyeButton.onClick.AddListener(OnRegisterConfirmPasswordEyeClicked);

        if (sendOtpButton != null) sendOtpButton.onClick.AddListener(OnSendOtpClicked);
        if (goToLoginButtonFromForgot != null) goToLoginButtonFromForgot.onClick.AddListener(() => LoadScene(loginSceneName));
        if (goToRegisterButtonFromForgot != null) goToRegisterButtonFromForgot.onClick.AddListener(() => LoadScene(registerSceneName));

        if (verifySubmitButton != null) verifySubmitButton.onClick.AddListener(OnVerifyResetClicked);
        if (resendOtpButton != null) resendOtpButton.onClick.AddListener(OnResendOtpClicked);
        if (goToLoginButtonFromVerify != null) goToLoginButtonFromVerify.onClick.AddListener(() => LoadScene(loginSceneName));
        if (resetPasswordEyeButton != null) resetPasswordEyeButton.onClick.AddListener(OnResetPasswordEyeClicked);
        if (resetConfirmPasswordEyeButton != null) resetConfirmPasswordEyeButton.onClick.AddListener(OnResetConfirmPasswordEyeClicked);
    }

    private void UnbindEvents()
    {
        if (loginSubmitButton != null) loginSubmitButton.onClick.RemoveListener(OnLoginClicked);
        if (goToRegisterButton != null) goToRegisterButton.onClick.RemoveAllListeners();
        if (goToForgotPasswordButton != null) goToForgotPasswordButton.onClick.RemoveAllListeners();
        if (loginPasswordEyeButton != null) loginPasswordEyeButton.onClick.RemoveListener(OnLoginPasswordEyeClicked);

        if (registerSubmitButton != null) registerSubmitButton.onClick.RemoveListener(OnRegisterClicked);
        if (goToLoginButtonFromRegister != null) goToLoginButtonFromRegister.onClick.RemoveAllListeners();
        if (registerPasswordEyeButton != null) registerPasswordEyeButton.onClick.RemoveListener(OnRegisterPasswordEyeClicked);
        if (registerConfirmPasswordEyeButton != null) registerConfirmPasswordEyeButton.onClick.RemoveListener(OnRegisterConfirmPasswordEyeClicked);

        if (sendOtpButton != null) sendOtpButton.onClick.RemoveListener(OnSendOtpClicked);
        if (goToLoginButtonFromForgot != null) goToLoginButtonFromForgot.onClick.RemoveAllListeners();
        if (goToRegisterButtonFromForgot != null) goToRegisterButtonFromForgot.onClick.RemoveAllListeners();

        if (verifySubmitButton != null) verifySubmitButton.onClick.RemoveListener(OnVerifyResetClicked);
        if (resendOtpButton != null) resendOtpButton.onClick.RemoveListener(OnResendOtpClicked);
        if (goToLoginButtonFromVerify != null) goToLoginButtonFromVerify.onClick.RemoveAllListeners();
        if (resetPasswordEyeButton != null) resetPasswordEyeButton.onClick.RemoveListener(OnResetPasswordEyeClicked);
        if (resetConfirmPasswordEyeButton != null) resetConfirmPasswordEyeButton.onClick.RemoveListener(OnResetConfirmPasswordEyeClicked);
    }

    private void RefreshUI()
    {
        bool signedIn = IsSignedIn();

        SyncVerifyEmailFieldFromPendingOtp();

        if (root != null)
        {
            root.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = GetStatusMessage();
        }

        bool canAuth = GetCanAuthenticate();

        SetInteractable(loginSubmitButton, canAuth && sceneMode == AuthSceneMode.Login);
        SetInteractable(goToRegisterButton, canAuth && sceneMode == AuthSceneMode.Login);
        SetInteractable(goToForgotPasswordButton, canAuth && sceneMode == AuthSceneMode.Login);
        SetInteractable(loginPasswordEyeButton, sceneMode == AuthSceneMode.Login);

        SetInteractable(registerSubmitButton, canAuth && sceneMode == AuthSceneMode.Register);
        SetInteractable(goToLoginButtonFromRegister, canAuth && sceneMode == AuthSceneMode.Register);
        SetInteractable(registerPasswordEyeButton, sceneMode == AuthSceneMode.Register);
        SetInteractable(registerConfirmPasswordEyeButton, sceneMode == AuthSceneMode.Register);

        SetInteractable(sendOtpButton, canAuth && sceneMode == AuthSceneMode.ForgotPassword);
        SetInteractable(goToLoginButtonFromForgot, canAuth && sceneMode == AuthSceneMode.ForgotPassword);
        SetInteractable(goToRegisterButtonFromForgot, canAuth && sceneMode == AuthSceneMode.ForgotPassword);

        SetInteractable(verifySubmitButton, canAuth && sceneMode == AuthSceneMode.VerifyOtp);
        SetInteractable(resendOtpButton, canAuth && sceneMode == AuthSceneMode.VerifyOtp && CanResendOtp());
        SetInteractable(goToLoginButtonFromVerify, canAuth && sceneMode == AuthSceneMode.VerifyOtp);
        SetInteractable(resetPasswordEyeButton, sceneMode == AuthSceneMode.VerifyOtp);
        SetInteractable(resetConfirmPasswordEyeButton, sceneMode == AuthSceneMode.VerifyOtp);

        // Log when the verify/confirm button interactable state changes to help debugging
        bool currentVerifyInteractable = verifySubmitButton != null && verifySubmitButton.interactable;
        if (currentVerifyInteractable != lastVerifySubmitInteractable)
        {
            lastVerifySubmitInteractable = currentVerifyInteractable;
            Debug.Log($"VerifySubmitButton.interactable changed -> {currentVerifyInteractable}");
        }

        if (resendOtpButtonText != null)
        {
            if (sceneMode != AuthSceneMode.VerifyOtp || CanResendOtp())
            {
                resendOtpButtonText.text = "Resend OTP";
            }
            else
            {
                resendOtpButtonText.text = "Resend OTP (" + Mathf.CeilToInt(GetOtpResendRemainingSeconds()) + "s)";
            }
        }

        if (signedIn && autoGoToMainMenuOnSignIn && !string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            pendingVerifyRedirect = false;
            pendingResetRedirect = false;
            LoadScene(mainMenuSceneName);
        }
    }

    private void TryHandleAutoNavigation()
    {
        string status = GetStatusMessage();

        // Use state-based checks instead of matching localized status text.
        bool otpRequestSucceeded = authClient != null
            ? !authClient.IsBusy && !string.IsNullOrWhiteSpace(authClient.PendingOtpEmail)
            : status.Contains("OTP sent");

        if (pendingVerifyRedirect && sceneMode == AuthSceneMode.ForgotPassword && otpRequestSucceeded)
        {
            pendingVerifyRedirect = false;
            pendingResetRedirect = true;
            LoadScene(verifyOtpSceneName);
            return;
        }

        if (pendingResetRedirect && sceneMode == AuthSceneMode.VerifyOtp && status.Contains("successful"))
        {
            pendingResetRedirect = false;
            LoadScene(loginSceneName);
        }
    }

    private void OnLoginClicked()
    {
        string identifier = ReadField(loginIdentifierField);
        string password = ReadField(loginPasswordField);

        if (authClient != null)
        {
            authClient.Login(identifier, password);
            return;
        }

        lobbyManager?.Login(identifier, password);
    }

    private void OnRegisterClicked()
    {
        string username = ReadField(registerUsernameField);
        string email = ReadField(registerEmailField);
        string password = ReadField(registerPasswordField);
        string confirmPassword = ReadField(registerConfirmPasswordField);

        if (authClient != null)
        {
            authClient.Register(username, email, password, confirmPassword);
            return;
        }

        lobbyManager?.Register(username, email, password, confirmPassword);
    }

    private void OnSendOtpClicked()
    {
        string email = ReadField(forgotEmailField);
        pendingVerifyRedirect = true;
        pendingResetRedirect = false;

        Debug.Log("OnSendOtpClicked: email='" + email + "'");

        if (authClient != null)
        {
            authClient.RequestPasswordResetOtp(email);

            if (otpNavigationCoroutine != null)
            {
                StopCoroutine(otpNavigationCoroutine);
            }

            otpNavigationCoroutine = StartCoroutine(WaitForOtpRequestThenNavigate(email));
            return;
        }

        lobbyManager?.RequestPasswordResetOtp(email);
    }

    private IEnumerator WaitForOtpRequestThenNavigate(string requestedEmail)
    {
        string normalizedRequestedEmail = string.IsNullOrWhiteSpace(requestedEmail)
            ? string.Empty
            : requestedEmail.Trim().ToLowerInvariant();

        float startTime = Time.unscaledTime;
        const float timeoutSeconds = 25f;

        Debug.Log($"WaitForOtpRequestThenNavigate started for email: {normalizedRequestedEmail}");

        while (authClient != null && Time.unscaledTime - startTime <= timeoutSeconds)
        {
            bool hasPendingOtpEmail = !string.IsNullOrWhiteSpace(authClient.PendingOtpEmail);
            bool sameEmail = string.IsNullOrWhiteSpace(normalizedRequestedEmail)
                || string.Equals(authClient.PendingOtpEmail, normalizedRequestedEmail, System.StringComparison.OrdinalIgnoreCase);

            if (!authClient.IsBusy && hasPendingOtpEmail && sameEmail)
            {
                pendingVerifyRedirect = false;
                pendingResetRedirect = true;
                otpNavigationCoroutine = null;
                Debug.Log("OTP request confirmed, navigating to verify scene: " + verifyOtpSceneName);
                LoadScene(verifyOtpSceneName);
                yield break;
            }

            // Debug logging every 1 second or at start
            if (Time.unscaledTime - startTime < 0.5f || (int)(Time.unscaledTime - startTime) % 3 == 0)
            {
                Debug.Log($"OTP navigation: IsBusy={authClient?.IsBusy}, PendingOtpEmail={authClient?.PendingOtpEmail ?? "null"}, " +
                    $"sameEmail={sameEmail}, elapsed={(Time.unscaledTime - startTime):F1}s / {timeoutSeconds}s");
            }

            yield return null;
        }

        otpNavigationCoroutine = null;
        Debug.Log($"OTP navigation coroutine ended: authClient null={authClient == null}, timeout elapsed");
    }

    private void OnVerifyResetClicked()
    {
        Debug.Log("OnVerifyResetClicked called");
        string email = ReadField(verifyEmailField, forgotEmailField, registerEmailField);
        if (string.IsNullOrWhiteSpace(email) && authClient != null)
        {
            email = authClient.PendingOtpEmail;
        }
        string otp = ReadField(otpField);
        string newPassword = ReadField(resetPasswordField);
        string confirmPassword = ReadField(resetConfirmPasswordField);
        pendingResetRedirect = true;

        Debug.Log($"OnVerifyResetClicked: email={email}, otp={otp}, newPasswordProvided={(string.IsNullOrEmpty(newPassword) ? "false" : "true")}");

        if (authClient != null)
        {
            authClient.ResetPassword(email, otp, newPassword, confirmPassword);
            return;
        }

        lobbyManager?.ConfirmPasswordResetOtp(email, otp, newPassword, confirmPassword);
    }

    private void OnResendOtpClicked()
    {
        if (authClient != null)
        {
            authClient.ResendPasswordResetOtp();
            return;
        }

        lobbyManager?.ResendPasswordResetOtp();
    }

    private void OnLoginPasswordEyeClicked()
    {
        showLoginPassword = !showLoginPassword;
        SetPasswordVisible(loginPasswordField, showLoginPassword);
    }

    private void OnRegisterPasswordEyeClicked()
    {
        showRegisterPassword = !showRegisterPassword;
        SetPasswordVisible(registerPasswordField, showRegisterPassword);
    }

    private void OnRegisterConfirmPasswordEyeClicked()
    {
        showRegisterConfirmPassword = !showRegisterConfirmPassword;
        SetPasswordVisible(registerConfirmPasswordField, showRegisterConfirmPassword);
    }

    private void OnResetPasswordEyeClicked()
    {
        showResetPassword = !showResetPassword;
        SetPasswordVisible(resetPasswordField, showResetPassword);
    }

    private void OnResetConfirmPasswordEyeClicked()
    {
        showResetConfirmPassword = !showResetConfirmPassword;
        SetPasswordVisible(resetConfirmPasswordField, showResetConfirmPassword);
    }

    private void ApplyPasswordFieldTypes()
    {
        SetPasswordVisible(loginPasswordField, false);
        SetPasswordVisible(registerPasswordField, false);
        SetPasswordVisible(registerConfirmPasswordField, false);
        SetPasswordVisible(resetPasswordField, false);
        SetPasswordVisible(resetConfirmPasswordField, false);
    }

    private bool IsSignedIn()
    {
        if (authClient != null)
        {
            return SpringAuthSession.IsSignedIn;
        }

        return lobbyManager != null && lobbyManager.IsSignedIn;
    }

    private bool GetCanAuthenticate()
    {
        if (authClient != null)
        {
            return !authClient.IsBusy;
        }

        return lobbyManager != null && lobbyManager.ServicesReady && !IsSignedIn();
    }

    private bool CanResendOtp()
    {
        if (authClient != null)
        {
            return authClient.CanResendOtp;
        }

        return lobbyManager != null && lobbyManager.CanResendOtp;
    }

    private float GetOtpResendRemainingSeconds()
    {
        if (authClient != null)
        {
            return authClient.OtpResendRemainingSeconds;
        }

        return lobbyManager != null ? lobbyManager.OtpResendRemainingSeconds : 0f;
    }

    private string GetStatusMessage()
    {
        if (authClient != null)
        {
            return authClient.StatusMessage;
        }

        return lobbyManager != null ? lobbyManager.statusMessage : "Waiting for SpringAuthClient...";
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void SyncVerifyEmailFieldFromPendingOtp()
    {
        if (sceneMode != AuthSceneMode.VerifyOtp || verifyEmailField == null || authClient == null)
        {
            return;
        }

        string pendingEmail = authClient.PendingOtpEmail;
        if (string.IsNullOrWhiteSpace(pendingEmail))
        {
            return;
        }

        if (string.Equals(verifyEmailField.text, pendingEmail, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        verifyEmailField.SetTextWithoutNotify(pendingEmail);
        Debug.Log($"Verify OTP email field populated from pending OTP email: {pendingEmail}");
    }

    private static void SetInteractable(Button button, bool value)
    {
        if (button != null)
        {
            button.interactable = value;
        }
    }

    private static void SetPasswordVisible(TMP_InputField input, bool visible)
    {
        if (input == null)
        {
            return;
        }

        input.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        input.ForceLabelUpdate();
    }

    private static string ReadField(params TMP_InputField[] fields)
    {
        if (fields == null)
        {
            return string.Empty;
        }

        for (int i = 0; i < fields.Length; i++)
        {
            TMP_InputField field = fields[i];
            if (field != null)
            {
                return field.text;
            }
        }

        return string.Empty;
    }
}
