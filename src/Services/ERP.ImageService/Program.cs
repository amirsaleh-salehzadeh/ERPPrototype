using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddLogging();

// No additional image processing services needed

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Image Service is running"))
    .AddCheck("storage", () =>
    {
        try
        {
            var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
            if (Directory.Exists(imagesPath))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Image storage is accessible");
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Image storage directory not found");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Image storage check failed", ex);
        }
    });

// Add CORS for Documentation service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowDocumentation", policy =>
    {
        policy.WithOrigins("http://localhost:5002", "http://localhost:5000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Ensure Images directory exists
var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
if (!Directory.Exists(imagesPath))
{
    Directory.CreateDirectory(imagesPath);
    app.Logger.LogInformation("📁 Created Images directory: {ImagesPath}", imagesPath);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Image Service API";
        options.Theme = ScalarTheme.BluePlanet;
        options.ShowSidebar = true;
    });
}

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowDocumentation");

// Health Check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "ImageService",
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration
            })
        });
        await context.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Name == "self",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            service = "ImageService",
            ready = report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            timestamp = DateTime.UtcNow
        });
        await context.Response.WriteAsync(result);
    }
});

// Metrics endpoint for Prometheus
app.MapGet("/metrics", () =>
{
    var metrics = new List<string>
    {
        "# HELP image_service_health Health status of the Image Service",
        "# TYPE image_service_health gauge",
        "image_service_health 1",
        "",
        "# HELP image_service_uptime_seconds Uptime of the Image Service in seconds",
        "# TYPE image_service_uptime_seconds counter",
        $"image_service_uptime_seconds {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
        "",
        "# HELP image_service_requests_total Total number of requests",
        "# TYPE image_service_requests_total counter",
        "image_service_requests_total 0",
        "",
        "# HELP image_service_images_served_total Total number of images served",
        "# TYPE image_service_images_served_total counter",
        "image_service_images_served_total 0"
    };
    
    return Results.Text(string.Join("\n", metrics), "text/plain");
})
.WithName("Metrics")
.WithTags("Monitoring")
.ExcludeFromDescription();

// Middleware to log incoming requests
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

    // Check if request came through gateway
    var serviceName = context.Request.Headers["X-Service-Name"].FirstOrDefault();
    if (!string.IsNullOrEmpty(serviceName))
    {
        logger.LogInformation("🖼️ ImageService received request from gateway - Service: {ServiceName}", serviceName);
    }
    else
    {
        logger.LogInformation("🖼️ ImageService received direct request");
    }

    await next();
});

// Image endpoints
app.MapGet("/images", (ILogger<Program> logger) =>
{
    logger.LogInformation("🖼️ Listing available images");

    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
    var imageFiles = Directory.GetFiles(imagesPath, "*.*", SearchOption.TopDirectoryOnly)
        .Where(file => IsImageFile(file))
        .Select(file => new
        {
            id = Path.GetFileNameWithoutExtension(file),
            name = Path.GetFileName(file),
            size = new FileInfo(file).Length,
            lastModified = new FileInfo(file).LastWriteTime,
            url = $"/images/{Path.GetFileNameWithoutExtension(file)}"
        })
        .ToArray();

    logger.LogInformation("🖼️ Found {Count} images", imageFiles.Length);
    return Results.Ok(new { images = imageFiles, count = imageFiles.Length });
})
.WithName("ListImages")
.WithTags("Images")
.WithSummary("List all available images")
.WithDescription("Returns a list of all images stored in the service")
.WithOpenApi();

app.MapGet("/images/{imageId}", async (string imageId, ILogger<Program> logger, HttpContext context) =>
{
    logger.LogInformation("🖼️ Streaming image: {ImageId}", imageId);

    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
    var imageFiles = Directory.GetFiles(imagesPath, $"{imageId}.*")
        .Where(file => IsImageFile(file))
        .ToArray();

    if (imageFiles.Length == 0)
    {
        logger.LogWarning("🖼️ Image not found: {ImageId}", imageId);
        return Results.NotFound(new { error = "Image not found", imageId });
    }

    var imagePath = imageFiles[0];
    var fileInfo = new FileInfo(imagePath);
    var contentType = GetContentType(imagePath);

    logger.LogInformation("🖼️ Serving image: {ImagePath} ({Size} bytes)", imagePath, fileInfo.Length);

    // Set appropriate headers for image streaming
    context.Response.Headers.ContentType = contentType;
    context.Response.Headers.ContentLength = fileInfo.Length;
    context.Response.Headers.CacheControl = "public, max-age=3600"; // Cache for 1 hour
    context.Response.Headers.ETag = $"\"{fileInfo.LastWriteTime.Ticks}\"";

    // Stream the image file
    await using var fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
    await fileStream.CopyToAsync(context.Response.Body);

    return Results.Empty;
})
.WithName("GetImage")
.WithTags("Images")
.WithSummary("Stream an image by ID")
.WithDescription("Streams an image file by its ID or filename (without extension)")
.WithOpenApi();

app.MapPost("/images", async (IFormFile file, ILogger<Program> logger) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "No file provided" });
    }

    if (!IsImageFile(file.FileName))
    {
        return Results.BadRequest(new { error = "File is not a valid image format" });
    }

    logger.LogInformation("🖼️ Uploading image: {FileName} ({Size} bytes)", file.FileName, file.Length);

    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var filePath = Path.Combine(imagesPath, fileName);

    await using var stream = new FileStream(filePath, FileMode.Create);
    await file.CopyToAsync(stream);

    var imageId = Path.GetFileNameWithoutExtension(fileName);
    logger.LogInformation("🖼️ Image uploaded successfully: {ImageId}", imageId);

    return Results.Created($"/images/{imageId}", new
    {
        id = imageId,
        name = fileName,
        originalName = file.FileName,
        size = file.Length,
        url = $"/images/{imageId}",
        uploadedAt = DateTime.UtcNow
    });
})
.WithName("UploadImage")
.WithTags("Images")
.WithSummary("Upload a new image")
.WithDescription("Uploads an image file and returns the image ID for future retrieval")
.WithOpenApi()
.DisableAntiforgery();

app.MapDelete("/images/{imageId}", (string imageId, ILogger<Program> logger) =>
{
    logger.LogInformation("🖼️ Deleting image: {ImageId}", imageId);

    var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "Images");
    var imageFiles = Directory.GetFiles(imagesPath, $"{imageId}.*")
        .Where(file => IsImageFile(file))
        .ToArray();

    if (imageFiles.Length == 0)
    {
        logger.LogWarning("🖼️ Image not found for deletion: {ImageId}", imageId);
        return Results.NotFound(new { error = "Image not found", imageId });
    }

    var imagePath = imageFiles[0];
    File.Delete(imagePath);

    logger.LogInformation("🖼️ Image deleted successfully: {ImageId}", imageId);
    return Results.Ok(new { message = "Image deleted successfully", imageId });
})
.WithName("DeleteImage")
.WithTags("Images")
.WithSummary("Delete an image by ID")
.WithDescription("Deletes an image file by its ID")
.WithOpenApi();

// Hello World endpoint
app.MapGet("/hello", () => new { Message = "Hello from Image Service!", Service = "ImageService", Timestamp = DateTime.UtcNow })
.WithName("HelloWorld")
.WithTags("General")
.WithSummary("Hello World endpoint")
.WithDescription("Returns a hello message from the Image Service")
.WithOpenApi();

// OpenAPI specification endpoint for Scalar Documentation
app.MapGet("/swagger/v1/swagger.json", () =>
{
    var openApiJson = """
    {
        "openapi": "3.0.1",
        "info": {
            "title": "Image Service API",
            "version": "v1",
            "description": "Image storage and streaming service for the ERP system"
        },
        "servers": [
            {
                "url": "http://localhost:5003",
                "description": "Image Service"
            }
        ],
        "paths": {
            "/images": {
                "get": {
                    "tags": ["Images"],
                    "summary": "List all available images",
                    "description": "Returns a list of all images stored in the service",
                    "responses": {
                        "200": {
                            "description": "List of images",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "object",
                                        "properties": {
                                            "images": {
                                                "type": "array",
                                                "items": {
                                                    "$ref": "#/components/schemas/ImageInfo"
                                                }
                                            },
                                            "count": {"type": "integer"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                "post": {
                    "tags": ["Images"],
                    "summary": "Upload a new image",
                    "description": "Uploads an image file and returns the image ID for future retrieval",
                    "requestBody": {
                        "content": {
                            "multipart/form-data": {
                                "schema": {
                                    "type": "object",
                                    "properties": {
                                        "file": {
                                            "type": "string",
                                            "format": "binary"
                                        }
                                    }
                                }
                            }
                        }
                    },
                    "responses": {
                        "201": {
                            "description": "Image uploaded successfully",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "$ref": "#/components/schemas/UploadResult"
                                    }
                                }
                            }
                        },
                        "400": {
                            "description": "Bad request"
                        }
                    }
                }
            },
            "/images/{imageId}": {
                "get": {
                    "tags": ["Images"],
                    "summary": "Stream an image by ID",
                    "description": "Streams an image file by its ID or filename (without extension)",
                    "parameters": [
                        {
                            "name": "imageId",
                            "in": "path",
                            "required": true,
                            "schema": {
                                "type": "string"
                            }
                        }
                    ],
                    "responses": {
                        "200": {
                            "description": "Image file",
                            "content": {
                                "image/*": {
                                    "schema": {
                                        "type": "string",
                                        "format": "binary"
                                    }
                                }
                            }
                        },
                        "404": {
                            "description": "Image not found"
                        }
                    }
                },
                "delete": {
                    "tags": ["Images"],
                    "summary": "Delete an image by ID",
                    "description": "Deletes an image file by its ID",
                    "parameters": [
                        {
                            "name": "imageId",
                            "in": "path",
                            "required": true,
                            "schema": {
                                "type": "string"
                            }
                        }
                    ],
                    "responses": {
                        "200": {
                            "description": "Image deleted successfully"
                        },
                        "404": {
                            "description": "Image not found"
                        }
                    }
                }
            },
            "/hello": {
                "get": {
                    "tags": ["General"],
                    "summary": "Hello World endpoint",
                    "description": "Returns a hello message from the Image Service",
                    "responses": {
                        "200": {
                            "description": "Hello message",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "object",
                                        "properties": {
                                            "Message": {"type": "string"},
                                            "Service": {"type": "string"},
                                            "Timestamp": {"type": "string", "format": "date-time"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            "/health": {
                "get": {
                    "tags": ["Health"],
                    "summary": "Health check endpoint",
                    "description": "Returns the health status of the Image Service",
                    "responses": {
                        "200": {
                            "description": "Service health status",
                            "content": {
                                "application/json": {
                                    "schema": {
                                        "type": "object",
                                        "properties": {
                                            "status": {"type": "string"},
                                            "service": {"type": "string"},
                                            "timestamp": {"type": "string", "format": "date-time"}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        },
        "components": {
            "schemas": {
                "ImageInfo": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string"},
                        "name": {"type": "string"},
                        "size": {"type": "integer"},
                        "lastModified": {"type": "string", "format": "date-time"},
                        "url": {"type": "string"}
                    }
                },
                "UploadResult": {
                    "type": "object",
                    "properties": {
                        "id": {"type": "string"},
                        "name": {"type": "string"},
                        "originalName": {"type": "string"},
                        "size": {"type": "integer"},
                        "url": {"type": "string"},
                        "uploadedAt": {"type": "string", "format": "date-time"}
                    }
                }
            }
        }
    }
    """;

    return Results.Content(openApiJson, "application/json");
})
.WithName("GetOpenApiSpec")
.WithTags("OpenAPI");

// Helper methods
static bool IsImageFile(string fileName)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
}

static string GetContentType(string fileName)
{
    var extension = Path.GetExtension(fileName).ToLowerInvariant();
    return extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        _ => "application/octet-stream"
    };
}

app.Run();
