"""
Main Entrypoint for Automated Lead Harvester (Tyresoles & Ecoflex)
100% Real-World Verified Data Pipeline - Zero Mock / Zero Fake Data.

Usage examples:
  python main.py --list-products
  python main.py --division Tyresoles --product "Commercial Retreading" --city Belgaum --limit 10 --dry-run
  python main.py --division Tyresoles --product "Ready-Retreaded Tyres (RRT)" --city Pune --source oem_dealers --export-csv leads_dealers.csv
  python main.py --division Ecoflex --product Tuffloor --city Mumbai --limit 15 --export-csv leads_mumbai.csv
"""

import sys
import os
import re
import csv
import json
import argparse
import logging
from typing import List, Dict, Any

# Ensure UTF-8 output encoding on all platforms
if sys.platform == "win32":
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass


from config import DIVISIONS, resolve_resp_center
from validator import LeadValidator
from ai_enricher import AILeadEnricher
from crm_client import CrmApiClient
from scrapers.google_maps import GoogleMapsScraper
from scrapers.transport_directory import TransportDirectoryScraper
from scrapers.oem_dealers import OEMDealerScraper
from scrapers.linkedin_osint import LinkedInOsintScraper
from scrapers.web_url_scraper import WebUrlScraper

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    datefmt="%H:%M:%S"
)
logger = logging.getLogger("TyresolesHarvester")

def list_products():
    print("\n=======================================================")
    print("  TYRESOLES & ECOFLEX CONFIGURED PRODUCT PROFILES")
    print("=======================================================\n")
    for div_key, div_info in DIVISIONS.items():
        print(f"Division: [{div_key}] - {div_info['name']}")
        print(f"Industry: {div_info['industry']}")
        print("Configured Products:")
        for prod_key, prod_info in div_info["products"].items():
            print(f"  • {prod_key} (Code: {prod_info['code']})")
            print(f"    Target Personas: {', '.join(prod_info['target_personas'])}")
            print(f"    Sample Queries : {prod_info['query_templates'][0]}")
        print("-" * 55)

def export_to_csv(leads: List[Dict[str, Any]], filepath: str):
    """Exports verified leads to a clean CSV file."""
    if not leads:
        logger.warning("No leads to export.")
        return

    fieldnames = [
        "companyName", "fullName", "mobileNo", "emailIds", "address", "city",
        "respCenter", "division", "targetProduct", "qualityScore",
        "leadSourceChannel", "website", "rating", "reviewsCount",
        "sourceUrl", "notes"
    ]

    with open(filepath, mode="w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for lead in leads:
            writer.writerow(lead)

    logger.info(f"Successfully exported {len(leads)} verified leads to CSV: {filepath}")

def export_to_json(leads: List[Dict[str, Any]], filepath: str):
    """Exports verified leads to a JSON file."""
    with open(filepath, mode="w", encoding="utf-8") as f:
        json.dump(leads, f, indent=2, ensure_ascii=False)
    logger.info(f"Successfully exported {len(leads)} verified leads to JSON: {filepath}")

def run_harvest(
    division: str,
    product_name: str,
    city: str,
    source: str,
    limit: int,
    dry_run: bool,
    export_csv_path: str,
    export_json_path: str,
    crm_url: str,
    auth_token: str,
    radius: int = 25,
    url: str = ""
):
    # Detect radius if present in city string
    rad_match = re.search(r'(?:within\s*|radius\s*|\b)(\d+)\s*km', city, re.IGNORECASE)
    if rad_match:
        radius = int(rad_match.group(1))
        city = re.sub(r'\s*\([^)]*km[^)]*\)', '', city, flags=re.IGNORECASE)
        city = re.sub(r'\s*(?:within\s*|radius\s*|\b)\d+\s*km.*$', '', city, flags=re.IGNORECASE).strip(' ,-')

    # Check division (case-insensitive)
    div_config = None
    matched_div = division
    for d_name, d_cfg in DIVISIONS.items():
        if d_name.lower() == division.lower():
            div_config = d_cfg
            matched_div = d_name
            break

    if not div_config:
        logger.error(f"Division '{division}' not found. Valid options: {list(DIVISIONS.keys())}")
        if export_json_path:
            import json as _json
            with open(export_json_path, 'w', encoding='utf-8') as f:
                _json.dump([], f)
        return
    division = matched_div

    # Find matching product (exact key, code, display name, or substring)
    product_config = None
    target_prod_name = None
    clean_p = product_name.strip().lower()

    for p_name, p_info in div_config["products"].items():
        key_low = p_name.lower()
        code_low = p_info.get("code", "").lower()
        name_low = p_info.get("name", "").lower()

        if (
            clean_p == key_low
            or clean_p == code_low
            or clean_p == name_low
            or key_low in clean_p
            or clean_p in key_low
            or name_low in clean_p
            or clean_p in name_low
        ):
            product_config = p_info
            target_prod_name = p_name
            break

    if not product_config:
        logger.error(f"Product '{product_name}' not found in division '{division}'. Valid products: {list(div_config['products'].keys())}")
        if export_json_path:
            import json as _json
            with open(export_json_path, 'w', encoding='utf-8') as f:
                _json.dump([], f)
        return

    logger.info("===================================================================")
    logger.info(f"STARTING VERIFIED HARVEST: [{division}] | [{target_prod_name}] | [{city}] (±{radius}km)")
    logger.info(f"Source Engine: [{source}] | Limit: {limit} | Dry-run: {dry_run}")
    logger.info("===================================================================")

    enricher = AILeadEnricher()
    crm_client = CrmApiClient(endpoint_url=crm_url, auth_token=auth_token)
    resp_center = resolve_resp_center(city)

    raw_listings: List[Dict[str, Any]] = []

    # 1. Scrape based on direct Web URL or selected source
    if url or source in ["web_scraper", "web_url", "transport_family"]:
        target_url = url or "https://transportfamily.com/listing-category/trailer-container-movement/"
        logger.info(f"Scraping direct Web URL: {target_url}")
        web_scraper = WebUrlScraper(timeout=20, delay_between_requests=0.2)
        url_leads = web_scraper.scrape(target_url, limit=limit, division=division, product=target_prod_name)
        for item in url_leads:
            raw_listings.append({
                "name": item.get("companyName"),
                "contact_person": item.get("fullName"),
                "phone": item.get("mobileNo"),
                "phone2": item.get("mobileNo2"),
                "email": item.get("emailIds"),
                "address": item.get("address"),
                "city": item.get("city") or city,
                "state": item.get("state"),
                "location": item.get("location"),
                "website": item.get("website"),
                "source_url": item.get("sourceUrl"),
                "category": item.get("leadSourceChannel") or "Web-Scraper",
                "notes": item.get("notes"),
                "raw_snippet": item.get("snippet")
            })

    if not url and source in ["all", "google_maps"]:
        maps_scraper = GoogleMapsScraper(headless=True)
        for template in product_config["query_templates"][:2]:
            base_query = template.format(city=city, industrial_area=f"{city} Industrial Area", rural_hub=city, district=city, state="")
            query = f"{base_query} within {radius} km" if radius and radius > 0 else base_query
            items = maps_scraper.scrape(query, max_results=limit)
            for item in items:
                item["query"] = query
                item["category"] = "Google-Maps"
            raw_listings.extend(items)

    if source in ["all", "transport_directory"] and division == "Tyresoles":
        td_scraper = TransportDirectoryScraper(headless=True)
        td_items = td_scraper.scrape(city=city, category="transporter", max_results=limit)
        for item in td_items:
            item["category"] = "Transport-Directory"
        raw_listings.extend(td_items)

    if source in ["all", "oem_dealers"] and ("dealer" in target_prod_name.lower() or "rrt" in product_config["code"].lower()):
        oem_scraper = OEMDealerScraper(headless=True)
        for brand in ["Apollo", "MRF", "JK Tyre"]:
            oem_items = oem_scraper.scrape(city=city, brand=brand, max_results=limit // 2 or 5)
            for item in oem_items:
                item["category"] = f"OEM-Dealer-{brand}"
            raw_listings.extend(oem_items)

    if source in ["all", "linkedin_osint"]:
        linkedin_scraper = LinkedInOsintScraper(headless=True)
        personas = product_config.get("target_personas", ["Fleet Owner", "Transport Contractor"])[:2]
        per_persona_limit = max(5, (limit // len(personas)) + 2) if personas else limit
        for persona in personas:
            if len(raw_listings) >= limit:
                break
            remaining = limit - len(raw_listings)
            li_items = linkedin_scraper.scrape_by_role_and_city(
                role=persona,
                industry_or_product=target_prod_name,
                city=city,
                max_results=min(remaining + 2, per_persona_limit)
            )
            for item in li_items:
                item["category"] = "LinkedIn-OSINT"
                item["address"] = f"{city} Corporate / Industrial Area"
                item["name"] = item.get("company") or item.get("name")
                item["contact_person"] = item.get("name")
            raw_listings.extend(li_items)

    logger.info(f"Raw scraped records: {len(raw_listings)}")

    # 2. Strict Anti-Fake Validation & Deduplication Gatekeeper
    verified_leads: List[Dict[str, Any]] = []
    rejected_count = 0
    rejection_reasons: Dict[str, int] = {}

    is_linkedin_source = (source == "linkedin_osint")

    for raw in raw_listings:
        is_linkedin_item = is_linkedin_source or (raw.get("category") == "LinkedIn-OSINT")
        # Pre-filter phone
        raw_phone = raw.get("phone", "")
        valid_phone, phone_res = LeadValidator.validate_indian_mobile(raw_phone)
        if not valid_phone and not is_linkedin_item:
            rejected_count += 1
            rejection_reasons[phone_res] = rejection_reasons.get(phone_res, 0) + 1
            continue

        # AI Enrich & Score
        lead_city = raw.get("city") or city
        lead = enricher.enrich_and_score(raw, division, target_prod_name, lead_city)
        lead["respCenter"] = resolve_resp_center(lead_city)
        if raw.get("city"):
            lead["city"] = raw.get("city")
        if raw.get("state"):
            lead["state"] = raw.get("state")
        if raw.get("location"):
            lead["location"] = raw.get("location")
        if raw.get("phone2"):
            lead["mobileNo2"] = raw.get("phone2")
        if raw.get("notes"):
            lead["notes"] = raw.get("notes")
            lead["snippet"] = raw.get("raw_snippet") or raw.get("notes")[:250]
        if raw.get("contact_person") and raw.get("contact_person") != raw.get("name"):
            lead["fullName"] = raw.get("contact_person")
        if raw.get("email"):
            lead["emailIds"] = raw.get("email")

        # For verified LinkedIn executive leads without direct mobile, keep mobile empty and set informative note
        if is_linkedin_item and not valid_phone:
            lead["mobileNo"] = ""
            if not lead.get("notes") or "Verified lead" in lead.get("notes", ""):
                lead["notes"] = f"LinkedIn Executive: {lead.get('fullName', '')} at {lead.get('companyName', '')} ({city})"

        # Final Lead Integrity Check
        is_valid, reason = LeadValidator.validate_lead(lead, allow_no_phone=is_linkedin_item)
        if is_valid:
            verified_leads.append(lead)
        else:
            rejected_count += 1
            rejection_reasons[reason] = rejection_reasons.get(reason, 0) + 1

    # 3. Deduplicate
    unique_verified_leads = LeadValidator.deduplicate(verified_leads)
    duplicate_count = len(verified_leads) - len(unique_verified_leads)

    logger.info("-------------------------------------------------------------------")
    logger.info("VERIFICATION & ANTI-MOCK AUDIT REPORT:")
    logger.info(f"  * Total Raw Scraped       : {len(raw_listings)}")
    logger.info(f"  * Rejected (Fake/No Phone): {rejected_count}")
    for reason, count in rejection_reasons.items():
        logger.info(f"    - {reason}: {count}")
    logger.info(f"  * Duplicate Skipped       : {duplicate_count}")
    logger.info(f"  * 100% REAL VERIFIED LEADS: {len(unique_verified_leads)}")
    logger.info("-------------------------------------------------------------------")

    # 4. Preview / Export / Ingestion
    if dry_run or not unique_verified_leads:
        print("\n======================= VERIFIED LEADS PREVIEW =======================")
        for idx, lead in enumerate(unique_verified_leads[:10], 1):
            print(f"[{idx}] {lead['companyName']}")
            print(f"    Contact: {lead['fullName']} | Mobile: {lead['mobileNo']} | Email: {lead.get('emailIds', 'N/A')}")
            print(f"    Address: {lead['address']} | City: {lead['city']} | Resp Center: {lead['respCenter']}")
            print(f"    Product: {lead['targetProduct']} | Quality Score: {lead['qualityScore']}")
            print(f"    Channel: {lead['leadSourceChannel']} | URL: {lead.get('sourceUrl', '')[:60]}...")
            print("-" * 70)

        if not unique_verified_leads:
            print("\n[INFO] 0 genuine verified leads found matching the exact query criteria.")

    # File exports
    if export_csv_path and unique_verified_leads:
        export_to_csv(unique_verified_leads, export_csv_path)

    if export_json_path and unique_verified_leads:
        export_to_json(unique_verified_leads, export_json_path)
    elif export_json_path:
        # Always write JSON even if empty — so the C# backend knows the script ran successfully
        import json as _json
        with open(export_json_path, 'w', encoding='utf-8') as f:
            _json.dump([], f)

    # CRM Push
    if not dry_run and unique_verified_leads:
        logger.info(f"Pushing {len(unique_verified_leads)} leads to Tyresoles CRM ({crm_url})...")
        res = crm_client.import_leads(unique_verified_leads)
        logger.info(f"CRM Ingestion Result: {res}")

def run_single_lead_osint_lookup(name: str, company: str, city: str):
    """Executes instant OSINT waterfall lookup on a specific lead without any login."""
    print("\n=======================================================")
    print(f"  LINKEDIN OSINT WATERFALL LOOKUP: [{name}] @ [{company}]")
    print("=======================================================\n")
    scraper = LinkedInOsintScraper(headless=True)
    phone, email = scraper.enrich_lead_contact(name, company, city)
    print(f"Target Lead : {name}")
    print(f"Company     : {company}")
    print(f"City        : {city or 'N/A'}")
    print("-" * 55)
    print(f"Verified Mobile : {phone if phone else 'Not found in public records'}")
    print(f"Verified Email  : {email if email else 'Not found in public records'}")
    print("=======================================================\n")

def main():
    parser = argparse.ArgumentParser(description="Tyresoles & Ecoflex 100% Real-World Lead Harvester & OSINT Engine")
    parser.add_argument("--list-products", action="store_true", help="List all configured products and templates")
    parser.add_argument("--division", type=str, default="Tyresoles", choices=["Tyresoles", "Ecoflex"], help="Target division")
    parser.add_argument("--product", type=str, default="Commercial Retreading", help="Product name or code")
    parser.add_argument("--city", type=str, default="Pune", help="Target city / Transport hub")
    parser.add_argument("--source", type=str, default="google_maps", choices=["google_maps", "transport_directory", "oem_dealers", "linkedin_osint", "web_scraper", "all"], help="Lead extraction source")
    parser.add_argument("--url", type=str, default="", help="Direct Web URL (directory category or listing) to harvest leads from")
    parser.add_argument("--limit", type=int, default=10, help="Max results per search query")
    parser.add_argument("--radius", type=int, default=25, help="Search radius in kilometers from city center")
    parser.add_argument("--dry-run", action="store_true", help="Preview verified leads without inserting into CRM")
    parser.add_argument("--export-csv", type=str, default="", help="Path to export verified leads as CSV")
    parser.add_argument("--export-json", type=str, default="", help="Path to export verified leads as JSON")
    parser.add_argument("--crm-url", type=str, default="http://localhost:5000/graphql", help="Tyresoles GraphQL endpoint")
    parser.add_argument("--auth-token", type=str, default="", help="JWT bearer token for CRM API")
    
    # Standalone OSINT Lookup flags
    parser.add_argument("--lookup-name", type=str, default="", help="Individual lead name to OSINT lookup")
    parser.add_argument("--lookup-company", type=str, default="", help="Individual company name to OSINT lookup")

    args = parser.parse_args()

    if args.list_products:
        list_products()
        return

    if args.lookup_name:
        run_single_lead_osint_lookup(args.lookup_name, args.lookup_company, args.city)
        return

    run_harvest(
        division=args.division,
        product_name=args.product,
        city=args.city,
        source=args.source,
        limit=args.limit,
        dry_run=args.dry_run,
        export_csv_path=args.export_csv,
        export_json_path=args.export_json,
        crm_url=args.crm_url,
        auth_token=args.auth_token,
        radius=args.radius,
        url=args.url
    )

if __name__ == "__main__":
    main()

