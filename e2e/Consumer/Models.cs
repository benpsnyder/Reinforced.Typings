using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Reinforced.Typings.Attributes;

namespace E2E.Consumer.Models
{
    /// <summary>Lifecycle state of an order.</summary>
    [TsEnum]
    public enum OrderStatus
    {
        /// <summary>Created but not yet submitted.</summary>
        Draft = 0,

        /// <summary>Submitted for fulfilment.</summary>
        Submitted = 1,

        /// <summary>Handed to the carrier.</summary>
        Shipped = 5,

        Cancelled = 10
    }

    /// <summary>
    /// A customer order.
    /// </summary>
    /// <remarks>
    /// Remarks span
    /// two lines.
    /// </remarks>
    [TsInterface]
    public class Order
    {
        /// <summary>Order identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>Human-readable order number.</summary>
        public string Number { get; set; } = string.Empty;

        /// <summary>Optional note; nullable reference type.</summary>
        public string? Note { get; set; }

        public decimal Total { get; set; }

        public int? Priority { get; set; }

        public DateTime CreatedAt { get; set; }

        public OrderStatus Status { get; set; }

        public Currency Currency { get; set; }

        public List<OrderLine> Lines { get; set; } = new List<OrderLine>();

        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        public Address[] Addresses { get; set; } = new Address[0];

        [TsIgnore]
        public string Secret { get; set; } = string.Empty;

        [TsProperty(Type = "Record<string, unknown>")]
        public object? Extra { get; set; }
    }

    /// <summary>A line of an order.</summary>
    public class OrderLine
    {
        public string Sku { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public IReadOnlyList<string> Options { get; set; } = new List<string>();
    }

    public class Address
    {
        public string Street { get; set; } = string.Empty;

        public string City { get; set; } = string.Empty;

        public string? PostalCode { get; set; }
    }

    /// <summary>Anything that tracks modification time.</summary>
    public interface IAuditable
    {
        DateTimeOffset ModifiedAt { get; }

        string? ModifiedBy { get; }
    }

    /// <summary>An order that carries audit information.</summary>
    [TsInterface]
    public class AuditedOrder : Order, IAuditable
    {
        public DateTimeOffset ModifiedAt { get; set; }

        public string? ModifiedBy { get; set; }
    }

    /// <summary>One page of results.</summary>
    /// <typeparam name="T">Item type.</typeparam>
    [TsInterface]
    public class Page<T>
    {
        public IReadOnlyList<T> Items { get; set; } = new List<T>();

        public int Total { get; set; }

        public Page<T>? Next { get; set; }
    }

    /// <summary>Order operations exposed to the client.</summary>
    [TsClass(AutoExportMethods = true, AutoExportProperties = false)]
    public class OrderService
    {
        /// <summary>Loads one order.</summary>
        /// <param name="id">The order identifier.</param>
        /// <returns>The order, if found.</returns>
        public Task<Order?> Load(Guid id) => Task.FromResult<Order?>(null);

        /// <summary>Lists orders.</summary>
        /// <param name="page">Zero-based page index.</param>
        /// <param name="status">Optional status filter.</param>
        public Page<Order> List(int page, OrderStatus? status = null) => new Page<Order>();

        /// <summary>Cancels an order.</summary>
        /// <param name="id">The order identifier.</param>
        /// <param name="reason">Why it was cancelled.</param>
        public void Cancel(Guid id, string? reason = null)
        {
        }
    }

    public enum Currency
    {
        Usd,
        Eur,
        Jpy
    }

    public static class Outer
    {
        /// <summary>A nested type.</summary>
        [TsInterface]
        public class Nested
        {
            public bool Flag { get; set; }

            public long[] Values { get; set; } = new long[0];
        }
    }
}
