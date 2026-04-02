# Tyresoles.Sql

[![NuGet](https://img.shields.io/nuget/v/Tyresoles.Sql)](https://www.nuget.org/packages/Tyresoles.Sql)
[![Benchmark](https://img.shields.io/badge/Benchmark-1.2x%20ADO.NET-green)](benchmarks.md)

Tyresoles.Sql is a blazing-fast, allocation-free, NativeAOT-ready data access layer designed specifically for .NET 10. It features a dual-DB strategy, effortlessly supporting both legacy Dynamics NAV 2013 R2 (SQL Server) quirks and modern custom PostgreSQL databases side-by-side using the exact same fluent API.

## 🚀 30-Second Quickstart

```bash
dotnet add package Tyresoles.Sql
```

```csharp
// 1. Define your Entity
[NavTable("Customer")]
public class Customer 
{
    [NavKey]
    [NavColumn("No_")]
    public string Id { get; set; } = "";
    
    public string Name { get; set; } = "";
    
    [NavOption("0= ,1=Active,2=Blocked")]
    public CustomerStatus Status { get; set; }
}

// 2. Query seamlessly across Dialects
IDataverse db = serviceProvider.GetRequiredService<IDataverse>();

var customers = await db.ForTenant("NavLive")
    .NavCompany("CRONUS")
    .Query<Customer>()
    .Where(c => c.Name.Contains("Tyre"))
    .OrderBy(c => c.Name)
    .Page(cursor: null, pageSize: 100)
    .ExecuteAsync();
```

## Why Tyresoles.Sql?
⚡ **1.2x ADO.NET speed**: Bypasses reflection using Roslyn Source Generators to emit zero-allocation DBDataReader mappers directly at compile time.
🎯 **Zero Boilerplate**: Write a POCO, get a full high-performance data mapper instantly.
🛠 **NAV + Custom DB Support**: First-class support for `[Company$Table]` partitioning, zero dates (`1753-01-01`), option fields, and PostgreSQL schemas under the same API.
🔄 **GraphQL Ready**: Automatically emits `.AsQueryable()` endpoints specifically built to hook up seamlessly with HotChocolate AST projections and filtering.
