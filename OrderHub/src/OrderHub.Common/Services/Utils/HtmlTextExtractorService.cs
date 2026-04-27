using System.Text.RegularExpressions;

namespace OrderHub.Common.Services.Utils
{
    public class HtmlTextExtractorService : IHtmlTextExtractorService
    {
        public string ExtractText(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            // Remove script tags and their content
            var noScript = Regex.Replace(content, @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", string.Empty, RegexOptions.IgnoreCase);

            // Remove style tags and their content
            var noStyle = Regex.Replace(noScript, @"<style\b[^<]*(?:(?!<\/style>)<[^<]*)*<\/style>", string.Empty, RegexOptions.IgnoreCase);

            // Replace <br> and <br/> with newlines (case-insensitive)
            var withLineBreaks = Regex.Replace(noStyle, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

            // Remove remaining HTML tags
            var noHtml = Regex.Replace(withLineBreaks, "<.*?>", string.Empty);

            // Remove \r characters
            string noCarriageReturns = noHtml.Replace("\r", string.Empty);

            // Split by newlines, trim each line, and convert to list
            List<string> cleanText = noCarriageReturns.Split('\n')
                                                      .Select(line => line.Trim())
                                                      .Where(line => !string.IsNullOrEmpty(line))
                                                      .ToList();

            return string.Join("\n", cleanText);
        }
    }
}
