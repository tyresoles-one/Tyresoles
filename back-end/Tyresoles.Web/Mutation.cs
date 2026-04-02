using System.Security.Claims;
using HotChocolate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Tyresoles.Data;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Data.Features.Admin.User;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Calendar.Dto;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Features.Sales;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Production;
using Tyresoles.Data.Features.Production.Models;

using ProductionFetchParams = Tyresoles.Data.Features.Production.Models.FetchParams;
using Tyresoles.Web.GraphQL;

namespace Tyresoles.Web;

public class Mutation
{
    /// <summary>
    /// Maps NAV WCF <see cref="System.ServiceModel.FaultException"/>, inner exceptions, and related failures
    /// to a <see cref="GraphQLException"/> so clients see the real message (see <see cref="NavConnectorErrorFilter"/>).
    /// </summary>
    private static GraphQLException ToGqlNavException(Exception ex) =>
        new(NavConnectorErrorFormatting.FormatMessage(ex));

    public async Task<LoginResult> Login(
        string username,
        string password,
        string? platform = null,
        [Service] IUserService userService = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await userService.LoginAsync(username, password, platform, cancellationToken);
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return new LoginResult { Success = false, Message = message, User = null };
        }
    }

    /// <summary>Remove a session by id. Requires authentication.</summary>
    [Authorize]
    public async Task<KillSessionResult> KillSession(
        string sessionId,
        [Service] ISessionStore sessionStore,
        CancellationToken cancellationToken = default)
    {
        var removed = await sessionStore.RemoveAsync(sessionId, cancellationToken);
        return new KillSessionResult { Success = removed, Message = removed ? "Session killed." : "Session not found or already expired." };
    }

    /// <summary>Remove all sessions for a user. Requires authentication.</summary>
    [Authorize]
    public async Task<KillSessionsByUserResult> KillSessionsByUser(
        string userId,
        [Service] ISessionStore sessionStore,
        CancellationToken cancellationToken = default)
    {
        var count = await sessionStore.RemoveByUserAsync(userId, cancellationToken);
        return new KillSessionsByUserResult { Success = true, KilledCount = count };
    }

    /// <summary>Reset a user's password. Requires authentication.</summary>
    [Authorize]
    public async Task<ResetPasswordResult> ResetPassword(
        string userId,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var newPassword = await userService.ResetPasswordAsync(userId, cancellationToken);
            if (newPassword != null)
            {
                return new ResetPasswordResult { Success = true, NewPassword = newPassword, Message = "Password reset successfully." };
            }
            return new ResetPasswordResult { Success = false, Message = "User not found or operation failed." };
        }
        catch (Exception ex)
        {
            return new ResetPasswordResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Change a user's password. Requires authentication. Provide either oldPassword (when user knows it) or securityPin (first login / forgot password).</summary>
    [Authorize]
    public async Task<ChangePasswordResult> ChangePassword(
        string userId,
        string newPassword,
        string? oldPassword = null,
        int? securityPin = null,
        [Service] IUserService userService = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.ChangePasswordAsync(userId, newPassword, oldPassword, securityPin, cancellationToken);
            return new ChangePasswordResult
            {
                Success = result,
                Message = result ? "Password changed successfully." : "Invalid current password or Security PIN, or user not found."
            };
        }
        catch (Exception ex)
        {
            return new ChangePasswordResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Forgot password: reset by username + Security PIN. No authentication required.</summary>
    public async Task<ChangePasswordResult> ForgotPassword(
        string username,
        int securityPin,
        string newPassword,
        [Service] IUserService userService = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.ForgotPasswordAsync(username, securityPin, newPassword, cancellationToken);
            return new ChangePasswordResult
            {
                Success = result,
                Message = result ? "Password changed successfully." : "Invalid username or Security PIN."
            };
        }
        catch (Exception ex)
        {
            return new ChangePasswordResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Update user profile. Only provided fields are updated. Requires authentication.</summary>
    [Authorize]
    public async Task<SetProfileResult> SetProfile(
        string userId,
        ProfileUpdateInput input,
        [Service] IUserService userService = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.SetProfileAsync(userId, input, cancellationToken);
            return new SetProfileResult
            {
                Success = result,
                Message = result ? "Profile updated successfully." : "User not found."
            };
        }
        catch (Exception ex)
        {
            return new SetProfileResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    // ---- Calendar ----
    /// <summary>Create a calendar event. Requires authentication.</summary>
    [Authorize]
    public async Task<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto?> CreateCalendarEvent(
        CreateEventInput input,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return null;
        return await calendarService.CreateEventAsync(userId, input, cancellationToken);
    }

    /// <summary>Update a calendar event. Requires authentication. updateScope: 0=All, 1=ThisOccurrence, 2=ThisAndFuture.</summary>
    [Authorize]
    public async Task<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto?> UpdateCalendarEvent(
        Guid eventId,
        UpdateEventInput input,
        int updateScope = 0,
        DateTime? occurrenceStartUtc = null,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return null;
        return await calendarService.UpdateEventAsync(eventId, userId, input, updateScope, occurrenceStartUtc, cancellationToken);
    }

    /// <summary>Delete a calendar event (soft delete). deleteScope: 0=All, 1=ThisOccurrence, 2=ThisAndFuture.</summary>
    [Authorize]
    public async Task<bool> DeleteCalendarEvent(
        Guid eventId,
        bool soft = true,
        int deleteScope = 0,
        DateTime? occurrenceStartUtc = null,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.DeleteEventAsync(eventId, userId, soft, deleteScope, occurrenceStartUtc, cancellationToken);
    }

    /// <summary>Snooze a reminder until the given time. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> SnoozeReminder(
        Guid reminderId,
        DateTime snoozeUntilUtc,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.SnoozeReminderAsync(reminderId, userId, snoozeUntilUtc, cancellationToken);
    }

    /// <summary>Share my calendar with another user. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> ShareCalendar(
        string sharedWithUserId,
        int permission,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.ShareCalendarAsync(userId, sharedWithUserId, permission, cancellationToken);
    }

    /// <summary>Respond to an event invitation. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> RespondToInvite(
        Guid eventId,
        int response,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.RespondToInviteAsync(eventId, userId, response, cancellationToken);
    }

    /// <summary>Set notification preference for reminders. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> SetNotificationPreference(
        Tyresoles.Data.Features.Calendar.NotificationPreferenceDto input,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.SetNotificationPreferenceAsync(userId, input, cancellationToken);
    }

    /// <summary>Mark a single notification as read. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> MarkNotificationAsRead(
        Guid notificationId,
        [Service] INotificationService notificationService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await notificationService.MarkAsReadAsync(notificationId, userId, cancellationToken);
    }

    /// <summary>Mark all notifications for the current user as read. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> MarkAllNotificationsAsRead(
        [Service] INotificationService notificationService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
    }

    /// <summary>Toggle a calendar task status. Requires authentication.</summary>
    [Authorize]
    public async Task<bool> ToggleCalendarTaskStatus(
        Guid taskId,
        bool isCompleted,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return false;
        return await calendarService.ToggleTaskStatusAsync(taskId, isCompleted, userId, cancellationToken);
    }

    /// <summary>
    /// Save / update dealer master: personal info, business details, banking, and GSTIN.
    /// Also persists the GST record in NAV (InsertGSTIN, type = "S").
    /// Requires authentication.
    /// </summary>
    [Authorize]
    [GraphQLName("saveDealer")]
    public async Task<MutationResult> SaveDealer(
        SaveDealerInput input,
        [Service] ISalesService salesService,
        [Service] IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await salesService.SaveDealerAsync(scope, input, cancellationToken);
            return new MutationResult { Success = true, Message = "Dealer saved successfully." };
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            logger.LogError(ex, "Error in saveDealer mutation for code {Code}", input.Code);
            return new MutationResult { Success = false, Message = message };
        }
    }

    /// <summary>
    /// Creates a dealer (Salesperson_Purchaser) from a customer with no dealer code, using derived codes
    /// LEFT(No,4)+RIGHT(No,5) or LEFT(No,4)+RIGHT(No,6), then updates Customer.Dealer Code.
    /// </summary>
    [Authorize]
    [GraphQLName("createDealer")]
    public async Task<CreateDealerResult> CreateDealer(
        string customerNo,
        [Service] ISalesService salesService,
        [Service] IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await salesService.CreateDealerAsync(scope, customerNo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "createDealer failed for customer {CustomerNo}", customerNo);
            return new CreateDealerResult
            {
                Success = false,
                Message = ex.InnerException?.Message ?? ex.Message,
                DealerCode = null
            };
        }
    }

    /// <summary>
    /// Print selected documents as a single PDF report.
    /// This uses the SalesReportService to render the appropriate report for the selected doc numbers.
    /// Returns the PDF as a base64 string for direct viewing/download in front-end.
    /// </summary>
    [Authorize]
    public async Task<string?> PrintDocuments(
        SalesReportParams parameters,
        [Service] IDataverseDataService dataService,
        [Service] ISalesReportService salesService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        
        var reportName = parameters.ReportName ?? (parameters.View switch {
            "Invoice" => "Posted Sales Invoice",
            "CrNote" => "Posted Sales CreditMemo",
            "Claim" => "Posted Claim Form",
            _ => "Posted Sales Invoice"
        });

        // Ensure PDF output
        parameters.ReportOutput = "PDF";

        try
        {
            var pdfBytes = await salesService.RenderReportAsync(scope, reportName, parameters, cancellationToken);
            if (pdfBytes == null || pdfBytes.Length == 0) return null;

            return Convert.ToBase64String(pdfBytes);
        }
        catch (Exception ex)
        {
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update casing. Ported from Db.Production.UpdateCasing.</summary>
    [Authorize]
    public async Task<MutationResult> UpdateProductionCasing(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await productionService.UpdateCasingAsync(scope, param, cancellationToken);
            return new MutationResult { Success = true, Message = "Casing updated successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = NavConnectorErrorFormatting.FormatMessage(ex) };
        }
    }

    /// <summary>Insert casing items. Ported from Db.Production.InsertCasingItems.</summary>
    [Authorize]
    public async Task<MutationResult> InsertProductionCasingItems(
        List<CasingItem> casingItems,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await productionService.InsertCasingItemsAsync(scope, casingItems, cancellationToken);
            return new MutationResult { Success = true, Message = "Casing items inserted successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = NavConnectorErrorFormatting.FormatMessage(ex) };
        }
    }

    /// <summary>Update vendor. Ported from Db.Production.UpdateVendor.</summary>
    [Authorize]
    public async Task<MutationResult> UpdateProductionVendor(
        VendorModel param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            var result = await productionService.UpdateVendorAsync(scope, param, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Vendor updated successfully." : "Failed to update vendor." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = NavConnectorErrorFormatting.FormatMessage(ex) };
        }
    }

    /// <summary>
    /// Creates a casing-procurement vendor in NAV via SOAP. Ported from Live <c>Db.Production.CreateVendor</c>.
    /// GraphQL: <c>createProductionVendor(param: FetchParamsInput!)</c>.
    /// </summary>
    [Authorize]
    public async Task<string> CreateProductionVendor(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.CreateVendorAsync(scope, param, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CreateProductionVendor failed");
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>
    /// Creates a new procurement (casing) purchase order in NAV; returns the new document number.
    /// Ported from <c>Tyresoles.One.Data.Navision.Db.Production.NewProcurementOrder</c> (<c>Db.Production.cs</c>).
    /// GraphQL: <c>newProductionProcurementOrder(param: FetchParamsInput!)</c>.
    /// </summary>
    [Authorize]
    public async Task<string> NewProductionProcurementOrder(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.NewProcurementOrderAsync(scope, param, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NewProductionProcurementOrder failed");
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update procurement order. Ported from Db.Production.UpdateProcurementOrder.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcurementOrder(
        OrderInfo order,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcurementOrderAsync(scope, order, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateProductionProcurementOrder failed OrderNo={OrderNo}", order.OrderNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>New proc shipment number. Ported from Db.Production.NewProcShipNo.</summary>
    [Authorize]
    public async Task<string> NewProductionProcShipNo(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.NewProcShipNoAsync(scope, param, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NewProductionProcShipNo failed");
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Generate GRAs. Ported from Db.Production.GenerateGRAs.</summary>
    [Authorize]
    public async Task<string> GenerateProductionGRAs(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.GenerateGRAsAsync(scope, param, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GenerateProductionGRAs failed");
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Insert procurement order line. Ported from Db.Production.InsertProcurementOrderLine.</summary>
    [Authorize]
    public async Task<int> InsertProductionProcurementOrderLine(
        OrderLine order,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.InsertProcurementOrderLineAsync(scope, order, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "InsertProductionProcurementOrderLine failed OrderNo={OrderNo} LineNo={LineNo} ItemNo={ItemNo}",
                order.No, order.LineNo, order.ItemNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update procurement order line. Ported from Db.Production.UpdateProcurementOrderLine.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcurementOrderLine(
        OrderLine order,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcurementOrderLineAsync(scope, order, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UpdateProductionProcurementOrderLine failed OrderNo={OrderNo} LineNo={LineNo} ItemNo={ItemNo}",
                order.No, order.LineNo, order.ItemNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update proc order line dispatch. Ported from Db.Production.UpdateProcOrdLineDispatch.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcOrdLineDispatch(
        List<OrderLineDispatch> lines,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineDispatchAsync(scope, lines, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateProductionProcOrdLineDispatch failed (lineCount={LineCount})", lines?.Count ?? 0);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update proc order line dispatch (single). Ported from Db.Production.UpdateProcOrdLineDispatch2.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcOrdLineDispatchSingle(
        OrderLineDispatch line,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineDispatchSingleAsync(scope, line, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UpdateProductionProcOrdLineDispatchSingle failed OrderNo={OrderNo} LineNo={LineNo}",
                line.OrderNo, line.LineNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>
    /// Ecomile new numbering: update one procurement dispatch line (new serial, inspection, status, etc.).
    /// Uses SOAP UpdateProcurementOrderLine2Async via IProductionService.UpdateProcOrdLineDispatchSingleAsync.
    /// </summary>
    [Authorize]
    public async Task<int> UpdateEcomileNewNumberLine(
        OrderLineDispatch line,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineDispatchSingleAsync(scope, line, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UpdateEcomileNewNumberLine failed OrderNo={OrderNo} LineNo={LineNo} No={No} SerialNo={SerialNo}",
                line.OrderNo, line.LineNo, line.No, line.SerialNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update proc order line receipt. Ported from Db.Production.UpdateProcOrdLineReceipt.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcOrdLineReceipt(
        List<OrderLineDispatch> lines,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineReceiptAsync(scope, lines, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "UpdateProductionProcOrdLineReceipt failed (lineCount={LineCount})",
                lines?.Count ?? 0);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update proc order line remove. Ported from Db.Production.UpdateProcOrdLineRemove.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcOrdLineRemove(
        List<OrderLineDispatch> lines,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineRemoveAsync(scope, lines, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateProductionProcOrdLineRemove failed (lineCount={LineCount})", lines?.Count ?? 0);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Update proc order line drop. Ported from Db.Production.UpdateProcOrdLineDrop.</summary>
    [Authorize]
    public async Task<int> UpdateProductionProcOrdLineDrop(
        List<OrderLineDispatch> lines,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.UpdateProcOrdLineDropAsync(scope, lines, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateProductionProcOrdLineDrop failed (lineCount={LineCount})", lines?.Count ?? 0);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Delete procurement order line. Ported from Db.Production.DeleteProcurementOrderLine.</summary>
    [Authorize]
    public async Task<int> DeleteProductionProcurementOrderLine(
        OrderLine order,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.DeleteProcurementOrderLineAsync(scope, order, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "DeleteProductionProcurementOrderLine failed OrderNo={OrderNo} LineNo={LineNo}",
                order.No, order.LineNo);
            throw ToGqlNavException(ex);
        }
    }

    /// <summary>Delete procurement order. Ported from Db.Production.DeleteProcurementOrder.</summary>
    [Authorize]
    public async Task<int> DeleteProductionProcurementOrder(
        OrderInfo order,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            return await productionService.DeleteProcurementOrderAsync(scope, order, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteProductionProcurementOrder failed OrderNo={OrderNo}", order.OrderNo);
            throw ToGqlNavException(ex);
        }
    }
}

public sealed class KillSessionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class KillSessionsByUserResult
{
    public bool Success { get; init; }
    public int KilledCount { get; init; }
}

public sealed class ResetPasswordResult
{
    public bool Success { get; init; }
    public string? NewPassword { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class ChangePasswordResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class SetProfileResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class MutationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
