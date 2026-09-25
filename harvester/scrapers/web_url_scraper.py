"""
Universal Web URL Scraper for Commercial Transport & Directory Lead Harvesting.
Accepts ANY category, directory, or single listing URL.
Features:
- Heuristic and pattern-based pagination (with or without dedicated Next buttons)
- Deep listing detail page extraction
- Contact person, phone, email, address, coordinates, website, and services extraction
- Lead validation & deduplication gatekeeper
"""

import re
import time
import logging
import threading
from typing import List, Dict, Any, Optional, Set, Tuple
from urllib.parse import urljoin, urlparse, parse_qs, urlencode, urlunparse
import urllib.request
import urllib.error
from bs4 import BeautifulSoup

from validator import LeadValidator

logger = logging.getLogger(__name__)

# Default realistic browser headers to prevent 403/406 blocking
DEFAULT_HEADERS = {
    "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9",
    "Accept-Encoding": "identity",
    "Connection": "keep-alive",
    "Upgrade-Insecure-Requests": "1"
}

INDIAN_STATES = [
    "Andhra Pradesh", "Arunachal Pradesh", "Assam", "Bihar", "Chhattisgarh",
    "Goa", "Gujarat", "Haryana", "Himachal Pradesh", "Jharkhand",
    "Karnataka", "Kerala", "Madhya Pradesh", "Maharashtra", "Manipur",
    "Meghalaya", "Mizoram", "Nagaland", "Odisha", "Punjab",
    "Rajasthan", "Sikkim", "Tamil Nadu", "Telangana", "Tripura",
    "Uttar Pradesh", "Uttarakhand", "West Bengal", "Delhi", "Chandigarh",
    "Puducherry", "Jammu and Kashmir", "Ladakh"
]

CITY_TO_STATE = {
    "pune": "Maharashtra", "mumbai": "Maharashtra", "bhiwandi": "Maharashtra",
    "thane": "Maharashtra", "navi mumbai": "Maharashtra", "nagpur": "Maharashtra",
    "nashik": "Maharashtra", "aurangabad": "Maharashtra", "solapur": "Maharashtra",
    "kolhapur": "Maharashtra", "panvel": "Maharashtra", "satara": "Maharashtra",
    "belgaum": "Karnataka", "belagavi": "Karnataka", "bangalore": "Karnataka",
    "bengaluru": "Karnataka", "hubli": "Karnataka", "hubballi": "Karnataka",
    "dharwad": "Karnataka", "mangalore": "Karnataka", "mangaluru": "Karnataka",
    "mysore": "Karnataka", "mysuru": "Karnataka", "gulbarga": "Karnataka",
    "ahmedabad": "Gujarat", "surat": "Gujarat", "vadodara": "Gujarat",
    "baroda": "Gujarat", "rajkot": "Gujarat", "vapi": "Gujarat", "ankleshwar": "Gujarat",
    "hyderabad": "Telangana", "secunderabad": "Telangana", "visakhapatnam": "Andhra Pradesh",
    "vijayawada": "Andhra Pradesh", "chennai": "Tamil Nadu", "coimbatore": "Tamil Nadu",
    "madurai": "Tamil Nadu", "salem": "Tamil Nadu", "hosur": "Tamil Nadu",
    "delhi": "Delhi", "new delhi": "Delhi", "noida": "Uttar Pradesh",
    "greater noida": "Uttar Pradesh", "lucknow": "Uttar Pradesh", "kanpur": "Uttar Pradesh",
    "faridabad": "Haryana", "gurugram": "Haryana", "gurgaon": "Haryana",
    "kolkata": "West Bengal", "howrah": "West Bengal", "jaipur": "Rajasthan",
    "jodhpur": "Rajasthan", "indore": "Madhya Pradesh", "bhopal": "Madhya Pradesh",
    "chandigarh": "Chandigarh", "ludhiana": "Punjab", "kochi": "Kerala"
}

class WebUrlScraper:
    """Universal web scraper capable of extracting leads from any transport/directory web URL."""

    def __init__(self, timeout: int = 30, delay_between_requests: float = 0.3):
        self.timeout = timeout
        self.delay = delay_between_requests
        self.headers = DEFAULT_HEADERS.copy()
        self._local = threading.local()
        # Seed known anti-bot bypass cookies (e.g. transportfamily.com / BitNinja challenge)
        self._known_cookies: Dict[str, str] = {
            "humans_21909": "1"
        }

    def _get_session(self) -> Any:
        """Returns a thread-local requests.Session configured with keep-alive and anti-bot cookies."""
        if not hasattr(self._local, "session"):
            try:
                import requests
                import urllib3
                urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)
                s = requests.Session()
                req_headers = self.headers.copy()
                req_headers.pop("Accept-Encoding", None)
                s.headers.update(req_headers)
                for c_name, c_val in self._known_cookies.items():
                    s.cookies.set(c_name, c_val)
                self._local.session = s
            except ImportError:
                self._local.session = None
        return getattr(self._local, "session", None)

    def _extract_cookie_from_html(self, html: str) -> Optional[Tuple[str, str]]:
        """Detects and extracts document.cookie assignments from anti-bot challenge scripts."""
        if not html or "document.cookie" not in html:
            return None
        m = re.search(r'document\.cookie\s*=\s*["\']([^=;\s]+)=([^;\'"\s]+)', html)
        if m:
            return (m.group(1).strip(), m.group(2).strip())
        return None

    def fetch_html(self, url: str, retries: int = 4) -> Optional[str]:
        """
        Fetches page content with realistic headers, anti-bot challenge auto-bypass,
        rate-limit handling (409/429/503), error handling, and exponential backoff.
        """
        session = self._get_session()

        for attempt in range(1, retries + 1):
            # 1. Try requests library with thread-local Session
            if session is not None:
                try:
                    if "transportfamily.com" in url:
                        session.cookies.set("humans_21909", "1")

                    resp = session.get(url, timeout=self.timeout, verify=False)

                    # Handle anti-bot JavaScript / 409 Conflict challenge
                    if resp.status_code == 409 or (resp.status_code == 200 and "document.cookie" in resp.text and len(resp.text) < 1000):
                        cookie_pair = self._extract_cookie_from_html(resp.text)
                        if cookie_pair:
                            c_name, c_val = cookie_pair
                            logger.info(f"Detected anti-bot challenge cookie: {c_name}={c_val}. Applying to session and retrying...")
                            session.cookies.set(c_name, c_val)
                            self._known_cookies[c_name] = c_val
                            time.sleep(0.5)
                            resp = session.get(url, timeout=self.timeout, verify=False)

                    if resp.status_code == 200:
                        return resp.text
                    elif resp.status_code == 404:
                        logger.info(f"Page returned HTTP 404 (Not Found): {url}")
                        return None
                    elif resp.status_code in (409, 429, 502, 503, 504):
                        retry_after = resp.headers.get("Retry-After")
                        wait_sec = min(10.0, float(retry_after)) if retry_after and retry_after.isdigit() else (attempt * 2.0)
                        logger.warning(f"HTTP {resp.status_code} received from {url} on attempt {attempt}/{retries}. Backing off {wait_sec:.1f}s...")
                        time.sleep(wait_sec)
                        continue
                    elif resp.status_code == 403:
                        logger.warning(f"HTTP 403 Forbidden on attempt {attempt}/{retries} for {url}. Waiting {attempt * 2.0:.1f}s...")
                        time.sleep(attempt * 2.0)
                        continue
                    else:
                        logger.warning(f"HTTP {resp.status_code} from {url} on attempt {attempt}/{retries}")
                except Exception as req_ex:
                    logger.warning(f"Requests fetch failed on attempt {attempt}/{retries} for {url}: {req_ex}")
                    if attempt < retries:
                        time.sleep(attempt * 2.0)
                        continue

            # 2. Fallback to urllib with unverified SSL context
            try:
                import ssl
                ctx = ssl._create_unverified_context()
                headers = self.headers.copy()
                if "transportfamily.com" in url or self._known_cookies:
                    cookie_str = "; ".join(f"{k}={v}" for k, v in self._known_cookies.items())
                    headers["Cookie"] = cookie_str

                req = urllib.request.Request(url, headers=headers)
                with urllib.request.urlopen(req, timeout=self.timeout, context=ctx) as resp:
                    charset = resp.headers.get_content_charset() or "utf-8"
                    content = resp.read().decode(charset, errors="ignore")
                    if "document.cookie" in content and len(content) < 1000:
                        cookie_pair = self._extract_cookie_from_html(content)
                        if cookie_pair:
                            c_name, c_val = cookie_pair
                            self._known_cookies[c_name] = c_val
                            time.sleep(0.5)
                            continue
                    return content
            except urllib.error.HTTPError as http_err:
                if http_err.code == 404:
                    logger.info(f"Urllib HTTP 404 (Not Found): {url}")
                    return None
                elif http_err.code in (409, 429, 502, 503, 504):
                    backoff = attempt * 2.0
                    logger.warning(f"Urllib HTTP {http_err.code} on attempt {attempt}/{retries} for {url}. Retrying in {backoff:.1f}s...")
                    time.sleep(backoff)
                else:
                    logger.warning(f"Urllib HTTP {http_err.code} for {url}: {http_err.reason}")
            except Exception as ex:
                if attempt < retries:
                    backoff = attempt * 2.0
                    logger.warning(f"Urllib fetch attempt {attempt}/{retries} failed for {url}: {ex}. Retrying in {backoff:.1f}s...")
                    time.sleep(backoff)
                else:
                    logger.error(f"Failed to fetch {url} after {retries} attempts: {ex}")
                    return None

        return None

    def is_single_listing_url(self, url: str) -> bool:
        """Determines if the URL points directly to an individual listing rather than a directory/category list."""
        clean = url.lower()
        if "/listing-category/" in clean or "/category/" in clean or "/categories/" in clean or "/search" in clean:
            return False
        # Specific directory pattern e.g. /listing/<company-slug>/
        if re.search(r'/listing/[a-z0-9\-]+/?$', clean):
            return True
        return False

    def extract_listing_urls_from_page(self, html: str, base_url: str) -> List[str]:
        """Extracts listing detail URLs from any generic directory, category, or listing page."""
        soup = BeautifulSoup(html, "html.parser")
        found_urls = []

        # Common URL segments that represent directory listings
        listing_patterns = [
            "/listing/", "/company/", "/transporter/", "/supplier/",
            "/dealer/", "/profile/", "/member/", "/firm/", "/business/",
            "/item/", "/details/", "/detail/", "/vendor/", "/directory/"
        ]

        # 1. Look for anchor tags matching listing patterns
        for a in soup.find_all("a", href=True):
            href = a["href"].strip()
            full_url = urljoin(base_url, href)
            parsed = urlparse(full_url)
            clean_path = parsed.path.rstrip("/")

            # Ignore category, tag, author, page navigation, asset, or auth links
            if any(x in clean_path for x in [
                "/listing-category/", "/category/", "/tag/", "/author/",
                "/submit-listing", "/pricing-plan", "/banner-advertisement",
                "/website-designing", "/contact", "/about-us", "/wp-login",
                "/feed", "/comments", "/privacy-policy", "/terms", "/search",
                "/categories/", "/tags/", "/page/", "/paged/"
            ]):
                continue

            # Check if this matches any known listing pattern
            if any(pat in clean_path for pat in listing_patterns):
                normalized = f"{parsed.scheme}://{parsed.netloc}{clean_path}/"
                if normalized not in found_urls and len(clean_path.split('/')) >= 2:
                    found_urls.append(normalized)

        # 2. Heuristic fallback: look for repeated directory cards / article tags
        if not found_urls:
            card_selectors = [
                ".lp-grid-box", ".listing-card", ".directory-item",
                ".card-title", ".business-card", "article.listing", "article",
                ".result-item", ".vendor-card", ".company-box",
                "div[class*='listing']", "div[class*='company']",
                "div[class*='business']", "div[class*='vendor']"
            ]
            for sel in card_selectors:
                for card in soup.select(sel):
                    a_elem = card.find("a", href=True)
                    if a_elem:
                        full = urljoin(base_url, a_elem["href"].strip())
                        p = urlparse(full)
                        clean = p.path.rstrip("/")
                        if p.netloc and full not in found_urls and len(clean) > 3:
                            if not any(ign in clean for ign in ["/category/", "/author/", "/tag/", "/page/"]):
                                found_urls.append(full)

        return found_urls

    def detect_pagination_and_badges(self, current_url: str, soup: BeautifulSoup, page_num: int) -> Dict[str, Any]:
        """
        Auto-detects pagination indicators, total pages badge, and next page link.
        Supports:
        - Badge elements: e.g. <span class="badge">Page 1 of 42</span>, <div class="badge">
        - Pagination containers: .pagination, .page-numbers, ul.pagination, nav[role="navigation"]
        - Numeric page badges / pills
        - Dedicated Next links (rel="next", class="next", text "Next »", "›", "»")
        """
        badge_text = None
        total_pages_detected = None
        next_page_url = None
        pagination_type = "none"

        page_text = soup.get_text(" ", strip=True)

        # 1. Search for text badge patterns: "Page X of Y", "Showing 1-20 of 350", "X of Y pages"
        m_of = re.search(r'(?:Page|Badge)\s+(\d+)\s+(?:of|/)\s+(\d+)', page_text, re.IGNORECASE)
        if m_of:
            badge_text = f"Page {m_of.group(1)} of {m_of.group(2)}"
            total_pages_detected = int(m_of.group(2))
            pagination_type = "badge_indicator"

        if not total_pages_detected:
            m_showing = re.search(r'Showing\s+\d+\s*[-–]\s*(\d+)\s+of\s+(\d+)', page_text, re.IGNORECASE)
            if m_showing:
                per_page = int(m_showing.group(1))
                total_items = int(m_showing.group(2))
                if per_page > 0:
                    total_pages_detected = (total_items + per_page - 1) // per_page
                    badge_text = f"Showing up to {total_items} items (~{total_pages_detected} pages)"
                    pagination_type = "items_count"

        # 2. Check pagination container elements for numbers & badges
        page_nums_found: List[int] = []
        pag_containers = soup.select(".pagination, .page-numbers, .pager, ul.pagination, nav[aria-label*='pagination'], nav.pagination")
        for container in pag_containers:
            # Check for badges inside pagination
            badge_el = container.find(class_=re.compile(r'\bbadge\b', re.I))
            if badge_el:
                badge_text = badge_el.get_text().strip()

            for elem in container.find_all(["a", "span", "li"]):
                txt = elem.get_text().strip()
                if txt.isdigit():
                    page_nums_found.append(int(txt))

        if page_nums_found:
            max_num = max(page_nums_found)
            if not total_pages_detected or max_num > total_pages_detected:
                total_pages_detected = max_num
                pagination_type = "numeric_badges"
            if not badge_text:
                badge_text = f"Page {page_num} of {total_pages_detected} (Badges: {min(page_nums_found)}..{max_num})"

        # 3. Strategy A: Dedicated Next Link in HTML
        next_tag = (
            soup.find("a", attrs={"rel": "next"})
            or soup.find("a", class_=re.compile(r'\b(next|next-page|pagination-next|page-link-next)\b', re.I))
            or soup.find("a", attrs={"aria-label": re.compile(r'\bnext\b', re.I)})
            or soup.find("li", class_=re.compile(r'\bnext\b', re.I))
        )
        if next_tag:
            a_elem = next_tag if next_tag.name == "a" else next_tag.find("a", href=True)
            if a_elem and a_elem.get("href"):
                href = a_elem["href"].strip()
                if href and not href.startswith("javascript") and href != "#":
                    next_page_url = urljoin(current_url, href)

        # Look for explicit Next anchor text
        if not next_page_url:
            for a in soup.find_all("a", href=True):
                txt = a.get_text().strip()
                if txt in ["Next", "next", "Next »", "»", "Next Page", ">", "›", "Next →"]:
                    href = a["href"].strip()
                    if href and not href.startswith("javascript") and href != "#":
                        next_page_url = urljoin(current_url, href)
                        break

        # Strategy B: Numeric pagination target page (page_num + 1)
        target_page = page_num + 1
        if not next_page_url:
            data_page_elem = soup.find(attrs={"data-pageurl": str(target_page)}) or soup.find(attrs={"data-page": str(target_page)})
            if data_page_elem:
                if data_page_elem.name == "a" and data_page_elem.get("href"):
                    next_page_url = urljoin(current_url, data_page_elem["href"])

        if not next_page_url:
            for a in soup.find_all("a", href=True):
                href = a["href"].strip()
                if re.search(rf'/(?:page|paged)/{target_page}/?$', href) or re.search(rf'[?&](?:page|paged)={target_page}(?:&|$)', href):
                    next_page_url = urljoin(current_url, href)
                    break

        # Strategy C: Algorithmic URL Pattern Progression
        if not next_page_url:
            parsed = urlparse(current_url)
            path = parsed.path
            query = parsed.query
            qs = parse_qs(query)

            if re.search(r'/page/\d+/?', path):
                new_path = re.sub(r'/page/\d+/?', f'/page/{target_page}/', path)
                next_page_url = urlunparse(parsed._replace(path=new_path))
            elif "paged" in qs:
                qs["paged"] = [str(target_page)]
                next_page_url = urlunparse(parsed._replace(query=urlencode(qs, doseq=True)))
            elif "page" in qs:
                qs["page"] = [str(target_page)]
                next_page_url = urlunparse(parsed._replace(query=urlencode(qs, doseq=True)))
            else:
                clean_path = path if path.endswith("/") else path + "/"
                new_path = f"{clean_path}page/{target_page}/"
                next_page_url = urlunparse(parsed._replace(path=new_path))

        has_next = bool(next_page_url and next_page_url != current_url)
        if total_pages_detected and page_num >= total_pages_detected:
            has_next = False

        return {
            "has_badge_detected": bool(badge_text or total_pages_detected),
            "badge_text": badge_text or f"Page {page_num}",
            "current_badge_page": page_num,
            "total_badge_pages": total_pages_detected or (page_num + 1 if has_next else page_num),
            "next_badge_url": next_page_url if has_next else None,
            "current_page": page_num,
            "total_pages_detected": total_pages_detected or (page_num + 1 if has_next else page_num),
            "has_next_page": has_next,
            "next_page_url": next_page_url if has_next else None,
            "pagination_type": pagination_type
        }

    def get_next_page_url(self, current_url: str, soup: BeautifulSoup, page_num: int) -> Optional[str]:
        """Returns the next page URL using auto-detection of badges and indicators."""
        meta = self.detect_pagination_and_badges(current_url, soup, page_num)
        return meta.get("next_page_url")

    def extract_leads_directly_from_page(self, html: str, base_url: str, fallback_category: str = "Commercial Lead") -> List[Dict[str, Any]]:
        """
        Fast on-page card extractor: detects directory cards that already display contact details
        (company name, phone number, address) directly without requiring separate detail page requests.
        """
        soup = BeautifulSoup(html, "html.parser")
        direct_leads: List[Dict[str, Any]] = []

        card_selectors = [
            ".lp-grid-box", ".listing-card", ".directory-item",
            ".business-card", "article.listing", "article",
            ".result-item", ".vendor-card", ".company-box",
            "div[class*='listing']", "div[class*='company']"
        ]

        seen_companies = set()
        for sel in card_selectors:
            cards = soup.select(sel)
            if not cards:
                continue

            for card in cards:
                # Find company name
                name_elem = card.find(["h2", "h3", "h4", ".card-title", ".title", "a"])
                if not name_elem:
                    continue
                name = name_elem.get_text().strip()
                name = re.sub(r'^(?:Ad|Claimed|Verified)\s*', '', name, flags=re.I).strip()
                if len(name) < 3 or name.lower() in seen_companies:
                    continue

                # Check for phone number on card
                phone = ""
                tel_a = card.find("a", href=re.compile(r'^tel:', re.I))
                if tel_a:
                    clean_tel = LeadValidator.clean_phone(tel_a["href"].replace("tel:", ""))
                    if len(clean_tel) == 10 and clean_tel[0] in {'6', '7', '8', '9'}:
                        phone = clean_tel

                if not phone:
                    text = card.get_text(" ", strip=True)
                    m = re.search(r'(?:\+?91[\-\s]?)?([6-9]\d{9})\b', text)
                    if m:
                        phone = m.group(1)

                if not phone:
                    continue  # Only keep direct cards if they have valid phone numbers

                seen_companies.add(name.lower())

                # Find address / city
                card_text = card.get_text(" ", strip=True)
                city = ""
                state = ""
                for c_name, s_name in CITY_TO_STATE.items():
                    if re.search(rf'\b{re.escape(c_name)}\b', card_text, re.I):
                        city = c_name.title()
                        state = s_name
                        break

                # Detail URL
                card_a = card.find("a", href=True)
                source_url = urljoin(base_url, card_a["href"]) if card_a else base_url

                direct_leads.append({
                    "companyName": name,
                    "fullName": name,
                    "mobileNo": phone,
                    "mobileNo2": "",
                    "emailIds": "",
                    "address": f"{name}, {city or 'Commercial Hub'}, India",
                    "city": city or "Commercial Hub",
                    "state": state or "India",
                    "location": "",
                    "website": "",
                    "sourceUrl": source_url,
                    "qualityScore": 0.88,
                    "leadSourceChannel": "Web-Scraper",
                    "leadSourceType": "Automated",
                    "contactType": "Lead",
                    "tags": f"Web-Harvested, DirectCard, {fallback_category}",
                    "notes": f"Extracted directly from directory listing card on {base_url}",
                    "snippet": f"Verified directory listing • {name}"
                })

        return direct_leads

    def scrape_listing_urls(self, start_url: str, max_results: int = 20) -> List[str]:
        """
        Discovers listing URLs across multiple pages until max_results is reached
        or pagination ends.
        """
        if self.is_single_listing_url(start_url):
            logger.info(f"Direct single listing URL detected: {start_url}")
            return [start_url]

        logger.info(f"Starting multi-page listing URL extraction from: {start_url} (limit: {max_results})")
        collected_urls: List[str] = []
        visited_pages: Set[str] = set()
        current_url = start_url
        page_num = 1
        max_pages = 25  # Safety threshold

        while current_url and len(collected_urls) < max_results and page_num <= max_pages:
            if current_url in visited_pages:
                logger.debug(f"Already visited page {current_url}. Stopping pagination.")
                break

            visited_pages.add(current_url)
            logger.info(f"[Page {page_num}] Fetching directory list: {current_url}")
            html = self.fetch_html(current_url)
            if not html:
                logger.warning(f"Could not load HTML from {current_url}. Stopping pagination.")
                break

            page_urls = self.extract_listing_urls_from_page(html, current_url)
            new_urls = [u for u in page_urls if u not in collected_urls]
            logger.info(f"[Page {page_num}] Found {len(page_urls)} listing URLs ({len(new_urls)} new).")

            if not new_urls:
                logger.info("No new listing URLs found on page. Reached end of listings.")
                break

            for u in new_urls:
                if len(collected_urls) < max_results:
                    collected_urls.append(u)

            if len(collected_urls) >= max_results:
                break

            # Find next page URL (Next button or pattern-based pagination)
            soup = BeautifulSoup(html, "html.parser")
            next_url = self.get_next_page_url(current_url, soup, page_num)
            if not next_url or next_url == current_url or next_url in visited_pages:
                logger.info(f"No further page link available after page {page_num}.")
                break

            current_url = next_url
            page_num += 1
            if self.delay > 0:
                time.sleep(self.delay)

        logger.info(f"Multi-page URL collection complete. Total unique listing URLs: {len(collected_urls)}")
        return collected_urls[:max_results]

    def extract_lead_details(self, detail_url: str, fallback_category: str = "Commercial Fleet / Transporter") -> Optional[Dict[str, Any]]:
        """
        Loads a listing detail page and extracts all CRM fields:
        Company Name, Contact Person, Mobile No, Secondary Phone, Email, Address,
        City, State, GPS Coordinates, Website, and Fleet/Services snippet.
        """
        html = self.fetch_html(detail_url)
        if not html:
            return None

        soup = BeautifulSoup(html, "html.parser")

        # 1. Company Name
        h1 = soup.find("h1")
        raw_company = h1.get_text().strip() if h1 else ""
        if not raw_company:
            title_tag = soup.find("title")
            raw_company = title_tag.get_text().strip() if title_tag else ""

        # Clean badges like "Claimed", "Ad", "Verified", "- Transport Times"
        company_name = re.sub(r'^(?:Ad|Claimed|Verified)\s*', '', raw_company, flags=re.IGNORECASE)
        company_name = re.sub(r'\s*(?:Claimed|Verified|Ad)\s*$', '', company_name, flags=re.IGNORECASE)
        company_name = re.sub(r'\s*[-–—|]\s*(?:Transport Times|TruckSuvidha|Trucoi).*$', '', company_name, flags=re.IGNORECASE).strip()

        # 2. Contact Person
        contact_person = ""
        detail_content = soup.find("div", class_="post-detail-content") or soup.find("div", class_="entry-content")
        content_text = detail_content.get_text(" ", strip=True) if detail_content else soup.get_text(" ", strip=True)

        cp_match = re.search(r'(?:Contact Person|Proprietor|Owner|Director|Manager|Contact)\s*:\s*([^|\n\r,;]+)', content_text, re.IGNORECASE)
        if cp_match:
            candidate = cp_match.group(1).strip()
            # Clean common parenthetical notes
            candidate = re.sub(r'\((?:Owner|Proprietor|Director|Partner|Manager)\)', '', candidate, flags=re.IGNORECASE).strip()
            if 3 <= len(candidate) <= 40 and not any(w in candidate.lower() for w in ["phone", "email", "address", "service"]):
                contact_person = candidate

        # 3. Phone Numbers (Primary & Secondary)
        phones: List[str] = []

        # A: Check for tel: links
        for a in soup.find_all("a", href=re.compile(r'^tel:', re.I)):
            href_tel = a["href"].replace("tel:", "").strip()
            for part in re.split(r'[,;/|\s]+', href_tel):
                cleaned = LeadValidator.clean_phone(part)
                if len(cleaned) == 10 and cleaned[0] in {'6', '7', '8', '9'}:
                    if cleaned not in phones:
                        phones.append(cleaned)

        # B: Check specific listing-phone element
        phone_li = soup.find("li", class_=re.compile(r'lp-listing-phone|phone', re.I))
        if phone_li:
            for part in re.split(r'[,;/|\s]+', phone_li.get_text()):
                cleaned = LeadValidator.clean_phone(part)
                if len(cleaned) == 10 and cleaned[0] in {'6', '7', '8', '9'}:
                    if cleaned not in phones:
                        phones.append(cleaned)

        # C: Regex phone matching in body text
        if not phones:
            phone_matches = re.findall(r'(?:\+?91[\-\s]?)?([6-9]\d{9})\b', html)
            for m in phone_matches:
                cleaned = LeadValidator.clean_phone(m)
                if len(cleaned) == 10 and cleaned not in phones:
                    phones.append(cleaned)

        primary_mobile = phones[0] if phones else ""
        secondary_mobile = phones[1] if len(phones) > 1 else ""

        # 4. Email IDs
        email_ids = ""
        for a in soup.find_all("a", href=re.compile(r'^mailto:', re.I)):
            mail = a["href"].replace("mailto:", "").split("?")[0].strip()
            if "@" in mail and not any(x in mail.lower() for x in ["example", "domain", "user"]):
                email_ids = mail
                break

        if not email_ids:
            found_emails = re.findall(r'\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b', html)
            clean_emails = [e for e in found_emails if not any(x in e.lower() for x in [
                "example.com", "sentry", "w.org", "schema.org", "wp.com", "theme", ".png", ".jpg",
                "website.com", "yourname", "domain.com", "test.com", "sitekit", "user@email",
                "sample.com", "mycompany", "transportfamily.com", "trucksuvidha.com", "trucoi.com",
                "johndoe", "mail.com", "test@"
            ])]
            if clean_emails:
                email_ids = clean_emails[0]

        # 5. Address, City, State
        address = ""
        addr_icon = soup.find("i", class_=re.compile(r'fa-(?:location-dot|map-marker)', re.I))
        if addr_icon and addr_icon.parent:
            address = addr_icon.parent.get_text(" ", strip=True)

        if not address:
            detail_infos = soup.find("div", class_="listing-detail-infos")
            if detail_infos:
                first_li = detail_infos.find("li")
                if first_li:
                    address = first_li.get_text(" ", strip=True)

        if not address:
            # Look for address element or schema itemprop="address"
            addr_elem = soup.find(attrs={"itemprop": "address"}) or soup.find("address")
            if addr_elem:
                address = addr_elem.get_text(" ", strip=True)

        # Resolve City & State
        city = ""
        state = ""
        text_to_scan = f"{address} {company_name} {detail_url}".lower()

        for c_name, s_name in CITY_TO_STATE.items():
            if re.search(rf'\b{re.escape(c_name)}\b', text_to_scan):
                city = c_name.title()
                state = s_name
                break

        if not state:
            for s in INDIAN_STATES:
                if re.search(rf'\b{re.escape(s)}\b', text_to_scan, re.IGNORECASE):
                    state = s
                    break

        if not city:
            # Check breadcrumbs for city
            crumbs = soup.find(class_=re.compile(r'breadcrumbs|breadcrumb', re.I))
            if crumbs:
                crumb_text = crumbs.get_text(" ", strip=True)
                for c_name, s_name in CITY_TO_STATE.items():
                    if re.search(rf'\b{re.escape(c_name)}\b', crumb_text, re.IGNORECASE):
                        city = c_name.title()
                        state = s_name
                        break

        # Fallback address format
        if not address:
            address = f"{company_name}, {city or 'Commercial Hub'}, India"

        # 6. GPS Coordinates (Location)
        location = ""
        map_el = soup.find(attrs={"data-lat": True})
        if map_el:
            lat = map_el.get("data-lat", "").strip()
            lan = (map_el.get("data-lan") or map_el.get("data-lng") or "").strip()
            if lat and lan:
                location = f"{lat}, {lan}"

        if not location:
            # Check Google Maps search/directions links
            map_a = soup.find("a", href=re.compile(r'google\.com/maps.*query=(-?\d+\.\d+),(-?\d+\.\d+)', re.I))
            if map_a:
                m = re.search(r'query=(-?\d+\.\d+),(-?\d+\.\d+)', map_a["href"])
                if m:
                    location = f"{m.group(1)}, {m.group(2)}"

        # 7. Website
        website = ""
        web_li = soup.find("li", class_=re.compile(r'lp-user-web|website', re.I))
        if web_li:
            web_a = web_li.find("a", href=True)
            if web_a and web_a.get("href"):
                website = web_a["href"].strip()

        # 8. Services / Features / Notes
        notes_parts = []
        if detail_content:
            text = detail_content.get_text(" ", strip=True)
            # Remove redundant labels
            cleaned_text = re.sub(r'Contact Person:\s*[^|]+\|?', '', text, flags=re.IGNORECASE)
            cleaned_text = re.sub(r'Phone No:\s*[^|]+\|?', '', cleaned_text, flags=re.IGNORECASE).strip()
            if cleaned_text:
                notes_parts.append(cleaned_text[:300])

        features_box = soup.find("div", class_=re.compile(r'post-feature-box|features', re.I))
        if features_box:
            feats = [li.get_text(" ", strip=True) for li in features_box.find_all("li")]
            if feats:
                notes_parts.append("Features: " + ", ".join(feats[:6]))

        notes = " | ".join(notes_parts) if notes_parts else f"Commercial logistics entity in {city or 'India'}"

        # 9. Quality Score Calculation
        score = 0.85
        if primary_mobile: score += 0.08
        if address and len(address) > 15: score += 0.03
        if location: score += 0.02
        if website: score += 0.01
        score = min(0.99, score)

        lead = {
            "companyName": company_name or "Commercial Lead",
            "fullName": contact_person or company_name or "Commercial Lead",
            "mobileNo": primary_mobile,
            "mobileNo2": secondary_mobile,
            "emailIds": email_ids,
            "address": address,
            "city": city or "Commercial Hub",
            "state": state or "India",
            "location": location,
            "website": website,
            "sourceUrl": detail_url,
            "qualityScore": round(score, 2),
            "leadSourceChannel": "TransportFamily" if "transportfamily.com" in detail_url else "Web-Scraper",
            "leadSourceType": "Automated",
            "contactType": "Lead",
            "tags": f"Web-Harvested, {fallback_category}, {city or 'Commercial Hub'}",
            "notes": notes,
            "snippet": notes[:250]
        }

        return lead

    def scrape(
        self,
        url: str,
        limit: int = 20,
        division: str = "Tyresoles",
        product: str = "Commercial Retreading"
    ) -> List[Dict[str, Any]]:
        """
        Executes end-to-end extraction from any category or detail URL:
        1. Multi-page listing URL extraction
        2. Detail loading & field parsing
        3. Anti-fake validation
        4. Deduplication
        """
        logger.info(f"Starting Web URL Scraper for: {url} (limit: {limit})")
        listing_urls = self.scrape_listing_urls(url, max_results=limit)
        if not listing_urls:
            logger.warning(f"No listing URLs could be extracted from: {url}")
            return []

        raw_leads: List[Dict[str, Any]] = []
        for idx, item_url in enumerate(listing_urls, 1):
            logger.info(f"[{idx}/{len(listing_urls)}] Loading details: {item_url}")
            try:
                lead = self.extract_lead_details(item_url, fallback_category=product)
                if lead:
                    lead["division"] = division
                    lead["targetProduct"] = product
                    raw_leads.append(lead)
            except Exception as ex:
                logger.error(f"Error parsing detail page {item_url}: {ex}")

            if self.delay > 0 and idx < len(listing_urls):
                time.sleep(self.delay)

        # Strict Anti-Mock Validation & Deduplication Gatekeeper
        valid_leads: List[Dict[str, Any]] = []
        for l in raw_leads:
            is_valid, reason = LeadValidator.validate_lead(l, allow_no_phone=False)
            if is_valid:
                valid_leads.append(l)
            else:
                logger.info(f"Skipped invalid lead '{l.get('companyName')}': {reason}")

        unique_leads = LeadValidator.deduplicate(valid_leads)
        logger.info(f"Web URL Scraper complete: {len(listing_urls)} URLs loaded, {len(valid_leads)} valid, {len(unique_leads)} unique genuine leads.")
        return unique_leads[:limit]
