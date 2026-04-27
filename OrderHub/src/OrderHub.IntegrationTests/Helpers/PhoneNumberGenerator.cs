namespace OrderHub.IntegrationTests.Helpers
{
    using PhoneNumbers;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum Region
    {
        US,
        CA,
        MX,
        GB,
        IN,
        AU
    }

    public class PhoneNumberGenerator
    {
        private static readonly PhoneNumberUtil phoneUtil = PhoneNumberUtil.GetInstance();
        private static readonly Random random = new Random();
        private static readonly Dictionary<Region, List<string>> endpointsByRegion = new Dictionary<Region, List<string>>();

        static PhoneNumberGenerator()
        {
            endpointsByRegion = GenerateMixedFormatEndpoints();
        }

        public static string GetRandomPhoneNumber()
        {
            var allEndpoints = endpointsByRegion.Values.SelectMany(list => list).ToList();
            return allEndpoints[random.Next(allEndpoints.Count)];
        }

        public static string GetRandomPhoneNumber(Region region)
        {
            if (!endpointsByRegion.ContainsKey(region) || endpointsByRegion[region].Count == 0)
            {
                throw new ArgumentException($"No phone numbers available for region: {region}");
            }
            return endpointsByRegion[region][random.Next(endpointsByRegion[region].Count)];
        }

        /// <summary>
        /// Returns the last 10 numbers in a string (for phone numbers). If there are less than or equal to 10 numbers, returns all of them.
        /// </summary>
        /// <param name="numberString">The input string containing the phone number.</param>
        /// <returns>A string containing the last 10 digits of the phone number.</returns>
        public static string GetLast10Numbers(string numberString)
        {
            if (string.IsNullOrWhiteSpace(numberString)) return "";

            string digits = new([.. numberString.Where(char.IsDigit)]);

            if (digits.Length > 10)
            {
                return digits[^10..];
            }

            return digits;
        }

        /// <summary>
        /// Generates phone numbers organized by region.
        /// The resulting dictionary contains shuffled lists for each region.
        /// </summary>
        private static Dictionary<Region, List<string>> GenerateMixedFormatEndpoints()
        {
            var endpointsByRegion = new Dictionary<Region, List<string>>();
            int attemptsPerRegion = 2000; // Number of numbers we attempt to get per region

            // Initialize empty lists for each region
            foreach (Region region in Enum.GetValues(typeof(Region)))
            {
                endpointsByRegion[region] = new List<string>();
            }

            // Generate numbers for each region
            foreach (Region region in Enum.GetValues(typeof(Region)))
            {
                string regionCode = region.ToString();

                for (int i = 0; i < attemptsPerRegion; i++)
                {
                    PhoneNumber exampleNumber = phoneUtil.GetExampleNumber(regionCode);
                    if (exampleNumber == null) break;

                    try
                    {
                        if (phoneUtil.IsValidNumber(exampleNumber))
                        {
                            // Add the number in E164 or International Format only. Other 2 formats are causing the tests to fail.
                            var formats = new List<PhoneNumberFormat>() { PhoneNumberFormat.E164, PhoneNumberFormat.INTERNATIONAL };
                            PhoneNumberFormat randomFormat = formats[random.Next(formats.Count)];
                            endpointsByRegion[region].Add(phoneUtil.Format(exampleNumber, randomFormat));
                        }
                    }
                    catch (NumberParseException)
                    {
                        // Ignore invalid random generations
                    }
                }

                // Shuffle each region's list
                endpointsByRegion[region] = endpointsByRegion[region].OrderBy(x => random.Next()).ToList();
            }

            return endpointsByRegion;
        }
    }
}
