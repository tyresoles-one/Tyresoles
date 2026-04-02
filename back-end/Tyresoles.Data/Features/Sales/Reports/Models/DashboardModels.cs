using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tyresoles.Data.Features.Sales.Reports.Models.Dashboard
{
    public class DashboardData
    {
        public string Name { get; set; }
        public dynamic Data { get; set; }
        
    }
    public interface IDashboardData
    {
        public int No { get; set; }
        public string Business { get; set; }
        public string Location { get; set; }
    }
    public class ProductSale : IDashboardData
    {
        public ProductSale()
        {
            Data = new List<Sales>();
        }
        public int No { get; set; }
        public string Business { get; set; }
        public string Location { get; set; }
        public string Product { get; set; }
        public int ProductId { get; set; }
        public List<Sales> Data { get; set; }
        
    }
    public class Sales
    {
        public Sales()
        {
            Items = new List<Item>();
        }
        public int No { get; set; }
        public string Label { get; set; }
        public decimal Sale { get; set; }
        public string SaleText { get; set; }
        public string SaleUnit { get; set; }
        public string DateRange { get; set; }
        public List<Item> Items { get; set; }
    }
    public class Item
    {
        public int No { get; set; }
        public string Label { get; set; }
        public decimal Value { get; set; }
        public string Unit { get; set; }
        
    }
    public class DateRange
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string Label { get; set; }
    }
    public class CustomerSaleBalance
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
    }

    /// <summary>Result of active customer sale balance query: records and total sale.</summary>
    public class ActCustSaleBalanceResult
    {
        public List<CustomerSaleBalance> Records { get; set; } = new();
        public decimal TotalSale { get; set; }
    }
    public class SaleLine
    {
        public decimal Amount { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
    }
    public class CustomerSales
    {
        public int No { get; set; }
        public string Label { get; set; }
        public string DateRange { get; set; }
        public List<SaleLine> Lines { get; set; }
        public List<CustomerSaleBalance> Records { get; set; }
    }
    public class ActiveCustomer: IDashboardData
    {
        public int No { get; set; }
        public string Business { get; set; }
        public string Location { get; set; }
        public string Product { get; set; }
        public int ProductId { get; set; }
        public List<CustomerSales> Data { get; set; }
    }
    public class SalesmanSaleBalance
    {
        public string Area { get; set; }
        public string Salesperson { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }        
        public int Total { get; set; }
    }
    public class SalesmanTyreSale
    {
        public string Area { get; set; }
        public string Salesperson { get; set; }
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int G { get; set; }
        public int R { get; set; }
        public int L { get; set; }
        public int P { get; set; }
        public int T { get; set; }
        public int O { get; set; }
        public int O1 { get; set; }
        public int Total { get; set; }
    }
    public class SalesmanTradeSale
    {
        public string Area { get; set; }
        public string Salesperson { get; set; }
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int Tube { get; set; }
        public int Flap { get; set; }
        public int Total { get; set; }
    }
    public class SalesmanEcoflexSale
    {
        public string Area { get; set; }
        public string Salesperson { get; set; }
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int Tile { get; set; }
        public int PlaySafe { get; set; }
        public int RunTrack { get; set; }
        public int Outdoor { get; set; }
        public int Indoor { get; set; }
        public int Charges { get; set; }
        public int Other { get; set; }
        public int Total { get; set; }
    }

    public class DealerTyreSale
    {
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int G { get; set; }
        public int R { get; set; }
        public int L { get; set; }
        public int P { get; set; }
        public int T { get; set; }
        public int O { get; set; }
        public int O1 { get; set; }
        public int Total { get; set; }
    }
    public class DealerTradeSale
    {
        public string Dealer { get; set; }        
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int Tube { get; set; }
        public int Flap { get; set; }
        public int Total { get; set; }
    }
    public class DealerEcoflexSale
    {
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public string Sale { get; set; }
        public string Balance { get; set; }
        public int Tile { get; set; }
        public int PlaySafe { get; set; }
        public int RunTrack { get; set; }
        public int Outdoors { get; set; }
        public int Indoors { get; set; }
        public int Charges { get; set; }
        public int Other { get; set; }
        public int Total { get; set; }
    }
    public class SalesmanSale : IDashboardData
    {
        public int No { get; set; }
        public string Business { get; set; }
        public string Location { get; set; }
        public string Product { get; set; }
        public int ProductId { get; set; }
        public dynamic Data { get; set; }
    }
    public class CustomerSaleBalanceEcoflex
    {
        public string No { get; set; }
        public string Area { get; set; }
        public string Salesman { get; set; }
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
        public int Tile { get; set; }
        public int PlaySafe { get; set; }
        public int RunTrack { get; set; }
        public int Outdoors { get; set; }
        public int Indoors { get; set; }   
        public int Charges { get; set; }
        public int Other { get; set; }
        public int Total
        {
            get
            {
                return Tile + PlaySafe + RunTrack + Outdoors + Indoors + Charges + Other;
            }
        }
    }
    public class CustomerSaleBalanceTyre
    {
        public string No { get; set; }
        public string Area { get; set; }
        public string Salesman { get; set; }
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
        public int G { get; set; }
        public int R { get; set; }
        public int L { get; set; }
        public int P { get; set; }
        public int T { get; set; }
        public int O { get; set; }
        public int O1 { get; set; }
        public int Total
        {
            get
            {
                return G + R + L + P + T + O + O1;
            }
        }
    }
    public class CustomerSaleBalanceTrade
    {
        public string No { get; set; }
        public string Area { get; set; }
        public string Salesman { get; set; }
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
        public int Tube { get; set; }
        public int Flap { get; set; }        
        public int Total
        {
            get
            {
                return Tube + Flap ;
            }
        }
    }
    public class CustomerSaleBalanceTile
    {
        public string No { get; set; }
        public string Area { get; set; }
        public string Salesman { get; set; }
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
        public int Tile { get; set; }
        public int RunTrack { get; set; }
        public int PlaySafe { get; set; }
        public int Outdoors { get; set; }
        public int Indoors { get; set; }
        public int Total
        {
            get
            {
                return Tile + PlaySafe + Outdoors + RunTrack + Indoors;
            }
        }
    }
    public class CustomerSaleBalanceRubber
    {
        public string No { get; set; }
        public string Area { get; set; }
        public string Salesman { get; set; }
        public string Dealer { get; set; }
        public int Customers { get; set; }
        public decimal Sale { get; set; }
        public decimal Balance { get; set; }
        public int Tube { get; set; }
        public int Flap { get; set; }
        public int Total { get; set; }
    }

    public class CollectionData : IDashboardData
    {
        public int No { get; set; }
        public string Business { get; set; }
        public string Location { get; set; }
        public string Period { get; set; }
        public decimal Collection { get; set; }
        public List<Collection> Data { get; set; }
    }
    public class Collection
    {
        public string Date { get; set; }
        public string Type { get; set; }
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string Amount { get; set; }
        public decimal Amt { get; set; }
        
    }

    /// <summary>One row for sales chart: date, product, sale amount, location (resp center).</summary>
    public class SalesChartRow
    {
        public DateTime Date { get; set; }
        public string Product { get; set; }
        public decimal Sale { get; set; }
        public string Location { get; set; }
    }

    public class MonthlySalesRow
    {
        public string Month { get; set; }
        public decimal Sale { get; set; }
        public string Unit { get; set; }
    }

    public class SummaryTile
    {
        public string Label { get; set; }
        public string Value { get; set; }
        public string Trend { get; set; }
        public string Icon { get; set; }
        public string Color { get; set; }
    }

    public class DashboardSummary
    {
        public List<SummaryTile> Tiles { get; set; } = new();
    }
}
 