using System.Collections.Generic;
using System.Security.Claims;
using Dataverse.NavLive;
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
using Tyresoles.Data.Features.Purchase;

using ProductionFetchParams = Tyresoles.Data.Features.Production.Models.FetchParams;
using Tyresoles.Web.GraphQL;
using Microsoft.EntityFrameworkCore;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Models;
using Tyresoles.Data.Features.Crm.Entities;

namespace Tyresoles.Web;

public class Mutation
{
    [Authorize]
    [GraphQLName("createCrmMasterItem")]
    public async Task<CrmMasterItem> CreateCrmMasterItem(
        CrmMasterType type,
        string name,
        int? parentId,
        bool? isPositive,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        int id = 0;
        switch (type)
        {
            case CrmMasterType.ContactType:
                var ctItem = new CrmContactType { Name = name };
                db.CrmContactTypes.Add(ctItem);
                await db.SaveChangesAsync(ct);
                id = ctItem.Id;
                break;
            case CrmMasterType.ContactCategory:
                var catItem = new CrmContactCategory { Name = name };
                db.CrmContactCategories.Add(catItem);
                await db.SaveChangesAsync(ct);
                id = catItem.Id;
                break;
            case CrmMasterType.Source:
                var srcItem = new CrmSource { Name = name };
                db.CrmSources.Add(srcItem);
                await db.SaveChangesAsync(ct);
                id = srcItem.Id;
                break;
            case CrmMasterType.Stage:
                var stItem = new CrmStage { Name = name };
                db.CrmStages.Add(stItem);
                await db.SaveChangesAsync(ct);
                id = stItem.Id;
                break;
            case CrmMasterType.Priority:
                var prItem = new CrmPriority { Name = name };
                db.CrmPriorities.Add(prItem);
                await db.SaveChangesAsync(ct);
                id = prItem.Id;
                break;
            case CrmMasterType.ActivityType:
                var actItem = new CrmActivityType { Name = name };
                db.CrmActivityTypes.Add(actItem);
                await db.SaveChangesAsync(ct);
                id = actItem.Id;
                break;
            case CrmMasterType.ActivityOutcome:
                var outItem = new CrmActivityOutcome { Name = name, ActivityTypeId = parentId, IsPositive = isPositive ?? false };
                db.CrmActivityOutcomes.Add(outItem);
                await db.SaveChangesAsync(ct);
                id = outItem.Id;
                break;
            case CrmMasterType.EntityType:
                var entItem = new CrmEntityType { Name = name };
                db.CrmEntityTypes.Add(entItem);
                await db.SaveChangesAsync(ct);
                id = entItem.Id;
                break;
            case CrmMasterType.VehicleType:
                var vtItem = new CrmFleetVehicleType { Name = name };
                db.CrmFleetVehicleTypes.Add(vtItem);
                await db.SaveChangesAsync(ct);
                id = vtItem.Id;
                break;
            case CrmMasterType.VehicleMake:
                var vmItem = new CrmFleetVehicleMake { Name = name, ParentId = parentId };
                db.CrmFleetVehicleMakes.Add(vmItem);
                await db.SaveChangesAsync(ct);
                id = vmItem.Id;
                break;
            case CrmMasterType.VehicleModel:
                var vmoItem = new CrmFleetVehicleModel { Name = name, ParentId = parentId };
                db.CrmFleetVehicleModels.Add(vmoItem);
                await db.SaveChangesAsync(ct);
                id = vmoItem.Id;
                break;
            case CrmMasterType.Application:
                var appItem = new CrmFleetApplication { Name = name };
                db.CrmFleetApplications.Add(appItem);
                await db.SaveChangesAsync(ct);
                id = appItem.Id;
                break;
        }
        return new CrmMasterItem { Id = id, Name = name, ParentId = parentId, IsPositive = isPositive ?? false };
    }

    [Authorize]
    [GraphQLName("updateCrmMasterItem")]
    public async Task<CrmMasterItem?> UpdateCrmMasterItem(
        CrmMasterType type,
        int id,
        string name,
        int? parentId,
        bool? isPositive,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        switch (type)
        {
            case CrmMasterType.ContactType:
                var ctItem = await db.CrmContactTypes.FindAsync(new object[] { id }, ct);
                if (ctItem == null) return null;
                ctItem.Name = name;
                break;
            case CrmMasterType.ContactCategory:
                var catItem = await db.CrmContactCategories.FindAsync(new object[] { id }, ct);
                if (catItem == null) return null;
                catItem.Name = name;
                break;
            case CrmMasterType.Source:
                var srcItem = await db.CrmSources.FindAsync(new object[] { id }, ct);
                if (srcItem == null) return null;
                srcItem.Name = name;
                break;
            case CrmMasterType.Stage:
                var stItem = await db.CrmStages.FindAsync(new object[] { id }, ct);
                if (stItem == null) return null;
                stItem.Name = name;
                break;
            case CrmMasterType.Priority:
                var prItem = await db.CrmPriorities.FindAsync(new object[] { id }, ct);
                if (prItem == null) return null;
                prItem.Name = name;
                break;
            case CrmMasterType.ActivityType:
                var actItem = await db.CrmActivityTypes.FindAsync(new object[] { id }, ct);
                if (actItem == null) return null;
                actItem.Name = name;
                break;
            case CrmMasterType.ActivityOutcome:
                var outItem = await db.CrmActivityOutcomes.FindAsync(new object[] { id }, ct);
                if (outItem == null) return null;
                outItem.Name = name;
                outItem.ActivityTypeId = parentId;
                outItem.IsPositive = isPositive ?? false;
                break;
            case CrmMasterType.EntityType:
                var entItem = await db.CrmEntityTypes.FindAsync(new object[] { id }, ct);
                if (entItem == null) return null;
                entItem.Name = name;
                break;
            case CrmMasterType.VehicleType:
                var vtItem = await db.CrmFleetVehicleTypes.FindAsync(new object[] { id }, ct);
                if (vtItem == null) return null;
                vtItem.Name = name;
                break;
            case CrmMasterType.VehicleMake:
                var vmItem = await db.CrmFleetVehicleMakes.FindAsync(new object[] { id }, ct);
                if (vmItem == null) return null;
                vmItem.Name = name;
                vmItem.ParentId = parentId;
                break;
            case CrmMasterType.VehicleModel:
                var vmoItem = await db.CrmFleetVehicleModels.FindAsync(new object[] { id }, ct);
                if (vmoItem == null) return null;
                vmoItem.Name = name;
                vmoItem.ParentId = parentId;
                break;
            case CrmMasterType.Application:
                var appItem = await db.CrmFleetApplications.FindAsync(new object[] { id }, ct);
                if (appItem == null) return null;
                appItem.Name = name;
                break;
        }
        await db.SaveChangesAsync(ct);
        return new CrmMasterItem { Id = id, Name = name, ParentId = parentId, IsPositive = isPositive ?? false };
    }

    [Authorize]
    [GraphQLName("deleteCrmMasterItem")]
    public async Task<bool> DeleteCrmMasterItem(
        CrmMasterType type,
        int id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        switch (type)
        {
            case CrmMasterType.ContactType:
                var ctItem = await db.CrmContactTypes.FindAsync(new object[] { id }, ct);
                if (ctItem == null) return false;
                db.CrmContactTypes.Remove(ctItem);
                break;
            case CrmMasterType.ContactCategory:
                var catItem = await db.CrmContactCategories.FindAsync(new object[] { id }, ct);
                if (catItem == null) return false;
                db.CrmContactCategories.Remove(catItem);
                break;
            case CrmMasterType.Source:
                var srcItem = await db.CrmSources.FindAsync(new object[] { id }, ct);
                if (srcItem == null) return false;
                db.CrmSources.Remove(srcItem);
                break;
            case CrmMasterType.Stage:
                var stItem = await db.CrmStages.FindAsync(new object[] { id }, ct);
                if (stItem == null) return false;
                db.CrmStages.Remove(stItem);
                break;
            case CrmMasterType.Priority:
                var prItem = await db.CrmPriorities.FindAsync(new object[] { id }, ct);
                if (prItem == null) return false;
                db.CrmPriorities.Remove(prItem);
                break;
            case CrmMasterType.ActivityType:
                var actItem = await db.CrmActivityTypes.FindAsync(new object[] { id }, ct);
                if (actItem == null) return false;
                db.CrmActivityTypes.Remove(actItem);
                break;
            case CrmMasterType.ActivityOutcome:
                var outItem = await db.CrmActivityOutcomes.FindAsync(new object[] { id }, ct);
                if (outItem == null) return false;
                db.CrmActivityOutcomes.Remove(outItem);
                break;
            case CrmMasterType.EntityType:
                var entItem = await db.CrmEntityTypes.FindAsync(new object[] { id }, ct);
                if (entItem == null) return false;
                db.CrmEntityTypes.Remove(entItem);
                break;
            case CrmMasterType.VehicleType:
                var vtItem = await db.CrmFleetVehicleTypes.FindAsync(new object[] { id }, ct);
                if (vtItem == null) return false;
                db.CrmFleetVehicleTypes.Remove(vtItem);
                break;
            case CrmMasterType.VehicleMake:
                var vmItem = await db.CrmFleetVehicleMakes.FindAsync(new object[] { id }, ct);
                if (vmItem == null) return false;
                db.CrmFleetVehicleMakes.Remove(vmItem);
                break;
            case CrmMasterType.VehicleModel:
                var vmoItem = await db.CrmFleetVehicleModels.FindAsync(new object[] { id }, ct);
                if (vmoItem == null) return false;
                db.CrmFleetVehicleModels.Remove(vmoItem);
                break;
            case CrmMasterType.Application:
                var appItem = await db.CrmFleetApplications.FindAsync(new object[] { id }, ct);
                if (appItem == null) return false;
                db.CrmFleetApplications.Remove(appItem);
                break;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("saveCrmContact")]
    public async Task<CrmContact> SaveCrmContact(
        CrmContactInput input,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        CrmContact? contact;
        if (input.Id == null || input.Id == Guid.Empty)
        {
            contact = new CrmContact
            {
                Id = Guid.NewGuid()
            };
            db.CrmContacts.Add(contact);
        }
        else
        {
            contact = await db.CrmContacts.FindAsync(new object[] { input.Id.Value }, ct);
            if (contact == null)
            {
                throw new GraphQLException($"CrmContact with Id {input.Id.Value} not found.");
            }
        }

        contact.ContactType = input.ContactType;
        contact.ContactCategory = input.ContactCategory;
        contact.FullName = input.FullName;
        contact.CompanyName = input.CompanyName;
        contact.MobileNo = input.MobileNo;
        contact.MobileNo2 = input.MobileNo2;
        contact.EmailIds = input.EmailIds;
        contact.IsDecisionMaker = input.IsDecisionMaker;
        contact.Address = input.Address;
        contact.City = input.City;
        contact.State = input.State;
        contact.RespCenter = input.RespCenter;
        contact.ERPCustomerNos = input.ERPCustomerNos;
        contact.ERPAreaCodes = input.ERPAreaCodes;
        contact.Products = input.Products;
        contact.Tags = input.Tags;
        contact.IsActive = input.IsActive;
        contact.CreatedBy = input.CreatedBy;

        await db.SaveChangesAsync(ct);
        return contact;
    }

    [Authorize]
    [GraphQLName("deleteCrmCallReminder")]
    public async Task<bool> DeleteCrmCallReminder(
        Guid id,
        [Service] CrmDbContext db,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken ct)
    {
        var item = await db.CrmCallReminders.FindAsync(new object[] { id }, ct);
        if (item == null) return false;

        db.CrmCallReminders.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("saveCrmContactFleetDetail")]
    public async Task<CrmContactFleetDetail> SaveCrmContactFleetDetail(
        CrmContactFleetDetailInput input,
        [Service] CrmDbContext db,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken ct)
    {
        CrmContactFleetDetail? fleet;
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) 
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "system";

        if (input.Id == null || input.Id == Guid.Empty)
        {
            fleet = new CrmContactFleetDetail
            {
                Id = Guid.NewGuid(),
                ContactId = input.ContactId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            };
            db.CrmContactFleetDetails.Add(fleet);
        }
        else
        {
            fleet = await db.CrmContactFleetDetails.FindAsync(new object[] { input.Id.Value }, ct);
            if (fleet == null)
            {
                throw new GraphQLException($"Fleet Detail with Id {input.Id.Value} not found.");
            }
        }

        fleet.VehicleType = input.VehicleType;
        fleet.Make = input.Make;
        fleet.Model = input.Model;
        fleet.Quantity = input.Quantity;
        fleet.TyreSize = input.TyreSize;
        fleet.Application = input.Application;

        await db.SaveChangesAsync(ct);
        return fleet;
    }

    [Authorize]
    [GraphQLName("deleteCrmContactFleetDetail")]
    public async Task<bool> DeleteCrmContactFleetDetail(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        var item = await db.CrmContactFleetDetails.FindAsync(new object[] { id }, ct);
        if (item == null) return false;

        db.CrmContactFleetDetails.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("deleteCrmContact")]
    public async Task<bool> DeleteCrmContact(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        var contact = await db.CrmContacts.FindAsync(new object[] { id }, ct);
        if (contact == null) return false;

        db.CrmContacts.Remove(contact);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("saveCrmWhatsappImage")]
    public async Task<CrmWhatsappImage> SaveCrmWhatsappImage(
        CrmWhatsappImageInput input,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        CrmWhatsappImage? img;
        if (input.Id == null || input.Id == Guid.Empty)
        {
            img = new CrmWhatsappImage
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmWhatsappImages.Add(img);
        }
        else
        {
            img = await db.CrmWhatsappImages.FindAsync(new object[] { input.Id.Value }, ct);
            if (img == null)
            {
                throw new GraphQLException($"CrmWhatsappImage with Id {input.Id.Value} not found.");
            }
        }

        img.Name = input.Name;
        img.ImageUrl = input.ImageUrl;
        img.Base64Data = input.Base64Data;
        img.Products = input.Products;

        await db.SaveChangesAsync(ct);
        return img;
    }

    [Authorize]
    [GraphQLName("deleteCrmWhatsappImage")]
    public async Task<bool> DeleteCrmWhatsappImage(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        var img = await db.CrmWhatsappImages.FindAsync(new object[] { id }, ct);
        if (img == null) return false;

        db.CrmWhatsappImages.Remove(img);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("saveCrmWhatsappTemplate")]
    public async Task<CrmWhatsappTemplate> SaveCrmWhatsappTemplate(
        CrmWhatsappTemplateInput input,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        CrmWhatsappTemplate? temp;
        if (input.Id == null || input.Id == Guid.Empty)
        {
            temp = new CrmWhatsappTemplate
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmWhatsappTemplates.Add(temp);
        }
        else
        {
            temp = await db.CrmWhatsappTemplates.FindAsync(new object[] { input.Id.Value }, ct);
            if (temp == null)
            {
                throw new GraphQLException($"CrmWhatsappTemplate with Id {input.Id.Value} not found.");
            }
        }

        temp.Name = input.Name;
        temp.Language = input.Language;
        temp.MessageText = input.MessageText;

        await db.SaveChangesAsync(ct);
        return temp;
    }

    [Authorize]
    [GraphQLName("deleteCrmWhatsappTemplate")]
    public async Task<bool> DeleteCrmWhatsappTemplate(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        var temp = await db.CrmWhatsappTemplates.FindAsync(new object[] { id }, ct);
        if (temp == null) return false;

        db.CrmWhatsappTemplates.Remove(temp);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("saveCrmProduct")]
    public async Task<CrmProduct> SaveCrmProduct(
        CrmProductInput input,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        CrmProduct? prod;
        if (input.Id == null || input.Id == Guid.Empty)
        {
            prod = new CrmProduct
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };
            db.CrmProducts.Add(prod);
        }
        else
        {
            prod = await db.CrmProducts.FindAsync(new object[] { input.Id.Value }, ct);
            if (prod == null)
            {
                throw new GraphQLException($"CrmProduct with Id {input.Id.Value} not found.");
            }
        }

        prod.Code = input.Code;
        prod.Category = input.Category;
        prod.ProductGroup = input.ProductGroup;
        prod.FinalPrice = input.FinalPrice;
        prod.RespCenters = input.RespCenters;
        prod.WhatsappImageCode = input.WhatsappImageCode;

        await db.SaveChangesAsync(ct);
        return prod;
    }

    [Authorize]
    [GraphQLName("deleteCrmProduct")]
    public async Task<bool> DeleteCrmProduct(
        Guid id,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        var prod = await db.CrmProducts.FindAsync(new object[] { id }, ct);
        if (prod == null) return false;

        db.CrmProducts.Remove(prod);
        await db.SaveChangesAsync(ct);
        return true;
    }

    [Authorize]
    [GraphQLName("syncCrmProductsFromPriceGroup")]
    public async Task<int> SyncCrmProductsFromPriceGroup(
        string priceGroupCode,
        string? respCenters,
        [Service] CrmDbContext db,
        [Service] IDataverseDataService dataService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(priceGroupCode))
            return 0;

        using var scope = dataService.ForTenant("NavLive");
        var spTable = scope.GetQualifiedTableName("Sales Price", false);
        var iTable = scope.GetQualifiedTableName("Item", false);
        var today = DateTime.UtcNow.Date;

        var sql = $@"
            SELECT sp.[Item No_] AS ItemNo, MAX(sp.[Unit Price]) AS UnitPrice, MAX(i.[Item Category Code]) AS ItemCategoryCode, MAX(i.[Product Group Code]) AS ProductGroupCode
            FROM {spTable} sp
            LEFT JOIN {iTable} i ON i.[No_] = sp.[Item No_]
            WHERE (sp.[Sales Code] = @salesCode OR sp.[Sales Type] = 2)
              AND (sp.[Starting Date] IS NULL OR sp.[Starting Date] <= @today OR YEAR(sp.[Starting Date]) <= 1753)
              AND (sp.[Ending Date] IS NULL OR sp.[Ending Date] >= @today OR YEAR(sp.[Ending Date]) <= 1753 OR YEAR(sp.[Ending Date]) = 1)
              AND sp.[Unit Price] > 0
            GROUP BY sp.[Item No_]";

        var parameters = new Dictionary<string, object>
        {
            { "salesCode", priceGroupCode.Trim() },
            { "today", today }
        };

        var rows = await scope.RawQueryToArrayAsync<PriceGroupSyncRowDto>(sql, parameters, ct).ConfigureAwait(false);
        if (rows == null || rows.Length == 0)
            return 0;

        List<CrmProduct> existingProducts = await db.CrmProducts.ToListAsync(ct);
        Dictionary<string, CrmProduct> existingDict = new(StringComparer.OrdinalIgnoreCase);
        foreach (var p in existingProducts)
        {
            if (!string.IsNullOrWhiteSpace(p.Code))
            {
                existingDict[p.Code.Trim()] = p;
            }
        }

        int count = 0;
        string? formattedRespCenters = string.IsNullOrWhiteSpace(respCenters) ? null : respCenters.Replace(", ", ",");

        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.ItemNo)) continue;
            string itemCodeKey = r.ItemNo.Trim();
            decimal priceIncGst = Math.Ceiling(r.UnitPrice * 1.18m);

            if (existingDict.TryGetValue(itemCodeKey, out CrmProduct? existing) && existing != null)
            {
                existing.Category = !string.IsNullOrWhiteSpace(r.ItemCategoryCode) ? r.ItemCategoryCode : existing.Category;
                existing.ProductGroup = !string.IsNullOrWhiteSpace(r.ProductGroupCode) ? r.ProductGroupCode : existing.ProductGroup;
                existing.FinalPrice = priceIncGst;
                if (!string.IsNullOrWhiteSpace(formattedRespCenters))
                {
                    existing.RespCenters = formattedRespCenters;
                }
            }
            else
            {
                var newProd = new CrmProduct
                {
                    Id = Guid.NewGuid(),
                    Code = itemCodeKey,
                    Category = r.ItemCategoryCode,
                    ProductGroup = r.ProductGroupCode,
                    FinalPrice = priceIncGst,
                    RespCenters = formattedRespCenters,
                    CreatedAt = DateTime.UtcNow
                };
                db.CrmProducts.Add(newProd);
                existingDict[itemCodeKey] = newProd;
            }
            count++;
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    public class PriceGroupSyncRowDto
    {
        public string ItemNo { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string? ItemCategoryCode { get; set; }
        public string? ProductGroupCode { get; set; }
    }


    /// <summary>
    /// Maps NAV WCF <see cref="System.ServiceModel.FaultException"/>, inner exceptions, and related failures
    /// to a <see cref="GraphQLException"/> so clients see the real message (see <see cref="NavConnectorErrorFilter"/>).
    /// </summary>
    private static GraphQLException ToGqlNavException(Exception ex) =>
        new(NavConnectorErrorFormatting.FormatMessage(ex));

    /// <summary>Save Drive Sync configuration for a user. Requires authentication (typically admin only, unless configuring own).</summary>
    [Authorize]
    [GraphQLName("saveDriveSyncConfig")]
    public async Task<Tyresoles.Data.Features.DriveSync.Entities.DriveSyncUserConfig> SaveDriveSyncConfig(
        Tyresoles.Data.Features.DriveSync.Entities.DriveSyncUserConfig input,
        [Service] Tyresoles.Data.Features.DriveSync.IDriveSyncService syncService,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = httpContextAccessor.HttpContext?.User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";

        // Admin saves config for a user
        return await syncService.SaveUserConfigAsync(input, adminUserId, cancellationToken);
    }

    /// <summary>
    /// Create a Google Drive subfolder under <c>DriveSync:UserBackupFoldersParentId</c> and assign its id to the Nav user.
    /// Caller must be an administrator (<c>userType</c> claim).
    /// </summary>
    [Authorize]
    [GraphQLName("provisionDriveSyncBackupFolder")]
    public async Task<Tyresoles.Data.Features.DriveSync.Entities.DriveSyncUserConfig> ProvisionDriveSyncBackupFolder(
        string targetUserId,
        string? folderName,
        bool replaceExisting,
        [Service] Tyresoles.Data.Features.DriveSync.IDriveSyncService syncService,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        if (!AdminAuthorization.IsAdministrator(httpContextAccessor.HttpContext?.User))
            throw new GraphQLException("Only administrators can provision Google Drive backup folders.");

        if (string.IsNullOrWhiteSpace(targetUserId))
            throw new GraphQLException("targetUserId is required.");

        try
        {
            return await syncService.ProvisionAndAssignBackupFolderAsync(
                    targetUserId.Trim(),
                    folderName,
                    replaceExisting,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    /// <summary>Short-lived OAuth access token (service account) for direct upload to the user backup folder. Hybrid sync path.</summary>
    [Authorize]
    [GraphQLName("requestDriveSyncUploadCredentials")]
    public async Task<Tyresoles.Data.Features.DriveSync.Entities.DriveSyncUploadCredentials> RequestDriveSyncUploadCredentials(
        long requestedUploadBytes,
        [Service] Tyresoles.Data.Features.DriveSync.IDriveSyncService syncService,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";

        if (string.IsNullOrEmpty(userId))
            throw new GraphQLException("Unauthorized");

        try
        {
            return await syncService.RequestUploadCredentialsAsync(userId, requestedUploadBytes, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    public async Task<LoginResult> Login(
        string username,
        string password,
        string? platform = null,
        [Service] IUserService userService = null!,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = httpContextAccessor.HttpContext;
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = context?.Request?.Headers["User-Agent"].ToString();
            var result = await userService.LoginAsync(username, password, platform, ipAddress, userAgent, cancellationToken);

            if (result.Success && OperatingSystem.IsWindows() && string.Equals(platform, "win", StringComparison.OrdinalIgnoreCase))
            {
                var options = (Microsoft.Extensions.Options.IOptions<Tyresoles.Data.Features.WindowsServices.WindowsServiceOptions>?)context?.RequestServices.GetService(typeof(Microsoft.Extensions.Options.IOptions<Tyresoles.Data.Features.WindowsServices.WindowsServiceOptions>));
                if (options?.Value.Enabled == true)
                {
                    var svcManager = (Tyresoles.Data.Features.WindowsServices.IWindowsServiceManager?)context?.RequestServices.GetService(typeof(Tyresoles.Data.Features.WindowsServices.IWindowsServiceManager));
                    var logger = (ILogger<Mutation>?)context?.RequestServices.GetService(typeof(ILogger<Mutation>));

                    if (svcManager != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var statuses = await svcManager.GetAllStatusesAsync(CancellationToken.None);
                                foreach (var svc in statuses)
                                {
                                    if (svc.CanStart && !svc.IsRunning && svc.State != "Unknown" && svc.State != "StartPending")
                                    {
                                        logger?.LogInformation("Auto-starting stopped service '{ServiceName}' upon Windows login.", svc.Name);
                                        await svcManager.StartAsync(svc.Name, CancellationToken.None);
                                    }
                                }
                            }
                            catch (Exception autoEx)
                            {
                                logger?.LogError(autoEx, "Failed to auto-start windows services during login.");
                            }
                        });
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return new LoginResult { Success = false, Message = message, User = null };
        }
    }

    public async Task<LoginResult> RefreshToken(
        string token,
        string refreshToken,
        [Service] IUserService userService = null!,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = httpContextAccessor.HttpContext;
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = context?.Request?.Headers["User-Agent"].ToString();
            return await userService.RefreshTokenAsync(token, refreshToken, ipAddress, userAgent, cancellationToken);
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
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

    [Authorize]
    public async Task<SetProfileResult> UpdateUserDetails(
        string userName,
        ProfileUpdateInput details,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.SetProfileAsync(userName, details, cancellationToken);
            return new SetProfileResult { Success = result, Message = result ? "User updated." : "User not found." };
        }
        catch (Exception ex)
        {
            return new SetProfileResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    public async Task<MutationResult> UpdateUserPermissions(
        string userName,
        List<UserPermissionInput> permissions,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.UpdatePermissionsAsync(userName, permissions, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Permissions updated." : "Failed to update permissions." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    public async Task<MutationResult> UpdateUserResponsibilityCenters(
        string userName,
        List<UserRespCenterInput> assignments,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.UpdateResponsibilityCentersAsync(userName, assignments, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Responsibility centers updated." : "Failed to update assignments." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    public async Task<MutationResult> UpdateUserPostingSetup(
        string userName,
        List<UserPostingSetupInput> assignments,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await userService.UpdatePostingSetupAsync(userName, assignments, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Posting setup updated." : "Failed to update posting setup." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
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
    /// Sanitizes and extracts valid mobile numbers from Sell-to Customer Name, Sell-to Address, and Sell-to Address 2 columns
    /// in Sales Invoice Header table, updating the Mobile No_ column if it is empty.
    /// </summary>
    [Authorize]
    [GraphQLName("sanitizeSalesInvoiceHeaderMobileNumbers")]
    public async Task<MutationResult> SanitizeSalesInvoiceHeaderMobileNumbers(
        [Service] ISalesService salesService,
        [Service] IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await salesService.SanitizeSalesInvoiceHeaderMobileNumbersAsync(scope, cancellationToken);
            return new MutationResult { Success = true, Message = "Sales invoice header mobile numbers sanitized successfully." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "sanitizeSalesInvoiceHeaderMobileNumbers failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("syncClaimPostedMobileNumbers")]
    public async Task<MutationResult> SyncClaimPostedMobileNumbers(
        [Service] ISalesService salesService,
        [Service] IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            var count = await salesService.SyncClaimPostedMobileNumbersAsync(scope, cancellationToken);
            return new MutationResult { Success = true, Message = $"Mobile numbers synced successfully for {count} claim record(s)." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "syncClaimPostedMobileNumbers failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }


    /// <summary>
    /// Fetches unique mobile numbers from SalesInvoiceHeader (NavLive) and imports them as new CrmContact records,
    /// skipping any mobile numbers already present in CrmContacts.
    /// </summary>
    [Authorize]
    [GraphQLName("importCrmContactsFromInvoices")]
    public async Task<MutationResult> ImportCrmContactsFromInvoices(
        [Service] ISalesService salesService,
        [Service] IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            var count = await salesService.ImportUniqueCrmContactsFromInvoicesAsync(scope, cancellationToken);
            return new MutationResult { Success = true, Message = $"Import complete. {count} new contact(s) created/updated." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "importCrmContactsFromInvoices failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("logCrmCall")]
    public async Task<MutationResult> LogCrmCall(
        Guid contactId,
        string outcome,
        string? notes,
        DateTime? followUpDate,
        string? followUpNotes,
        bool? contactIsActive,
        [Service] CrmDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            // Create call log
            var callLog = new CrmCallLog
            {
                Id = Guid.NewGuid(),
                ContactId = contactId,
                CallDate = DateTime.UtcNow,
                Outcome = outcome,
                Notes = notes,
                CreatedBy = callerUserId
            };

            db.CrmCallLogs.Add(callLog);

            // Update contact's LastCallDate and LastCallOutcome
            var contact = await db.CrmContacts.FindAsync(new object[] { contactId }, ct);
            if (contact != null)
            {
                contact.LastCallDate = callLog.CallDate;
                contact.LastCallOutcome = outcome;
                
                if (contactIsActive.HasValue)
                {
                    contact.IsActive = contactIsActive.Value;
                }
            }

            // Update active agent allocation stats if exists
            var agentContact = await db.CrmAgentContacts
                .FirstOrDefaultAsync(ac => ac.ContactId == contactId && ac.AgentUsername == callerUserId && ac.DeallocatedAt == null, ct);
            if (agentContact != null)
            {
                agentContact.LastCallOutcome = outcome;
                agentContact.LastCallDate = callLog.CallDate;
                agentContact.LastCallNotes = notes;
                agentContact.CallCount += 1;
            }

            // Create reminder if followUpDate is specified
            if (followUpDate.HasValue)
            {
                var reminder = new CrmCallReminder
                {
                    Id = Guid.NewGuid(),
                    ContactId = contactId,
                    ReminderDate = followUpDate.Value,
                    Notes = followUpNotes ?? notes,
                    IsCompleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = callerUserId
                };
                db.CrmCallReminders.Add(reminder);
            }

            await db.SaveChangesAsync(ct);
            return new MutationResult { Success = true, Message = "Call log recorded successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("undoCrmCall")]
    public async Task<MutationResult> UndoCrmCall(
        Guid callLogId,
        [Service] CrmDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            var callLog = await db.CrmCallLogs.FindAsync(new object[] { callLogId }, ct);
            if (callLog == null)
            {
                return new MutationResult { Success = false, Message = "Call log not found." };
            }

            if (callLog.CreatedBy != callerUserId)
            {
                return new MutationResult { Success = false, Message = "You can only undo your own call logs." };
            }

            if (callLog.CallDate.Date != DateTime.UtcNow.Date)
            {
                return new MutationResult { Success = false, Message = "You can only undo call logs from today." };
            }

            var contactId = callLog.ContactId;
            db.CrmCallLogs.Remove(callLog);
            await db.SaveChangesAsync(ct);

            var latestLog = await db.CrmCallLogs
                .Where(x => x.ContactId == contactId)
                .OrderByDescending(x => x.CallDate)
                .FirstOrDefaultAsync(ct);

            var contact = await db.CrmContacts.FindAsync(new object[] { contactId }, ct);
            if (contact != null)
            {
                contact.LastCallDate = latestLog?.CallDate;
                contact.LastCallOutcome = latestLog?.Outcome;
            }

            var agentContact = await db.CrmAgentContacts
                .FirstOrDefaultAsync(ac => ac.ContactId == contactId && ac.AgentUsername == callerUserId && ac.DeallocatedAt == null, ct);
            if (agentContact != null)
            {
                agentContact.LastCallOutcome = latestLog?.Outcome;
                agentContact.LastCallDate = latestLog?.CallDate;
                agentContact.LastCallNotes = latestLog?.Notes;
                agentContact.CallCount = Math.Max(0, agentContact.CallCount - 1);
            }

            var reminder = await db.CrmCallReminders
                .FirstOrDefaultAsync(r => r.ContactId == contactId && r.CreatedBy == callerUserId && !r.IsCompleted && r.CreatedAt >= callLog.CallDate.AddMinutes(-1) && r.CreatedAt <= callLog.CallDate.AddMinutes(1), ct);
            
            if (reminder != null)
            {
                db.CrmCallReminders.Remove(reminder);
            }

            await db.SaveChangesAsync(ct);
            return new MutationResult { Success = true, Message = "Call log undone successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    public class AllocateAgentContactsPayload
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<CrmAgentContact> AllocatedContacts { get; set; } = new();
    }

    public class AllocateAgentContactsInput
    {
        public int? CoolDownDays { get; set; }
        public List<string>? RespCenters { get; set; }
        public List<string>? Products { get; set; }
        public List<string>? Areas { get; set; }
        public List<string>? States { get; set; }
        public List<string>? Cities { get; set; }
        public List<string>? Types { get; set; }
        public List<string>? Categories { get; set; }
        public List<string>? Tags { get; set; }
    }

    [Authorize]
    [GraphQLName("allocateAgentContacts")]
    public async Task<AllocateAgentContactsPayload> AllocateAgentContacts(
        AllocateAgentContactsInput? input,
        [Service] CrmDbContext db,
        [Service] IDataverseDataService dataService,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            if (string.IsNullOrEmpty(callerUserId))
            {
                return new AllocateAgentContactsPayload { Success = false, Message = "User username not found in claims." };
            }

            // Retrieve agent's responsibility center locations from NAV
            using var scope = dataService.ForTenant("NavLive");
            var userLocsQry = scope.Query<RespCenterUserSetup>()
                .Where(r => r.UserID == callerUserId)
                .Select(r => r.RespCenter);
            var userLocsList = await scope.ToArrayAsync(userLocsQry, ct);
            var userLocs = userLocsList.Where(x => !string.IsNullOrEmpty(x)).ToList();

            if (userLocs.Count == 0)
            {
                return new AllocateAgentContactsPayload { Success = true, Message = "No responsibility centers assigned to user. No contacts allocated." };
            }

            // Apply selected RespCenters filter but intersect with authorized userLocs
            if (input != null && input.RespCenters != null && input.RespCenters.Any())
            {
                userLocs = userLocs.Intersect(input.RespCenters).ToList();
            }

            if (userLocs.Count == 0)
            {
                return new AllocateAgentContactsPayload { Success = true, Message = "Selected responsibility centers are not authorized. No contacts allocated." };
            }

            // Fetch the ContactsPerAgent setting
            int contactsLimit = 10;
            var limitSetting = await db.CrmSettings.FindAsync(new object[] { "ContactsPerAgent" }, ct);
            if (limitSetting != null && int.TryParse(limitSetting.Value, out var parsedLimit))
            {
                contactsLimit = parsedLimit;
            }

            // Auto-deallocate previous days' contacts to provide a fresh lot
            var todayUtc = DateTime.UtcNow.Date;
            var oldAllocations = await db.CrmAgentContacts
                .Where(ac => ac.AgentUsername == callerUserId && ac.DeallocatedAt == null && ac.AllocatedAt.Date < todayUtc)
                .ToListAsync(ct);
            
            if (oldAllocations.Count > 0)
            {
                foreach (var old in oldAllocations)
                {
                    old.DeallocatedAt = DateTime.UtcNow;
                    old.DeallocatedBy = "System_AutoRefresh";
                }
                await db.SaveChangesAsync(ct);
            }

            // Manual allocation triggered via dialog should fetch up to contactsLimit
            // regardless of currently allocated count.
            int needed = contactsLimit;

                var cooldownCutoffRetry = DateTime.UtcNow.AddDays(-3);
                var cooldownCutoffGeneral = DateTime.UtcNow.AddDays(-30);

                int recentSalesDays = 0;
                if (input != null && input.CoolDownDays.HasValue)
                {
                    recentSalesDays = input.CoolDownDays.Value;
                }
                else
                {
                    var recentSalesSetting = await db.CrmSettings.FindAsync(new object[] { "ContactsRecentSalesDaysCooldown" }, ct);
                    if (recentSalesSetting != null && int.TryParse(recentSalesSetting.Value, out var parsedDays))
                    {
                        recentSalesDays = parsedDays;
                    }
                }

                List<string> recentCustomerNos = new();
                if (recentSalesDays > 0)
                {
                    var cutoffDate = DateTime.UtcNow.Date.AddDays(-recentSalesDays);
                    var qry = scope.Query<SalesInvoiceHeader>()
                        .Where(h => h.PostingDate >= cutoffDate)
                        .Select(h => h.SellToCustomerNo)
                        .Distinct();
                    var tempNos = await scope.ToArrayAsync(qry, ct).ConfigureAwait(false);
                    recentCustomerNos = tempNos.ToList();
                }

                var qryUnassigned = db.CrmContacts
                    .Where(c => c.IsActive
                        && c.RespCenter != null && userLocs.Contains(c.RespCenter)
                        && c.LastCallOutcome != "Wrong Number"
                        && c.LastCallOutcome != "Not Interested"
                        && (c.LastCallDate == null
                            || ((c.LastCallOutcome == "Busy" || c.LastCallOutcome == "No Answer") && c.LastCallDate <= cooldownCutoffRetry)
                            || (c.LastCallOutcome != "Busy" && c.LastCallOutcome != "No Answer" && c.LastCallDate <= cooldownCutoffGeneral)
                           )
                        && !db.CrmAgentContacts.Any(ac => ac.ContactId == c.Id && ac.DeallocatedAt == null)
                    );

                if (input?.Areas != null && input.Areas.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Areas.Any(a => c.ERPAreaCodes.Contains(a)));
                }

                if (input?.Products != null && input.Products.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Products.Any(p => c.Products.Contains(p)));
                }

                if (input?.States != null && input.States.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.States.Contains(c.State));
                }

                if (input?.Cities != null && input.Cities.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Cities.Contains(c.City));
                }

                if (input?.Types != null && input.Types.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Types.Contains(c.ContactType));
                }

                if (input?.Categories != null && input.Categories.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Categories.Contains(c.ContactCategory));
                }

                if (input?.Tags != null && input.Tags.Any())
                {
                    qryUnassigned = qryUnassigned.Where(c => input.Tags.Any(t => c.Tags.Contains(t)));
                }

                var unassigned = new List<CrmContact>();
                int skip = 0;
                int batchSize = 100;

                while (unassigned.Count < needed)
                {
                    var batch = await qryUnassigned
                        .OrderBy(c => c.LastCallDate)
                        .ThenBy(c => c.FullName)
                        .Skip(skip)
                        .Take(batchSize)
                        .ToListAsync(ct);

                    if (batch.Count == 0) break;

                    foreach (var c in batch)
                    {
                        if (unassigned.Count >= needed) break;

                        if (input?.Areas != null && input.Areas.Any())
                        {
                            if (string.IsNullOrWhiteSpace(c.ERPAreaCodes)) continue;
                            var areas = c.ERPAreaCodes.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (!areas.Any(a => input.Areas.Contains(a))) continue;
                        }

                        // Product filter is already applied in SQL via Contains, allowing partial matches.
                        // We removed the strict in-memory token check.

                        if (recentCustomerNos.Count > 0)
                        {
                            if (!string.IsNullOrWhiteSpace(c.ERPCustomerNos))
                            {
                                var nos = c.ERPCustomerNos.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                                if (nos.Any(no => recentCustomerNos.Contains(no))) continue;
                            }
                        }

                        unassigned.Add(c);
                    }
                    skip += batchSize;
                }

                if (unassigned.Count > 0)
                {
                    var newAllocations = new List<CrmAgentContact>(unassigned.Count);
                    foreach (var contact in unassigned)
                    {
                        var allocation = new CrmAgentContact
                        {
                            Id = Guid.NewGuid(),
                            AgentUsername = callerUserId,
                            ContactId = contact.Id,
                            Contact = contact, // populate navigation property for GraphQL response
                            AllocatedAt = DateTime.UtcNow,
                            CallCount = 0
                        };
                        newAllocations.Add(allocation);
                    }
                    db.CrmAgentContacts.AddRange(newAllocations);

                    await db.SaveChangesAsync(ct);
                    return new AllocateAgentContactsPayload { Success = true, Message = $"Successfully allocated {unassigned.Count} new contacts.", AllocatedContacts = newAllocations };
                }

            return new AllocateAgentContactsPayload { Success = true, Message = "No contacts found matching the criteria." };
        }
        catch (Exception ex)
        {
            return new AllocateAgentContactsPayload { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("deallocateCrmContact")]
    public async Task<MutationResult> DeallocateCrmContact(
        Guid contactId,
        [Service] CrmDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            // Find the active allocation
            var allocation = await db.CrmAgentContacts
                .FirstOrDefaultAsync(ac => ac.ContactId == contactId && ac.DeallocatedAt == null, ct);

            if (allocation != null)
            {
                allocation.DeallocatedAt = DateTime.UtcNow;
                allocation.DeallocatedBy = callerUserId;
                await db.SaveChangesAsync(ct);
                return new MutationResult { Success = true, Message = "Contact deallocated successfully." };
            }

            return new MutationResult { Success = false, Message = "No active allocation found for this contact." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("deallocateCrmContacts")]
    public async Task<MutationResult> DeallocateCrmContacts(
        IReadOnlyList<Guid> contactIds,
        [Service] CrmDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            if (contactIds == null || contactIds.Count == 0)
            {
                return new MutationResult { Success = false, Message = "No contacts provided." };
            }

            var now = DateTime.UtcNow;

            var updatedCount = await db.CrmAgentContacts
                .Where(ac => contactIds.Contains(ac.ContactId) && ac.DeallocatedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ac => ac.DeallocatedAt, now)
                    .SetProperty(ac => ac.DeallocatedBy, callerUserId), ct);

            return new MutationResult { 
                Success = true, 
                Message = $"{updatedCount} contact{(updatedCount == 1 ? "" : "s")} deallocated successfully." 
            };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("deallocateUnattendedAgentContacts")]
    public async Task<MutationResult> DeallocateUnattendedAgentContacts(
        string agentUsername,
        [Service] CrmDbContext db,
        ClaimsPrincipal claimsPrincipal,
        CancellationToken ct)
    {
        try
        {
            var callerUserId = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? claimsPrincipal.FindFirst("sub")?.Value
                ?? claimsPrincipal.Identity?.Name
                ?? "";

            if (string.IsNullOrWhiteSpace(agentUsername))
            {
                return new MutationResult { Success = false, Message = "Agent username not provided." };
            }

            var now = DateTime.UtcNow;

            var updatedCount = await db.CrmAgentContacts
                .Where(ac => ac.AgentUsername == agentUsername && ac.DeallocatedAt == null && ac.CallCount == 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(ac => ac.DeallocatedAt, now)
                    .SetProperty(ac => ac.DeallocatedBy, callerUserId), ct);

            return new MutationResult { 
                Success = true, 
                Message = $"{updatedCount} unattended contact{(updatedCount == 1 ? "" : "s")} deallocated successfully." 
            };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [GraphQLName("wipeCrmCallingRecordsTemporary")]
    public async Task<MutationResult> WipeCrmCallingRecordsTemporary(
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM CrmCallLog; DELETE FROM CrmCallReminder; DELETE FROM CrmAgentContact;", ct);
            return new MutationResult { Success = true, Message = "Data wiped successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("saveCrmSetting")]
    public async Task<MutationResult> SaveCrmSetting(
        string key,
        string value,
        string? description,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        try
        {
            var setting = await db.CrmSettings.FindAsync(new object[] { key }, ct);
            if (setting == null)
            {
                setting = new CrmSetting
                {
                    Key = key,
                    Value = value,
                    Description = description
                };
                db.CrmSettings.Add(setting);
            }
            else
            {
                setting.Value = value;
                if (description != null)
                {
                    setting.Description = description;
                }
            }

            await db.SaveChangesAsync(ct);
            return new MutationResult { Success = true, Message = $"Setting '{key}' saved successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("completeCrmReminder")]
    public async Task<MutationResult> CompleteCrmReminder(
        Guid reminderId,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        try
        {
            var reminder = await db.CrmCallReminders.FindAsync(new object[] { reminderId }, ct);
            if (reminder == null)
            {
                return new MutationResult { Success = false, Message = "Reminder not found." };
            }

            reminder.IsCompleted = true;
            await db.SaveChangesAsync(ct);
            return new MutationResult { Success = true, Message = "Reminder marked as completed." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    [GraphQLName("deleteCrmReminder")]
    public async Task<MutationResult> DeleteCrmReminder(
        Guid reminderId,
        [Service] CrmDbContext db,
        CancellationToken ct)
    {
        try
        {
            var reminder = await db.CrmCallReminders.FindAsync(new object[] { reminderId }, ct);
            if (reminder == null)
            {
                return new MutationResult { Success = false, Message = "Reminder not found." };
            }

            db.CrmCallReminders.Remove(reminder);
            await db.SaveChangesAsync(ct);
            return new MutationResult { Success = true, Message = "Reminder deleted successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
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

    /// <summary>Update casing items, adding or removing based on IsActive prop.</summary>
    [Authorize]
    public async Task<MutationResult> UpdateProductionCasingItems(
        List<CasingItem> casingItems,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await productionService.UpdateCasingItemsAsync(scope, casingItems, cancellationToken);
            return new MutationResult { Success = true, Message = "Casing items updated successfully." };
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

    /// <summary>Update transporter / vehicle via NAV WebServe <c>UpdateVehicle</c>.</summary>
    [Authorize]
    [GraphQLName("saveVehicle")]
    public async Task<MutationResult> SaveVehicle(
        VehicleSaveInput input,
        [Service] Connector connector,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input.No))
                return new MutationResult { Success = false, Message = "Vehicle number is required." };

            var n = await connector.UpdateVehicleNavAsync(
                input.No.Trim(),
                input.Name ?? "",
                input.MobileNo ?? "",
                input.GstNo ?? "",
                input.LineNo,
                input.ResponsibilityCenter ?? "",
                input.Status).ConfigureAwait(false);

            return n > 0
                ? new MutationResult { Success = true, Message = "Vehicle saved successfully." }
                : new MutationResult { Success = false, Message = "Failed to save vehicle." };
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

    /// <summary>Insert a row into NAV <c>Procurement Configs</c>.</summary>
    [Authorize]
    [GraphQLName("insertProductionProcurementConfig")]
    public async Task<MutationResult> InsertProductionProcurementConfig(
        ProcurementConfigDto row,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scope = dataService.ForTenant("NavLive");
            httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
            await productionService.InsertProcurementConfigAsync(scope, row, cancellationToken);
            return new MutationResult { Success = true, Message = "Procurement configuration saved." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "InsertProductionProcurementConfig failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Update a NAV <c>Procurement Configs</c> row (match on <paramref name="original"/> key columns).</summary>
    [Authorize]
    [GraphQLName("updateProductionProcurementConfig")]
    public async Task<MutationResult> UpdateProductionProcurementConfig(
        ProcurementConfigDto original,
        ProcurementConfigDto updated,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scope = dataService.ForTenant("NavLive");
            httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
            await productionService.UpdateProcurementConfigAsync(scope, original, updated, cancellationToken);
            return new MutationResult { Success = true, Message = "Procurement configuration updated." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UpdateProductionProcurementConfig failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Delete a row from NAV <c>Procurement Configs</c>.</summary>
    [Authorize]
    [GraphQLName("deleteProductionProcurementConfig")]
    public async Task<MutationResult> DeleteProductionProcurementConfig(
        ProcurementConfigDto key,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scope = dataService.ForTenant("NavLive");
            httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
            await productionService.DeleteProcurementConfigAsync(scope, key, cancellationToken);
            return new MutationResult { Success = true, Message = "Procurement configuration deleted." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DeleteProductionProcurementConfig failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    // ── Navision Edit Requests ─────────────────────────────────

    /// <summary>Create or update a request type template (admin only).</summary>
    [Authorize]
    [GraphQLName("navEditSaveRequestType")]
    public async Task<MutationResult> NavEditSaveRequestType(
        Tyresoles.Data.Features.NavisionEdits.NavEditRequestTypeInput input,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            await navEditService.SaveRequestTypeAsync(input, userId, cancellationToken);
            return new MutationResult { Success = true, Message = "Request type saved successfully." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditSaveRequestType failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Delete (deactivate) a request type template.</summary>
    [Authorize]
    [GraphQLName("navEditDeleteRequestType")]
    public async Task<MutationResult> NavEditDeleteRequestType(
        int id,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await navEditService.DeleteRequestTypeAsync(id, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Request type deleted." : "Request type not found." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditDeleteRequestType failed id={Id}", id);
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Submit a new edit request.</summary>
    [Authorize]
    [GraphQLName("navEditSubmitRequest")]
    public async Task<MutationResult> NavEditSubmitRequest(
        Tyresoles.Data.Features.NavisionEdits.NavEditRequestInput input,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var fullName = httpContextAccessor.HttpContext?.User?.FindFirstValue("FullName");
            await navEditService.SubmitRequestAsync(input, userId, fullName, cancellationToken);
            return new MutationResult { Success = true, Message = "Request submitted successfully." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditSubmitRequest failed");
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Re-send notifications to IT admin and current-level approvers (requester only).</summary>
    [Authorize]
    [GraphQLName("navEditResendNotifications")]
    public async Task<MutationResult> NavEditResendNotifications(
        Guid requestId,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var ok = await navEditService.ResendSubmitNotificationsAsync(requestId, userId, cancellationToken);
            return ok
                ? new MutationResult { Success = true, Message = "Notifications sent." }
                : new MutationResult { Success = false, Message = "Could not resend notifications for this request." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditResendNotifications failed requestId={RequestId}", requestId);
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Approve a pending approval level on a request.</summary>
    [Authorize]
    [GraphQLName("navEditApproveRequest")]
    public async Task<MutationResult> NavEditApproveRequest(
        Guid requestId,
        int level,
        string? comment,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await navEditService.ApproveRequestAsync(requestId, level, userId, comment, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Approved successfully." : "Approval failed or not applicable." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditApproveRequest failed requestId={RequestId}", requestId);
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Reject an edit request.</summary>
    [Authorize]
    [GraphQLName("navEditRejectRequest")]
    public async Task<MutationResult> NavEditRejectRequest(
        Guid requestId,
        string? comment,
        bool? isApproval,
        int? level,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await navEditService.RejectRequestAsync(requestId, userId, comment, isApproval ?? false, level ?? 0, cancellationToken);
            return new MutationResult { Success = result, Message = result ? "Rejected." : "Rejection failed." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditRejectRequest failed requestId={RequestId}", requestId);
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    /// <summary>Mark a request as processed (admin only).</summary>
    [Authorize]
    [GraphQLName("navEditProcessRequest")]
    public async Task<MutationResult> NavEditProcessRequest(
        Guid requestId,
        string? adminRemark,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await navEditService.ProcessRequestAsync(requestId, userId, adminRemark, cancellationToken);
            return new MutationResult
            {
                Success = result,
                Message = result
                    ? "Request processed."
                    : "Cannot process this request. NAV may have returned false — check the request type connector settings and field values."
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "navEditProcessRequest failed requestId={RequestId}", requestId);
            return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }
    }

    [Authorize]
    public async Task<MutationResult> SaveFixedAsset(
        Tyresoles.Data.Features.Purchase.Models.FixedAsset input,
        [Service] IFixedAssetService fixedAssetService,
        [Service] IDataverseDataService dataService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            await fixedAssetService.SaveFixedAssetAsync(scope, input);
            return new MutationResult { Success = true, Message = "Fixed asset saved successfully." };
        }
        catch (Exception ex)
        {
            return new MutationResult { Success = false, Message = NavConnectorErrorFormatting.FormatMessage(ex) };
        }
    }

    [Authorize]
    [GraphQLName("rectifyCustLedgers")]
    public async Task<MutationResult> RectifyCustLedgers(
        [Service] Tyresoles.Data.Features.Common.Connector connector,
        [Service] Tyresoles.Data.IDataverseDataService dataService,
        [Service] ILogger<Mutation> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = dataService.ForTenant("NavLive");
            var detLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
            
            var sql = $@"
SELECT [Customer No_] as CustomerNo 
FROM {detLedgerT}
GROUP BY [Customer No_]
HAVING SUM(Amount) <> SUM(CASE WHEN [Entry Type] <> 2 THEN Amount ELSE 0 END)
ORDER BY [Customer No_]";

            var customers = await scope.RawQueryToArrayAsync<CustomerLedgerRow>(sql, null, cancellationToken);
            
            int successCount = 0;
            foreach (var row in customers)
            {
                if (string.IsNullOrWhiteSpace(row.CustomerNo)) continue;
                
                try
                {
                    int result = 0;
                    while (result != 1)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        result = await connector.RectifyCustLedgerAsync(row.CustomerNo);
                        if (result != 1) await Task.Delay(1000, cancellationToken);
                    }
                    if (result == 1) successCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error rectifying ledger for customer {CustomerNo}", row.CustomerNo);
                }
            }

            return new MutationResult { Success = true, Message = $"Rectified {successCount} customer ledgers." };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "rectifyCustLedgers failed");
            return new MutationResult { Success = false, Message = Tyresoles.Web.GraphQL.NavConnectorErrorFormatting.FormatMessage(ex) };
        }
    }

    private class CustomerLedgerRow
    {
        public string CustomerNo { get; set; } = "";
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
