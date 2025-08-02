var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ERP Order Service API",
        Version = "v1",
        Description = "Handles order processing, fulfillment, and order management for the ERP system",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Order Service Team",
            Email = "orders@erpprototype.com"
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
        logger.LogInformation("📦 OrderService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("📦 OrderService received direct request");
    }

    await next();
});

// Sample data
var orders = new List<Order>
{
    new(1, 101, DateTime.Now.AddDays(-5), OrderStatus.Delivered, 299.99m, "Premium headphones"),
    new(2, 102, DateTime.Now.AddDays(-3), OrderStatus.Shipped, 149.50m, "Wireless mouse and keyboard set"),
    new(3, 103, DateTime.Now.AddDays(-1), OrderStatus.Processing, 89.99m, "USB-C charging cable"),
    new(4, 104, DateTime.Now, OrderStatus.Pending, 599.99m, "4K Monitor"),
    new(5, 105, DateTime.Now, OrderStatus.Pending, 1299.99m, "Gaming laptop")
};

// API Endpoints
app.MapGet("/orders", (ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching all orders");
    return orders.OrderByDescending(o => o.OrderDate);
})
.WithName("GetAllOrders")
.WithTags("Orders")
.WithSummary("Get all orders")
.WithDescription("Returns a list of all orders in the system")
.WithOpenApi();

app.MapGet("/orders/{id:int}", (int id, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Fetching order with ID: {OrderId}", id);
    var order = orders.FirstOrDefault(o => o.Id == id);
    return order is not null ? Results.Ok(order) : Results.NotFound($"Order {id} not found");
})
.WithName("GetOrderById")
.WithTags("Orders")
.WithSummary("Get order by ID")
.WithDescription("Returns a specific order by its ID")
.WithOpenApi();

app.MapGet("/orders/customer/{customerId:int}", (int customerId, ILogger<Program> logger) =>
{
    logger.LogInformation("👤 Fetching orders for customer: {CustomerId}", customerId);
    var customerOrders = orders.Where(o => o.CustomerId == customerId).ToList();
    return customerOrders.Any() ? Results.Ok(customerOrders) : Results.NotFound($"No orders found for customer {customerId}");
})
.WithName("GetOrdersByCustomer")
.WithTags("Orders")
.WithSummary("Get orders by customer ID")
.WithDescription("Returns all orders for a specific customer")
.WithOpenApi();

app.MapPost("/orders", (CreateOrderRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("➕ Creating new order for customer: {CustomerId}", request.CustomerId);
    var newOrder = new Order(
        orders.Max(o => o.Id) + 1,
        request.CustomerId,
        DateTime.Now,
        OrderStatus.Pending,
        request.TotalAmount,
        request.Description
    );
    orders.Add(newOrder);
    logger.LogInformation("✅ Order created with ID: {OrderId}", newOrder.Id);
    return Results.Created($"/orders/{newOrder.Id}", newOrder);
})
.WithName("CreateOrder")
.WithTags("Orders")
.WithSummary("Create a new order")
.WithDescription("Creates a new order in the system")
.WithOpenApi();

app.MapPut("/orders/{id:int}/status", (int id, UpdateOrderStatusRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("🔄 Updating order {OrderId} status to: {Status}", id, request.Status);
    var order = orders.FirstOrDefault(o => o.Id == id);
    if (order is null)
        return Results.NotFound($"Order {id} not found");

    var updatedOrder = order with { Status = request.Status };
    var index = orders.FindIndex(o => o.Id == id);
    orders[index] = updatedOrder;

    logger.LogInformation("✅ Order {OrderId} status updated to: {Status}", id, request.Status);
    return Results.Ok(updatedOrder);
})
.WithName("UpdateOrderStatus")
.WithTags("Orders")
.WithSummary("Update order status")
.WithDescription("Updates the status of an existing order")
.WithOpenApi();

app.MapGet("/orders/stats", (ILogger<Program> logger) =>
{
    logger.LogInformation("📊 Generating order statistics");
    var stats = new
    {
        TotalOrders = orders.Count,
        PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
        ProcessingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
        ShippedOrders = orders.Count(o => o.Status == OrderStatus.Shipped),
        DeliveredOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
        TotalRevenue = orders.Sum(o => o.TotalAmount),
        AverageOrderValue = orders.Average(o => o.TotalAmount)
    };
    return stats;
})
.WithName("GetOrderStats")
.WithTags("Analytics")
.WithSummary("Get order statistics")
.WithDescription("Returns comprehensive order statistics and analytics")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Order Service!", Service = "OrderService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Order Service")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "OrderService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Order Service")
.WithOpenApi();

app.Run();

// Models
record Order(int Id, int CustomerId, DateTime OrderDate, OrderStatus Status, decimal TotalAmount, string Description);

record CreateOrderRequest(int CustomerId, decimal TotalAmount, string Description);

record UpdateOrderStatusRequest(OrderStatus Status);

enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}
