﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Bangumi.Model;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Bangumi;

public class PlaybackScrobbler : IServerEntryPoint
{
    private static readonly Dictionary<Guid, HashSet<string>> Store = new();
    private readonly BangumiApi _api;
    private readonly PluginDatabase _db;
    private readonly ILogger<PlaybackScrobbler> _log;

    private readonly Plugin _plugin;
    private readonly IUserDataManager _userDataManager;

    public PlaybackScrobbler(Plugin plugin, IUserManager userManager, IUserDataManager userDataManager, PluginDatabase db, BangumiApi api, ILogger<PlaybackScrobbler> log)
    {
        _plugin = plugin;
        _userDataManager = userDataManager;
        _db = db;
        _api = api;
        _log = log;

        foreach (var userId in userManager.UsersIds) GetPlaybackHistory(userId);
    }

    public void Dispose()
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
        GC.SuppressFinalize(this);
    }

    public Task RunAsync()
    {
        _userDataManager.UserDataSaved += OnUserDataSaved;
        return Task.CompletedTask;
    }

    private void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        switch (e.SaveReason)
        {
            case UserDataSaveReason.TogglePlayed when e.UserData.Played:
                // delay 3 seconds to avoid conflict with playback finished event
                Task.Delay(TimeSpan.FromSeconds(3))
                    .ContinueWith(_ =>
                    {
                        GetPlaybackHistory(e.UserId).Add(e.UserData.Key);
                        _log.LogInformation("mark {Name} (#{Id}) as played for user #{User}", e.Item.Name, e.Item.Id, e.UserId);
                    }).ConfigureAwait(false);
                if (_plugin.Configuration.ReportManualStatusChangeToBangumi)
                    ReportPlaybackStatus(e.Item, e.UserId, true).ConfigureAwait(false);
                break;

            case UserDataSaveReason.TogglePlayed when !e.UserData.Played:
                GetPlaybackHistory(e.UserId).Remove(e.UserData.Key);
                _log.LogInformation("mark {Name} (#{Id}) as new for user #{User}", e.Item.Name, e.Item.Id, e.UserId);
                if (_plugin.Configuration.ReportManualStatusChangeToBangumi)
                    ReportPlaybackStatus(e.Item, e.UserId, true).ConfigureAwait(false);
                break;

            case UserDataSaveReason.PlaybackFinished when e.UserData.Played:
                if (_plugin.Configuration.ReportPlaybackStatusToBangumi)
                    ReportPlaybackStatus(e.Item, e.UserId, true).ConfigureAwait(false);
                e.Keys.ForEach(key => GetPlaybackHistory(e.UserId).Add(key));
                break;
        }
    }

    private async Task ReportPlaybackStatus(BaseItem item, Guid userId, bool played)
    {
        var bangumiId = item.GetProviderId(Constants.ProviderName);

        if (string.IsNullOrEmpty(bangumiId))
        {
            _log.LogInformation("item {Name} (#{Id}) doesn't have bangumi id, ignored", item.Name, item.Id);
            return;
        }

        if (item is Movie)
        {
            // jellyfin only have subject id for movie, so we need to get episode id from bangumi api
            var episodeList = await _api.GetSubjectEpisodeListWithOffset(bangumiId, EpisodeType.Normal, 0, CancellationToken.None);
            if (episodeList?.Data.Count > 0)
                bangumiId = episodeList.Data.First().Id.ToString();
        }

        var user = _db.Logins.FindById(userId);
        if (user == null)
        {
            _log.LogInformation("access token for user #{User} not found, ignored", userId);
            return;
        }

        if (user.Expired)
        {
            _log.LogInformation("access token for user #{User} expired, ignored", userId);
            return;
        }

        if (item.GetUserDataKeys().Intersect(GetPlaybackHistory(userId)).Any())
        {
            _log.LogInformation("item {Name} (#{Id}) has been played before, ignored", item.Name, item.Id);
            return;
        }

        _log.LogInformation("report episode #{Episode} status {Status} to bangumi", bangumiId, EpisodeStatus.Watched);
        await _api.UpdateEpisodeStatus(user.AccessToken, bangumiId, played ? EpisodeStatus.Watched : EpisodeStatus.Removed, CancellationToken.None);

        _log.LogInformation("report completed");
    }

    private HashSet<string> GetPlaybackHistory(Guid userId)
    {
        if (!Store.TryGetValue(userId, out var history))
            Store[userId] = history = _userDataManager.GetAllUserData(userId).Where(item => item.Played).Select(item => item.Key).ToHashSet();
        return history;
    }
}