using Autodesk.Authentication;
using Autodesk.Revit.UI;
using revit_aec_dm_extensibility_sample.Models;

namespace revit_aec_dm_extensibility_sample.TokenHandlers
{
    public class TokenHandler
    {
        private static string _currentAccessToken;
        private static string _refreshToken;
        private static DateTime _tokenExpiryTime;
        private static Timer _refreshTimer;
        private static readonly object _lockObject = new object();
        private static APS _apsConfig;

        public static string Login()
        {
            try
            {
                string token = string.Empty;

                var apsConfiguration = ConfigurationLoader.LoadConfigurationAsync().GetAwaiter().GetResult();
                if (apsConfiguration == null)
                {
                    TaskDialog.Show("Error", "Failed to load configuration");
                    return null;
                }

                _apsConfig = apsConfiguration.APS; // Store config for refresh operations
                var oAuthHandler = OAuthHandler.Create(_apsConfig);

                AutoResetEvent stopWaitHandle = new AutoResetEvent(false);
                oAuthHandler.Invoke3LeggedOAuth(async (bearer) =>
                {
                    if (bearer == null)
                    {
                        TaskDialog.Show("Login Response", "Sorry, Authentication failed! 3legged test");
                        return;
                    }

                    lock (_lockObject)
                    {
                        _currentAccessToken = bearer.AccessToken;
                        _refreshToken = bearer.RefreshToken; // Store refresh token
                        _tokenExpiryTime = DateTime.Now.AddSeconds(double.Parse(bearer.ExpiresIn.ToString()));
                        token = _currentAccessToken;
                    }

                    // Start the refresh timer - refresh 5 minutes before expiry
                    StartRefreshTimer();

                    var authenticationClient = new AuthenticationClient();
                    Autodesk.Authentication.Model.UserInfo profileApi = await authenticationClient.GetUserInfoAsync(token);
                    TaskDialog.Show("Login Response", $"Hello {profileApi.Name}!! You are Logged in!");
                    stopWaitHandle.Set();
                });

                stopWaitHandle.WaitOne();
                return token;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                TaskDialog.Show("Login Error", "Access Denied");
                throw new Exception("Login Error");
            }
        }

        public static string GetCurrentToken()
        {
            lock (_lockObject)
            {
                // Check if token is still valid (with 5-minute buffer)
                if (DateTime.Now.AddMinutes(5) >= _tokenExpiryTime)
                {
                    // Token is expiring soon, try to refresh synchronously
                    RefreshTokenSync();
                }
                return _currentAccessToken;
            }
        }

        private static void StartRefreshTimer()
        {
            // Stop existing timer if any
            _refreshTimer?.Dispose();

            lock (_lockObject)
            {
                // Calculate time until refresh (5 minutes before expiry)
                var refreshTime = _tokenExpiryTime.AddMinutes(-5);
                var timeUntilRefresh = refreshTime - DateTime.Now;

                if (timeUntilRefresh.TotalMilliseconds > 0)
                {
                    _refreshTimer = new Timer(RefreshTokenCallback, null, timeUntilRefresh, TimeSpan.FromMilliseconds(-1));
                    Console.WriteLine($"Token refresh scheduled for: {refreshTime}");
                }
                else
                {
                    // Token is already close to expiry, refresh immediately
                    Task.Run(RefreshTokenAsync);
                }
            }
        }

        private static void RefreshTokenCallback(object state)
        {
            Task.Run(RefreshTokenAsync);
        }

        private static async Task RefreshTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_refreshToken) || _apsConfig == null)
                {
                    Console.WriteLine("No refresh token available or config missing");
                    return;
                }

                var authenticationClient = new AuthenticationClient();

                // Refresh the token
                var newBearer = await authenticationClient.RefreshTokenAsync(
                    _apsConfig.ClientId,
                    _apsConfig.ClientSecret,
                    _refreshToken);

                if (newBearer != null)
                {
                    lock (_lockObject)
                    {
                        _currentAccessToken = newBearer.AccessToken;
                        _refreshToken = newBearer.RefreshToken ?? _refreshToken;
                        _tokenExpiryTime = DateTime.Now.AddSeconds(double.Parse(newBearer.ExpiresIn.ToString()));
                    }

                    Console.WriteLine($"Token refreshed successfully. New expiry: {_tokenExpiryTime}");

                    // Schedule next refresh
                    StartRefreshTimer();
                }
                else
                {
                    Console.WriteLine("Failed to refresh token - bearer is null");
                    // Could trigger re-authentication here if needed
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing token: {ex.Message}");
            }
        }

        private static void RefreshTokenSync()
        {
            try
            {
                var task = RefreshTokenAsync();
                task.Wait(TimeSpan.FromSeconds(30)); // Wait max 30 seconds for refresh
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Synchronous token refresh failed: {ex.Message}");
            }
        }

        public static void Cleanup()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
        }

        // Method to check if token is valid
        public static bool IsTokenValid()
        {
            lock (_lockObject)
            {
                return !string.IsNullOrEmpty(_currentAccessToken) &&
                       DateTime.Now.AddMinutes(1) < _tokenExpiryTime;
            }
        }
    }
}