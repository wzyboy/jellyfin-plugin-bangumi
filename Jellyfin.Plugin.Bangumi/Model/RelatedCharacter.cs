using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MediaBrowser.Controller.Entities;
using PersonEntityType = MediaBrowser.Model.Entities.PersonType;

namespace Jellyfin.Plugin.Bangumi.Model;

public class RelatedCharacter
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public CharacterType Type { get; set; }

    public Dictionary<string, string> Images { get; set; } = new();

    [JsonIgnore]
    public string? DefaultImage => Images?["large"];

    public string Relation { get; set; } = "";

    public Person[]? Actors { get; set; }

    public IEnumerable<PersonInfo> ToPersonInfos()
    {
        if (Actors == null)
            return Enumerable.Empty<PersonInfo>();
#if !EMBY
        return Actors.Select(actor => new PersonInfo
        {
            Name = actor.Name,
            Role = Name,
            ImageUrl = actor.DefaultImage,
            Type = PersonEntityType.Actor,
            ProviderIds = new Dictionary<string, string> { { Constants.ProviderName, $"{actor.Id}" } }
        });
#else
        var result = new List<PersonInfo>();
        foreach (var actor in Actors)
        {
            var person = new PersonInfo
            {
                Name = actor.Name,
                Role = Name,
                ImageUrl = actor.DefaultImage,
                Type = PersonEntityType.Actor
            };
            person.ProviderIds.Add(Constants.ProviderName, $"{actor.Id}");
            result.Add(person);
        }

        return result;
#endif
    }
}