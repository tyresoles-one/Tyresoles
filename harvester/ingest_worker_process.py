"""
Process 2 (Consumer): Validation & CRM Ingestion Worker
Pulls PENDING leads from the SQLite staging buffer, applies strict anti-fake
validation, deduplicates against CRM, and imports genuine leads into Tyresoles CRM.
"""

import os
import re
import sys
import logging
from typing import Dict, Any, List, Optional, Set

from staging_store import StagingStore
from validator import LeadValidator
from crm_client import CrmApiClient

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")
logger = logging.getLogger("IngestWorkerProcess")


class IngestWorkerProcess:
    def __init__(
        self,
        store: Optional[StagingStore] = None,
        crm_endpoint: Optional[str] = None,
        auth_token: str = ""
    ):
        self.store = store or StagingStore()
        endpoint = crm_endpoint or os.environ.get("CRM_ENDPOINT", "http://localhost:5001/graphql")
        token = auth_token or os.environ.get("CRM_AUTH_TOKEN", "")
        self.crm_client = CrmApiClient(endpoint_url=endpoint, auth_token=token)

    def _fetch_existing_crm_mobiles(self) -> Set[str]:
        """
        Attempts to query CRM GraphQL API for existing contact phone numbers
        to guarantee deduplication before importing.
        """
        existing = set()
        query = """
        query CheckMobiles {
          crmContacts: getCrmContacts(take: 1000) {
            items {
              mobileNo
            }
          }
        }
        """
        try:
            import urllib.request
            import json
            req = urllib.request.Request(
                self.crm_client.endpoint_url,
                data=json.dumps({"query": query}).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST"
            )
            with urllib.request.urlopen(req, timeout=10) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                items = data.get("data", {}).get("crmContacts", {}).get("items", [])
                for it in items:
                    mob = it.get("mobileNo")
                    if mob:
                        digits = re.sub(r'\D', '', mob)[-10:]
                        if len(digits) == 10:
                            existing.add(digits)
            logger.info(f"Loaded {len(existing)} existing contact mobile numbers from CRM.")
        except Exception as e:
            logger.debug(f"Could not pre-fetch CRM contacts for deduplication: {e}")

        return existing

    def run_ingest_batch(
        self,
        target_url: Optional[str] = None,
        batch_limit: int = 50,
        dry_run: bool = False
    ) -> Dict[str, Any]:
        """
        Executes Process 2 (Consumer):
        1. Reads PENDING records from staging buffer.
        2. Validates against LeadValidator (10-digit Indian mobile, anti-mock).
        3. Deduplicates against existing CRM contacts and intra-batch records.
        4. Imports genuine leads into Tyresoles CRM.
        5. Updates status in staging_leads (IMPORTED, DUPLICATE, REJECTED).
        """
        logger.info(f"==> Process 2 (Consumer) starting: Pulling up to {batch_limit} PENDING leads from staging buffer")
        pending_items = self.store.get_pending_leads(target_url=target_url, limit=batch_limit)

        if not pending_items:
            logger.info("No PENDING leads in staging buffer to process.")
            return {
                "success": True,
                "processed": 0,
                "imported": 0,
                "duplicates": 0,
                "rejected": 0,
                "pending_remaining": 0,
                "queue_summary": self.store.get_queue_summary(target_url)
            }

        existing_crm_mobiles = self._fetch_existing_crm_mobiles()
        seen_batch_mobiles: Set[str] = set()

        processed_count = 0
        imported_count = 0
        duplicate_count = 0
        rejected_count = 0

        leads_to_import_batch: List[Dict[str, Any]] = []
        staging_ids_for_import: List[str] = []

        for item in pending_items:
            staging_id = item["staging_id"]
            lead_data = item["lead_data"]
            processed_count += 1

            # Step A: Strict Anti-Fake & Phone Validation
            is_valid, reason = LeadValidator.validate_lead(lead_data, allow_no_phone=False)
            if not is_valid:
                logger.info(f"[{processed_count}/{len(pending_items)}] REJECTED lead '{lead_data.get('companyName')}': {reason}")
                self.store.mark_lead_status(staging_id, status="REJECTED", validation_error=reason)
                rejected_count += 1
                continue

            # Step B: Clean phone number
            mobile_raw = lead_data.get("mobileNo") or ""
            clean_digits = re.sub(r'\D', '', mobile_raw)[-10:]
            lead_data["mobileNo"] = clean_digits

            # Step C: Check if duplicate in CRM or intra-batch
            if clean_digits in existing_crm_mobiles:
                logger.info(f"[{processed_count}/{len(pending_items)}] DUPLICATE (Already in CRM): '{lead_data.get('companyName')}' ({clean_digits})")
                self.store.mark_lead_status(staging_id, status="DUPLICATE", validation_error="Already in CRM contacts")
                duplicate_count += 1
                continue

            if clean_digits in seen_batch_mobiles:
                logger.info(f"[{processed_count}/{len(pending_items)}] DUPLICATE (Repeating in batch): '{lead_data.get('companyName')}' ({clean_digits})")
                self.store.mark_lead_status(staging_id, status="DUPLICATE", validation_error="Repeating listing in current batch")
                duplicate_count += 1
                continue

            seen_batch_mobiles.add(clean_digits)
            leads_to_import_batch.append(lead_data)
            staging_ids_for_import.append(staging_id)

        # Step D: Import genuine leads to CRM
        if leads_to_import_batch:
            if dry_run:
                logger.info(f"[DRY-RUN] Would import {len(leads_to_import_batch)} genuine leads to CRM.")
                for sid in staging_ids_for_import:
                    self.store.mark_lead_status(sid, status="VALIDATED", validation_error="Dry-run verified")
                imported_count = len(leads_to_import_batch)
            else:
                logger.info(f"Submitting batch of {len(leads_to_import_batch)} genuine leads to CRM endpoint...")
                import_res = self.crm_client.import_leads(leads_to_import_batch)
                actual_imported = import_res.get("importedCount", 0)
                dup_crm = import_res.get("duplicateCount", 0)

                if import_res.get("success", True) or actual_imported > 0:
                    for sid in staging_ids_for_import:
                        self.store.mark_lead_status(sid, status="IMPORTED")
                        existing_crm_mobiles.add(clean_digits)
                    imported_count = actual_imported if actual_imported > 0 else len(leads_to_import_batch)
                    duplicate_count += dup_crm
                    logger.info(f"Successfully imported {imported_count} leads to CRM Contacts.")
                else:
                    # Fallback: retain PENDING status if CRM API temporarily unreachable so it can be retried
                    logger.warning(f"CRM API returned error: {import_res.get('errors') or import_res.get('error')}. Retaining PENDING status.")
                    for sid in staging_ids_for_import:
                        self.store.mark_lead_status(sid, status="PENDING", validation_error="CRM API endpoint pending")
                    imported_count = 0

        final_summary = self.store.get_queue_summary(target_url)
        result = {
            "success": True,
            "processed": processed_count,
            "imported": imported_count,
            "duplicates": duplicate_count,
            "rejected": rejected_count,
            "pending_remaining": final_summary["pending"],
            "queue_summary": final_summary
        }
        logger.info(f"Process 2 Finished: Processed {processed_count} -> Imported {imported_count}, Duplicates {duplicate_count}, Rejected {rejected_count}. Remaining in queue: {final_summary['pending']}")
        return result


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Process 2: Lead Validator & CRM Ingest Worker (Consumer)")
    parser.add_argument("--url", type=str, default=None, help="Optional filter by target category/directory URL")
    parser.add_argument("--limit", type=int, default=50, help="Maximum pending leads to process in this run")
    parser.add_argument("--dry-run", action="store_true", help="Validate and deduplicate without writing to CRM")
    parser.add_argument("--crm-endpoint", type=str, default="http://localhost:5000/graphql", help="Tyresoles GraphQL endpoint")

    args = parser.parse_args()
    worker = IngestWorkerProcess(crm_endpoint=args.crm_endpoint)
    res = worker.run_ingest_batch(
        target_url=args.url,
        batch_limit=args.limit,
        dry_run=args.dry_run
    )
    import json
    print(json.dumps(res, indent=2))
