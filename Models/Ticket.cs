using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PrintSpoolJobService.Models
{
    public class Ticket
    {
        public CompanyInfo Company { get; set; } = new CompanyInfo();

        public HeaderInfo Header { get; set; } = new HeaderInfo();

        public List<ItemDetails> Items { get; set; } = new List<ItemDetails>();

        public FooterInfo Footer { get; set; } = new FooterInfo();

        // Maximum allowed lengths and limits (tweak to project rules)
        private const int MaxCompanyName = 100;
        private const int MaxAddressLine = 100;
        private const int MaxItems = 200;
        private const int DefaultLineWidth = 42; // typical thermal printer width

        public class CompanyInfo
        {
            public string Name { get; set; } = string.Empty;
            public List<string> AddressLines { get; set; } = new List<string>();
            public string PhoneNumber { get; set; } = string.Empty;
            public string WebsiteUrl { get; set; } = string.Empty;
            public string FiscalCode { get; set; } = string.Empty;
            public string LogoBase64 { get; set; } = string.Empty;
            public string LogoUrl { get; set; } = string.Empty;
        }
        public class HeaderInfo
        {
            public string Title { get; set; } = string.Empty;
            public string? Username { get; set; }
            public string? Profile { get; set; }
            public string? Host { get; set; }
            public string? ClientName { get; set; }
            public string? ClientID { get; set; }
            public DateTime? SaleDate { get; set; }
        }

        public class ItemDetails
        {
            public string Description { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public decimal Quantity { get; set; } = 1m;

            public decimal Amount => Math.Round(UnitPrice * Quantity, 2);
        }

        public class FooterInfo
        {
            public decimal TotalAmount { get; set; }
            public string ThankYouMessage { get; set; } = "Thank you for your purchase!";
            public string ContactInfo { get; set; } = string.Empty;

        }

        /// <summary>
        /// Validate the ticket content and return a sequence of error messages (empty if valid).
        /// </summary>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Header.Title))
                errors.Add("Title is required.");

            if (!string.IsNullOrEmpty(Company.Name) && Company.Name.Length > MaxCompanyName)
                errors.Add($"Company name exceeds {MaxCompanyName} characters.");

            if (Company.AddressLines.Any(al => al?.Length > MaxAddressLine))
                errors.Add($"One or more company address lines exceed {MaxAddressLine} characters.");

            if (Items.Count == 0)
                errors.Add("Ticket must contain at least one item.");

            if (Items.Count > MaxItems)
                errors.Add($"Ticket contains too many items (max {MaxItems}).");

            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                if (string.IsNullOrWhiteSpace(it.Description))
                    errors.Add($"Item #{i + 1}: description is required.");
                if (it.Quantity <= 0)
                    errors.Add($"Item #{i + 1}: quantity must be greater than zero.");
                if (it.UnitPrice < 0)
                    errors.Add($"Item #{i + 1}: unit price cannot be negative.");
            }

            var computed = Items.Sum(it => it.Amount);
            var roundedComputed = Math.Round(computed, 2);
            var roundedTotal = Math.Round(Footer.TotalAmount, 2);

            if (Items.Count > 0 && roundedComputed != roundedTotal)
            {
                errors.Add($"TotalAmount ({roundedTotal.ToString("F2", CultureInfo.InvariantCulture)}) does not match sum of items ({roundedComputed.ToString("F2", CultureInfo.InvariantCulture)}).");
            }

            return errors;
        }

        /// <summary>
        /// Render the ticket as plain text formatted for a mono-spaced thermal printer.
        /// Controls are stripped to avoid accidental printer control sequences.
        /// </summary>
        public string RenderAsPlainText(int lineWidth = DefaultLineWidth, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentCulture;
            var sb = new StringBuilder();

            static string Sanitize(string? s)
            {
                if (string.IsNullOrEmpty(s)) return string.Empty;
                // Remove C0 control chars except common whitespace (LF/CR will be normalized)
                return Regex.Replace(s, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", string.Empty).Trim();
            }

            void AppendCenter(string? text)
            {
                var t = Sanitize(text);
                if (t.Length >= lineWidth) { sb.AppendLine(t); return; }
                var pad = (lineWidth - t.Length) / 2;
                sb.AppendLine(new string(' ', pad) + t);
            }

            void AppendLine(string? text = null)
            {
                sb.AppendLine(Sanitize(text));
            }

            // Header: Company
            if (!string.IsNullOrWhiteSpace(Company.Name))
                AppendCenter(Company.Name.ToUpperInvariant());

            foreach (var line in Company.AddressLines)
                AppendCenter(line);

            if (!string.IsNullOrWhiteSpace(Company.PhoneNumber))
                AppendCenter($"Tel: {Company.PhoneNumber}");

            if (!string.IsNullOrWhiteSpace(Company.WebsiteUrl))
                AppendCenter(Company.WebsiteUrl);

            sb.AppendLine(new string('-', lineWidth));

            // Title and metadata
            AppendCenter(Header.Title);
            if (!string.IsNullOrWhiteSpace(Header.Username)) AppendLine($"User: {Sanitize(Header.Username)}");
            if (!string.IsNullOrWhiteSpace(Header.Profile)) AppendLine($"Profile: {Sanitize(Header.Profile)}");
            if (Header.SaleDate.HasValue) AppendLine($"Date: {Header.SaleDate.Value.ToString("g", culture)}");
            if (!string.IsNullOrWhiteSpace(Header.ClientName)) AppendLine($"Client: {Sanitize(Header.ClientName)}");
            if (!string.IsNullOrWhiteSpace(Header.ClientID)) AppendLine($"ID: {Sanitize(Header.ClientID)}");

            sb.AppendLine(new string('-', lineWidth));

            // Items table: Description (left) | qty x price (center) | amount (right)
            // Reserve space for qty+price and amount
            const int amountWidth = 10;
            const int qtyPriceWidth = 12;
            var descWidth = Math.Max(10, lineWidth - amountWidth - qtyPriceWidth - 2);

            foreach (var it in Items)
            {
                var desc = Sanitize(it.Description);
                // Wrap description if too long
                var descLines = WrapText(desc, descWidth);
                var qtyPrice = $"{it.Quantity:G} x {it.UnitPrice.ToString("F2", culture)}";
                var amount = it.Amount.ToString("F2", culture);

                for (int li = 0; li < descLines.Count; li++)
                {
                    var d = descLines[li];
                    if (li == 0)
                    {
                        // first line: include qty/price and amount
                        sb.Append(d.PadRight(descWidth));
                        sb.Append("  ");
                        sb.Append(qtyPrice.PadRight(qtyPriceWidth));
                        sb.Append(amount.PadLeft(amountWidth));
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine(d);
                    }
                }
            }

            sb.AppendLine(new string('-', lineWidth));

            // Totals
            var totalLabel = "TOTAL";
            var totalStr = Footer.TotalAmount.ToString("F2", culture);
            var left = totalLabel.PadRight(lineWidth - totalStr.Length - 1);
            sb.AppendLine($"{left} {totalStr}");

            if (!string.IsNullOrWhiteSpace(Footer.ThankYouMessage))
            {
                sb.AppendLine();
                AppendCenter(Footer.ThankYouMessage);
            }

            if (!string.IsNullOrWhiteSpace(Footer.ContactInfo))
            {
                sb.AppendLine();
                AppendCenter(Footer.ContactInfo);
            }

            // Footer spacing
            sb.AppendLine();
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Convert rendered plain text into a byte[] suitable to send as a raw job to printers.
        /// Default encoding is UTF8 without BOM.
        /// </summary>
        public byte[] ToPrinterBytes(int lineWidth = DefaultLineWidth, Encoding? encoding = null)
        {
            encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var text = RenderAsPlainText(lineWidth);
            return encoding.GetBytes(text);
        }

        // Helper: wrap text to max width, breaking on whitespace
        private static List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();
            foreach (var w in words)
            {
                if (current.Length == 0)
                {
                    current.Append(w);
                }
                else if (current.Length + 1 + w.Length <= maxWidth)
                {
                    current.Append(' ').Append(w);
                }
                else
                {
                    lines.Add(current.ToString());
                    current.Clear();
                    current.Append(w);
                }
            }

            if (current.Length > 0) lines.Add(current.ToString());

            // If any single word longer than maxWidth, break it
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length <= maxWidth) continue;
                var overflow = lines[i];
                lines[i] = overflow.Substring(0, maxWidth);
                var rest = overflow.Substring(maxWidth);
                while (rest.Length > 0)
                {
                    var take = Math.Min(maxWidth, rest.Length);
                    lines.Insert(++i, rest.Substring(0, take));
                    rest = rest.Substring(take);
                }
            }

            return lines;
        }
    }
}
