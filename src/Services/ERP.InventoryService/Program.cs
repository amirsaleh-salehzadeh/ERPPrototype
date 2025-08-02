var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ERP Inventory Service API",
        Version = "v1",
        Description = "Manages product inventory, stock levels, and warehouse operations for the ERP system",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Inventory Service Team",
            Email = "inventory@erpprototype.com"
        }
    });
});
builder.Services.AddLogging();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Check if request came through gateway
    var serviceName = context.Request.Headers["X-Service-Name"].FirstOrDefault();
    if (!string.IsNullOrEmpty(serviceName))
    {
        logger.LogInformation("📦 InventoryService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("📦 InventoryService received direct request");
    }

    await next();
});

// Sample data
var products = new List<Product>
{
    new(1, "LAPTOP001", "Gaming Laptop", "High-performance gaming laptop", 50, 25, 1299.99m, ProductCategory.Electronics),
    new(2, "MOUSE001", "Wireless Mouse", "Ergonomic wireless mouse", 200, 50, 49.99m, ProductCategory.Electronics),
    new(3, "DESK001", "Standing Desk", "Adjustable height standing desk", 15, 5, 599.99m, ProductCategory.Furniture),
    new(4, "CHAIR001", "Office Chair", "Ergonomic office chair", 30, 10, 299.99m, ProductCategory.Furniture),
    new(5, "MONITOR001", "4K Monitor", "27-inch 4K display", 75, 20, 399.99m, ProductCategory.Electronics),
    new(6, "KEYBOARD001", "Mechanical Keyboard", "RGB mechanical keyboard", 120, 30, 149.99m, ProductCategory.Electronics),
    new(7, "NOTEBOOK001", "Spiral Notebook", "A4 spiral notebook", 500, 100, 5.99m, ProductCategory.Office),
    new(8, "PEN001", "Ballpoint Pen", "Blue ink ballpoint pen", 1000, 200, 1.99m, ProductCategory.Office)
};

// API Endpoints
app.MapGet("/products", (ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching all products");
    return products.OrderBy(p => p.Name);
})
.WithName("GetAllProducts")
.WithTags("Products")
.WithSummary("Get all products")
.WithDescription("Returns a list of all products in inventory")
.WithOpenApi();

app.MapGet("/products/{id:int}", (int id, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Fetching product with ID: {ProductId}", id);
    var product = products.FirstOrDefault(p => p.Id == id);
    return product is not null ? Results.Ok(product) : Results.NotFound($"Product {id} not found");
})
.WithName("GetProductById")
.WithTags("Products")
.WithSummary("Get product by ID")
.WithDescription("Returns a specific product by its ID")
.WithOpenApi();

app.MapGet("/products/sku/{sku}", (string sku, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Fetching product with SKU: {SKU}", sku);
    var product = products.FirstOrDefault(p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
    return product is not null ? Results.Ok(product) : Results.NotFound($"Product with SKU {sku} not found");
})
.WithName("GetProductBySKU")
.WithTags("Products")
.WithSummary("Get product by SKU")
.WithDescription("Returns a specific product by its SKU")
.WithOpenApi();

app.MapGet("/products/low-stock", (ILogger<Program> logger) =>
{
    logger.LogInformation("⚠️ Fetching low stock products");
    var lowStockProducts = products.Where(p => p.CurrentStock <= p.MinimumStock).ToList();
    return lowStockProducts;
})
.WithName("GetLowStockProducts")
.WithTags("Inventory")
.WithSummary("Get low stock products")
.WithDescription("Returns products that are at or below minimum stock levels")
.WithOpenApi();

app.MapGet("/products/category/{category}", (ProductCategory category, ILogger<Program> logger) =>
{
    logger.LogInformation("🏷️ Fetching products in category: {Category}", category);
    var categoryProducts = products.Where(p => p.Category == category).ToList();
    return categoryProducts;
})
.WithName("GetProductsByCategory")
.WithTags("Products")
.WithSummary("Get products by category")
.WithDescription("Returns all products in a specific category")
.WithOpenApi();

app.MapPost("/products", (CreateProductRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("➕ Creating new product: {ProductName}", request.Name);
    var newProduct = new Product(
        products.Max(p => p.Id) + 1,
        request.SKU,
        request.Name,
        request.Description,
        request.CurrentStock,
        request.MinimumStock,
        request.Price,
        request.Category
    );
    products.Add(newProduct);
    logger.LogInformation("✅ Product created with ID: {ProductId}", newProduct.Id);
    return Results.Created($"/products/{newProduct.Id}", newProduct);
})
.WithName("CreateProduct")
.WithTags("Products")
.WithSummary("Create a new product")
.WithDescription("Creates a new product in the inventory")
.WithOpenApi();

app.MapPut("/products/{id:int}/stock", (int id, UpdateStockRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("📊 Updating stock for product {ProductId}: {Operation} {Quantity}", id, request.Operation, request.Quantity);
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product is null)
        return Results.NotFound($"Product {id} not found");

    var newStock = request.Operation switch
    {
        StockOperation.Add => product.CurrentStock + request.Quantity,
        StockOperation.Remove => Math.Max(0, product.CurrentStock - request.Quantity),
        StockOperation.Set => request.Quantity,
        _ => product.CurrentStock
    };

    var updatedProduct = product with { CurrentStock = newStock };
    var index = products.FindIndex(p => p.Id == id);
    products[index] = updatedProduct;

    logger.LogInformation("✅ Product {ProductId} stock updated to: {NewStock}", id, newStock);
    return Results.Ok(updatedProduct);
})
.WithName("UpdateProductStock")
.WithTags("Inventory")
.WithSummary("Update product stock")
.WithDescription("Updates the stock level of a product")
.WithOpenApi();

app.MapGet("/inventory/stats", (ILogger<Program> logger) =>
{
    logger.LogInformation("📊 Generating inventory statistics");
    var stats = new
    {
        TotalProducts = products.Count,
        TotalStockValue = products.Sum(p => p.CurrentStock * p.Price),
        LowStockProducts = products.Count(p => p.CurrentStock <= p.MinimumStock),
        OutOfStockProducts = products.Count(p => p.CurrentStock == 0),
        CategoryBreakdown = products.GroupBy(p => p.Category)
            .Select(g => new { Category = g.Key.ToString(), Count = g.Count(), TotalValue = g.Sum(p => p.CurrentStock * p.Price) })
            .ToList()
    };
    return stats;
})
.WithName("GetInventoryStats")
.WithTags("Analytics")
.WithSummary("Get inventory statistics")
.WithDescription("Returns comprehensive inventory statistics and analytics")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Inventory Service!", Service = "InventoryService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Inventory Service")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "InventoryService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Inventory Service")
.WithOpenApi();

app.Run();

// Models
record Product(int Id, string SKU, string Name, string Description, int CurrentStock, int MinimumStock, decimal Price, ProductCategory Category);

record CreateProductRequest(string SKU, string Name, string Description, int CurrentStock, int MinimumStock, decimal Price, ProductCategory Category);

record UpdateStockRequest(StockOperation Operation, int Quantity);

enum ProductCategory
{
    Electronics,
    Furniture,
    Office,
    Clothing,
    Books,
    Other
}

enum StockOperation
{
    Add,
    Remove,
    Set
}
