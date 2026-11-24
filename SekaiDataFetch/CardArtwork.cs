using SekaiToolsBase.DataList;

namespace SekaiDataFetch;

public static class CardArtwork
{
    public static bool HasTrainedArtwork(Card card) => card.CardRarityType is "rarity_3" or "rarity_4";

    public static Uri? Locate(string storageBaseUrl, Card card, bool trained)
    {
        var bundle = card.AssetbundleName;
        if (string.IsNullOrWhiteSpace(bundle) ||
            !bundle.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-') ||
            (trained && !HasTrainedArtwork(card)) ||
            !Uri.TryCreate(storageBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
            root.Scheme is not ("https" or "http"))
            return null;

        var fileName = trained ? "card_after_training.png" : "card_normal.png";
        return new Uri(root, $"character/member/{bundle}/{fileName}");
    }
}
