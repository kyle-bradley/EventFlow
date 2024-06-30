namespace ClearFlow.ValueObjects.Dtos;
public class MobileNumberDto
{
    public string CountryCode { get; }
    public string MobileNumber { get; }
    public MobileNumberDto(string countryCode, string mobileNumber)
    {
        CountryCode = countryCode;
        MobileNumber = mobileNumber;
    }
}