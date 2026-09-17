"""
Process 1 (Producer): Fast Web Crawler & Stager
Fetches directory listings starting from the last checkpoint, extracts raw detail fields,
and stores them into the persistent staging buffer with status PENDING.
"""

import os
import sys
import time
import logging
from typing import Dict, Any, List, Optional
from urllib.parse import urlparse, urlunparse, parse_qs, urlencode

from staging_store import StagingStore
from scrapers.web_url_scraper import WebUrlScraper

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("FetcherProcess")


class FetcherProcess:
    def __init__(self, store: Optional[StagingStore] = None, delay_between_requests: float = 0.3):
        self.store = store or StagingStore()
        self.scraper = WebUrlScraper(delay_between_requests=delay_between_requests)

    def _build_page_url(self, base_url: str, page_num: int) -> str:
        """Constructs the exact URL for a specific page number."""
        if page_num <= 1:
            return base_url.strip()

        parsed = urlparse(base_url.strip())
        path = parsed.path
        qs = parse_qs(parsed.query)

        # Query param pagination (?page=n or ?paged=n)
        if "paged" in qs:
            qs["paged"] = [str(page_num)]
            return urlunparse(parsed._replace(query=urlencode(qs, doseq=True)))
        if "page" in qs:
            qs["page"] = [str(page_num)]
            return urlunparse(parsed._replace(query=urlencode(qs, doseq=True)))

        # Path-based pagination (/page/n/)
        clean_path = path.rstrip("/")
        if "/page/" in clean_path:
            import re
            new_path = re.sub(r'/page/\d+', f'/page/{page_num}', clean_path) + "/"
        else:
            new_path = f"{clean_path}/page/{page_num}/"

        return urlunparse(parsed._replace(path=new_path))

    def run_fetch_batch(
        self,
        target_url: str,
        pages_to_crawl: int = 1,
        reset_checkpoint: bool = False,
        division: str = "Tyresoles",
        product: str = "Commercial Retreading"
    ) -> Dict[str, Any]:
        """
        Executes Process 1 (Producer):
        1. Checks current checkpoint. Resumes from last_crawled_page + 1.
        2. Crawls requested number of pages.
        3. Extracts detail pages and enqueues to SQLite staging buffer with status PENDING.
        4. Updates checkpoint.
        """
        if reset_checkpoint:
            logger.info(f"Resetting checkpoint for {target_url} to Page 1")
            self.store.reset_checkpoint(target_url, clear_staging=False)

        checkpoint = self.store.get_checkpoint(target_url)
        start_page = checkpoint["next_page_to_crawl"]
        logger.info(f"==> Process 1 (Producer) starting for {target_url}")
        logger.info(f"    Resuming from checkpoint Page {start_page} (Crawling {pages_to_crawl} page(s))")

        pages_crawled = 0
        total_discovered = 0
        total_enqueued = 0
        has_reached_end = False
        last_success_page = checkpoint["last_crawled_page"]
        latest_badge_meta: Dict[str, Any] = {}

        for offset in range(pages_to_crawl):
            current_page = start_page + offset
            page_url = self._build_page_url(target_url, current_page)
            logger.info(f"[{offset + 1}/{pages_to_crawl}] Fetching directory page {current_page}: {page_url}")

            html = self.scraper.fetch_html(page_url)
            if not html:
                logger.warning(f"Could not load HTML for Page {current_page} after retries. Transient network error or server timeout.")
                # Do NOT flag has_reached_end on network failure, allowing safe resumption
                break

            from bs4 import BeautifulSoup
            soup = BeautifulSoup(html, "html.parser")
            badge_meta = self.scraper.detect_pagination_and_badges(page_url, soup, current_page)
            latest_badge_meta = badge_meta
            logger.info(f"Page {current_page} badge detection: {badge_meta.get('badge_text')} | Next page: {badge_meta.get('has_next_page')}")

            # 1. Direct on-page listing card extraction (instant, no extra HTTP round-trips)
            direct_leads = self.scraper.extract_leads_directly_from_page(html, page_url, fallback_category=product)
            page_enqueued = 0
            if direct_leads:
                logger.info(f"Direct card extractor found {len(direct_leads)} contact cards on page {current_page}")
                for dl in direct_leads:
                    dl["division"] = division
                    dl["targetProduct"] = product
                    is_new, lead_id = self.store.enqueue_raw_lead(target_url, current_page, dl)
                    if is_new:
                        page_enqueued += 1

            # 2. Extract listing detail URLs for deep extraction
            listing_urls = self.scraper.extract_listing_urls_from_page(html, page_url)
            logger.info(f"Page {current_page}: found {len(listing_urls)} listing URLs")

            if not listing_urls and not direct_leads:
                total_detected = badge_meta.get("total_pages_detected") or checkpoint.get("total_pages_detected", 0)
                if total_detected and current_page < total_detected:
                    logger.warning(f"Page {current_page} returned 0 listings, but total pages is {total_detected}. Skipping false end-of-directory.")
                else:
                    logger.info(f"Page {current_page} has 0 listings. Category exhausted.")
                    has_reached_end = True
                break

            total_discovered += len(listing_urls) + len(direct_leads)

            # 3. Parallel Multi-Worker Detail Page Fetching
            if listing_urls:
                from concurrent.futures import ThreadPoolExecutor, as_completed

                def _fetch_single_detail(item_url: str):
                    try:
                        lead = self.scraper.extract_lead_details(item_url, fallback_category=product)
                        if lead:
                            lead["division"] = division
                            lead["targetProduct"] = product
                            return lead
                    except Exception as ex:
                        logger.error(f"Failed to fetch detail for {item_url}: {ex}")
                    return None

                # Launch concurrent worker threads for 5x-10x performance
                max_workers = min(8, len(listing_urls))
                logger.info(f"  Dispatching {len(listing_urls)} detail pages across {max_workers} parallel workers...")

                with ThreadPoolExecutor(max_workers=max_workers) as executor:
                    future_to_url = {executor.submit(_fetch_single_detail, u): u for u in listing_urls}
                    for future in as_completed(future_to_url):
                        lead_result = future.result()
                        if lead_result:
                            is_new, lead_id = self.store.enqueue_raw_lead(target_url, current_page, lead_result)
                            if is_new:
                                page_enqueued += 1

            total_enqueued += page_enqueued
            pages_crawled += 1
            last_success_page = current_page

            total_detected = badge_meta.get("total_pages_detected") or checkpoint.get("total_pages_detected", 0)
            if total_detected and current_page >= total_detected:
                logger.info(f"Reached final detected page {current_page} of {total_detected}.")
                has_reached_end = True
            elif not badge_meta.get("has_next_page") and (not total_detected or current_page >= total_detected):
                logger.info(f"Badge detector indicated final page reached at page {current_page}.")
                has_reached_end = True

            # Update checkpoint after each completed page to ensure resilience against crashes
            self.store.update_checkpoint(
                target_url=target_url,
                last_page=last_success_page,
                new_listings_count=page_enqueued,
                has_reached_end=has_reached_end,
                total_pages_detected=badge_meta.get("total_pages_detected", 0)
            )

            if has_reached_end:
                break

        updated_cp = self.store.get_checkpoint(target_url)
        result = {
            "success": True,
            "target_url": target_url,
            "start_page": start_page,
            "last_crawled_page": last_success_page,
            "next_page_to_crawl": updated_cp["next_page_to_crawl"],
            "pages_crawled": pages_crawled,
            "listings_discovered": total_discovered,
            "new_leads_enqueued": total_enqueued,
            "has_reached_end": has_reached_end,
            "badge_metadata": latest_badge_meta,
            "queue_summary": updated_cp["queue_summary"]
        }
        logger.info(f"Process 1 Finished: Parallel workers enqueued {total_enqueued} new leads. Next page: {updated_cp['next_page_to_crawl']}")
        return result


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Process 1: Fast Web Crawler & Stager (Producer)")
    parser.add_argument("--url", type=str, required=True, help="Target category/directory URL")
    parser.add_argument("--pages", type=int, default=1, help="Number of pages to crawl in this batch")
    parser.add_argument("--reset", action="store_true", help="Reset checkpoint to restart from Page 1")
    parser.add_argument("--division", type=str, default="Tyresoles", help="CRM Division")
    parser.add_argument("--product", type=str, default="Commercial Retreading", help="Target Product")

    args = parser.parse_args()
    fetcher = FetcherProcess()
    res = fetcher.run_fetch_batch(
        target_url=args.url,
        pages_to_crawl=args.pages,
        reset_checkpoint=args.reset,
        division=args.division,
        product=args.product
    )
    import json
    print(json.dumps(res, indent=2))
