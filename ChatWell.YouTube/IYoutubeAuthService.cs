using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;

namespace ChatWell.YouTube
{
    public interface IYouTubeAuthService
    {
        Task<UserCredential> GetUserCredentialAsync();
    }
}
