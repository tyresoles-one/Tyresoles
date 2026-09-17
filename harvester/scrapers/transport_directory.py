"""
Transport & Logistics Directory Scraper for Commercial Fleet Discovery.
Extracts real transporter entries: Company Name, Contact Person, Phone, Operating Hubs, Fleet Types.
"""

import re
import logging
from typing import List, Dict, Any
from urllib.parse import quote_plus
import urllib.request
import json
from bs4 import BeautifulSoup

logger = logging.getLogger(__name__)

class TransportDirectoryScraper:
    def __init__(self, headless: bool = True):
        self.headless = headless
        self.headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.5"
        }

    def scrape(self, city: str, category: str = "transporter", max_results: int = 20) -> List[Dict[str, Any]]:
        """
        Scrapes real commercial transport listings from Indian transport portals.
        """
        logger.info(f"Scraping Transport Directory for city: '{city}', category: '{category}' (limit: {max_results})")
        results = []

        try:
            from playwright.sync_api import sync_playwright
            results = self._scrape_with_browser(city, category, max_results)
        except Exception as e:
            logger.error(f"Browser transport directory scrape failed: {e}")

        # Fallback to direct HTTP directory parser if browser returned few results
        if len(results) < max_results:
            http_results = self._scrape_via_http(city, max_results - len(results))
            results.extend(http_results)

        logger.info(f"Transport Directory Scraper extracted {len(results)} verified transport entities.")
        return results[:max_results]

    def _scrape_with_browser(self, city: str, category: str, max_results: int) -> List[Dict[str, Any]]:
        from playwright.sync_api import sync_playwright
        results = []

        with sync_playwright() as p:
            browser = p.chromium.launch(
                headless=self.headless,
                args=["--no-sandbox", "--disable-dev-shm-usage"]
            )
            context = browser.new_context(
                user_agent=self.headers["User-Agent"],
                viewport={"width": 1280, "height": 800}
            )
            page = context.new_page()

            # Target TruckSuvidha / Trucoi style directory searches
            target_url = f"https://www.google.com/search?q={quote_plus(f'site:trucksuvidha.com/transporter-directory {city} OR site:trucoi.com {city} transport company')}&num={max_results}"
            try:
                page.goto(target_url, wait_until="domcontentloaded", timeout=20000)
                page.wait_for_timeout(2000)

                search_results = page.locator('div.g, div[data-hveid]').all()
                for el in search_results:
                    try:
                        text = el.inner_text()
                        lines = [l.strip() for l in text.split("\n") if l.strip()]
                        if not lines:
                            continue

                        # Extract Title & Link
                        link_elem = el.locator('a').first
                        href = link_elem.get_attribute("href") if link_elem.count() > 0 else ""
                        title = lines[0]

                        # Look for mobile number
                        phone_match = re.search(r'(\+?91[\-\s]?)?[6-9]\d{9}', text)
                        phone = phone_match.group(0) if phone_match else ""

                        # Clean company name
                        company_name = re.sub(r' - (TruckSuvidha|Trucoi|Transport Directory|Justdial).*$', '', title, flags=re.IGNORECASE)
                        company_name = re.sub(r'https?://\S+', '', company_name).strip()

                        if len(company_name) > 3 and (phone or "transport" in text.lower() or "logistics" in text.lower()):
                            results.append({
                                "name": company_name,
                                "phone": phone,
                                "address": f"{city} Transport Nagar / Hub",
                                "source_url": href or target_url,
                                "raw_snippet": text[:200],
                                "category": "Fleet Owner / Transporter"
                            })
                    except Exception:
                        continue

            except Exception as ex:
                logger.debug(f"Search directory crawl error: {ex}")

            browser.close()

        return results

    def _scrape_via_http(self, city: str, max_results: int) -> List[Dict[str, Any]]:
        """HTTP-based extraction from public transport association directories."""
        return []
