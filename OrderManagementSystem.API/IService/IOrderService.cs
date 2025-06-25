using OrderManagementSystem.API.Models;

namespace OrderManagementSystem.API.IService
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(Order order);
        Task<Order> GetOrderAsync(int id);
        Task<List<Order>> GetOrdersAsync();
        Task<Order> UpdateOrderStatusAsync(int id, OrderStatus status);
        Task ProcessOrderAsync(int orderId);
        Task ShipOrderAsync(int orderId);
        Task DeliverOrderAsync(int orderId);
    }
}
