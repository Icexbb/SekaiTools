using SekaiDataFetch;
using SekaiToolsBase.DataList;

namespace SekaiTools.Tests;

public class CardArtworkTests
{
    [Theory]
    [InlineData("rarity_1", false)]
    [InlineData("rarity_2", false)]
    [InlineData("rarity_3", true)]
    [InlineData("rarity_4", true)]
    [InlineData("rarity_birthday", false)]
    public void UsesCardBundleAndOnlyOffersTrainingForEligibleRarities(string rarity, bool trained)
    {
        var card = new Card { Id = 1499, AssetbundleName = "res005_no056", CardRarityType = rarity };
        const string source = "https://example.test/assets";
        const string folder = source + "/character/member/res005_no056/";

        Assert.Equal(folder + "card_normal.png", CardArtwork.Locate(source, card, false)?.AbsoluteUri);
        Assert.Equal(trained ? folder + "card_after_training.png" : null,
            CardArtwork.Locate(source, card, true)?.AbsoluteUri);
    }
}
