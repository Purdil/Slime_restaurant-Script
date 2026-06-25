using System;
using System.Threading.Tasks;
using _Project.Core.CustomLogging;
using Firebase;
using Firebase.Auth;
using Google;

namespace _Project.Core.Systems.Firebase.Auth
{
    public class FirebaseAuthClient : IFirebaseAuthClient
    {
        private const int GOOGLE_SESSION_CLEAR_DELAY_MS = 150;

        private readonly string _webClientId;

        private FirebaseAuth _firebaseAuth;
        private GoogleSignInConfiguration _googleSignInConfiguration;
        private bool _isInitialized;

        public FirebaseUser CurrentUser => _firebaseAuth?.CurrentUser;
        public bool HasCachedUser => _isInitialized && _firebaseAuth != null && _firebaseAuth.CurrentUser != null;

        public FirebaseAuthClient(string webClientId)
        {
            _webClientId = webClientId;
        }

        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                return true;
            }

            ConfigureGoogleSignIn();

            DependencyStatus dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();

            if (dependencyStatus != DependencyStatus.Available)
            {
                CLog.LogError($"Firebase dependency error: {dependencyStatus}");
                return false;
            }

            _firebaseAuth = FirebaseAuth.DefaultInstance;
            _isInitialized = true;

            return true;
        }

        public async Task<FirebaseUser> SignInGuestAsync()
        {
            AuthResult result = await _firebaseAuth.SignInAnonymouslyAsync();
            return result.User;
        }

        public async Task<FirebaseUser> SignInGoogleAsync()
        {
            await ClearGoogleSignInSessionAsync(false);

            Credential credential = await CreateGoogleCredentialAsync();
            if (credential == null)
            {
                return null;
            }

            FirebaseUser result = await _firebaseAuth.SignInWithCredentialAsync(credential);
            return result;
        }

        public async Task<FirebaseUser> LinkGuestWithGoogleAsync()
        {
            if (_firebaseAuth?.CurrentUser == null || _firebaseAuth.CurrentUser.IsAnonymous == false)
            {
                return null;
            }

            await ClearGoogleSignInSessionAsync(false);

            Credential credential = await CreateGoogleCredentialAsync();
            if (credential == null)
            {
                return null;
            }

            AuthResult result = await _firebaseAuth.CurrentUser.LinkWithCredentialAsync(credential);
            return result.User;
        }

        public void SignOut()
        {
            _firebaseAuth?.SignOut();
            ClearGoogleSignInSession(true);
        }

        private void ConfigureGoogleSignIn()
        {
            _googleSignInConfiguration ??= new GoogleSignInConfiguration
            {
                RequestEmail = true,
                RequestIdToken = true,
                WebClientId = _webClientId
            };

            GoogleSignIn.Configuration = _googleSignInConfiguration;
        }

        private async Task ClearGoogleSignInSessionAsync(bool shouldDisconnect)
        {
            ClearGoogleSignInSession(shouldDisconnect);
            await Task.Delay(GOOGLE_SESSION_CLEAR_DELAY_MS);
        }

        private void ClearGoogleSignInSession(bool shouldDisconnect)
        {
            try
            {
                GoogleSignIn.DefaultInstance.SignOut();
                if (shouldDisconnect)
                {
                    GoogleSignIn.DefaultInstance.Disconnect();
                }
            }
            catch (Exception exception)
            {
                CLog.LogWarning(exception.Message);
            }

            ConfigureGoogleSignIn();
        }

        private async Task<Credential> CreateGoogleCredentialAsync()
        {
            ConfigureGoogleSignIn();
            GoogleSignInUser googleUser;
            try
            {
                googleUser = await GoogleSignIn.DefaultInstance.SignIn();
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (GoogleSignIn.SignInException exception)
                when (exception.Status == GoogleSignInStatusCode.Canceled)
            {
                return null;
            }

            if (googleUser == null)
            {
                return null;
            }

            return GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
        }
    }
}
