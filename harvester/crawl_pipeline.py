"""
Unified Pipeline Orchestrator for Decoupled Web Crawling & Ingestion.
Coordinates Process 1 (Producer / Fetcher) and Process 2 (Consumer / Ingestor)
with persistent checkpointing and staging buffer management.
"""

import os
import sys
import json
import logging
import argparse
from typing import Dict, Any

from staging_store import StagingStore
from fetcher_process import FetcherProcess
from ingest_worker_process import IngestWorkerProcess

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("CrawlPipeline")


class CrawlPipeline:
    def __init__(self, db_path: str = None, crm_endpoint: str = "http://localhost:5000/graphql"):
        self.store = StagingStore(db_path) if db_path else StagingStore()
        self.fetcher = FetcherProcess(store=self.store)
        self.worker = IngestWorkerProcess(store=self.store, crm_endpoint=crm_endpoint)

    def get_status(self, url: str) -> Dict[str, Any]:
        """Returns the current checkpoint and queue summary for a URL."""
        checkpoint = self.store.get_checkpoint(url)
        return {
            "target_url": checkpoint["target_url"],
            "last_crawled_page": checkpoint["last_crawled_page"],
            "next_page_to_crawl": checkpoint["next_page_to_crawl"],
            "total_pages_crawled": checkpoint["total_pages_crawled"],
            "total_listings_discovered": checkpoint["total_listings_discovered"],
            "total_pages_detected": checkpoint.get("total_pages_detected", 0),
            "has_reached_end": checkpoint["has_reached_end"],
            "updated_at": checkpoint["updated_at"],
            "queue_summary": checkpoint["queue_summary"]
        }

    def fetch_batch(
        self,
        url: str,
        pages: int = 1,
        reset: bool = False,
        division: str = "Tyresoles",
        product: str = "Commercial Retreading"
    ) -> Dict[str, Any]:
        """Runs Process 1: Crawl next batch of pages and buffer raw listings."""
        return self.fetcher.run_fetch_batch(
            target_url=url,
            pages_to_crawl=pages,
            reset_checkpoint=reset,
            division=division,
            product=product
        )

    def ingest_batch(
        self,
        url: str = None,
        limit: int = 50,
        dry_run: bool = False
    ) -> Dict[str, Any]:
        """Runs Process 2: Validate pending staged leads and ingest into CRM."""
        return self.worker.run_ingest_batch(
            target_url=url,
            batch_limit=limit,
            dry_run=dry_run
        )

    def run_full_pipeline(
        self,
        url: str,
        pages: int = 1,
        reset: bool = False,
        division: str = "Tyresoles",
        product: str = "Commercial Retreading",
        dry_run: bool = False
    ) -> Dict[str, Any]:
        """
        Runs full 2-stage pipeline:
        Stage 1: Fetch next batch of pages into staging buffer.
        Stage 2: Process, validate, and import staged leads into CRM.
        """
        logger.info(f"=== Running Full Pipeline for {url} (Pages: {pages}) ===")
        fetch_res = self.fetch_batch(url=url, pages=pages, reset=reset, division=division, product=product)
        ingest_res = self.ingest_batch(url=url, limit=100, dry_run=dry_run)

        return {
            "success": True,
            "target_url": url,
            "stage1_fetch": fetch_res,
            "stage2_ingest": ingest_res,
            "current_checkpoint": self.get_status(url)
        }

    def auto_extract(
        self,
        url: str,
        pages: int = 1,
        auto_ingest: bool = True,
        reset: bool = False,
        division: str = "Tyresoles",
        product: str = "Commercial Retreading",
        dry_run: bool = False
    ) -> Dict[str, Any]:
        """
        Runs single-action automatic information extraction:
        1. Multi-worker page scouting & detail extraction (5x-10x parallel speedup)
        2. Auto-detection of next badge data / pagination
        3. Staging buffer insertion
        4. Optional automatic validation and CRM synchronization
        Returns extracted leads, badge info, and updated checkpoint.
        """
        logger.info(f"=== Running Automatic Extraction for {url} (Pages: {pages}, Auto-Ingest: {auto_ingest}) ===")
        fetch_res = self.fetch_batch(url=url, pages=pages, reset=reset, division=division, product=product)

        ingest_res = None
        if auto_ingest:
            ingest_res = self.ingest_batch(url=url, limit=100, dry_run=dry_run)

        staged_leads = self.store.get_staged_leads(target_url=url, limit=50)

        return {
            "success": True,
            "target_url": url,
            "stage1_fetch": fetch_res,
            "stage2_ingest": ingest_res,
            "badge_metadata": fetch_res.get("badge_metadata", {}),
            "current_checkpoint": self.get_status(url),
            "leads": staged_leads
        }

    def reset_url(self, url: str, clear_staging: bool = False) -> Dict[str, Any]:
        """Resets the checkpoint for a URL so next crawl starts from Page 1."""
        return self.store.reset_checkpoint(url, clear_staging=clear_staging)


def main():
    parser = argparse.ArgumentParser(description="Tyresoles Decoupled Web Crawl & CRM Ingestion Pipeline")
    subparsers = parser.add_subparsers(dest="command", required=True, help="Pipeline command to execute")

    # Auto Extract (Single Process / Multi-worker)
    auto_p = subparsers.add_parser("auto_extract", help="Single process auto-extraction with parallel workers & badge detection")
    auto_p.add_argument("--url", type=str, required=True, help="Target URL")
    auto_p.add_argument("--pages", type=int, default=1, help="Number of pages to crawl")
    auto_p.add_argument("--auto-ingest", action="store_true", default=True, help="Automatically validate and ingest staged leads into CRM")
    auto_p.add_argument("--no-ingest", dest="auto_ingest", action="store_false", help="Do not auto-ingest into CRM")
    auto_p.add_argument("--reset", action="store_true", help="Reset checkpoint before fetching")
    auto_p.add_argument("--division", type=str, default="Tyresoles", help="CRM Division")
    auto_p.add_argument("--product", type=str, default="Commercial Retreading", help="Product Line")
    auto_p.add_argument("--dry-run", action="store_true", help="Dry-run CRM import")

    # Status
    status_p = subparsers.add_parser("status", help="Get crawl checkpoint & queue status for a URL")
    status_p.add_argument("--url", type=str, required=True, help="Target URL")

    # Leads query
    leads_p = subparsers.add_parser("leads", help="Query staged leads buffer")
    leads_p.add_argument("--url", type=str, default=None, help="Target URL (optional)")
    leads_p.add_argument("--status", type=str, default="ALL", help="Lead status (ALL, PENDING, IMPORTED, DUPLICATE, REJECTED)")
    leads_p.add_argument("--limit", type=int, default=50, help="Max items")

    # Fetch (Process 1)
    fetch_p = subparsers.add_parser("fetch", help="Run Process 1: Fetch next N pages to staging buffer")
    fetch_p.add_argument("--url", type=str, required=True, help="Target URL")
    fetch_p.add_argument("--pages", type=int, default=1, help="Number of pages to crawl")
    fetch_p.add_argument("--reset", action="store_true", help="Reset checkpoint before fetching")
    fetch_p.add_argument("--division", type=str, default="Tyresoles", help="CRM Division")
    fetch_p.add_argument("--product", type=str, default="Commercial Retreading", help="Product Line")

    # Ingest (Process 2)
    ingest_p = subparsers.add_parser("ingest", help="Run Process 2: Validate and import staged leads to CRM")
    ingest_p.add_argument("--url", type=str, default=None, help="Target URL (optional)")
    ingest_p.add_argument("--limit", type=int, default=50, help="Max leads to process")
    ingest_p.add_argument("--dry-run", action="store_true", help="Dry-run without writing to CRM")

    # Run Full Pipeline (Process 1 + Process 2)
    run_p = subparsers.add_parser("run", help="Run full pipeline: Fetch next batch then validate and import")
    run_p.add_argument("--url", type=str, required=True, help="Target URL")
    run_p.add_argument("--pages", type=int, default=1, help="Number of pages to crawl")
    run_p.add_argument("--reset", action="store_true", help="Reset checkpoint before fetching")
    run_p.add_argument("--division", type=str, default="Tyresoles", help="CRM Division")
    run_p.add_argument("--product", type=str, default="Commercial Retreading", help="Product Line")
    run_p.add_argument("--dry-run", action="store_true", help="Dry-run CRM import")

    # Reset
    reset_p = subparsers.add_parser("reset", help="Reset checkpoint to restart from Page 1")
    reset_p.add_argument("--url", type=str, required=True, help="Target URL")
    reset_p.add_argument("--clear-staging", action="store_true", help="Also clear staged records")

    # Pending for Direct CRM Ingest
    pending_p = subparsers.add_parser("pending", help="Fetch unimported pending/validated leads")
    pending_p.add_argument("--url", type=str, default=None, help="Target URL (optional)")
    pending_p.add_argument("--limit", type=int, default=100, help="Max leads to fetch")

    # Mark Imported
    mark_p = subparsers.add_parser("mark_imported", help="Update staging statuses after CRM import")
    mark_p.add_argument("--file", type=str, default=None, help="Path to JSON payload file")
    mark_p.add_argument("--data", type=str, default=None, help="Inline JSON string payload")
    mark_p.add_argument("--url", type=str, default=None, help="Target URL (optional)")

    args = parser.parse_args()
    pipeline = CrawlPipeline()

    if args.command == "auto_extract":
        res = pipeline.auto_extract(
            url=args.url,
            pages=args.pages,
            auto_ingest=args.auto_ingest,
            reset=args.reset,
            division=args.division,
            product=args.product,
            dry_run=args.dry_run
        )
        print(json.dumps(res, indent=2))
    elif args.command == "pending":
        res = pipeline.store.get_pending_leads(
            target_url=args.url,
            limit=args.limit
        )
        print(json.dumps(res, indent=2))
    elif args.command == "mark_imported":
        payload = {}
        if args.file and os.path.exists(args.file):
            with open(args.file, "r", encoding="utf-8") as f:
                payload = json.load(f)
        elif args.data:
            payload = json.loads(args.data)
        
        imported_map = payload.get("imported") or {}
        duplicate_ids = payload.get("duplicates") or []
        rejected_ids = payload.get("rejected") or []
        ok = pipeline.store.mark_imported_batch(imported_map, duplicate_ids, rejected_ids)
        summary = pipeline.store.get_queue_summary(args.url)
        print(json.dumps({"success": ok, "queue_summary": summary}, indent=2))
    elif args.command == "leads":
        res = pipeline.store.get_staged_leads(
            target_url=args.url,
            status=args.status,
            limit=args.limit
        )
        print(json.dumps(res, indent=2))
    elif args.command == "status":
        res = pipeline.get_status(args.url)
        print(json.dumps(res, indent=2))
    elif args.command == "fetch":
        res = pipeline.fetch_batch(
            url=args.url,
            pages=args.pages,
            reset=args.reset,
            division=args.division,
            product=args.product
        )
        print(json.dumps(res, indent=2))
    elif args.command == "ingest":
        res = pipeline.ingest_batch(
            url=args.url,
            limit=args.limit,
            dry_run=args.dry_run
        )
        print(json.dumps(res, indent=2))
    elif args.command == "run":
        res = pipeline.run_full_pipeline(
            url=args.url,
            pages=args.pages,
            reset=args.reset,
            division=args.division,
            product=args.product,
            dry_run=args.dry_run
        )
        print(json.dumps(res, indent=2))
    elif args.command == "reset":
        res = pipeline.reset_url(args.url, clear_staging=args.clear_staging)
        print(json.dumps(res, indent=2))


if __name__ == "__main__":
    main()

