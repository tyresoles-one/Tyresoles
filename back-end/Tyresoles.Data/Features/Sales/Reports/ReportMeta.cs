using System;
using System.Collections.Generic;
using System.Text;

namespace Tyresoles.Data.Features.Sales.Reports
{
    public class ReportMeta
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DatePreset { get; set; } = string.Empty;
        public string OutputFormats {  get; set; } = string.Empty;
        public List<string> TypeOptions { get; set; } = new();
        public List<string> ViewOptions { get; set; } = new();
        public bool ShowType { get; set; }
        public bool ShowView { get; set; }
        public bool ShowCustomers { get; set; }
        public bool ShowDealers { get; set; }
        public bool ShowAreas { get; set; }
        public bool ShowRegions { get; set; }
        public bool ShowRespCenters { get; set; }
        public bool ShowNos { get; set; }
        public string RequiredFields { get; set; } = string.Empty;
    }
}
