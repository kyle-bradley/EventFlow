namespace ClearFlow.ValueObjects.Dtos;
public class MoneyDto
{
    public string Currency { get; }
    public decimal Amount { get; }
    public MoneyDto(string currency, decimal amount)
    {
        Currency = currency;
        Amount = amount;
    }
}
