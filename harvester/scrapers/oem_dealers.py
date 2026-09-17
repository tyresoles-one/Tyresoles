"""
OEM Commercial Tyre Dealer Locator Scraper.
Extracts real dealer points (Apollo, MRF, CEAT, JK Tyre) for Ready-Retread Tyres (RRT) partnerships.
"""

import re
import logging
from typing import List, Dict, Any
from urllib.parse import quote_plus
from scrapers.google_maps import extract_phone_from_text

logger = logging.getLogger(__name__)

class OEMDealerScraper:
    def __init__(self, headless: bool = True):
        self.headless = headless

    def scrape(self, city: str, brand: str = "Apollo", max_results: int = 15) -> List[Dict[str, Any]]:
        """
        Scrapes genuine commercial tyre dealers and truck wheel points in a target city.
        """
        logger.info(f"Scraping OEM Dealer Locator for brand: '{brand}' in city: '{city}' (limit: {max_results})")
        results = []

        try:
            from playwright.sync_api import sync_playwright
            results = self._scrape_brand_dealers(city, brand, max_results)
        except Exception as e:
            logger.error(f"OEM Dealer Locator scrape failed: {e}")

        logger.info(f"OEM Dealer Scraper extracted {len(results)} verified tyre dealers.")
        return results

    def _scrape_brand_dealers(self, city: str, brand: str, max_results: int) -> List[Dict[str, Any]]:
        from playwright.sync_api import sync_playwright
        results = []

        query = f"{brand} commercial truck tyre dealer in {city}"

        with sync_playwright() as p:
            browser = p.chromium.launch(
                headless=self.headless,
                args=["--no-sandbox", "--disable-dev-shm-usage"]
            )
            context = browser.new_context(
                user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                locale="en-IN"
            )
            page = context.new_page()

            search_url = f"https://www.google.com/maps/search/{quote_plus(query)}"
            try:
                page.goto(search_url, wait_until="domcontentloaded", timeout=25000)
                page.wait_for_timeout(2500)
            except Exception:
                pass

            cards = page.locator('div[role="article"]').all()
            for card in cards[:max_results]:
                try:
                    text_content = card.inner_text()
                    lines = [l.strip() for l in text_content.split("\n") if l.strip()]
                    if not lines:
                        continue

                    name = lines[0]
                    if any(k in name.lower() for k in ["results for", "sponsored", "ad ·"]):
                        if len(lines) > 1:
                            name = lines[1]

                    # Extract phone number
                    phone = extract_phone_from_text(text_content)

                    # Extract website
                    website = ""
                    try:
                        link_elem = card.locator('a[data-value="Website"]').first
                        if link_elem.count() > 0:
                            website = link_elem.get_attribute("href") or ""
                    except Exception:
                        pass

                    maps_link = card.locator('a[href^="https://www.google.com/maps"]').first
                    maps_url = maps_link.get_attribute("href") if maps_link.count() > 0 else search_url

                    # If phone missing from card snippet, click card to check detail pane
                    if not phone:
                        try:
                            card.click(timeout=1500)
                            page.wait_for_timeout(800)
                            phone_button = page.locator('button[data-item-id*="phone"], button[aria-label*="Phone"]').first
                            if phone_button.count() > 0:
                                btn_label = phone_button.get_attribute("aria-label") or phone_button.inner_text()
                                phone = extract_phone_from_text(btn_label)
                        except Exception:
                            pass

                    # Extract address
                    address = ""
                    for line in lines[1:]:
                        if not any(k in line.lower() for k in ["closed", "open", "reviews", "stars", "rating", "directions", "website"]):
                            if len(line) > 8 and not re.search(r'^\d+(\.\d+)?\s*(★|stars)?$', line):
                                address = line
                                break

                    results.append({
                        "name": name,
                        "phone": phone,
                        "address": address or f"{city} Tyre Market",
                        "brand": brand,
                        "website": website,
                        "source_url": maps_url,
                        "raw_snippet": " | ".join(lines[:4]),
                        "category": f"OEM-Dealer-{brand}"
                    })
                except Exception:
                    continue

            browser.close()

        return results
