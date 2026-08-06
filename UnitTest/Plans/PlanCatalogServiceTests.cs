using FluentAssertions;
using MotoSOS.API.Modules.Plans.Application;

namespace UnitTest.Plans;

public sealed class PlanCatalogServiceTests
{
    [Fact]
    public void GetPlansReturnsBasicPlusAndFamilyPro()
    {
        var service = new PlanCatalogService();

        var response = service.GetPlans();

        response.Plans.Select(plan => plan.Tier).Should().Equal("Basic", "Plus", "FamilyPro");
    }

    [Fact]
    public void BasicIsDefaultSelectableAndHasBasicLimits()
    {
        var service = new PlanCatalogService();

        var basic = service.GetPlans().Plans.Single(plan => plan.Tier == "Basic");

        basic.IsDefault.Should().BeTrue();
        basic.IsSelectableInWeb.Should().BeTrue();
        basic.IsPaid.Should().BeFalse();
        basic.Limits.MaxEmergencyContacts.Should().Be(1);
        basic.Limits.MaxVehicles.Should().Be(1);
    }

    [Fact]
    public void PaidPlansAreVisibleButNotSelectableInWebAndHaveHigherLimits()
    {
        var service = new PlanCatalogService();

        var plus = service.GetPlans().Plans.Single(plan => plan.Tier == "Plus");
        var family = service.GetPlans().Plans.Single(plan => plan.Tier == "FamilyPro");

        plus.IsSelectableInWeb.Should().BeFalse();
        family.IsSelectableInWeb.Should().BeFalse();
        plus.IsPaid.Should().BeTrue();
        family.IsPaid.Should().BeTrue();
        plus.Limits.MaxEmergencyContacts.Should().BeGreaterThan(1);
        family.Limits.MaxVehicles.Should().BeGreaterThan(plus.Limits.MaxVehicles);
    }
}
