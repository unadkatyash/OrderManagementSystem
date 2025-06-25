using OrderManagementSystem.API.IService;
using OrderManagementSystem.API.Models;
using System.Text.Json;

namespace OrderManagementSystem.API.Service
{
    public class OrderProcessingService : BackgroundService
    {
        private readonly IRabbitMQService _rabbitMQ;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderProcessingService> _logger;

        public OrderProcessingService(IRabbitMQService rabbitMQ, IServiceProvider serviceProvider, ILogger<OrderProcessingService> logger)
        {
            _rabbitMQ = rabbitMQ;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Start consuming different queues
            _rabbitMQ.StartConsuming("order.created", HandleOrderCreated);
            _rabbitMQ.StartConsuming("order.ready.to.ship", HandleReadyToShip);
            _rabbitMQ.StartConsuming("order.in.transit", HandleInTransit);

            return Task.CompletedTask;
        }

        private async Task HandleOrderCreated(string message)
        {
            var orderEvent = JsonSerializer.Deserialize<OrderEvent>(message);
            _logger.LogInformation($"Processing order created event for Order {orderEvent.OrderId}");

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            // Auto-process the order after a delay
            await Task.Delay(5000); // Simulate processing time
            await orderService.ProcessOrderAsync(orderEvent.OrderId);
        }

        private async Task HandleReadyToShip(string message)
        {
            var data = JsonSerializer.Deserialize<dynamic>(message);
            var orderId = ((JsonElement)data).GetProperty("OrderId").GetInt32();

            _logger.LogInformation($"Processing ready to ship event for Order {orderId}");

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            await Task.Delay(3000); // Simulate shipping preparation
            await orderService.ShipOrderAsync(orderId);
        }

        private async Task HandleInTransit(string message)
        {
            var data = JsonSerializer.Deserialize<dynamic>(message);
            var orderId = ((JsonElement)data).GetProperty("OrderId").GetInt32();

            _logger.LogInformation($"Processing in transit event for Order {orderId}");

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            // Simulate delivery time
            await Task.Delay(10000);
            await orderService.DeliverOrderAsync(orderId);
        }
    }
}
