"""
Production Google Maps Scraper for Tyresoles & Ecoflex Lead Discovery.
Extracts 100% real business listings: Name, Phone, Full Address, Rating, Reviews, Website, Maps URL.
Supports deep card inspection to extract verified phone numbers from detail panes.
Strictly zero mock data fallbacks.
"""

import re
import time
import logging
from typing import List, Dict, Any
from urllib.parse import quote_plus

logger = logging.getLogger(__name__)

PHONE_PATTERN = re.compile(r'(\+?91[\-\s]?)?[6-9]\d{2}[\-\s]?\d{3}[\-\s]?\d{4}')

def extract_phone_from_text(text: str) -> str:
    """Extracts a valid Indian mobile number from raw text content."""
    if not text:
        return ""
    # Try standard mobile format
    match = PHONE_PATTERN.search(text)
    if match:
        return match.group(0)
    # Try generic 10-digit mobile
    match2 = re.search(r'(\+?91[\-\s]?)?[6-9]\d{9}', text)
    if match2:
        return match2.group(0)
    # Try numbers with multiple spaces or hyphens e.g. +91 9822 012 345
    match3 = re.search(r'(\+?91[\-\s]?)?[6-9](\s*[\d\-]\s*){9}', text)
    if match3:
        return match3.group(0)
    return ""

class GoogleMapsScraper:
    def __init__(self, headless: bool = True):
        self.headless = headless

    def scrape(self, query: str, max_results: int = 15) -> List[Dict[str, Any]]:
        """
        Scrapes live Google Maps search results for a specific query.
        Returns ONLY genuine scraped listings. If scraping yields nothing, returns an empty list.
        """
        logger.info(f"Searching Google Maps for: '{query}' (limit: {max_results})")
        
        try:
            from playwright.sync_api import sync_playwright
            return self._scrape_with_playwright(query, max_results)
        except Exception as e:
            logger.error(f"Playwright scraping error for '{query}': {e}")
            return []

    def _scrape_with_playwright(self, query: str, max_results: int) -> List[Dict[str, Any]]:
        from playwright.sync_api import sync_playwright
        results = []
        
        with sync_playwright() as p:
            browser = p.chromium.launch(
                headless=self.headless,
                args=["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
            )
            context = browser.new_context(
                user_agent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                viewport={"width": 1366, "height": 768},
                locale="en-IN"
            )
            page = context.new_page()
            
            search_url = f"https://www.google.com/maps/search/{quote_plus(query)}"
            try:
                page.goto(search_url, wait_until="domcontentloaded", timeout=25000)
                page.wait_for_timeout(3000)
            except Exception as e:
                logger.warning(f"Initial page load timed out: {e}")

            # Accept consent/cookies if present
            try:
                consent_btn = page.locator('button[aria-label*="Accept"], button:has-text("Accept all")').first
                if consent_btn.is_visible(timeout=1500):
                    consent_btn.click()
                    page.wait_for_timeout(1000)
            except Exception:
                pass

            # Scroll the feed to load more listings
            try:
                feed = page.locator('div[role="feed"]')
                for _ in range(3):
                    if feed.count() > 0:
                        feed.evaluate("el => el.scrollBy(0, 1500)")
                        page.wait_for_timeout(1000)
            except Exception:
                pass

            cards = page.locator('div[role="article"]').all()
            for card in cards[:max_results]:
                try:
                    text_content = card.inner_text()
                    lines = [l.strip() for l in text_content.split("\n") if l.strip()]
                    if not lines:
                        continue

                    # First substantial non-status line is the business name
                    name = lines[0]
                    if any(k in name.lower() for k in ["results for", "sponsored", "ad ·"]):
                        if len(lines) > 1:
                            name = lines[1]

                    # Extract phone number from card text
                    phone = extract_phone_from_text(text_content)

                    # Extract website
                    website = ""
                    try:
                        link_elem = card.locator('a[data-value="Website"]').first
                        if link_elem.count() > 0:
                            website = link_elem.get_attribute("href") or ""
                    except Exception:
                        pass

                    # Extract Maps URL
                    maps_url = ""
                    try:
                        main_link = card.locator('a[href^="https://www.google.com/maps"]').first
                        if main_link.count() > 0:
                            maps_url = main_link.get_attribute("href") or ""
                    except Exception:
                        pass

                    # If phone is not in card text, click the card to open detail pane
                    if not phone:
                        try:
                            card.click(timeout=1500)
                            page.wait_for_timeout(800)
                            
                            # Inspect detail panel for phone button / text
                            phone_button = page.locator('button[data-item-id*="phone"], button[aria-label*="Phone"]').first
                            if phone_button.count() > 0:
                                btn_label = phone_button.get_attribute("aria-label") or phone_button.inner_text()
                                phone = extract_phone_from_text(btn_label)

                            if not website:
                                site_btn = page.locator('a[data-item-id="authority"]').first
                                if site_btn.count() > 0:
                                    website = site_btn.get_attribute("href") or ""
                        except Exception:
                            pass

                    # Extract full address line
                    address = ""
                    for line in lines[1:]:
                        if not any(k in line.lower() for k in ["closed", "open", "reviews", "stars", "rating", "directions", "website", "share", "save", "located in"]):
                            if len(line) > 8 and not re.search(r'^\d+(\.\d+)?\s*(★|stars)?$', line):
                                address = line
                                break

                    # Extract ratings if present
                    rating_match = re.search(r'(\d\.\d)\s*★', text_content)
                    rating = float(rating_match.group(1)) if rating_match else None

                    reviews_match = re.search(r'\((\d+[\d,]*)\)', text_content)
                    reviews_count = int(reviews_match.group(1).replace(",", "")) if reviews_match else 0

                    location = ""
                    target_url = maps_url or search_url
                    if target_url:
                        coord_match = re.search(r'/@(-?\d+\.\d+),(-?\d+\.\d+)', target_url) or re.search(r'!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)', target_url) or re.search(r'[?&]q=(-?\d+\.\d+),(-?\d+\.\d+)', target_url)
                        if coord_match:
                            location = f"{coord_match.group(1)}, {coord_match.group(2)}"

                    results.append({
                        "name": name,
                        "phone": phone,
                        "address": address,
                        "rating": rating,
                        "reviews_count": reviews_count,
                        "website": website,
                        "source_url": target_url,
                        "location": location,
                        "raw_snippet": " | ".join(lines[:5])
                    })
                except Exception as ex:
                    logger.debug(f"Error parsing card: {ex}")
                    continue

            browser.close()

        logger.info(f"Successfully scraped {len(results)} live places from Google Maps.")
        return results
