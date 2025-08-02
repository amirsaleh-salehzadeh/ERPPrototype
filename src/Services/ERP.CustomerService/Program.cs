var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ERP Customer Service API",
        Version = "v1",
        Description = "Manages customer data, relationships, and customer-related operations for the ERP system",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Customer Service Team",
            Email = "customers@erpprototype.com"
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
        logger.LogInformation("👤 CustomerService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("👤 CustomerService received direct request");
    }

    await next();
});

// Sample data
var customers = new List<Customer>
{
    new(101, "John", "Doe", "john.doe@email.com", "+1-555-0101",
        new Address("123 Main St", "Anytown", "CA", "12345", "USA"),
        DateTime.Now.AddYears(-2), CustomerType.Premium),
    new(102, "Jane", "Smith", "jane.smith@email.com", "+1-555-0102",
        new Address("456 Oak Ave", "Springfield", "NY", "67890", "USA"),
        DateTime.Now.AddYears(-1), CustomerType.Standard),
    new(103, "Bob", "Johnson", "bob.johnson@email.com", "+1-555-0103",
        new Address("789 Pine Rd", "Riverside", "TX", "54321", "USA"),
        DateTime.Now.AddMonths(-6), CustomerType.Standard),
    new(104, "Alice", "Williams", "alice.williams@email.com", "+1-555-0104",
        new Address("321 Elm St", "Lakeside", "FL", "98765", "USA"),
        DateTime.Now.AddMonths(-3), CustomerType.Premium),
    new(105, "Charlie", "Brown", "charlie.brown@email.com", "+1-555-0105",
        new Address("654 Maple Dr", "Hilltown", "WA", "13579", "USA"),
        DateTime.Now.AddMonths(-1), CustomerType.Basic)
};

// API Endpoints
app.MapGet("/customers", (ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching all customers");
    return customers.OrderBy(c => c.LastName).ThenBy(c => c.FirstName);
})
.WithName("GetAllCustomers")
.WithTags("Customers")
.WithSummary("Get all customers")
.WithDescription("Returns a list of all customers in the system")
.WithOpenApi();

app.MapGet("/customers/{id:int}", (int id, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Fetching customer with ID: {CustomerId}", id);
    var customer = customers.FirstOrDefault(c => c.Id == id);
    return customer is not null ? Results.Ok(customer) : Results.NotFound($"Customer {id} not found");
})
.WithName("GetCustomerById")
.WithTags("Customers")
.WithSummary("Get customer by ID")
.WithDescription("Returns a specific customer by their ID")
.WithOpenApi();

app.MapGet("/customers/search", (string? email, string? phone, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Searching customers with email: {Email}, phone: {Phone}", email, phone);

    var results = customers.AsQueryable();

    if (!string.IsNullOrEmpty(email))
        results = results.Where(c => c.Email.Contains(email, StringComparison.OrdinalIgnoreCase));

    if (!string.IsNullOrEmpty(phone))
        results = results.Where(c => c.Phone.Contains(phone));

    return results.ToList();
})
.WithName("SearchCustomers")
.WithTags("Customers")
.WithSummary("Search customers")
.WithDescription("Search customers by email or phone number")
.WithOpenApi();

app.MapGet("/customers/type/{type}", (CustomerType type, ILogger<Program> logger) =>
{
    logger.LogInformation("🏷️ Fetching customers of type: {CustomerType}", type);
    var typeCustomers = customers.Where(c => c.Type == type).ToList();
    return typeCustomers;
})
.WithName("GetCustomersByType")
.WithTags("Customers")
.WithSummary("Get customers by type")
.WithDescription("Returns all customers of a specific type (Basic, Standard, Premium)")
.WithOpenApi();

app.MapPost("/customers", (CreateCustomerRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("➕ Creating new customer: {FirstName} {LastName}", request.FirstName, request.LastName);
    var newCustomer = new Customer(
        customers.Max(c => c.Id) + 1,
        request.FirstName,
        request.LastName,
        request.Email,
        request.Phone,
        request.Address,
        DateTime.Now,
        request.Type
    );
    customers.Add(newCustomer);
    logger.LogInformation("✅ Customer created with ID: {CustomerId}", newCustomer.Id);
    return Results.Created($"/customers/{newCustomer.Id}", newCustomer);
})
.WithName("CreateCustomer")
.WithTags("Customers")
.WithSummary("Create a new customer")
.WithDescription("Creates a new customer in the system")
.WithOpenApi();

app.MapPut("/customers/{id:int}", (int id, UpdateCustomerRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("🔄 Updating customer {CustomerId}", id);
    var customer = customers.FirstOrDefault(c => c.Id == id);
    if (customer is null)
        return Results.NotFound($"Customer {id} not found");

    var updatedCustomer = customer with
    {
        FirstName = request.FirstName,
        LastName = request.LastName,
        Email = request.Email,
        Phone = request.Phone,
        Address = request.Address,
        Type = request.Type
    };

    var index = customers.FindIndex(c => c.Id == id);
    customers[index] = updatedCustomer;

    logger.LogInformation("✅ Customer {CustomerId} updated", id);
    return Results.Ok(updatedCustomer);
})
.WithName("UpdateCustomer")
.WithTags("Customers")
.WithSummary("Update customer")
.WithDescription("Updates an existing customer's information")
.WithOpenApi();

app.MapGet("/customers/stats", (ILogger<Program> logger) =>
{
    logger.LogInformation("📊 Generating customer statistics");
    var stats = new
    {
        TotalCustomers = customers.Count,
        BasicCustomers = customers.Count(c => c.Type == CustomerType.Basic),
        StandardCustomers = customers.Count(c => c.Type == CustomerType.Standard),
        PremiumCustomers = customers.Count(c => c.Type == CustomerType.Premium),
        NewCustomersThisMonth = customers.Count(c => c.CreatedDate >= DateTime.Now.AddMonths(-1)),
        CustomersByState = customers.GroupBy(c => c.Address.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList()
    };
    return stats;
})
.WithName("GetCustomerStats")
.WithTags("Analytics")
.WithSummary("Get customer statistics")
.WithDescription("Returns comprehensive customer statistics and analytics")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Customer Service!", Service = "CustomerService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Customer Service")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "CustomerService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Customer Service")
.WithOpenApi();

app.Run();

// Models
record Customer(int Id, string FirstName, string LastName, string Email, string Phone, Address Address, DateTime CreatedDate, CustomerType Type);

record Address(string Street, string City, string State, string ZipCode, string Country);

record CreateCustomerRequest(string FirstName, string LastName, string Email, string Phone, Address Address, CustomerType Type);

record UpdateCustomerRequest(string FirstName, string LastName, string Email, string Phone, Address Address, CustomerType Type);

enum CustomerType
{
    Basic,
    Standard,
    Premium
}
