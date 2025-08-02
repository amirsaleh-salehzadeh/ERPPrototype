var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ERP Finance Service API",
        Version = "v1",
        Description = "Handles accounting, invoicing, financial reporting, and financial operations for the ERP system",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Finance Service Team",
            Email = "finance@erpprototype.com"
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
        logger.LogInformation("💰 FinanceService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("💰 FinanceService received direct request");
    }

    await next();
});

// Sample data
var invoices = new List<Invoice>
{
    new(1001, 101, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(-30).AddDays(30), 1299.99m, InvoiceStatus.Paid, "Gaming laptop purchase"),
    new(1002, 102, DateTime.Now.AddDays(-25), DateTime.Now.AddDays(-25).AddDays(30), 149.50m, InvoiceStatus.Paid, "Wireless peripherals"),
    new(1003, 103, DateTime.Now.AddDays(-15), DateTime.Now.AddDays(-15).AddDays(30), 89.99m, InvoiceStatus.Pending, "USB-C cable"),
    new(1004, 104, DateTime.Now.AddDays(-10), DateTime.Now.AddDays(-10).AddDays(30), 599.99m, InvoiceStatus.Overdue, "4K Monitor"),
    new(1005, 105, DateTime.Now.AddDays(-5), DateTime.Now.AddDays(-5).AddDays(30), 299.99m, InvoiceStatus.Pending, "Premium headphones")
};

var transactions = new List<Transaction>
{
    new(2001, 1001, DateTime.Now.AddDays(-28), 1299.99m, TransactionType.Payment, "Payment received for invoice 1001"),
    new(2002, 1002, DateTime.Now.AddDays(-23), 149.50m, TransactionType.Payment, "Payment received for invoice 1002"),
    new(2003, 0, DateTime.Now.AddDays(-20), -500.00m, TransactionType.Expense, "Office supplies purchase"),
    new(2004, 0, DateTime.Now.AddDays(-15), -1200.00m, TransactionType.Expense, "Equipment maintenance"),
    new(2005, 0, DateTime.Now.AddDays(-10), -800.00m, TransactionType.Expense, "Marketing campaign")
};

// API Endpoints
app.MapGet("/invoices", (ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching all invoices");
    return invoices.OrderByDescending(i => i.IssueDate);
})
.WithName("GetAllInvoices")
.WithTags("Invoices")
.WithSummary("Get all invoices")
.WithDescription("Returns a list of all invoices in the system")
.WithOpenApi();

app.MapGet("/invoices/{id:int}", (int id, ILogger<Program> logger) =>
{
    logger.LogInformation("🔍 Fetching invoice with ID: {InvoiceId}", id);
    var invoice = invoices.FirstOrDefault(i => i.Id == id);
    return invoice is not null ? Results.Ok(invoice) : Results.NotFound($"Invoice {id} not found");
})
.WithName("GetInvoiceById")
.WithTags("Invoices")
.WithSummary("Get invoice by ID")
.WithDescription("Returns a specific invoice by its ID")
.WithOpenApi();

app.MapGet("/invoices/customer/{customerId:int}", (int customerId, ILogger<Program> logger) =>
{
    logger.LogInformation("👤 Fetching invoices for customer: {CustomerId}", customerId);
    var customerInvoices = invoices.Where(i => i.CustomerId == customerId).ToList();
    return customerInvoices.Any() ? Results.Ok(customerInvoices) : Results.NotFound($"No invoices found for customer {customerId}");
})
.WithName("GetInvoicesByCustomer")
.WithTags("Invoices")
.WithSummary("Get invoices by customer ID")
.WithDescription("Returns all invoices for a specific customer")
.WithOpenApi();

app.MapGet("/invoices/overdue", (ILogger<Program> logger) =>
{
    logger.LogInformation("⚠️ Fetching overdue invoices");
    var overdueInvoices = invoices.Where(i => i.Status == InvoiceStatus.Overdue ||
        (i.Status == InvoiceStatus.Pending && i.DueDate < DateTime.Now)).ToList();
    return overdueInvoices;
})
.WithName("GetOverdueInvoices")
.WithTags("Invoices")
.WithSummary("Get overdue invoices")
.WithDescription("Returns all invoices that are overdue or past their due date")
.WithOpenApi();

app.MapPost("/invoices", (CreateInvoiceRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("➕ Creating new invoice for customer: {CustomerId}", request.CustomerId);
    var newInvoice = new Invoice(
        invoices.Max(i => i.Id) + 1,
        request.CustomerId,
        DateTime.Now,
        DateTime.Now.AddDays(30),
        request.Amount,
        InvoiceStatus.Pending,
        request.Description
    );
    invoices.Add(newInvoice);
    logger.LogInformation("✅ Invoice created with ID: {InvoiceId}", newInvoice.Id);
    return Results.Created($"/invoices/{newInvoice.Id}", newInvoice);
})
.WithName("CreateInvoice")
.WithTags("Invoices")
.WithSummary("Create a new invoice")
.WithDescription("Creates a new invoice in the system")
.WithOpenApi();

app.MapPut("/invoices/{id:int}/status", (int id, UpdateInvoiceStatusRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("🔄 Updating invoice {InvoiceId} status to: {Status}", id, request.Status);
    var invoice = invoices.FirstOrDefault(i => i.Id == id);
    if (invoice is null)
        return Results.NotFound($"Invoice {id} not found");

    var updatedInvoice = invoice with { Status = request.Status };
    var index = invoices.FindIndex(i => i.Id == id);
    invoices[index] = updatedInvoice;

    logger.LogInformation("✅ Invoice {InvoiceId} status updated to: {Status}", id, request.Status);
    return Results.Ok(updatedInvoice);
})
.WithName("UpdateInvoiceStatus")
.WithTags("Invoices")
.WithSummary("Update invoice status")
.WithDescription("Updates the status of an existing invoice")
.WithOpenApi();

app.MapGet("/transactions", (ILogger<Program> logger) =>
{
    logger.LogInformation("📋 Fetching all transactions");
    return transactions.OrderByDescending(t => t.Date);
})
.WithName("GetAllTransactions")
.WithTags("Transactions")
.WithSummary("Get all transactions")
.WithDescription("Returns a list of all financial transactions")
.WithOpenApi();

app.MapGet("/transactions/type/{type}", (TransactionType type, ILogger<Program> logger) =>
{
    logger.LogInformation("🏷️ Fetching transactions of type: {TransactionType}", type);
    var typeTransactions = transactions.Where(t => t.Type == type).ToList();
    return typeTransactions;
})
.WithName("GetTransactionsByType")
.WithTags("Transactions")
.WithSummary("Get transactions by type")
.WithDescription("Returns all transactions of a specific type (Payment, Expense, Refund)")
.WithOpenApi();

app.MapPost("/transactions", (CreateTransactionRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("➕ Creating new transaction: {Type} - {Amount}", request.Type, request.Amount);
    var newTransaction = new Transaction(
        transactions.Max(t => t.Id) + 1,
        request.InvoiceId,
        DateTime.Now,
        request.Amount,
        request.Type,
        request.Description
    );
    transactions.Add(newTransaction);
    logger.LogInformation("✅ Transaction created with ID: {TransactionId}", newTransaction.Id);
    return Results.Created($"/transactions/{newTransaction.Id}", newTransaction);
})
.WithName("CreateTransaction")
.WithTags("Transactions")
.WithSummary("Create a new transaction")
.WithDescription("Creates a new financial transaction")
.WithOpenApi();

app.MapGet("/finance/reports/summary", (ILogger<Program> logger) =>
{
    logger.LogInformation("📊 Generating financial summary report");
    var totalRevenue = transactions.Where(t => t.Type == TransactionType.Payment).Sum(t => t.Amount);
    var totalExpenses = Math.Abs(transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount));
    var totalRefunds = Math.Abs(transactions.Where(t => t.Type == TransactionType.Refund).Sum(t => t.Amount));

    var summary = new
    {
        TotalRevenue = totalRevenue,
        TotalExpenses = totalExpenses,
        TotalRefunds = totalRefunds,
        NetIncome = totalRevenue - totalExpenses - totalRefunds,
        TotalInvoices = invoices.Count,
        PaidInvoices = invoices.Count(i => i.Status == InvoiceStatus.Paid),
        PendingInvoices = invoices.Count(i => i.Status == InvoiceStatus.Pending),
        OverdueInvoices = invoices.Count(i => i.Status == InvoiceStatus.Overdue),
        OutstandingAmount = invoices.Where(i => i.Status != InvoiceStatus.Paid).Sum(i => i.Amount)
    };
    return summary;
})
.WithName("GetFinancialSummary")
.WithTags("Reports")
.WithSummary("Get financial summary")
.WithDescription("Returns comprehensive financial summary and key metrics")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Finance Service!", Service = "FinanceService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Finance Service")
.WithOpenApi();

// Health check endpoint
app.MapGet("/health", () => new { Status = "Healthy", Service = "FinanceService", Timestamp = DateTime.UtcNow })
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Health check endpoint")
.WithDescription("Returns the health status of the Finance Service")
.WithOpenApi();

app.Run();

// Models
record Invoice(int Id, int CustomerId, DateTime IssueDate, DateTime DueDate, decimal Amount, InvoiceStatus Status, string Description);

record Transaction(int Id, int InvoiceId, DateTime Date, decimal Amount, TransactionType Type, string Description);

record CreateInvoiceRequest(int CustomerId, decimal Amount, string Description);

record UpdateInvoiceStatusRequest(InvoiceStatus Status);

record CreateTransactionRequest(int InvoiceId, decimal Amount, TransactionType Type, string Description);

enum InvoiceStatus
{
    Pending,
    Paid,
    Overdue,
    Cancelled
}

enum TransactionType
{
    Payment,
    Expense,
    Refund
}
