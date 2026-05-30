using FluentAssertions;
using WarehouseApp.Models;
using WarehouseApp.Services;
using Xunit;

namespace WarehouseApp.Tests.Unit;

public sealed class CounterpartyServiceTests
{
    private readonly CounterpartyService _sut = new();

    [Theory]
    [InlineData(null, "Введите ИНН")]
    [InlineData("", "Введите ИНН")]
    [InlineData("abc", "только цифры")]
    [InlineData("12345", "10")]
    public void ValidateInn_InvalidInput_ReturnsFalse(string? inn, string expectedErrorPart)
    {
        var valid = _sut.ValidateInn(inn!, out var error);

        valid.Should().BeFalse();
        error.Should().Contain(expectedErrorPart);
    }

    [Theory]
    [InlineData("7712345678")]
    [InlineData("123456789012")]
    public void ValidateInn_ValidTenOrTwelveDigits_ReturnsTrue(string inn)
    {
        var valid = _sut.ValidateInn(inn, out var error);

        valid.Should().BeTrue();
        error.Should().BeEmpty();
    }

    [Fact]
    public void Check_CleanInn_AllowsDealAndFillsAllResults()
    {
        var logistics = new ShipmentLogistics { Inn = "7712345678" };

        var result = _sut.Check(logistics);

        result.Success.Should().BeTrue();
        logistics.CheckPerformed.Should().BeTrue();
        logistics.TaxOutcome.Should().Be(CheckOutcome.Clean);
        logistics.BankruptcyOutcome.Should().Be(CheckOutcome.Clean);
        logistics.DirectorOutcome.Should().Be(CheckOutcome.Clean);
        logistics.Decision.Should().Be(DealDecision.Allowed);
        logistics.TaxNote.Should().NotBeNullOrWhiteSpace();
        logistics.BankruptcyNote.Should().NotBeNullOrWhiteSpace();
        logistics.DirectorNote.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("7700000001", nameof(ShipmentLogistics.TaxOutcome))]
    [InlineData("7700000002", nameof(ShipmentLogistics.BankruptcyOutcome))]
    [InlineData("7700000003", nameof(ShipmentLogistics.DirectorOutcome))]
    public void Check_KnownRiskInn_RequiresReview(string inn, string riskyPropertyName)
    {
        var logistics = new ShipmentLogistics { Inn = inn };

        var result = _sut.Check(logistics);

        result.Success.Should().BeTrue();
        logistics.Decision.Should().Be(DealDecision.NeedsReview);
        logistics.CheckPerformed.Should().BeTrue();

        var riskyValue = typeof(ShipmentLogistics).GetProperty(riskyPropertyName)!.GetValue(logistics);
        riskyValue.Should().Be(CheckOutcome.Risk);
    }

    [Fact]
    public void Check_InvalidInn_ReturnsValidationErrorWithoutMarkingChecked()
    {
        var logistics = new ShipmentLogistics { Inn = "abc" };

        var result = _sut.Check(logistics);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("только цифры");
        logistics.CheckPerformed.Should().BeFalse();
        logistics.Decision.Should().Be(DealDecision.NotChecked);
    }

    [Fact]
    public void ComputeDecision_WhenNotChecked_ReturnsNotChecked()
    {
        var logistics = new ShipmentLogistics { CheckPerformed = false };

        CounterpartyService.ComputeDecision(logistics).Should().Be(DealDecision.NotChecked);
    }

    [Fact]
    public void ComputeDecision_WhenCheckedAndNoRisks_ReturnsAllowed()
    {
        var logistics = new ShipmentLogistics
        {
            CheckPerformed = true,
            TaxOutcome = CheckOutcome.Clean,
            BankruptcyOutcome = CheckOutcome.Clean,
            DirectorOutcome = CheckOutcome.Clean
        };

        CounterpartyService.ComputeDecision(logistics).Should().Be(DealDecision.Allowed);
    }

    [Theory]
    [InlineData(CheckOutcome.Risk, CheckOutcome.Clean, CheckOutcome.Clean)]
    [InlineData(CheckOutcome.Clean, CheckOutcome.Risk, CheckOutcome.Clean)]
    [InlineData(CheckOutcome.Clean, CheckOutcome.Clean, CheckOutcome.Risk)]
    public void ComputeDecision_WhenAnyRisk_ReturnsNeedsReview(
        CheckOutcome tax,
        CheckOutcome bankruptcy,
        CheckOutcome director)
    {
        var logistics = new ShipmentLogistics
        {
            CheckPerformed = true,
            TaxOutcome = tax,
            BankruptcyOutcome = bankruptcy,
            DirectorOutcome = director
        };

        CounterpartyService.ComputeDecision(logistics).Should().Be(DealDecision.NeedsReview);
    }
}
