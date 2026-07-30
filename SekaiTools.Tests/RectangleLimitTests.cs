using System.Drawing;
using SekaiToolsCore.Utils;

namespace SekaiTools.Tests;

public class RectangleLimitTests
{
    [Fact]
    public void Limit_ClampsOversizedRectangleToBounds()
    {
        var rectangle = new Rectangle(-50, -20, 300, 200);
        var bounds = new Rectangle(0, 0, 100, 80);

        rectangle.Limit(bounds);

        Assert.Equal(bounds, rectangle);
    }

    [Fact]
    public void Limit_MovesRectangleInsideBoundsWithoutChangingValidSize()
    {
        var rectangle = new Rectangle(95, 75, 20, 10);

        rectangle.Limit(new Rectangle(0, 0, 100, 80));

        Assert.Equal(new Rectangle(80, 70, 20, 10), rectangle);
    }

    [Fact]
    public void Limit_ReturnsEmptyForInvalidBounds()
    {
        var rectangle = new Rectangle(1, 2, 3, 4);

        rectangle.Limit(Rectangle.Empty);

        Assert.Equal(Rectangle.Empty, rectangle);
    }
}
