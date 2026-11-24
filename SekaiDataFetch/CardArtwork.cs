using SekaiToolsBase.DataList;
using SekaiDataFetch.Source;

namespace SekaiDataFetch;

public static class CardArtwork
{
    public static bool HasTrainedArtwork(Card card) => card.CardRarityType is "rarity_3" or "rarity_4";

    public static Uri? Locate(SourceData source, Card card, bool trained)
    {
        var bundle = card.AssetbundleName;
        if (string.IsNullOrWhiteSpace(bundle) ||
            !bundle.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-') ||
            (trained && !HasTrainedArtwork(card)) ||
            !Uri.TryCreate(source.StorageBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
            root.Scheme is not ("https" or "http"))
            return null;

        var fileName = trained ? "card_after_training.png" : "card_normal.png";
        var template = source.MemberStoryTemplate;
        var separator = template.LastIndexOf('/');
        var directory = template[..(separator + 1)].Replace("{abName}", bundle, StringComparison.Ordinal);
        return Uri.TryCreate(root.AbsoluteUri + directory.TrimStart('/') + fileName, UriKind.Absolute, out var artwork)
            ? artwork
            : null;
    }
}
