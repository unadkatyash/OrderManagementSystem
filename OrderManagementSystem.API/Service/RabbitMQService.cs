using OrderManagementSystem.API.IService;
using System.Text.Json;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderManagementSystem.API.Service
{
    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;

        public RabbitMQService(ILogger<RabbitMQService> logger)
        {
            _logger = logger;
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }

        public void PublishOrderEvent(string queueName, object message)
        {
            try
            {
                _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);
                _channel.BasicPublish(exchange: "", routingKey: queueName, body: body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing message to {queueName}");
            }
        }

        public void StartConsuming(string queueName, Func<string, Task> messageHandler)
        {
            try
            {
                _channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);
                var consumer = new EventingBasicConsumer(_channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    try
                    {
                        await messageHandler(message);
                        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                };
                _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error starting consumer for {queueName}");
            }
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}