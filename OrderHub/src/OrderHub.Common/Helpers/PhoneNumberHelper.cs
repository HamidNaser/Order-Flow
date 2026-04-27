using PhoneNumberUtil = PhoneNumbers.PhoneNumberUtil;
using PhoneNumberFormat = PhoneNumbers.PhoneNumberFormat;
using Serilog;

namespace OrderHub.Common.Helpers;

public static class PhoneNumberHelper
{
    private static readonly PhoneNumberUtil PhoneNumberUtil = PhoneNumberUtil.GetInstance();
    private const string DefaultRegion = "US";

    /// <summary>
    /// Normalizes a phone number to E.164 format using libphonenumber-csharp
    /// </summary>
    public static string Normalize(string phoneNumber)
    {
        //for short codes, checking for less than 10 digits
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 10)
        {
            return phoneNumber;
        }

        try
        {
            var parsedNumber = PhoneNumberUtil.Parse(phoneNumber, DefaultRegion);
            return PhoneNumberUtil.Format(parsedNumber, PhoneNumberFormat.E164);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Exception normalizing the phone number: {phoneNumber}", phoneNumber);
            return phoneNumber;
        }
    }
}
