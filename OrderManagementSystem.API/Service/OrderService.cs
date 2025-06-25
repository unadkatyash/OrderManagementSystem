using Microsoft.EntityFrameworkCore;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.IService;
using OrderManagementSystem.API.Models;

namespace OrderManagementSystem.API.Service
{
    public class OrderService : IOrderService
    {
        private readonly OrderContext _context;
        private readonly IRabbitMQService _rabbitMQ;
        private readonly ILogger<OrderService> _logger;

        public OrderService(OrderContext context, IRabbitMQService rabbitMQ, ILogger<OrderService> logger)
        {
            _context = context;
            _rabbitMQ = rabbitMQ;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            order.OrderDate = DateTime.UtcNow;
            order.Status = OrderStatus.Pending;
            order.TotalAmount = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Publish order created event
            var orderEvent = new OrderEvent
            {
                OrderId = order.Id,
                Status = OrderStatus.Pending,
                Message = "Order created successfully",
                Timestamp = DateTime.UtcNow
            };

            _rabbitMQ.PublishOrderEvent("order.created", orderEvent);
            _logger.LogInformation($"Order {order.Id} created successfully");

            return order;
        }

        public async Task<Order> GetOrderAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<List<Order>> GetOrdersAsync()
        {
            return await _context.Orders
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Order> UpdateOrderStatusAsync(int id, OrderStatus status)
        {
            var order = await GetOrderAsync(id);
            if (order == null) return null;

            order.Status = status;

            if (status == OrderStatus.Shipped && order.ShippedDate == null)
            {
                order.ShippedDate = DateTime.UtcNow;
                order.TrackingNumber = GenerateTrackingNumber();
            }
            else if (status == OrderStatus.Delivered && order.DeliveredDate == null)
            {
                order.DeliveredDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Publish status update event
            var orderEvent = new OrderEvent
            {
                OrderId = order.Id,
                Status = status,
                Message = $"Order status updated to {status}",
                Timestamp = DateTime.UtcNow
            };

            _rabbitMQ.PublishOrderEvent("order.status.updated", orderEvent);

            return order;
        }

        public async Task ProcessOrderAsync(int orderId)
        {
            await UpdateOrderStatusAsync(orderId, OrderStatus.Processing);

            // Simulate processing time
            await Task.Delay(2000);

            // Auto-ship after processing
            _rabbitMQ.PublishOrderEvent("order.ready.to.ship", new { OrderId = orderId });
        }

        public async Task ShipOrderAsync(int orderId)
        {
            await UpdateOrderStatusAsync(orderId, OrderStatus.Shipped);

            // Simulate shipping time
            await Task.Delay(1000);

            // Set to in-transit
            await UpdateOrderStatusAsync(orderId, OrderStatus.InTransit);

            // Schedule delivery
            _rabbitMQ.PublishOrderEvent("order.in.transit", new { OrderId = orderId });
        }

        public async Task DeliverOrderAsync(int orderId)
        {
            await UpdateOrderStatusAsync(orderId, OrderStatus.Delivered);
        }

        private string GenerateTrackingNumber()
        {
            return $"TRK{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
        }
    }
}