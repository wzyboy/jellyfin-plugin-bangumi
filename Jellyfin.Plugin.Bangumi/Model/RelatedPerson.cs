using System.Collections.Generic;
using System.Text.Json.Serialization;
using MediaBrowser.Controller.Entities;
using PersonEntityType = MediaBrowser.Model.Entities.PersonType;

namespace Jellyfin.Plugin.Bangumi.Model;

public class RelatedPerson
{
    public int Id { get; set; }

    public int Type { get; set; }

    public string Name { get; set; } = "";

    public List<PersonCareer>? Career { get; set; }

    public Dictionary<string, string> Images { get; set; } = new();

    [JsonIgnore]
    public string? DefaultImage => Images?["large"];

    public string? Relation { get; set; }

    public PersonInfo? ToPersonInfo()
    {
#if !EMBY
        var personInfo = new PersonInfo
        {
            Name = Name,
            ImageUrl = DefaultImage,
            Type = Relation switch
            {
                "导演" => PersonEntityType.Director,
                "制片人" => PersonEntityType.Producer,
                "系列构成" => PersonEntityType.Composer,
                "脚本" => PersonEntityType.Writer,
                _ => ""
            },
            ProviderIds = new Dictionary<string, string> { { Constants.ProviderName, $"{Id}" } }
        };
        return string.IsNullOrEmpty(personInfo.Type) ? null : personInfo;
#else
        PersonEntityType? type = Relation switch
        {
            "导演" => PersonEntityType.Director,
            "制片人" => PersonEntityType.Producer,
            "系列构成" => PersonEntityType.Composer,
            "脚本" => PersonEntityType.Writer,
            _ => null
        };
        if (type == null)
            return null;
        var personInfo = new PersonInfo
        {
            Name = Name,
            ImageUrl = DefaultImage,
            Type = type.GetValueOrDefault()
        };
        personInfo.ProviderIds.Add(Constants.ProviderName, $"{Id}");
        return personInfo;
#endif
    }
}