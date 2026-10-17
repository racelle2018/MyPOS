using System.Reflection;
using RetailForma.Core;
using RetailForma.Core.Services;

namespace RetailForma.Tests;

public class ProductBrandTests
{
    [Fact]
    public void Public_brand_and_new_license_requests_use_RetailForma_identity()
    {
        Assert.Equal("RetailForma", ProductBrand.Name);
        Assert.Equal("RetailForma License Generator", ProductBrand.LicenseGeneratorTitle);
        Assert.Equal("RetailForma.Basic", new LicenseRequest(1, "TEST COMPANY", new string('A', 64)).Product);
        Assert.Equal("RetailForma", typeof(ProductBrand).Assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product);
    }
}
