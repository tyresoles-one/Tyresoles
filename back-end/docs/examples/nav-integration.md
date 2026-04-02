# Dynamics NAV Integration Examples

Integrating with legacy NAV 2013 R2 implies several severe idiosyncrasies, natively dealt with inherently using `Tyresoles.Sql`.

## 1. Querying Specific NAV Companies
NAV separates corporate entities completely cleanly using table prefixing. (`[CRONUS India Ltd.$Customer]`). 

Use `.NavCompany()` directly off the `System.IServiceProvider`:

```csharp
var companyDb = db.NavCompany("CRONUS India Ltd.", "NavLive");

var customers = await companyDb
    .Query<Customer>()
    .ToArrayAsync();
```

## 2. Nav Date Fixes
NAV writes blank temporal references literally as `1753-01-01`. 
Mark dates securely inside your `POCO`s using `[NavDate]` causing the compiler to emit `NavDateConverter` parsing rules internally on execution.

```csharp
[NavTable("Sales Invoice Header")]
public class SalesInvoice 
{
    [NavDate]
    public DateTime? PostingDate { get; set; } // Null correctly instead of 1753!
}
```

## 3. NAV Options to Enums
NAV defines Choice schemas as integers. Attach definitions straight to the column properties to securely allow native string mapping. 

```csharp
public enum DocType { Quote = 0, Order = 1, Invoice = 2 }

[NavTable("Sales Header")]
public class SalesHeader
{
    [NavKey] public string No { get; set; } = string.Empty;

    [NavOption("0=Quote,1=Order,2=Invoice")]
    [NavColumn("Document Type")]
    public DocType DocumentType { get; set; }
}

// In your application logic:
var invoices = await scope.Query<SalesHeader>()
    .Where(x => x.DocumentType == DocType.Invoice)
    .ToArrayAsync();
// Safely executes: WHERE [Document Type] = 2
```

## 4. Querying Legacy External 'Filters'
External tools often pass legacy syntax (`BLOCKED=FILTER(<>1)`). 

```csharp
var activeCustoms = await scope
    .Query<Customer>()
    .Filter("BLOCKED=FILTER(<>1)")
    .ToArrayAsync();
```
