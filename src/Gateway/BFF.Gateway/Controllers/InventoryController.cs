using Microsoft.AspNetCore.Mvc;
using BFF.Gateway.Services;
using ERP.Contracts.Inventory;

namespace BFF.Gateway.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IGrpcClientService _grpcClientService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IGrpcClientService grpcClientService, ILogger<InventoryController> logger)
    {
        _grpcClientService = grpcClientService;
        _logger = logger;
    }

    [HttpGet("hello")]
    public async Task<IActionResult> GetHello()
    {
        try
        {
            var request = new ERP.Contracts.Inventory.HelloRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetInventoryHelloAsync(request);
            
            return Ok(new
            {
                message = response.Message,
                service = response.Service,
                timestamp = response.Timestamp?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Inventory service GetHello");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] ProductCategory category = ProductCategory.Electronics, [FromQuery] bool lowStockOnly = false)
    {
        try
        {
            var request = new GetProductsRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
                Page = page,
                PageSize = pageSize,
                Category = category,
                LowStockOnly = lowStockOnly
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetProductsAsync(request);
            
            return Ok(new
            {
                products = response.Products.Select(p => new
                {
                    id = p.Id,
                    sku = p.Sku,
                    name = p.Name,
                    description = p.Description,
                    currentStock = p.CurrentStock,
                    minimumStock = p.MinimumStock,
                    price = p.Price,
                    category = p.Category.ToString(),
                    createdAt = p.CreatedAt?.ToDateTime(),
                    updatedAt = p.UpdatedAt?.ToDateTime(),
                    isActive = p.IsActive
                }),
                totalCount = response.TotalCount,
                page = response.Page,
                pageSize = response.PageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Inventory service GetProducts");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("products/{id}")]
    public async Task<IActionResult> GetProduct(int id)
    {
        try
        {
            var request = new GetProductRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
                ProductId = id
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetProductAsync(request);
            
            if (!response.Found)
            {
                return NotFound($"Product with ID {id} not found");
            }
            
            return Ok(new
            {
                id = response.Product.Id,
                sku = response.Product.Sku,
                name = response.Product.Name,
                description = response.Product.Description,
                currentStock = response.Product.CurrentStock,
                minimumStock = response.Product.MinimumStock,
                price = response.Product.Price,
                category = response.Product.Category.ToString(),
                createdAt = response.Product.CreatedAt?.ToDateTime(),
                updatedAt = response.Product.UpdatedAt?.ToDateTime(),
                isActive = response.Product.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Inventory service GetProduct");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetInventoryStats()
    {
        try
        {
            var request = new GetInventoryStatsRequest
            {
                UserId = Request.Headers["X-User-Id"].FirstOrDefault() ?? "",
                UserName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "",
            };
            
            // Add permissions
            var permissions = Request.Headers["X-User-Permissions"].FirstOrDefault()?.Split(',') ?? Array.Empty<string>();
            request.Permissions.AddRange(permissions);

            var response = await _grpcClientService.GetInventoryStatsAsync(request);
            
            return Ok(new
            {
                totalProducts = response.TotalProducts,
                lowStockProducts = response.LowStockProducts,
                outOfStockProducts = response.OutOfStockProducts,
                totalInventoryValue = response.TotalInventoryValue,
                activeProducts = response.ActiveProducts,
                generatedAt = response.GeneratedAt?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Inventory service GetInventoryStats");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var request = new ERP.Contracts.Inventory.HealthRequest();
            var response = await _grpcClientService.GetInventoryHealthAsync(request);
            
            return Ok(new
            {
                status = response.Status,
                service = response.Service,
                timestamp = response.Timestamp?.ToDateTime()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Inventory service GetHealth");
            return StatusCode(500, "Internal server error");
        }
    }
}
