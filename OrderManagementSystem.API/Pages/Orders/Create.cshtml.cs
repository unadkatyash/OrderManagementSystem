using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderManagementSystem.API.IService;
using OrderManagementSystem.API.Models;

namespace OrderManagementSystem.API.Pages.Orders
{
    public class CreateModel : PageModel
    {
        private readonly IOrderService _orderService;

        public CreateModel(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [BindProperty]
        public Order Order { get; set; } = new();

        public void OnGet()
        {
            // Initialize with one empty order item
            Order.OrderItems.Add(new OrderItem());
        }

        [HttpPost]
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Remove empty order items
            Order.OrderItems = Order.OrderItems
                .Where(oi => !string.IsNullOrWhiteSpace(oi.ProductName) && oi.Quantity > 0 && oi.UnitPrice > 0)
                .ToList();

            if (!Order.OrderItems.Any())
            {
                ModelState.AddModelError("", "At least one order item is required.");
                return Page();
            }

            try
            {
                await _orderService.CreateOrderAsync(Order);
                TempData["SuccessMessage"] = "Order created successfully!";
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating order: {ex.Message}");
                return Page();
            }
        }
    }
}
