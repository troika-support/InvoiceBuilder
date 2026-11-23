using InvoiceBuilder.Domain.Businesses;

namespace InvoiceBuilder.Domain.Tests;

public sealed class BusinessLogoTests
{
    private static Business NewBusiness() =>
        Business.Create(TestData.TenantId, "Troika", "Troika (Pvt) Ltd", "hello@troika.co.zw", "Harare", TestData.Now);

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/svg+xml")]
    public void Logo_png_or_svg_up_to_1_mb_is_accepted(string contentType)
    {
        var business = NewBusiness();

        business.SetLogo(new byte[Business.MaxLogoBytes], contentType, TestData.Now).IsSuccess.ShouldBeTrue();

        business.LogoContentType.ShouldBe(contentType);
        business.Logo!.Length.ShouldBe(Business.MaxLogoBytes);
    }

    [Fact]
    public void Logo_over_1_mb_is_rejected()
    {
        var business = NewBusiness();

        business.SetLogo(new byte[Business.MaxLogoBytes + 1], "image/png", TestData.Now).IsFailure.ShouldBeTrue();
        business.Logo.ShouldBeNull();
    }

    [Fact]
    public void Logo_of_other_type_is_rejected() =>
        NewBusiness().SetLogo([1, 2, 3], "image/jpeg", TestData.Now).IsFailure.ShouldBeTrue();

    [Fact]
    public void Logo_can_be_removed()
    {
        var business = NewBusiness();
        business.SetLogo([1, 2, 3], "image/png", TestData.Now);

        business.RemoveLogo(TestData.Now);

        business.Logo.ShouldBeNull();
        business.LogoContentType.ShouldBeNull();
    }
}
