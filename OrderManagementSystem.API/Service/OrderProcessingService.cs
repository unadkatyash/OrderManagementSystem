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
            _rabbitMQ.StartConsuming("order.created", HandleOrderCreated);
            _rabbitMQ.StartConsuming("order.ready.to.ship", HandleReadyToShip);
            _rabbitMQ.StartConsuming("order.in.transit", HandleInTransit);

            return Task.CompletedTask;
        }

        private async Task HandleOrderCreated(string message)
        {
            var orderEvent = JsonSerializer.Deserialize<OrderEvent>(message);

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            await Task.Delay(10000);
            await orderService.ProcessOrderAsync(orderEvent.OrderId);
        }

        private async Task HandleReadyToShip(string message)
        {
            var data = JsonSerializer.Deserialize<dynamic>(message);
            var orderId = ((JsonElement)data).GetProperty("OrderId").GetInt32();

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            await Task.Delay(10000);
            await orderService.ShipOrderAsync(orderId);
        }

        private async Task HandleInTransit(string message)
        {
            var data = JsonSerializer.Deserialize<dynamic>(message);
            var orderId = ((JsonElement)data).GetProperty("OrderId").GetInt32();

            using var scope = _serviceProvider.CreateScope();
            var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

            await Task.Delay(10000);
            await orderService.DeliverOrderAsync(orderId);
        }
    }
}
