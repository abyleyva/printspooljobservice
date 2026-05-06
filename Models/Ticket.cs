using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PrintSpoolJobService.Models
{
    public class Ticket
    {

        public TicketConfig newTicket { get; set; } = new TicketConfig();

        public class TicketConfig
        {
            public string JWT { get; set; } = string.Empty;
            public string PrinterName { get; set; } = string.Empty;
            public string Encoding { get; set; } = "UTF-8"; // Default to UTF-8
            public List<OperationTicket> Operations { get; set; } = new List<OperationTicket>();
        }
        public class OperationTicket //Operations like Print,Feed, Cut, OpenCashDrawer, etc.
        {
            public string Action { get; set; }
            public List<object> Args { get; set; } // 'object' allows mixing int, string, and bool
        }
        
    }
}
