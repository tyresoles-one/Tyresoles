using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.ServiceModel;
using System.ServiceModel.Security;
using WebService;
using Procurement;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Tyresoles.Data.Features.Common;

public class Connector
{
    private readonly NavWebServiceSettings _settings;
    private readonly ILogger<Connector> _logger;

    public Connector(IOptions<NavWebServiceSettings> options, ILogger<Connector> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public WebServe_PortClient GetClient()
    {
        if (string.IsNullOrWhiteSpace(_settings.Url))
        {
            throw new InvalidOperationException(
                "Nav Web Service URL is not configured. Set configuration key NavWebService:Url to the Dynamics NAV SOAP endpoint (e.g. http://server:7047/DynamicsNAV/WS/Company/Codeunit/WebServe).");
        }

        var binding = new BasicHttpBinding();
        binding.Security.Mode = BasicHttpSecurityMode.TransportCredentialOnly;
        binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
        binding.Security.Transport.ProxyCredentialType = HttpProxyCredentialType.Windows;
        binding.MaxReceivedMessageSize = int.MaxValue;
        binding.MaxBufferSize = int.MaxValue;

        var endpointAddress = new EndpointAddress(_settings.Url);
        var client = new WebServe_PortClient(binding, endpointAddress);

        if (!string.IsNullOrWhiteSpace(_settings.UserID))
        {
            client.ClientCredentials.Windows.ClientCredential = new System.Net.NetworkCredential(
                _settings.UserID,
                _settings.Password,
                _settings.Domain);
        }

        client.ClientCredentials.Windows.AllowedImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;

        return client;
    }

    /// <summary>
    /// FaultException subclasses CommunicationException; SOAP faults from NAV must not be retried as transient network errors.
    /// MessageSecurityException (e.g. 401 Negotiate failure) is not transient—retries only repeat the same auth failure.
    /// </summary>
    private static bool IsTransientWcfFailure(Exception ex) =>
        ex is TimeoutException
        || (ex is CommunicationException
            && ex is not FaultException
            && ex is not MessageSecurityException);

    private async Task<T> ExecuteWithRetryAsync<T>(Func<WebServe_PortClient, Task<T>> action, int maxRetries = 3)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                using var client = GetClient();
                return await action(client);
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientWcfFailure(ex))
            {
                attempt++;
                _logger.LogWarning(ex, "WCF transient failure on attempt {Attempt}. Retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
    }

    private async Task ExecuteWithRetryAsync(Func<WebServe_PortClient, Task> action, int maxRetries = 3)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                using var client = GetClient();
                await action(client);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransientWcfFailure(ex))
            {
                attempt++;
                _logger.LogWarning(ex, "WCF transient failure on attempt {Attempt}. Retrying...", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
    }

    public Task<string> TestConnectionAsync() => ExecuteWithRetryAsync(async client => 
    {
        var result = await client.ConnectionTestAsync();
        return result.return_value;
    });

    // NAV GST API log table / codeunit fields are short Text fields; oversize strings cause FaultException (e.g. errMsg max 150).
    private const int GstApiLogMaxDocTypeLen = 20;
    private const int GstApiLogMaxDocNoLen = 20;
    private const int GstApiLogMaxSourceLen = 50;
    private const int GstApiLogMaxErrCodeLen = 50;
    private const int GstApiLogMaxErrMsgLen = 150;

    private static string TruncateForGstApiLog(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }

    public async Task InsertGstApiLogAsync(GSTApiLog log)
    {
        if (string.IsNullOrWhiteSpace(_settings.Url))
        {
            _logger.LogWarning(
                "GST API log not written: NavWebService:Url is empty. DocType={DocType}, DocNo={DocNo}, Source={Source}, ErrorCode={ErrorCode}",
                log.DocumentType, log.DocumentNo, log.Source, log.ErrorCode);
            return;
        }

        var docType = TruncateForGstApiLog(log.DocumentType, GstApiLogMaxDocTypeLen);
        var docNo = TruncateForGstApiLog(log.DocumentNo, GstApiLogMaxDocNoLen);
        var source = TruncateForGstApiLog(log.Source, GstApiLogMaxSourceLen);
        var errCode = TruncateForGstApiLog(log.ErrorCode, GstApiLogMaxErrCodeLen);
        var errMsg = TruncateForGstApiLog(log.ErrorMessage, GstApiLogMaxErrMsgLen);

        _logger.LogDebug("GSTApiLog: {Log}", log);
        try
        {
            await ExecuteWithRetryAsync(client => client.InsertGstApiLogAsync(docType, docNo, source, errCode, errMsg));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "InsertGstApiLog failed for DocNo={DocNo}; message not persisted to NAV.", log.DocumentNo);
        }
    }

    public async Task InsertEInvoiceAsync(EInvoice eInvoice)
    {
        await ExecuteWithRetryAsync(client => client.InsertEInvoiceDetailsAsync(eInvoice.DocumentType, eInvoice.DocumentNo, eInvoice.AckDate, eInvoice.AckNo, eInvoice.IRN, eInvoice.QRImage, eInvoice.JsonText));
    }

    public async Task InsertGSTINAsync(PartyGstin gstRec)
    {
        if (gstRec != null)
        {
            if (string.IsNullOrWhiteSpace(gstRec.BlockStatus))
                gstRec.BlockStatus = gstRec.Status == "ACT" ? "U" : "B";
        }
        _logger.LogDebug("GSTIN: {GstRec}", gstRec);
        await ExecuteWithRetryAsync(client => client.InsertGSTINAsync(gstRec!.Type, gstRec.Code, gstRec.Gstin, gstRec.LegalName, gstRec.TradeName, gstRec.Status, gstRec.BlockStatus));
    }

    public async Task CreateDealerAsync(CreateDealer dealer)
    {
        _logger.LogDebug("CreateDealer: {Dealer}", dealer);
        await ExecuteWithRetryAsync(client => client.CreateDealerAsync(dealer.CustomerNo, dealer.Product, dealer.BusModel, dealer.Name, dealer.Email, dealer.DlrshipName, dealer.InvAmt, dealer.DoB, dealer.DoA, dealer.BrdShop, dealer.Pan, dealer.Gst, dealer.Adhar, dealer.BkName, dealer.BkAcNo, dealer.BkBrch, dealer.BkIfsc, dealer.DoE, dealer.DoJ, dealer.Comments));
    }

    public Task<string> InsertEntityAsync(MergerEntity entity) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.CreateEntityAsync(entity.Type, entity.OldCode, entity.OldCompany, entity.OldRespCenter, entity.NewRespCenter,
            entity.SerRespCenter, entity.Name, entity.Address, entity.Address2, entity.PostCode, entity.City, entity.State,
            entity.AreaCode, entity.DealerCode, entity.MobileNo, entity.Primary, entity.PrimaryNo, entity.GSTIN, entity.PAN, entity.PriceType);
        return result.return_value;
    });

    public Task<string> CreateClaimAsync(ClaimRequest request) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.CreateClaimAsync(request.InvoiceNo, request.Date, request.LineNo, request.CustomerNo,
            request.Tyre, request.Make, request.SerialNo, request.UnitPrice,
            request.TWDAmount, request.DealerDisc, request.LineDisc, request.Variant,
            request.InspReport, request.OwnerRisk, request.OldCompany);
        return result.return_value;
    });

    public Task<bool> UpdateVendorAsync(Vendor request) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpdateVendorAsync(request.No, request.RespCenter, request.Name, request.Address,
            request.Address2, request.City, request.StateCode, request.MobileNo, request.Category, request.Detail, request.EcoMgrCode,
            request.SelfInvoice, request.NameOnInvoice, request.BankName, request.BankAccNo, request.BankIFSC, request.BankBranch, request.PanNo, request.AdhaarNo);
        return result.return_value;
    });

    public Task<bool> UpsertFixedAssetAsync(FixedAsset request) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpsertFixedAssetAsync(request.No, request.Description, request.Description2,
            request.RespCenter, request.Employee, request.PurchaseDate, request.ExpiryDate,
            request.MainAssetNo, request.Inactive, request.Blocked, request.Class, request.SubClass,
            request.SerialNo, request.VendorNo, request.PurchaseCost);
        return result.return_value;
    });

    public Task<string> CreateVendorAsync(string respCenter, string userID, string mgrID) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.CreateVendorAsync(respCenter, userID, mgrID);
        return result.return_value;
    });

    public Task<string> EntityBalanceAsync(EntityBalance request) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.CustVendOpeningBalanceAsync(request.Type, request.No, request.Company, request.Balance);
        return result.return_value;
    });

    public Task RectifyCustLedgerAsync(string customerNo) => ExecuteWithRetryAsync(client => client.RectifyCustLedgerAsync(customerNo));

    public Task SyncEcomileItemsAsync() => ExecuteWithRetryAsync(client => client.SyncEcomileRecordsAsync());

    public Task<string> NewProcurementOrderAsync(FetchParams param) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.NewProcurementOrderAsync(param.RespCenters.FirstOrDefault() ?? "", param.UserCode);
        return result.return_value;
    });

    public Task<string> NewProcShipNoAsync(FetchParams param) => ExecuteWithRetryAsync(async client =>
    {
        DateTime date;
        if (!DateTime.TryParse(param.From, out date)) date = DateTime.Now;
        var result = await client.NewProcShipNoAsync(param.RespCenters.FirstOrDefault() ?? "", date);
        return result.return_value;
    });

    public Task<int> UpdateProcurementOrderAsync(OrderInfo order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpdateProcurementOrderAsync(order.OrderNo, order.SupplierCode, order.ManagerCode, order.Status);
        return result.return_value;
    });

    public Task<string> GenerateGRAsAsync(string orderNo) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.GenerateGRAsAsync(orderNo);
        return result.return_value;
    });

    public Task<int> InsertProcurementOrderLineAsync(OrderLine order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.InsertProcurementOrderLineAsync(order.No, order.VendorNo, order.ItemNo, order.Make, order.SerialNo, order.Inspection, order.Amount, order.InspectorCode);
        return result.return_value;
    });

    public Task<int> UpdateProcurementOrderLineAsync(OrderLine order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpdateProcurementOrderLineAsync(order.LineNo, order.No, order.VendorNo, order.ItemNo, order.Make, order.SerialNo, order.Inspection, order.Amount, order.InspectorCode, order.SubMake);
        return result.return_value ? 1 : 0;
    });

    public Task<int> DispatchOrderLineAsync(OrderLineDispatch order) => ExecuteWithRetryAsync(async client =>
    {
        DateTime date;
        if (!DateTime.TryParse(order.DispatchDate, out date)) date = DateTime.Now;
        var result = await client.DispatchOrderLineAsync(order.OrderNo, order.LineNo, order.DispatchOrderNo, date, order.DispatchDestination, order.DispatchVehicleNo, order.DispatchMobileNo, order.DispatchTransporter);
        return result.return_value ? 1 : 0;
    });

    public Task<int> UpdateProcurementOrderLine2Async(OrderLineDispatch order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpdateProcurementOrderLine2Async(order.LineNo, order.OrderNo, order.No,
            order.Make, order.SerialNo, order.FactInspection, order.NewSerialNo, order.Button, order.Model,
            order.FactInspector, order.FactInspectorFinal, order.RejectionReason, order.Remark, order.OrderStatus);
        return result.return_value ? 1 : 0;
    });

    public Task<int> ReceiptOrderLineAsync(OrderLineDispatch order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.ReceiptOrderLineAsync(order.OrderNo, order.LineNo, order.Inspector);
        return result.return_value ? 1 : 0;
    });

    public Task<int> RemoveShippedOrdLineAsync(OrderLineDispatch order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.RemoveShippedLineAsync(order.OrderNo, order.LineNo);
        return result.return_value ? 1 : 0;
    });

    public Task<int> DropPostedOrdLineAsync(OrderLineDispatch order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.DropPostedOrderLineAsync(order.OrderNo, order.LineNo);
        return result.return_value ? 1 : 0;
    });

    public Task<int> DeleteProcurementOrderLineAsync(OrderLine order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.DeleteProcOrdLineAsync(order.No, order.LineNo);
        return result.return_value ? 1 : 0;
    });

    public Task<int> DeleteProcurementOrderAsync(OrderInfo order) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.DeleteProcOrdAsync(order.OrderNo);
        return result.return_value ? 1 : 0;
    });

    public Task<int> AddUpdateVehicleAsync(Vehicle vehicle) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.UpdateVehicleAsync(vehicle.No, vehicle.Name, vehicle.MobileNo, vehicle.GSTIN, vehicle.LineNo, vehicle.RespCenter, vehicle.Status == "In Active" ? 1 : 0);
        return result.return_value ? 1 : 0;
    });

    public Task<int> AddUpdateDocumentImageAsync(DocumentImage record) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.AddUpdateImageAsync(record.DocumentNo, record.Type, record.LineNo, record.Image);
        return result.return_value ? 1 : 0;
    });

    public Task<int> ActivatePortalAsync(int userType, string userCode) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.ActivatePortalAsync(userType, userCode);
        return result.return_value ? 1 : 0;
    });

    public Task<int> DealerPortalActivateAsync(string dealerCode) => ExecuteWithRetryAsync(async client =>
    {
        var result = await client.PortalForDealerAsync(dealerCode);
        return result.return_value ? 1 : 0;
    });

    public async Task<string> CreateSupportRequestAsync(SupportRequest request)
    {
        // Note: The original had this as placeholder/commented out/no API implementation.
        await Task.CompletedTask;
        return "SUCCESS";
    }
}
