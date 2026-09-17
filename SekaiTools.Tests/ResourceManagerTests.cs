using System.Security.Cryptography;
using SekaiToolsInfrastructure.Resources;
using Xunit;

namespace SekaiTools.Tests;

public class ResourceManagerTests
{
    [Fact]
    public async Task ResourceValidationAcceptsMatchingSizeAndMd5()
    {
        var filename = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filename, "resource-content");
            var bytes = await File.ReadAllBytesAsync(filename);
            var md5 = Convert.ToHexString(MD5.HashData(bytes));
            var resource = new Resource { Path = "test.bin", Size = bytes.Length, Md5 = md5 };

            Assert.True(ResourceManager.IsResourceValid(filename, resource));
            Assert.False(ResourceManager.IsResourceValid(filename, new Resource
            {
                Path = resource.Path,
                Size = bytes.Length + 1,
                Md5 = resource.Md5
            }));
        }
        finally
        {
            File.Delete(filename);
        }
    }
}
