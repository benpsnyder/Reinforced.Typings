#if RT_E2E_MODERN
// Compiled only for the "modern" e2e variant (-p:RtE2EVariant=modern): System.Text.Json attributes,
// the 3.1.0 output options and the Effect Schema emitter. Needs System.Text.Json (net10.0/net11.0).
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace E2E.Consumer.Modern
{
    /// <summary>How the customer prefers to buy.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Channel
    {
        Web,

        [JsonStringEnumMemberName("in-store")]
        InStore
    }

    /// <summary>A payment; serialized polymorphically.</summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(CardPayment), "card")]
    [JsonDerivedType(typeof(CashPayment), "cash")]
    public class Payment
    {
        public decimal Amount { get; set; }

        public string? Reference { get; set; }
    }

    public class CardPayment : Payment
    {
        public string Last4 { get; set; } = string.Empty;
    }

    public class CashPayment : Payment
    {
        [JsonPropertyName("change_given")]
        public decimal Change { get; set; }
    }

    /// <summary>A customer.</summary>
    public class Customer
    {
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyOrder(-1)]
        public Guid Id { get; set; }

        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        public int? LoyaltyPoints { get; set; }

        public Channel PreferredChannel { get; set; }

        public List<Payment> Payments { get; set; } = new List<Payment>();

        public Dictionary<string, int> Counters { get; set; } = new Dictionary<string, int>();

        public E2E.Consumer.Models.Address[] Addresses { get; set; } = new E2E.Consumer.Models.Address[0];
    }
}
#endif
