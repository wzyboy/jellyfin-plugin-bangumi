using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Bangumi.Model;

namespace Jellyfin.Plugin.Bangumi;

public partial class BangumiApi
{
    public async Task<User?> GetAccountInfo(string accessToken, CancellationToken token)
    {
        return await SendRequest<User>("https://api.bgm.tv/v0/me", accessToken, token);
    }

    public async Task UpdateCollectionStatus(string accessToken, int subjectId, CollectionType type, CancellationToken token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"https://api.bgm.tv/v0/users/-/collections/{subjectId}");
        request.Content = new JsonContent(new Collection { Type = type });
        await SendRequest(request, accessToken, token);
    }

    public async Task UpdateEpisodeStatus(string accessToken, int subjectId, int episodeId, EpisodeCollectionType status, CancellationToken token)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"https://api.bgm.tv/v0/users/-/collections/{subjectId}/episodes");
        request.Content = new JsonContent(new EpisodesCollectionInfo
        {
            EpisodeIdList = new List<int> { episodeId },
            Type = status
        });
        await SendRequest(request, accessToken, token);
    }

    private class JsonContent : StringContent
    {
        public JsonContent(object obj) : base(JsonSerializer.Serialize(obj, Options), Encoding.UTF8, "application/json")
        {
            Headers.ContentType!.CharSet = null;
        }
    }
}