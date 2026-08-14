using System.Globalization;

namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates adding pricing responsibilities by wrapping the same small interface repeatedly.
/// </summary>
public sealed class DecoratorDemo : IPatternDemo
{
    public string Key => "decorator";

    public string Name => "Decorator / 装饰器模式";

    public string Category => "Structural";

    public string Intent => "在不修改原对象的前提下，通过包装动态叠加职责。";

    public IReadOnlyList<string> Run()
    {
        IPrice basePrice = new RoomRate(400m);
        IPrice discounted = new MemberDiscountDecorator(basePrice, 0.10m);
        IPrice withServiceFee = new ServiceFeeDecorator(discounted, 25m);
        IPrice finalPrice = new TaxDecorator(withServiceFee, 0.08m);

        return
        [
            $"基础房价: {Money(basePrice.Total)}",
            $"会员九折后: {Money(discounted.Total)}",
            $"加服务费后: {Money(withServiceFee.Total)}",
            $"加税后应付: {Money(finalPrice.Total)}",
            $"计算链: {finalPrice.Description}"
        ];
    }

    private interface IPrice
    {
        decimal Total { get; }

        string Description { get; }
    }

    private sealed class RoomRate(decimal amount) : IPrice
    {
        public decimal Total { get; } = amount;

        public string Description => "房价";
    }

    // Base decorator delegates the shared interface; concrete decorators add one responsibility each.
    private abstract class PriceDecorator(IPrice inner) : IPrice
    {
        protected IPrice Inner { get; } = inner;

        public abstract decimal Total { get; }

        public abstract string Description { get; }
    }

    private sealed class MemberDiscountDecorator : PriceDecorator
    {
        private readonly decimal _discountRate;

        internal MemberDiscountDecorator(IPrice inner, decimal discountRate)
            : base(inner)
        {
            if (discountRate is < 0m or > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(discountRate),
                    discountRate,
                    "The discount rate must be between 0 and 1.");
            }

            _discountRate = discountRate;
        }

        public override decimal Total => Inner.Total * (1m - _discountRate);

        public override string Description =>
            $"{Inner.Description} -> 会员折扣{_discountRate.ToString("0.#%", CultureInfo.InvariantCulture)}";
    }

    private sealed class ServiceFeeDecorator(IPrice inner, decimal fee)
        : PriceDecorator(inner)
    {
        public override decimal Total => Inner.Total + fee;

        public override string Description => $"{Inner.Description} -> 服务费{Money(fee)}";
    }

    private sealed class TaxDecorator : PriceDecorator
    {
        private readonly decimal _taxRate;

        internal TaxDecorator(IPrice inner, decimal taxRate)
            : base(inner)
        {
            if (taxRate is < 0m or > 1m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(taxRate),
                    taxRate,
                    "The tax rate must be between 0 and 1.");
            }

            _taxRate = taxRate;
        }

        public override decimal Total => Inner.Total * (1m + _taxRate);

        public override string Description =>
            $"{Inner.Description} -> 税率{_taxRate.ToString("0.#%", CultureInfo.InvariantCulture)}";
    }

    private static string Money(decimal amount) =>
        $"CNY {amount.ToString("0.00", CultureInfo.InvariantCulture)}";
}
