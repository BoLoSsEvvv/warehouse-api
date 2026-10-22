namespace WarehouseApi.Models;

/// <summary>
/// Складская позиция: товар, его остаток на складе и минимально допустимый остаток.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public int Quantity { get; set; }
    public int MinQuantity { get; set; }
    public decimal Price { get; set; }

    /// <summary>Остаток ниже минимального — пора заказывать.</summary>
    public bool LowStock => Quantity < MinQuantity;
}

/// <summary>Тело запроса на создание товара.</summary>
public record CreateProductRequest(string Sku, string Name, int Quantity, int MinQuantity, decimal Price);

/// <summary>Тело запроса на приход/расход: сколько единиц приняли или списали.</summary>
public record StockMovementRequest(int Amount);
