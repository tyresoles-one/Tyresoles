"""
Production LinkedIn OSINT & Contact Extraction Engine (Zero Login, Zero Paid API).
Uses duckduckgo-search library for reliable X-Ray dorking, Playwright Bing as fallback,
Emoji/Unicode Phone Unmasking, PDF/Resume Hunting, and Corporate Registry Enrichment.
"""

import re
import time
import logging
from typing import List, Dict, Any, Optional, Tuple
from urllib.parse import quote_plus, unquote

logger = logging.getLogger(__name__)

# Indian Mobile Regex Patterns
INDIAN_MOBILE_REGEXES = [
    re.compile(r'(?:\+?91[\-\s]?)?[6-9]\d{2}[\-\s]?\d{3}[\-\s]?\d{4}'),
    re.compile(r'(?:\+?91[\-\s]?)?[6-9]\d{4}[\-\s]?\d{5}'),
    re.compile(r'(?:\+?91[\-\s]?)?[6-9]\d{9}'),
    re.compile(r'(?:\+?91[\-\s\./]?)?[6-9]\d{4}[\-\s\./]?\d{5}'),
    re.compile(r'(?:\+?91[\-\s]?)?[6-9](?:\s*[\d\-]\s*){9}')
]

EMAIL_REGEX = re.compile(r'[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}')

FORBIDDEN_PERSONAS = [
    "ai mode", "ai overview", "people also ask", "images for", "videos for",
    "log in", "sign up", "top 10", "jobs in", "best 20", "find out more",
    "wikipedia", "quora.com", "reddit.com"
]

def extract_phone_numbers(text: str) -> List[str]:
    """Extracts all matching Indian mobile numbers from raw text content."""
    if not text:
        return []
    
    found = []
    clean_text = re.sub(r'[☎☏✆📞📱📲✉]', ' ', text)
    
    for regex in INDIAN_MOBILE_REGEXES:
        matches = regex.finditer(clean_text)
        for m in matches:
            raw_match = m.group(0).strip()
            digits = re.sub(r'[^\d]', '', raw_match)
            if digits.startswith("91") and len(digits) == 12:
                digits = digits[2:]
            elif digits.startswith("0") and len(digits) == 11:
                digits = digits[1:]
            
            if len(digits) == 10 and digits[0] in {'6', '7', '8', '9'}:
                if len(set(digits)) > 2 and digits not in {"1234567890", "9876543210"}:
                    if digits not in found:
                        found.append(digits)
                    
    return found

def extract_emails(text: str) -> List[str]:
    """Extracts email addresses from text."""
    if not text:
        return []
    matches = EMAIL_REGEX.findall(text)
    filtered = [e.lower() for e in matches if not any(k in e.lower() for k in ["example.com", "schema.org", "sentry.io", "w3.org", "google.com", "linkedin.com"])]
    return list(set(filtered))


class LinkedInOsintScraper:
    def __init__(self, headless: bool = True):
        self.headless = headless
        self.headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.9,hi;q=0.8"
        }

    def scrape_by_role_and_city(
        self,
        role: str,
        industry_or_product: str,
        city: str,
        max_results: int = 10
    ) -> List[Dict[str, Any]]:
        """
        Executes X-Ray dorking targeting LinkedIn profiles for specific roles in a city.
        Uses duckduckgo-search library (primary) → Playwright Bing (fallback).
        """
        # Build targeted dork queries (flexible keywords for higher hit rate)
        role_kw = role.rstrip('s') if role.endswith('s') and not role.endswith('ss') else role
        dork_queries = [
            f'site:linkedin.com/in/ {role_kw} "{city}"',
            f'site:linkedin.com/in/ "{city}" (Fleet OR Transport OR Logistics OR "Commercial Vehicles" OR Director OR Owner)'
        ]

        all_results = []
        seen_urls = set()

        for query in dork_queries:
            if len(all_results) >= max_results:
                break
            logger.info(f"Running LinkedIn X-Ray Dork: {query}")
            leads = self._execute_search(query, max_results=max_results - len(all_results), require_linkedin=True)
            for lead in leads:
                url = lead.get("source_url", "")
                if url and url not in seen_urls:
                    seen_urls.add(url)
                    all_results.append(lead)

        logger.info(f"Discovered {len(all_results)} LinkedIn executive profiles for role '{role}' in {city}")
        return all_results

    def enrich_lead_contact(self, full_name: str, company_name: str, city: str = "") -> Tuple[str, str]:
        """
        Fast OSINT Waterfall enrichment to unmask mobile numbers via lightweight HTTP searches:
        1. Resume / CV Search: "Name" "Company" filetype:pdf / mobile
        2. Corporate Registry Search: "Name" "Company" director
        """
        if not full_name or any(k in full_name.lower() for k in FORBIDDEN_PERSONAS):
            return "", ""

        clean_name = re.sub(r'[\(\)\[\]]', '', full_name).strip()
        clean_company = re.sub(r'[\(\)\[\]]', '', company_name).strip() if company_name else ""

        waterfall_dorks = [
            f'"{clean_name}" "{clean_company}" (filetype:pdf OR resume OR "+91" OR mobile)',
            f'"{clean_name}" "{clean_company}" (site:zaubacorp.com OR site:tofler.in OR director)'
        ]

        for dork in waterfall_dorks:
            try:
                # Use lightweight DDG library only — NEVER launch heavy Playwright browsers in a waterfall loop
                results = self._search_via_ddgs_library(dork, max_results=2, require_linkedin=False)
                for res in results:
                    snippet = res.get("raw_snippet", "")
                    phones = extract_phone_numbers(snippet)
                    emails = extract_emails(snippet)
                    
                    found_phone = phones[0] if phones else ""
                    found_email = emails[0] if emails else ""

                    if found_phone or found_email:
                        logger.info(f"-> OSINT Waterfall Match for '{clean_name}': Phone={found_phone}, Email={found_email}")
                        return found_phone, found_email
            except Exception as e:
                logger.debug(f"Waterfall step error: {e}")
                continue

        return "", ""

    def _execute_search(self, query: str, max_results: int = 10, require_linkedin: bool = False) -> List[Dict[str, Any]]:
        """
        Executes search via duckduckgo-search library (primary), falling back to Playwright Bing if needed.
        """
        # Primary: duckduckgo-search Python library (fast, reliable HTTP)
        results = self._search_via_ddgs_library(query, max_results, require_linkedin)
        if results:
            logger.info(f"DDG library returned {len(results)} results")
            return results

        # Fallback 1: Playwright Bing search (Bing is much less prone to CAPTCHAs than Google)
        logger.info("DDG library returned 0, trying Bing browser fallback...")
        results = self._search_via_bing_browser(query, max_results, require_linkedin)
        if results:
            logger.info(f"Bing browser fallback returned {len(results)} results")
            return results

        return []

    def _search_via_ddgs_library(self, query: str, max_results: int, require_linkedin: bool) -> List[Dict[str, Any]]:
        """Primary search using the ddgs Python library — reliable, maintained, no selector issues."""
        results = []
        try:
            try:
                from ddgs import DDGS
            except ImportError:
                from duckduckgo_search import DDGS

            # Retry up to 3 times — DDG backend sometimes returns "Document is empty"
            ddg_results = []
            for attempt in range(3):
                try:
                    with DDGS() as ddgs:
                        ddg_results = list(ddgs.text(query, max_results=max_results + 5, region="in-en"))
                    if ddg_results:
                        break
                except Exception as retry_err:
                    logger.debug(f"DDG attempt {attempt + 1}/3 failed: {retry_err}")
                    if attempt < 2:
                        import time as _time
                        _time.sleep(1)
            
            logger.info(f"DDG library raw results: {len(ddg_results)}")

            for item in ddg_results:
                title = item.get("title", "")
                href = item.get("href", "") or item.get("link", "")
                snippet = item.get("body", "") or item.get("snippet", "")
                full_text = f"{title} {snippet}"

                # Filters
                if require_linkedin and "linkedin.com/in/" not in href:
                    continue

                if any(k in title.lower() for k in FORBIDDEN_PERSONAS):
                    continue

                phones = extract_phone_numbers(full_text)
                emails = extract_emails(full_text)
                phone = phones[0] if phones else ""
                email = emails[0] if emails else ""

                if require_linkedin:
                    parsed = self._parse_linkedin_title(title)
                    if any(k in parsed["name"].lower() for k in FORBIDDEN_PERSONAS):
                        continue
                    if not parsed["name"] or parsed["name"] == "Commercial Contact":
                        continue
                        
                    results.append({
                        "name": parsed["name"],
                        "role": parsed["role"],
                        "company": parsed["company"],
                        "phone": phone,
                        "email": email,
                        "source_url": href,
                        "raw_snippet": full_text[:300],
                        "category": "LinkedIn-OSINT"
                    })
                else:
                    results.append({
                        "title": title,
                        "phone": phone,
                        "email": email,
                        "source_url": href,
                        "raw_snippet": full_text
                    })

                if len(results) >= max_results:
                    break
        except ImportError:
            logger.warning("ddgs / duckduckgo-search package not installed. Install with: pip install ddgs")
        except Exception as e:
            logger.warning(f"DDG library search error: {e}")

        return results

    def _search_via_bing_browser(self, query: str, max_results: int, require_linkedin: bool) -> List[Dict[str, Any]]:
        """Playwright Bing search fallback — Bing is less aggressive with bot detection than Google."""
        try:
            from playwright.sync_api import sync_playwright
        except ImportError:
            return []

        results = []
        search_url = f"https://www.bing.com/search?q={quote_plus(query)}&count={max_results + 5}"

        try:
            with sync_playwright() as p:
                browser = p.chromium.launch(
                    headless=self.headless,
                    args=["--no-sandbox", "--disable-dev-shm-usage", "--disable-blink-features=AutomationControlled"]
                )
                context = browser.new_context(
                    user_agent=self.headers["User-Agent"],
                    viewport={"width": 1366, "height": 768},
                    locale="en-IN"
                )
                page = context.new_page()

                try:
                    page.goto(search_url, wait_until="domcontentloaded", timeout=15000)
                    page.wait_for_timeout(2000)
                except Exception:
                    pass

                # Bing search result selectors
                cards = page.locator('li.b_algo').all()
                logger.info(f"Bing browser found {len(cards)} result cards")

                for card in cards:
                    try:
                        text = card.inner_text()
                        lines = [l.strip() for l in text.split("\n") if l.strip()]
                        if not lines:
                            continue

                        link_elem = card.locator('a[href^="http"]').first
                        href = link_elem.get_attribute("href") if link_elem.count() > 0 else ""

                        if require_linkedin and "linkedin.com/in/" not in (href or ""):
                            continue

                        title_line = lines[0]
                        if any(k in title_line.lower() for k in FORBIDDEN_PERSONAS):
                            continue

                        full_text = " ".join(lines)
                        phones = extract_phone_numbers(full_text)
                        emails = extract_emails(full_text)
                        phone = phones[0] if phones else ""
                        email = emails[0] if emails else ""

                        if require_linkedin:
                            parsed = self._parse_linkedin_title(title_line)
                            if any(k in parsed["name"].lower() for k in FORBIDDEN_PERSONAS):
                                continue
                            if not parsed["name"] or parsed["name"] == "Commercial Contact":
                                continue

                            results.append({
                                "name": parsed["name"],
                                "role": parsed["role"],
                                "company": parsed["company"],
                                "phone": phone,
                                "email": email,
                                "source_url": href,
                                "raw_snippet": full_text[:300],
                                "category": "LinkedIn-OSINT"
                            })
                        else:
                            results.append({
                                "title": title_line,
                                "phone": phone,
                                "email": email,
                                "source_url": href,
                                "raw_snippet": full_text
                            })

                        if len(results) >= max_results:
                            break
                    except Exception:
                        continue

                browser.close()
        except Exception as ex:
            logger.debug(f"Bing browser search error: {ex}")

        return results

    def _search_via_google_browser(self, query: str, max_results: int, require_linkedin: bool) -> List[Dict[str, Any]]:
        """Browser-based Google search fallback with strict profile card parsing."""
        try:
            from playwright.sync_api import sync_playwright
        except ImportError:
            return []

        results = []
        search_url = f"https://www.google.com/search?q={quote_plus(query)}&hl=en&num={max_results + 5}"

        try:
            with sync_playwright() as p:
                browser = p.chromium.launch(
                    headless=self.headless,
                    args=["--no-sandbox", "--disable-dev-shm-usage", "--disable-blink-features=AutomationControlled"]
                )
                context = browser.new_context(
                    user_agent=self.headers["User-Agent"],
                    viewport={"width": 1366, "height": 768},
                    locale="en-IN"
                )
                page = context.new_page()

                try:
                    page.goto(search_url, wait_until="domcontentloaded", timeout=15000)
                    page.wait_for_timeout(1500)
                except Exception:
                    pass

                cards = page.locator('div.g').all()
                for card in cards:
                    try:
                        text = card.inner_text()
                        lines = [l.strip() for l in text.split("\n") if l.strip()]
                        if not lines:
                            continue

                        link_elem = card.locator('a[href^="http"]').first
                        href = link_elem.get_attribute("href") if link_elem.count() > 0 else ""

                        if require_linkedin and "linkedin.com/in/" not in href:
                            continue

                        title_line = lines[0]
                        if any(k in title_line.lower() for k in FORBIDDEN_PERSONAS):
                            continue

                        full_text = " ".join(lines)
                        phones = extract_phone_numbers(full_text)
                        emails = extract_emails(full_text)
                        phone = phones[0] if phones else ""
                        email = emails[0] if emails else ""

                        if require_linkedin:
                            parsed = self._parse_linkedin_title(title_line)
                            if any(k in parsed["name"].lower() for k in FORBIDDEN_PERSONAS):
                                continue

                            results.append({
                                "name": parsed["name"],
                                "role": parsed["role"],
                                "company": parsed["company"],
                                "phone": phone,
                                "email": email,
                                "source_url": href,
                                "raw_snippet": full_text[:300],
                                "category": "LinkedIn-OSINT"
                            })
                        else:
                            results.append({
                                "title": title_line,
                                "phone": phone,
                                "email": email,
                                "source_url": href,
                                "raw_snippet": full_text
                            })

                        if len(results) >= max_results:
                            break
                    except Exception:
                        continue

                browser.close()
        except Exception as ex:
            logger.debug(f"Google browser search fallback error: {ex}")

        return results

    def _parse_linkedin_title(self, title: str) -> Dict[str, str]:
        """
        Parses LinkedIn title strings into Name, Role, and Company.
        Format examples:
          'Ramesh Patel - Managing Director - Patel Roadlines Pvt Ltd | LinkedIn'
          'Suresh Gupta - Fleet Manager - VRL Logistics | LinkedIn'
          'Pooja Mehta - Owner - Gold Gym Pune | LinkedIn'
        """
        clean_title = re.sub(r'\s*\|\s*LinkedIn.*$', '', title, flags=re.IGNORECASE)
        clean_title = re.sub(r'https?://\S+', '', clean_title).strip()
        parts = [p.strip() for p in re.split(r'[\-\|\–\—]', clean_title) if p.strip()]

        name = parts[0] if len(parts) > 0 else "Commercial Contact"
        role = parts[1] if len(parts) > 1 else ""
        company = parts[2] if len(parts) > 2 else ""

        if " at " in role:
            role_sub = role.split(" at ")
            role = role_sub[0].strip()
            if not company:
                company = role_sub[1].strip()

        return {
            "name": name,
            "role": role,
            "company": company
        }
