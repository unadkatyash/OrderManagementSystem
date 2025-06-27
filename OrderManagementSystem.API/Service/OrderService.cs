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

            var orderEvent = new OrderEvent
            {
                OrderId = order.Id,
                Status = OrderStatus.Pending,
                Message = "Order created successfully",
                Timestamp = DateTime.UtcNow
            };

            _rabbitMQ.PublishOrderEvent("order.created", orderEvent);

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

            // Prevent status updates if order is already cancelled or delivered
            if (order.Status == OrderStatus.Cancelled)
            {
                _logger.LogWarning($"Cannot update status for cancelled order {id}");
                return order;
            }

            if (order.Status == OrderStatus.Delivered && status != OrderStatus.Cancelled)
            {
                _logger.LogWarning($"Cannot update status for delivered order {id}");
                return order;
            }

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
            else if (status == OrderStatus.Cancelled)
            {
                // Clear shipping/delivery dates if cancelled
                order.ShippedDate = null;
                order.DeliveredDate = null;
            }

            await _context.SaveChangesAsync();

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
            // Check if order is still valid before processing
            var order = await GetOrderAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled)
            {
                _logger.LogInformation($"Order {orderId} is cancelled or not found. Cannot process.");
                return;
            }

            await UpdateOrderStatusAsync(orderId, OrderStatus.Processing);

            await Task.Delay(10000);

            // Check again after delay to make sure it wasn't cancelled during processing
            order = await GetOrderAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled)
            {
                _logger.LogInformation($"Order {orderId} was cancelled during processing. Stopping workflow.");
                return;
            }

            _rabbitMQ.PublishOrderEvent("order.ready.to.ship", new { OrderId = orderId });
        }

        public async Task ShipOrderAsync(int orderId)
        {
            // Check if order is still valid before shipping
            var order = await GetOrderAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled)
            {
                _logger.LogInformation($"Order {orderId} is cancelled or not found. Cannot ship.");
                return;
            }

            await UpdateOrderStatusAsync(orderId, OrderStatus.Shipped);

            await Task.Delay(10000);

            // Check again after delay
            order = await GetOrderAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled)
            {
                _logger.LogInformation($"Order {orderId} was cancelled during shipping. Stopping workflow.");
                return;
            }

            await UpdateOrderStatusAsync(orderId, OrderStatus.InTransit);

            _rabbitMQ.PublishOrderEvent("order.in.transit", new { OrderId = orderId });
        }

        public async Task DeliverOrderAsync(int orderId)
        {
            // Check if order is still valid before delivery
            var order = await GetOrderAsync(orderId);
            if (order == null || order.Status == OrderStatus.Cancelled)
            {
                _logger.LogInformation($"Order {orderId} is cancelled or not found. Cannot deliver.");
                return;
            }

            await UpdateOrderStatusAsync(orderId, OrderStatus.Delivered);
        }

        private string GenerateTrackingNumber()
        {
            return $"TRK{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
        }
    }
}