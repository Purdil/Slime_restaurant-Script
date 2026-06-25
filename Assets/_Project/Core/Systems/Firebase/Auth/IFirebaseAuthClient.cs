using System.Threading.Tasks;
using Firebase.Auth;

namespace _Project.Core.Systems.Firebase.Auth
{
    public interface IFirebaseAuthClient
    {
        FirebaseUser CurrentUser { get; }
        bool HasCachedUser { get; }

        Task<bool> InitializeAsync();
        Task<FirebaseUser> SignInGuestAsync();
        Task<FirebaseUser> SignInGoogleAsync();
        Task<FirebaseUser> LinkGuestWithGoogleAsync();
        void SignOut();
    }
}
