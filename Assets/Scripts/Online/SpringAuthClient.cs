using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SpringAuthClient : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField] private string baseUrl = "http://localhost:8082";
    [SerializeField] private int requestTimeoutSeconds = 20;

    [Header("Forgot Password")]
    [SerializeField] private float otpResendCooldownSeconds = 60f;

    public string StatusMessage { get; private set; } = "Ready";
    public string PendingOtpEmail { get; private set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public float OtpResendRemainingSeconds => Mathf.Max(0f, otpResendCooldownSeconds - (Time.unscaledTime - lastOtpRequestTime));
    public bool CanResendOtp => !string.IsNullOrWhiteSpace(PendingOtpEmail) && OtpResendRemainingSeconds <= 0f;

    private float lastOtpRequestTime = -999f;

    public void Register(string username, string email, string password, string confirmPassword)
    {
        StartCoroutine(RegisterRoutine(username, email, password, confirmPassword));
    }

    public void Login(string identifier, string password)
    {
        StartCoroutine(LoginRoutine(identifier, password));
    }

    public void RequestPasswordResetOtp(string email)
    {
        StartCoroutine(RequestPasswordResetOtpRoutine(email));
    }

    public void VerifyPasswordResetOtp(string email, string otp)
    {
        StartCoroutine(VerifyPasswordResetOtpRoutine(email, otp));
    }

    public void ResetPassword(string email, string otp, string newPassword, string confirmPassword)
    {
        StartCoroutine(ResetPasswordRoutine(email, otp, newPassword, confirmPassword));
    }

    public void ResendPasswordResetOtp()
    {
        if (!string.IsNullOrWhiteSpace(PendingOtpEmail))
        {
            RequestPasswordResetOtp(PendingOtpEmail);
        }
    }

    public void Logout()
    {
        SpringAuthSession.Clear();
        PendingOtpEmail = string.Empty;
        StatusMessage = "Logged out";
    }

    public void ClearForgotPasswordState()
    {
        PendingOtpEmail = string.Empty;
        lastOtpRequestTime = -999f;
    }

    public string GetProfileSummary()
    {
        if (SpringAuthSession.Profile != null)
        {
            return $"{SpringAuthSession.Profile.username} | {SpringAuthSession.Profile.email} | {SpringAuthSession.Profile.role}";
        }

        if (SpringAuthSession.IsSignedIn)
        {
            return $"{SpringAuthSession.Username} | {SpringAuthSession.Email} | {SpringAuthSession.Role}";
        }

        return "No profile loaded.";
    }

    private IEnumerator RegisterRoutine(string username, string email, string password, string confirmPassword)
    {
        if (!ValidateRegisterInput(username, email, password, confirmPassword))
        {
            yield break;
        }

        SpringAuthRegisterRequest requestBody = new SpringAuthRegisterRequest
        {
            username = username.Trim(),
            email = email.Trim(),
            password = password,
            confirmPassword = confirmPassword
        };

        StatusMessage = "Registering...";
        yield return SendJsonRequest("/api/auth/register", requestBody, responseText =>
        {
            SpringAuthResponse authResponse = Parse<SpringAuthResponse>(responseText);
            if (!ValidateAuthResponse(authResponse))
            {
                return;
            }

            SpringAuthSession.SetSession(authResponse);
            StatusMessage = BuildLoggedInMessage("Registered", authResponse);
            StartCoroutine(FetchProfileAfterLoginRoutine());
        }, error => StatusMessage = error);
    }

    private IEnumerator LoginRoutine(string identifier, string password)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            StatusMessage = "Username/email and password cannot be empty";
            yield break;
        }

        SpringAuthLoginRequest requestBody = new SpringAuthLoginRequest
        {
            identifier = identifier.Trim(),
            password = password
        };

        StatusMessage = "Signing in...";
        yield return SendJsonRequest("/api/auth/login", requestBody, responseText =>
        {
            SpringAuthResponse authResponse = Parse<SpringAuthResponse>(responseText);
            if (!ValidateAuthResponse(authResponse))
            {
                return;
            }

            SpringAuthSession.SetSession(authResponse);
            StatusMessage = BuildLoggedInMessage("Logged in", authResponse);
            StartCoroutine(FetchProfileAfterLoginRoutine());
        }, error => StatusMessage = error);
    }

    private IEnumerator FetchProfileAfterLoginRoutine()
    {
        if (!SpringAuthSession.IsSignedIn)
        {
            yield break;
        }

        yield return SendAuthorizedRequest("/api/auth/me", responseText =>
        {
            SpringAuthProfileResponse profile = Parse<SpringAuthProfileResponse>(responseText);
            if (profile == null)
            {
                StatusMessage = "Profile loaded, but response was invalid";
                return;
            }

            SpringAuthSession.SetProfile(profile);
            StatusMessage = BuildLoggedInMessage("Logged in", null) + " | Profile: " + GetProfileSummary();
        }, error =>
        {
            StatusMessage = error + " | Token saved";
        });
    }

    private IEnumerator RequestPasswordResetOtpRoutine(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusMessage = "Email cannot be empty";
            yield break;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();

        // Only enforce resend cooldown after at least one successful OTP request for the same email.
        bool hasPendingOtp = !string.IsNullOrWhiteSpace(PendingOtpEmail);
        bool isSameEmailAsPending = hasPendingOtp && string.Equals(PendingOtpEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        if (isSameEmailAsPending && OtpResendRemainingSeconds > 0f)
        {
            StatusMessage = $"Please wait {Mathf.CeilToInt(OtpResendRemainingSeconds)}s to resend OTP";
            yield break;
        }

        SpringAuthForgotPasswordRequest requestBody = new SpringAuthForgotPasswordRequest
        {
            email = normalizedEmail
        };

        StatusMessage = "Sending OTP...";
        yield return SendJsonRequest("/api/auth/password/forgot/request", requestBody, responseText =>
        {
            SpringAuthApiResponse response = Parse<SpringAuthApiResponse>(responseText);
            if (response != null && !response.success)
            {
                StatusMessage = string.IsNullOrWhiteSpace(response.message) ? "Failed to send OTP" : response.message;
                return;
            }

            PendingOtpEmail = normalizedEmail;
            lastOtpRequestTime = Time.unscaledTime;
            StatusMessage = string.IsNullOrWhiteSpace(response?.message) ? "OTP sent to your email" : response.message;
        }, error => StatusMessage = error);
    }

    private IEnumerator VerifyPasswordResetOtpRoutine(string email, string otp)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp))
        {
            StatusMessage = "Email and OTP are required";
            yield break;
        }

        SpringAuthVerifyOtpRequest requestBody = new SpringAuthVerifyOtpRequest
        {
            email = email.Trim().ToLowerInvariant(),
            otp = otp.Trim()
        };

        StatusMessage = "Verifying OTP...";
        yield return SendJsonRequest("/api/auth/password/forgot/verify", requestBody, responseText =>
        {
            SpringAuthApiResponse response = Parse<SpringAuthApiResponse>(responseText);
            if (response != null && response.success)
            {
                StatusMessage = string.IsNullOrWhiteSpace(response.message) ? "OTP verified" : response.message;
                return;
            }

            StatusMessage = string.IsNullOrWhiteSpace(response?.message) ? "Invalid or expired OTP" : response.message;
        }, error => StatusMessage = error);
    }

    private IEnumerator ResetPasswordRoutine(string email, string otp, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(newPassword))
        {
            StatusMessage = "Email, OTP and password are required";
            yield break;
        }

        if (newPassword != confirmPassword)
        {
            StatusMessage = "Confirm password does not match";
            yield break;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        SpringAuthVerifyOtpRequest verifyBody = new SpringAuthVerifyOtpRequest
        {
            email = normalizedEmail,
            otp = otp.Trim()
        };

        StatusMessage = "Checking OTP...";
        bool otpValid = false;
        yield return SendJsonRequest("/api/auth/password/forgot/verify", verifyBody, responseText =>
        {
            SpringAuthApiResponse response = Parse<SpringAuthApiResponse>(responseText);
            otpValid = response != null && response.success;
            if (!otpValid)
            {
                StatusMessage = string.IsNullOrWhiteSpace(response?.message) ? "Invalid or expired OTP" : response.message;
            }
        }, error => StatusMessage = error);

        if (!otpValid)
        {
            yield break;
        }

        SpringAuthResetPasswordRequest resetBody = new SpringAuthResetPasswordRequest
        {
            email = normalizedEmail,
            otp = otp.Trim(),
            newPassword = newPassword,
            confirmPassword = confirmPassword
        };

        StatusMessage = "Resetting password...";
        yield return SendJsonRequest("/api/auth/password/forgot/reset", resetBody, responseText =>
        {
            SpringAuthApiResponse response = Parse<SpringAuthApiResponse>(responseText);
            if (response != null && response.success)
            {
                ClearForgotPasswordState();
                StatusMessage = string.IsNullOrWhiteSpace(response.message) ? "Password reset successful" : response.message;
                return;
            }

            StatusMessage = string.IsNullOrWhiteSpace(response?.message) ? "Password reset failed" : response.message;
        }, error => StatusMessage = error);
    }

    private IEnumerator SendAuthorizedRequest(string path, Action<string> onSuccess, Action<string> onError)
    {
        if (!SpringAuthSession.IsSignedIn)
        {
            onError?.Invoke("Please login first");
            yield break;
        }

        using UnityWebRequest request = new UnityWebRequest(BuildUrl(path), UnityWebRequest.kHttpVerbGET);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = requestTimeoutSeconds;
        request.SetRequestHeader("Authorization", SpringAuthSession.BuildAuthorizationHeader());

        yield return ExecuteRequest(request, onSuccess, onError);
    }

    private IEnumerator SendJsonRequest(string path, object body, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest request = new UnityWebRequest(BuildUrl(path), UnityWebRequest.kHttpVerbPOST);
        string json = JsonUtility.ToJson(body);
        byte[] payload = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(payload);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = requestTimeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");

        yield return ExecuteRequest(request, onSuccess, onError);
    }

    private IEnumerator ExecuteRequest(UnityWebRequest request, Action<string> onSuccess, Action<string> onError)
    {
        IsBusy = true;
        yield return request.SendWebRequest();

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        IsBusy = false;

        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(responseText);
            yield break;
        }

        onError?.Invoke(BuildRequestError(request, responseText));
    }

    private static string BuildRequestError(UnityWebRequest request, string responseText)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            return responseText;
        }

        return request.responseCode > 0 ? $"HTTP {request.responseCode}: {request.error}" : request.error;
    }

    private string BuildUrl(string path)
    {
        string trimmedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:8082" : baseUrl.TrimEnd('/');
        string trimmedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : path.TrimStart('/');
        return trimmedBaseUrl + "/" + trimmedPath;
    }

    private static T Parse<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch
        {
            return null;
        }
    }

    private bool ValidateRegisterInput(string username, string email, string password, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            StatusMessage = "Username, email and password cannot be empty";
            return false;
        }

        if (password != confirmPassword)
        {
            StatusMessage = "Confirm password does not match";
            return false;
        }

        return true;
    }

    private static bool ValidateAuthResponse(SpringAuthResponse response)
    {
        return response != null && response.success && !string.IsNullOrWhiteSpace(response.token);
    }

    private static string BuildLoggedInMessage(string actionName, SpringAuthResponse response)
    {
        if (response == null)
        {
            return actionName + " successful";
        }

        string displayName = !string.IsNullOrWhiteSpace(response.username) ? response.username : response.email;
        string roleText = string.IsNullOrWhiteSpace(response.role) ? string.Empty : " | " + response.role;
        return string.IsNullOrWhiteSpace(displayName) ? actionName + " successful" : actionName + " as " + displayName + roleText;
    }
}