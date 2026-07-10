public static class OnlineSessionState
{
    public static bool IsOnlineSession { get; set; }
    public static int SelectedThemeIndex { get; set; } = -1;

    public static void ClearMatchMap()
    {
        SelectedThemeIndex = -1;
    }
}
