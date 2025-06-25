using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrderManagementSystem.API.IService;
using OrderManagementSystem.API.Models;

namespace OrderManagementSystem.API.Pages.Orders
{
    public class DetailsModel : PageModel
    {
        private readonly IOrderService _orderService;

        public DetailsModel(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public Order Order { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Order = await _orderService.GetOrderAsync(id);

            if (Order == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
