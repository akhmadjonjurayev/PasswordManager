namespace PasswordManager.Services;

public class SessionService
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _isLoggedIn;
    private readonly System.Timers.Timer _timer;

    public event EventHandler? SessionExpired;

    public string UserFullName { get; private set; } = "";
    public string UserInitials { get; private set; } = "";
    public bool IsLoggedIn => _isLoggedIn;

    public SessionService()
    {
        _timer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
        _timer.Elapsed += CheckSession;
        _timer.AutoReset = true;
    }

    public void Login(string fullName, string initials)
    {
        _isLoggedIn = true;
        UserFullName = fullName;
        UserInitials = initials;
        _lastActivity = DateTime.UtcNow;
        _timer.Start();
    }

    public void Logout()
    {
        _isLoggedIn = false;
        UserFullName = "";
        UserInitials = "";
        _timer.Stop();
    }

    public void ResetActivity()
    {
        if (_isLoggedIn)
            _lastActivity = DateTime.UtcNow;
    }

    private void CheckSession(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (_isLoggedIn && DateTime.UtcNow - _lastActivity > IdleTimeout)
        {
            _isLoggedIn = false;
            _timer.Stop();
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }
    }
}
