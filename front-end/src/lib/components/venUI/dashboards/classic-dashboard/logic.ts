import type {
  DashboardData,
  BusinessLocation,
  ProductSale,
  Sales,
  Item,
  ActiveCustomer,
  CustomerSales,
  SaleLine,
  CustomerSaleBalance,
  CollectionData,
  SalesmanSale,
} from "./types";

function groupBy<T>(arr: T[], key: keyof T): Record<string, T[]> {
  return arr.reduce(
    (acc, item) => {
      const k = String(item[key]);
      (acc[k] ||= []).push(item);
      return acc;
    },
    {} as Record<string, T[]>,
  );
}

function sumBy<T>(arr: T[], key: keyof T): number {
  return arr.reduce((acc, item) => acc + (Number(item[key]) || 0), 0);
}

export function prepareBusinessLocations(
  input: DashboardData | undefined,
  activeView: string,
): BusinessLocation[] {
  if (!input?.data?.length) return [];

  const businesses = groupBy(input.data, "business");
  let locations: BusinessLocation[] = [];

  for (const business in businesses) {
    const locs = groupBy(businesses[business], "location");
    const locKeys = Object.keys(locs);
    locations.push({
      business,
      locations: locKeys,
      selections: [...locKeys],
      default:
        locKeys.reduce(
          (best, k) => (locs[k].length > (locs[best]?.length ?? 0) ? k : best),
          locKeys[0],
        ) || "",
    });
  }

  if (activeView === "Salesperson" || activeView === "Dealer") {
    locations = locations.filter((f) => f.business !== "Rubber Business");
  }

  return locations;
}

export function prepareData(
  input: DashboardData | undefined,
  busLocations: BusinessLocation[],
): DashboardData | undefined {
  if (!input?.name) return input;

  switch (input.name) {
    case "ProductSale":
      return prepareProductSale(input, busLocations);
    case "ActiveCustomer":
      return prepareActiveCustomer(input, busLocations);
    case "Collection":
      return prepareCollection(input, busLocations);
    case "SalesmanSale":
    case "DealerSale":
      return prepareSalesman(input, busLocations);
    default:
      return input;
  }
}

function prepareProductSale(
  input: DashboardData,
  busLocations: BusinessLocation[],
): DashboardData {
  if (!input?.data?.length || input.name !== "ProductSale") return input;

  const working: DashboardData = { ...input, data: [] };
  const output: DashboardData = { ...input, data: [] };

  busLocations.forEach((busLoc) => {
    const defaultLocData = input.data.filter(
      (d) => d.business === busLoc.business && d.location === busLoc.default,
    ) as ProductSale[];
    if (!defaultLocData.length) return;

    const byProduct = new Map<string, ProductSale>();
    for (const row of defaultLocData) {
      const pk = row.product ?? "";
      const cur = byProduct.get(pk);
      if (!cur) {
        byProduct.set(pk, { ...row, data: [...row.data] });
      } else {
        cur.data.push(...row.data);
      }
    }

    for (const defLocData of byProduct.values()) {
      const byLabel = new Map<string, Sales>();
      for (const el of defLocData.data) {
        if (!byLabel.has(el.label)) byLabel.set(el.label, el);
      }
      const elements = [...byLabel.values()];

      const newProductSales: ProductSale = { ...defLocData, data: [] };
      const newData: Sales[] = [];

      elements.forEach((element) => {
        const filtered = input.data.filter(
          (d) =>
            d.business === defLocData.business &&
            d.product === defLocData.product &&
            busLoc.selections.includes(d.location),
        ) as ProductSale[];

        const filteredSales: Sales[] = [];
        filtered.forEach((fl) => {
          filteredSales.push(...fl.data.filter((f) => f.label === element.label));
        });

        const filteredItems: Item[] = [];
        filteredSales.forEach((fs) => filteredItems.push(...fs.items));

        const newItems: Item[] = element.items.map((i) => ({
          ...i,
          value: sumBy(
            filteredItems.filter((f) => f.label === i.label),
            "value",
          ),
        }));

        const totalSale = sumBy(filteredSales, "sale");
        newData.push({
          ...element,
          sale: totalSale,
          saleText: totalSale === 0 ? "-" : (totalSale / 100000).toFixed(2),
          items: newItems,
        });
      });

      newProductSales.data = newData;
      working.data.push(newProductSales);
    }
  });

  busLocations.forEach((busLoc) => {
    const firstData = working.data[0] as ProductSale;
    if (!firstData) return;

    const locData = working.data.filter(
      (d) => d.business === busLoc.business,
    ) as ProductSale[];

    const grandTotalData: Sales[] = firstData.data.map((f) => {
      const filtered: Sales[] = [];
      locData
        .filter(
          (fl) =>
            !fl.product.includes("Intercompany") &&
            !fl.product.includes("Exchange"),
        )
        .forEach((ld) => {
          filtered.push(...ld.data.filter((d) => d.label === f.label));
        });

      const totalSale = sumBy(filtered, "sale");
      return {
        ...f,
        sale: totalSale,
        saleText: totalSale === 0 ? "-" : (totalSale / 100000).toFixed(2),
        items: [],
      };
    });

    if (busLoc.business !== "Rubber Business") {
      output.data.push({
        product: "Grand Total",
        business: busLoc.business,
        location: busLoc.default,
        data: grandTotalData,
      } as ProductSale);
    }
    output.data.push(...locData);
  });

  return output;
}

function prepareActiveCustomer(
  input: DashboardData,
  busLocations: BusinessLocation[],
): DashboardData {
  if (!input?.data?.length || input.name !== "ActiveCustomer") return input;

  const working: DashboardData = { ...input, data: [] };

  busLocations.forEach((busLoc) => {
    const defaultLocData = input.data.filter(
      (d) => d.business === busLoc.business && d.location === busLoc.default,
    ) as ActiveCustomer[];
    if (!defaultLocData.length) return;

    // Merge API rows that share the same default location + product (incl. missing
    // product), then dedupe inner periods by dateRange — avoids duplicate {#each} keys
    // (group.product / cs.dateRange) in the Active Customers view.
    const byProduct = new Map<string, ActiveCustomer>();
    for (const row of defaultLocData) {
      const pk = row.product ?? "";
      const cur = byProduct.get(pk);
      if (!cur) {
        byProduct.set(pk, { ...row, data: [...row.data] });
      } else {
        cur.data.push(...row.data);
      }
    }

    for (const defLocData of byProduct.values()) {
      const byDateRange = new Map<string, CustomerSales>();
      for (const cs of defLocData.data) {
        const dk = cs.dateRange ?? "";
        if (!byDateRange.has(dk)) byDateRange.set(dk, cs);
      }
      const dedupedElements = [...byDateRange.values()];

      const newCustomer: ActiveCustomer = { ...defLocData, data: [] };
      const newData: CustomerSales[] = [];

      dedupedElements.forEach((element) => {
        const filtered = input.data.filter(
          (d) =>
            d.business === defLocData.business &&
            d.product === defLocData.product &&
            busLoc.selections.includes(d.location),
        ) as ActiveCustomer[];

        const filteredSales: CustomerSales[] = [];
        filtered.forEach((fl) => {
          filteredSales.push(
            ...fl.data.filter((f) => f.dateRange === element.dateRange),
          );
        });

        const filteredLinesFig: SaleLine[] = [];
        const filteredLinesQty: SaleLine[] = [];
        const filteredBalances: CustomerSaleBalance[] = [];

        filteredSales.forEach((fs) => {
          filteredLinesFig.push(
            ...fs.lines.filter((fl) => fl.description === "Sales per Customer"),
          );
          filteredLinesQty.push(
            ...fs.lines.filter(
              (fl) => fl.description === "Unit sold per Customer",
            ),
          );
          filteredBalances.push(...fs.records);
        });

        newData.push({
          ...element,
          lines: [
            {
              ...filteredLinesFig[0],
              amount: sumBy(filteredLinesFig, "amount"),
            },
            {
              ...filteredLinesQty[0],
              amount: sumBy(filteredLinesQty, "amount"),
            },
          ],
          records: filteredBalances,
        });
      });

      newCustomer.data = newData;
      working.data.push(newCustomer);
    }
  });

  return working;
}

function prepareCollection(
  input: DashboardData,
  busLocations: BusinessLocation[],
): DashboardData {
  if (!input?.data?.length || input.name !== "Collection") return input;

  const working: DashboardData = { ...input, data: [] };

  busLocations.forEach((busLoc) => {
    const defaultLocData = input.data.filter(
      (d) => d.business === busLoc.business && d.location === busLoc.default,
    ) as CollectionData[];
    if (!defaultLocData.length) return;

    const byPeriod = new Map<string, CollectionData>();
    for (const row of defaultLocData) {
      const pk = row.period ?? "";
      const cur = byPeriod.get(pk);
      if (!cur) {
        byPeriod.set(pk, { ...row, data: [...row.data] });
      } else {
        cur.data.push(...row.data);
      }
    }

    for (const defLocData of byPeriod.values()) {
      const filtered = input.data.filter(
        (d) =>
          d.business === defLocData.business &&
          d.period === defLocData.period &&
          busLoc.selections.includes(d.location),
      ) as CollectionData[];

      const newData: any[] = [];
      filtered.forEach((fd) => newData.push(...fd.data));

      working.data.push({
        ...defLocData,
        collection: sumBy(filtered, "collection"),
        data: newData,
      } as CollectionData);
    }
  });

  return working;
}

function prepareSalesman(
  input: DashboardData,
  busLocations: BusinessLocation[],
): DashboardData {
  if (
    !input?.data?.length ||
    (input.name !== "SalesmanSale" && input.name !== "DealerSale")
  )
    return input;

  const working: DashboardData = { ...input, data: [] };

  busLocations.forEach((busLoc) => {
    const defaultLocData = input.data.filter(
      (d) => d.business === busLoc.business && d.location === busLoc.default,
    ) as SalesmanSale[];
    if (!defaultLocData.length) return;

    // One row per unique product for this business+default location. The API can
    // return duplicate default-loc rows (e.g. same missing product); iterating
    // each produced duplicate {#each} keys (e.g. undefined) in the Salesperson tabs.
    const byProduct = new Map<string, SalesmanSale>();
    for (const defLocData of defaultLocData) {
      const pk = defLocData.product ?? "";
      if (!byProduct.has(pk)) {
        byProduct.set(pk, { ...defLocData, data: [] });
      }
    }

    for (const newSalesmanSale of byProduct.values()) {
      const filtered = input.data.filter(
        (d) =>
          d.business === newSalesmanSale.business &&
          d.product === newSalesmanSale.product &&
          busLoc.selections.includes(d.location),
      ) as SalesmanSale[];

      const newData: Record<string, any>[] = [];
      filtered.forEach((fd) => {
        if (fd?.data?.length) newData.push(...fd.data);
      });
      newSalesmanSale.data = newData;
      working.data.push(newSalesmanSale);
    }
  });

  return working;
}
