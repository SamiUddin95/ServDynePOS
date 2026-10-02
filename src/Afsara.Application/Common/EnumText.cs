using Afsara.Domain.Enums;

namespace Afsara.Application.Common;

public static class EnumText
{
    public static string ToDisplay(this OrderType type) => type switch
    {
        OrderType.DineIn => "Dine In",
        OrderType.TakeAway => "Take Away",
        OrderType.Delivery => "Delivery",
        _ => type.ToString()
    };

    public static string ToDisplay(this PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Cash",
        PaymentMethod.Card => "Card",
        PaymentMethod.Online => "Online",
        _ => method.ToString()
    };
}
