using UnityEngine;

public static class SpringAuthSession
{
    private const string TokenKey = "bombros_spring_auth_token";
    private const string UsernameKey = "bombros_spring_auth_username";
    private const string EmailKey = "bombros_spring_auth_email";
    private const string RoleKey = "bombros_spring_auth_role";
    private const string ProfileJsonKey = "bombros_spring_auth_profile";
    private const string GuestKey = "bombros_guest_session";

    public static string Token { get; private set; } = string.Empty;
    public static string Username { get; private set; } = string.Empty;
    public static string Email { get; private set; } = string.Empty;
    public static string Role { get; private set; } = string.Empty;
    public static SpringAuthProfileResponse Profile { get; private set; }
    public static bool IsGuest { get; private set; }

    public static bool IsSignedIn => IsGuest || !string.IsNullOrWhiteSpace(Token);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        LoadFromPrefs();
    }

    public static void LoadFromPrefs()
    {
        Token = PlayerPrefs.GetString(TokenKey, string.Empty);
        Username = PlayerPrefs.GetString(UsernameKey, string.Empty);
        Email = PlayerPrefs.GetString(EmailKey, string.Empty);
        Role = PlayerPrefs.GetString(RoleKey, string.Empty);
        IsGuest = PlayerPrefs.GetInt(GuestKey, 0) == 1;

        string profileJson = PlayerPrefs.GetString(ProfileJsonKey, string.Empty);
        Profile = string.IsNullOrWhiteSpace(profileJson) ? null : JsonUtility.FromJson<SpringAuthProfileResponse>(profileJson);

        if (IsGuest && string.IsNullOrWhiteSpace(Username))
        {
            Clear();
        }
    }

    public static void SetSession(SpringAuthResponse authResponse, SpringAuthProfileResponse profileResponse = null)
    {
        if (authResponse == null)
        {
            return;
        }

        Token = authResponse.token != null ? authResponse.token : string.Empty;
        Username = authResponse.username != null ? authResponse.username : string.Empty;
        Email = authResponse.email != null ? authResponse.email : string.Empty;
        Role = authResponse.role != null ? authResponse.role : string.Empty;
        IsGuest = false;

        if (profileResponse != null)
        {
            Profile = profileResponse;
            Username = string.IsNullOrWhiteSpace(profileResponse.username) ? Username : profileResponse.username;
            Email = string.IsNullOrWhiteSpace(profileResponse.email) ? Email : profileResponse.email;
            Role = string.IsNullOrWhiteSpace(profileResponse.role) ? Role : profileResponse.role;
        }

        PlayerPrefs.SetString(TokenKey, Token);
        PlayerPrefs.SetString(UsernameKey, Username);
        PlayerPrefs.SetString(EmailKey, Email);
        PlayerPrefs.SetString(RoleKey, Role);
        PlayerPrefs.DeleteKey(GuestKey);

        if (Profile != null)
        {
            PlayerPrefs.SetString(ProfileJsonKey, JsonUtility.ToJson(Profile));
        }

        PlayerPrefs.Save();
    }

    public static string StartGuestSession()
    {
        Token = string.Empty;
        Username = "Guest-" + System.Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        Email = string.Empty;
        Role = "GUEST";
        Profile = null;
        IsGuest = true;

        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.SetString(UsernameKey, Username);
        PlayerPrefs.DeleteKey(EmailKey);
        PlayerPrefs.SetString(RoleKey, Role);
        PlayerPrefs.DeleteKey(ProfileJsonKey);
        PlayerPrefs.SetInt(GuestKey, 1);
        PlayerPrefs.Save();
        return Username;
    }

    public static void SetProfile(SpringAuthProfileResponse profileResponse)
    {
        if (profileResponse == null)
        {
            return;
        }

        Profile = profileResponse;
        Username = string.IsNullOrWhiteSpace(profileResponse.username) ? Username : profileResponse.username;
        Email = string.IsNullOrWhiteSpace(profileResponse.email) ? Email : profileResponse.email;
        Role = string.IsNullOrWhiteSpace(profileResponse.role) ? Role : profileResponse.role;

        PlayerPrefs.SetString(UsernameKey, Username);
        PlayerPrefs.SetString(EmailKey, Email);
        PlayerPrefs.SetString(RoleKey, Role);
        PlayerPrefs.SetString(ProfileJsonKey, JsonUtility.ToJson(Profile));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        Token = string.Empty;
        Username = string.Empty;
        Email = string.Empty;
        Role = string.Empty;
        Profile = null;
        IsGuest = false;

        PlayerPrefs.DeleteKey(TokenKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.DeleteKey(EmailKey);
        PlayerPrefs.DeleteKey(RoleKey);
        PlayerPrefs.DeleteKey(ProfileJsonKey);
        PlayerPrefs.DeleteKey(GuestKey);
        PlayerPrefs.Save();
    }

    public static string BuildAuthorizationHeader()
    {
        return !string.IsNullOrWhiteSpace(Token) ? "Bearer " + Token : string.Empty;
    }
}
