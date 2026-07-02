using System;

[Serializable]
public class SpringAuthRegisterRequest
{
    public string username;
    public string email;
    public string password;
    public string confirmPassword;
}

[Serializable]
public class SpringAuthLoginRequest
{
    public string identifier;
    public string password;
}

[Serializable]
public class SpringAuthForgotPasswordRequest
{
    public string email;
}

[Serializable]
public class SpringAuthVerifyOtpRequest
{
    public string email;
    public string otp;
}

[Serializable]
public class SpringAuthResetPasswordRequest
{
    public string email;
    public string otp;
    public string newPassword;
    public string confirmPassword;
}

[Serializable]
public class SpringAuthApiResponse
{
    public bool success;
    public string message;
    public int retryAfterSeconds;
}

[Serializable]
public class SpringAuthResponse : SpringAuthApiResponse
{
    public string token;
    public string username;
    public string email;
    public string role;
}

[Serializable]
public class SpringAuthProfileResponse : SpringAuthApiResponse
{
    public string username;
    public string email;
    public string role;
}