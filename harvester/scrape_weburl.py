"""
Standalone CLI & Script for Scraping Any Web Directory / Listing URL into Tyresoles CRM.
Accepts any category or single listing URL, handles pagination, extracts leads,
validates contacts, deduplicates, and optionally imports into the CRM.

Usage:
  python scrape_weburl.py --url "https://transportfamily.com/listing-category/trailer-container-movement/" --limit 10 --dry-run
  python scrape_weburl.py --url "https://transportfamily.com/listing-category/trailer-container-movement/" --limit 20 --import-crm
  python scrape_weburl.py --url "https://transportfamily.com/listing-category/trailer-container-movement/" --export-json leads.json --export-csv leads.csv
"""

import sys
import os
import re
import json
import csv
import argparse
import logging
from typing import List, Dict, Any

# Ensure UTF-8 console output on Windows
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

from scrapers.web_url_scraper import WebUrlScraper
from validator import LeadValidator
from crm_client import CrmApiClient
from config import resolve_resp_center

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%H:%M:%S"
)
logger = logging.getLogger("WebUrlScraperCLI")

def export_to_csv(leads: List[Dict[str, Any]], filepath: str):
    if not leads:
        return
    fieldnames = [
        "companyName", "fullName", "mobileNo", "mobileNo2", "emailIds",
        "address", "city", "state", "location", "division", "targetProduct",
        "respCenter", "leadSourceChannel", "website", "qualityScore",
        "sourceUrl", "notes"
    ]
    with open(filepath, mode="w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for lead in leads:
            writer.writerow(lead)
    logger.info(f"Exported {len(leads)} verified leads to CSV: {filepath}")

def export_to_json(leads: List[Dict[str, Any]], filepath: str):
    with open(filepath, mode="w", encoding="utf-8") as f:
        json.dump(leads, f, indent=2, ensure_ascii=False)
    logger.info(f"Exported {len(leads)} verified leads to JSON: {filepath}")

def scrape_and_process(
    url: str,
    limit: int = 10,
    division: str = "Tyresoles",
    product: str = "Commercial Retreading (Pre-Cure & Mould-Cure)",
    dry_run: bool = True,
    import_crm: bool = False,
    export_csv_path: str = "",
    export_json_path: str = "",
    crm_url: str = "http://localhost:5000/graphql",
    auth_token: str = ""
) -> List[Dict[str, Any]]:
    print("\n" + "=" * 75)
    print(f"  TYRESOLES WEB URL LEAD HARVESTER & CRM INGESTION ENGINE")
    print(f"  Target URL : {url}")
    print(f"  Division   : {division} | Product: {product} | Limit: {limit}")
    print("=" * 75 + "\n")

    scraper = WebUrlScraper(timeout=20, delay_between_requests=0.2)
    leads = scraper.scrape(url, limit=limit, division=division, product=product)

    # Enrich each lead with respCenter
    for l in leads:
        l["respCenter"] = resolve_resp_center(l.get("city", ""))

    print("\n" + "-" * 75)
    print(f"VERIFICATION & ANTI-MOCK AUDIT SUMMARY:")
    print(f"  * Total Verified Genuine Leads : {len(leads)}")
    print("-" * 75 + "\n")

    # Preview
    for idx, lead in enumerate(leads, 1):
        print(f"[{idx}] {lead['companyName']}")
        print(f"    Contact: {lead['fullName']} | Mobile: {lead['mobileNo']} (Alt: {lead.get('mobileNo2') or 'N/A'})")
        print(f"    Address: {lead['address'][:60]}... | City: {lead['city']} | State: {lead['state']}")
        print(f"    GPS: {lead.get('location') or 'N/A'} | Website: {lead.get('website') or 'N/A'}")
        print(f"    URL: {lead['sourceUrl']}")
        print(f"    Services: {lead.get('notes', '')[:80]}...")
        print("-" * 75)

    if export_csv_path and leads:
        export_to_csv(leads, export_csv_path)

    if export_json_path:
        export_to_json(leads, export_json_path)

    if (import_crm or not dry_run) and leads:
        print(f"\nPushing {len(leads)} leads to Tyresoles CRM ({crm_url})...")
        crm_client = CrmApiClient(endpoint_url=crm_url, auth_token=auth_token)
        # Format payload according to HarvestedLeadInput schema
        crm_payload = []
        for l in leads:
            crm_payload.append({
                "fullName": l.get("fullName") or l.get("companyName"),
                "companyName": l.get("companyName"),
                "mobileNo": l.get("mobileNo"),
                "mobileNo2": l.get("mobileNo2"),
                "emailIds": l.get("emailIds"),
                "address": l.get("address"),
                "city": l.get("city"),
                "state": l.get("state"),
                "respCenter": l.get("respCenter"),
                "division": l.get("division", "Tyresoles"),
                "targetProduct": l.get("targetProduct", "Commercial Retreading"),
                "leadSourceType": l.get("leadSourceType", "Automated"),
                "leadSourceChannel": l.get("leadSourceChannel", "TransportFamily"),
                "sourceUrl": l.get("sourceUrl"),
                "website": l.get("website"),
                "qualityScore": l.get("qualityScore", 0.9),
                "scrapingQuery": url,
                "tags": l.get("tags", ""),
                "notes": l.get("notes", ""),
                "snippet": l.get("snippet", ""),
                "contactType": l.get("contactType", "Lead"),
                "location": l.get("location")
            })
        res = crm_client.import_leads(crm_payload)
        print(f"CRM Ingestion Result: {res}")
        return leads

    return leads

def main():
    parser = argparse.ArgumentParser(description="Scrape leads from any directory/category/listing web URL with pagination into Tyresoles CRM")
    parser.add_argument("--url", type=str, required=True, help="Web URL (category list, directory page, or single listing)")
    parser.add_argument("--limit", type=int, default=10, help="Maximum number of leads to extract")
    parser.add_argument("--division", type=str, default="Tyresoles", choices=["Tyresoles", "Ecoflex"], help="Target division")
    parser.add_argument("--product", type=str, default="Commercial Retreading", help="Target product name or code")
    parser.add_argument("--dry-run", action="store_true", default=False, help="Preview leads without pushing to CRM")
    parser.add_argument("--import-crm", action="store_true", default=False, help="Import verified leads to CRM")
    parser.add_argument("--export-csv", type=str, default="", help="Filepath to export CSV")
    parser.add_argument("--export-json", type=str, default="", help="Filepath to export JSON")
    parser.add_argument("--crm-url", type=str, default="http://localhost:5000/graphql", help="Tyresoles GraphQL endpoint")
    parser.add_argument("--auth-token", type=str, default="", help="Bearer token for CRM GraphQL")

    args = parser.parse_args()

    # If neither import-crm nor dry-run is specified explicitly, default to dry-run unless --import-crm is set
    dry_run = args.dry_run or (not args.import_crm)

    scrape_and_process(
        url=args.url,
        limit=args.limit,
        division=args.division,
        product=args.product,
        dry_run=dry_run,
        import_crm=args.import_crm,
        export_csv_path=args.export_csv,
        export_json_path=args.export_json,
        crm_url=args.crm_url,
        auth_token=args.auth_token
    )

if __name__ == "__main__":
    main()
