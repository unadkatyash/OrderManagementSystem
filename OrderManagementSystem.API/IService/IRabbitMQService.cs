namespace OrderManagementSystem.API.IService
{
    public interface IRabbitMQService
    {
        void PublishOrderEvent(string queueName, object message);
        void StartConsuming(string queueName, Func<string, Task> messageHandler);
    }
}
