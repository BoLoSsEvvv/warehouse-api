using WarehouseApi.Models;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Хранилище в памяти: для контрольной точки БД не нужна,
// после перезапуска контейнера данные сбрасываются к стартовым.
var products = new List<Product>
{
    new() { Id = 1, Sku = "BOLT-M8",  Name = "Болт M8x40",         Quantity = 1200, MinQuantity = 500, Price = 3.50m },
    new() { Id = 2, Sku = "NUT-M8",   Name = "Гайка M8",           Quantity = 300,  MinQuantity = 500, Price = 1.20m },
    new() { Id = 3, Sku = "PAINT-WH", Name = "Краска белая, 10 л", Quantity = 15,   MinQuantity = 10,  Price = 2400m },
    new() { Id = 4, Sku = "TAPE-50",  Name = "Скотч 50 мм",        Quantity = 0,    MinQuantity = 20,  Price = 85m },
};
var nextId = products.Count + 1;
var lockObj = new object();

// Проверка, что сервис жив — удобно для docker ps / мониторинга.
app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

// Список всех товаров на складе.
app.MapGet("/products", (ILogger<Program> log) =>
{
    log.LogInformation("GET /products -> {Count} items", products.Count);
    return Results.Ok(products);
});

// Товары, у которых остаток ниже минимального — что нужно дозаказать.
app.MapGet("/products/low-stock", (ILogger<Program> log) =>
{
    var low = products.Where(p => p.LowStock).ToList();
    log.LogInformation("GET /products/low-stock -> {Count} items below minimum", low.Count);
    return Results.Ok(low);
});

// Один товар по id.
app.MapGet("/products/{id:int}", (int id, ILogger<Program> log) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);
    if (product is null)
    {
        log.LogWarning("GET /products/{Id} -> not found", id);
        return Results.NotFound(new { error = $"Product {id} not found" });
    }
    return Results.Ok(product);
});

// Добавить новую позицию на склад.
app.MapPost("/products", (CreateProductRequest req, ILogger<Program> log) =>
{
    if (string.IsNullOrWhiteSpace(req.Sku) || string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest(new { error = "Sku and Name are required" });
    if (req.Quantity < 0 || req.MinQuantity < 0 || req.Price < 0)
        return Results.BadRequest(new { error = "Quantity, MinQuantity and Price must be >= 0" });

    lock (lockObj)
    {
        if (products.Any(p => p.Sku.Equals(req.Sku, StringComparison.OrdinalIgnoreCase)))
            return Results.Conflict(new { error = $"Sku '{req.Sku}' already exists" });

        var product = new Product
        {
            Id = nextId++,
            Sku = req.Sku,
            Name = req.Name,
            Quantity = req.Quantity,
            MinQuantity = req.MinQuantity,
            Price = req.Price
        };
        products.Add(product);
        log.LogInformation("POST /products -> created {Sku} (id={Id})", product.Sku, product.Id);
        return Results.Created($"/products/{product.Id}", product);
    }
});

// Приход: увеличить остаток.
app.MapPost("/products/{id:int}/receive", (int id, StockMovementRequest req, ILogger<Program> log) =>
{
    if (req.Amount <= 0)
        return Results.BadRequest(new { error = "Amount must be > 0" });

    lock (lockObj)
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product is null)
            return Results.NotFound(new { error = $"Product {id} not found" });

        product.Quantity += req.Amount;
        log.LogInformation("RECEIVE {Sku}: +{Amount} -> {Quantity}", product.Sku, req.Amount, product.Quantity);
        return Results.Ok(product);
    }
});

// Расход: уменьшить остаток, в минус уйти нельзя.
app.MapPost("/products/{id:int}/issue", (int id, StockMovementRequest req, ILogger<Program> log) =>
{
    if (req.Amount <= 0)
        return Results.BadRequest(new { error = "Amount must be > 0" });

    lock (lockObj)
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        if (product is null)
            return Results.NotFound(new { error = $"Product {id} not found" });

        if (product.Quantity < req.Amount)
        {
            log.LogWarning("ISSUE {Sku}: requested {Amount}, only {Quantity} in stock", product.Sku, req.Amount, product.Quantity);
            return Results.BadRequest(new { error = $"Not enough stock: {product.Quantity} available, {req.Amount} requested" });
        }

        product.Quantity -= req.Amount;
        log.LogInformation("ISSUE {Sku}: -{Amount} -> {Quantity}", product.Sku, req.Amount, product.Quantity);
        return Results.Ok(product);
    }
});

app.Logger.LogInformation("Warehouse API started with {Count} products", products.Count);
app.Run();
