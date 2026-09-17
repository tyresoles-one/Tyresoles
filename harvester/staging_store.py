"""
SQLite-backed Persistent Staging Buffer and Crawler Checkpoint Store.
Enables decoupled, fault-tolerant Producer-Consumer crawling and ingestion.
"""

import os
import json
import sqlite3
import hashlib
import logging
from datetime import datetime
from typing import Dict, Any, List, Optional, Tuple

logger = logging.getLogger(__name__)

DEFAULT_DB_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data", "staging.db")


class StagingStore:
    """Thread-safe SQLite store for crawler checkpoints and staging lead queue."""

    def __init__(self, db_path: str = DEFAULT_DB_PATH):
        self.db_path = db_path
        os.makedirs(os.path.dirname(os.path.abspath(self.db_path)), exist_ok=True)
        self._init_db()

    def _get_connection(self) -> sqlite3.Connection:
        conn = sqlite3.connect(self.db_path, timeout=30.0)
        conn.row_factory = sqlite3.Row
        conn.execute("PRAGMA journal_mode=WAL;")
        conn.execute("PRAGMA synchronous=NORMAL;")
        return conn

    def _init_db(self):
        """Initializes tables and indexes."""
        with self._get_connection() as conn:
            conn.executescript("""
                CREATE TABLE IF NOT EXISTS crawl_checkpoints (
                    target_url TEXT PRIMARY KEY,
                    last_crawled_page INTEGER NOT NULL DEFAULT 0,
                    next_page_to_crawl INTEGER NOT NULL DEFAULT 1,
                    total_pages_crawled INTEGER NOT NULL DEFAULT 0,
                    total_listings_discovered INTEGER NOT NULL DEFAULT 0,
                    total_pages_detected INTEGER DEFAULT 0,
                    has_reached_end INTEGER NOT NULL DEFAULT 0,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS staging_leads (
                    id TEXT PRIMARY KEY,
                    target_url TEXT NOT NULL,
                    source_url TEXT UNIQUE NOT NULL,
                    page_num INTEGER NOT NULL DEFAULT 1,
                    company_name TEXT,
                    contact_person TEXT,
                    mobile_raw TEXT,
                    alt_mobile_raw TEXT,
                    email_raw TEXT,
                    address_raw TEXT,
                    city_raw TEXT,
                    state_raw TEXT,
                    gps_raw TEXT,
                    website_raw TEXT,
                    notes_raw TEXT,
                    raw_json TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'PENDING',
                    validation_error TEXT,
                    crm_contact_id TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    processed_at DATETIME
                );

                CREATE INDEX IF NOT EXISTS idx_staging_target_status ON staging_leads (target_url, status);
                CREATE INDEX IF NOT EXISTS idx_staging_status ON staging_leads (status);
                CREATE INDEX IF NOT EXISTS idx_staging_page ON staging_leads (target_url, page_num);
            """)

            # Migration: ensure total_pages_detected exists on existing databases
            try:
                conn.execute("ALTER TABLE crawl_checkpoints ADD COLUMN total_pages_detected INTEGER DEFAULT 0;")
            except Exception:
                pass

    @staticmethod
    def _normalize_url(url: str) -> str:
        """Normalizes a URL to serve as a reliable dictionary key."""
        clean = (url or "").strip().rstrip("/")
        return clean.lower()

    @staticmethod
    def _hash_id(source_url: str) -> str:
        """Generates deterministic ID from source URL."""
        return hashlib.sha256(source_url.strip().lower().encode("utf-8")).hexdigest()[:24]

    # ─────────────────────────────────────────────────────────────
    # Checkpoint Management
    # ─────────────────────────────────────────────────────────────

    def get_checkpoint(self, target_url: str) -> Dict[str, Any]:
        """Retrieves the crawl checkpoint for a target URL."""
        norm_url = self._normalize_url(target_url)
        with self._get_connection() as conn:
            row = conn.execute(
                "SELECT * FROM crawl_checkpoints WHERE target_url = ?",
                (norm_url,)
            ).fetchone()

            if row:
                summary = self.get_queue_summary(norm_url)
                col_keys = row.keys() if hasattr(row, "keys") else []
                tpd = row["total_pages_detected"] if "total_pages_detected" in col_keys and row["total_pages_detected"] else 0
                return {
                    "target_url": row["target_url"],
                    "last_crawled_page": row["last_crawled_page"],
                    "next_page_to_crawl": row["next_page_to_crawl"],
                    "total_pages_crawled": row["total_pages_crawled"],
                    "total_listings_discovered": row["total_listings_discovered"],
                    "total_pages_detected": tpd,
                    "has_reached_end": bool(row["has_reached_end"]),
                    "updated_at": row["updated_at"],
                    "queue_summary": summary
                }
            else:
                return {
                    "target_url": norm_url,
                    "last_crawled_page": 0,
                    "next_page_to_crawl": 1,
                    "total_pages_crawled": 0,
                    "total_listings_discovered": 0,
                    "total_pages_detected": 0,
                    "has_reached_end": False,
                    "updated_at": None,
                    "queue_summary": self.get_queue_summary(norm_url)
                }

    def update_checkpoint(
        self,
        target_url: str,
        last_page: int,
        new_listings_count: int = 0,
        has_reached_end: bool = False,
        total_pages_detected: int = 0
    ) -> Dict[str, Any]:
        """Updates or creates a crawl checkpoint."""
        norm_url = self._normalize_url(target_url)
        next_page = 1 if has_reached_end else last_page + 1

        with self._get_connection() as conn:
            conn.execute("""
                INSERT INTO crawl_checkpoints (
                    target_url, last_crawled_page, next_page_to_crawl,
                    total_pages_crawled, total_listings_discovered, total_pages_detected, has_reached_end, updated_at
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
                ON CONFLICT(target_url) DO UPDATE SET
                    last_crawled_page = ?,
                    next_page_to_crawl = ?,
                    total_pages_crawled = total_pages_crawled + 1,
                    total_listings_discovered = total_listings_discovered + ?,
                    total_pages_detected = CASE WHEN ? > 0 THEN ? ELSE total_pages_detected END,
                    has_reached_end = ?,
                    updated_at = CURRENT_TIMESTAMP
            """, (
                norm_url, last_page, next_page, 1, new_listings_count, total_pages_detected, int(has_reached_end),
                last_page, next_page, new_listings_count, total_pages_detected, total_pages_detected, int(has_reached_end)
            ))
            conn.commit()

        return self.get_checkpoint(norm_url)

    def reset_checkpoint(self, target_url: str, clear_staging: bool = False) -> Dict[str, Any]:
        """Resets the crawl checkpoint so the next batch restarts from Page 1."""
        norm_url = self._normalize_url(target_url)
        with self._get_connection() as conn:
            conn.execute("""
                INSERT INTO crawl_checkpoints (
                    target_url, last_crawled_page, next_page_to_crawl,
                    total_pages_crawled, total_listings_discovered, has_reached_end, updated_at
                )
                VALUES (?, 0, 1, 0, 0, 0, CURRENT_TIMESTAMP)
                ON CONFLICT(target_url) DO UPDATE SET
                    last_crawled_page = 0,
                    next_page_to_crawl = 1,
                    has_reached_end = 0,
                    updated_at = CURRENT_TIMESTAMP
            """, (norm_url,))

            if clear_staging:
                conn.execute("DELETE FROM staging_leads WHERE target_url = ?", (norm_url,))

            conn.commit()

        return self.get_checkpoint(norm_url)

    # ─────────────────────────────────────────────────────────────
    # Staging Queue Operations (Producer)
    # ─────────────────────────────────────────────────────────────

    def enqueue_raw_lead(self, target_url: str, page_num: int, lead_data: Dict[str, Any]) -> Tuple[bool, str]:
        """
        Enqueues a raw lead into staging_leads buffer with status PENDING.
        Returns (is_new, lead_id).
        """
        source_url = (lead_data.get("sourceUrl") or "").strip()
        if not source_url:
            return False, ""

        norm_target = self._normalize_url(target_url)
        lead_id = self._hash_id(source_url)
        raw_json_str = json.dumps(lead_data, ensure_ascii=False)

        company = lead_data.get("companyName") or ""
        contact = lead_data.get("fullName") or ""
        mobile = lead_data.get("mobileNo") or ""
        alt_mobile = lead_data.get("mobileNo2") or ""
        email = ",".join(lead_data.get("emailIds", [])) if isinstance(lead_data.get("emailIds"), list) else (lead_data.get("emailIds") or "")
        address = lead_data.get("address") or ""
        city = lead_data.get("city") or ""
        state = lead_data.get("state") or ""
        gps = lead_data.get("location") or ""
        website = lead_data.get("website") or ""
        notes = lead_data.get("notes") or lead_data.get("snippet") or ""

        with self._get_connection() as conn:
            try:
                conn.execute("""
                    INSERT INTO staging_leads (
                        id, target_url, source_url, page_num,
                        company_name, contact_person, mobile_raw, alt_mobile_raw, email_raw,
                        address_raw, city_raw, state_raw, gps_raw, website_raw, notes_raw,
                        raw_json, status, created_at
                    ) VALUES (
                        ?, ?, ?, ?,
                        ?, ?, ?, ?, ?,
                        ?, ?, ?, ?, ?, ?,
                        ?, 'PENDING', CURRENT_TIMESTAMP
                    )
                    ON CONFLICT(source_url) DO UPDATE SET
                        page_num = ?,
                        company_name = COALESCE(NULLIF(excluded.company_name, ''), staging_leads.company_name),
                        mobile_raw = COALESCE(NULLIF(excluded.mobile_raw, ''), staging_leads.mobile_raw),
                        raw_json = excluded.raw_json
                """, (
                    lead_id, norm_target, source_url, page_num,
                    company, contact, mobile, alt_mobile, email,
                    address, city, state, gps, website, notes,
                    raw_json_str,
                    page_num
                ))
                conn.commit()
                return True, lead_id
            except Exception as ex:
                logger.error(f"Failed to enqueue lead {source_url}: {ex}")
                return False, ""

    def enqueue_raw_leads_batch(self, target_url: str, page_num: int, leads: List[Dict[str, Any]]) -> int:
        """Enqueues a batch of raw leads and returns count of successfully enqueued records."""
        inserted = 0
        for item in leads:
            ok, _ = self.enqueue_raw_lead(target_url, page_num, item)
            if ok:
                inserted += 1
        return inserted

    # ─────────────────────────────────────────────────────────────
    # Staging Queue Operations (Consumer)
    # ─────────────────────────────────────────────────────────────

    def get_pending_leads(self, target_url: Optional[str] = None, limit: int = 50) -> List[Dict[str, Any]]:
        """Pulls leads with status PENDING (or unimported VALIDATED) for validation and ingestion."""
        with self._get_connection() as conn:
            query = "SELECT * FROM staging_leads WHERE status = 'PENDING' OR (status = 'VALIDATED' AND (validation_error LIKE '%CRM API%' OR validation_error IS NULL OR validation_error = 'Dry-run verified'))"
            params: List[Any] = []

            if target_url:
                query += " AND target_url = ?"
                params.append(self._normalize_url(target_url))

            query += " ORDER BY page_num ASC, created_at ASC LIMIT ?"
            params.append(limit)

            rows = conn.execute(query, params).fetchall()
            results = []
            for r in rows:
                lead_dict = json.loads(r["raw_json"])
                results.append({
                    "staging_id": r["id"],
                    "target_url": r["target_url"],
                    "source_url": r["source_url"],
                    "page_num": r["page_num"],
                    "status": r["status"],
                    "lead_data": lead_dict
                })
            return results

    def mark_lead_status(
        self,
        staging_id: str,
        status: str,
        validation_error: Optional[str] = None,
        crm_contact_id: Optional[str] = None
    ) -> bool:
        """
        Updates the status of a lead in staging (IMPORTED, DUPLICATE, REJECTED).
        """
        with self._get_connection() as conn:
            conn.execute("""
                UPDATE staging_leads
                SET status = ?,
                    validation_error = ?,
                    crm_contact_id = ?,
                    processed_at = CURRENT_TIMESTAMP
                WHERE id = ?
            """, (status, validation_error, crm_contact_id, staging_id))
            conn.commit()
            return True

    def mark_imported_batch(
        self,
        imported_map: Dict[str, str],
        duplicate_ids: Optional[List[str]] = None,
        rejected_ids: Optional[List[str]] = None
    ) -> bool:
        """
        Batch updates statuses for imported, duplicate, and rejected leads.
        """
        with self._get_connection() as conn:
            if imported_map:
                for sid, cid in imported_map.items():
                    conn.execute("""
                        UPDATE staging_leads
                        SET status = 'IMPORTED',
                            crm_contact_id = ?,
                            validation_error = NULL,
                            processed_at = CURRENT_TIMESTAMP
                        WHERE id = ?
                    """, (cid, sid))
            if duplicate_ids:
                for did in duplicate_ids:
                    conn.execute("""
                        UPDATE staging_leads
                        SET status = 'DUPLICATE',
                            validation_error = 'Already in CRM contacts',
                            processed_at = CURRENT_TIMESTAMP
                        WHERE id = ?
                    """, (did,))
            if rejected_ids:
                for rid in rejected_ids:
                    conn.execute("""
                        UPDATE staging_leads
                        SET status = 'REJECTED',
                            validation_error = 'Invalid phone number or mock data',
                            processed_at = CURRENT_TIMESTAMP
                        WHERE id = ?
                    """, (rid,))
            conn.commit()
            return True

    def get_queue_summary(self, target_url: Optional[str] = None) -> Dict[str, int]:
        """Returns counts by status (pending, imported, duplicates, rejected, total)."""
        norm_url = self._normalize_url(target_url) if target_url else None
        with self._get_connection() as conn:
            where_clause = "WHERE target_url = ?" if norm_url else ""
            params = [norm_url] if norm_url else []

            rows = conn.execute(f"""
                SELECT status, COUNT(*) as cnt
                FROM staging_leads
                {where_clause}
                GROUP BY status
            """, params).fetchall()

            counts = {
                "total": 0,
                "pending": 0,
                "imported": 0,
                "duplicate": 0,
                "rejected": 0
            }

            for r in rows:
                st = (r["status"] or "").lower()
                cnt = r["cnt"]
                counts["total"] += cnt
                if st == "pending" or st == "validated":
                    counts["pending"] += cnt
                elif st == "imported":
                    counts["imported"] = cnt
                elif st == "duplicate":
                    counts["duplicate"] = cnt
                elif st == "rejected":
                    counts["rejected"] = cnt

            return counts

    def get_staged_leads(
        self,
        target_url: Optional[str] = None,
        status: Optional[str] = None,
        limit: int = 100
    ) -> List[Dict[str, Any]]:
        """Returns list of staged leads for frontend inspection."""
        with self._get_connection() as conn:
            query = "SELECT * FROM staging_leads WHERE 1=1"
            params: List[Any] = []

            if target_url:
                query += " AND target_url = ?"
                params.append(self._normalize_url(target_url))

            if status and status.upper() != "ALL":
                query += " AND status = ?"
                params.append(status.upper())

            query += " ORDER BY created_at DESC LIMIT ?"
            params.append(limit)

            rows = conn.execute(query, params).fetchall()
            output = []
            for r in rows:
                output.append({
                    "id": r["id"],
                    "targetUrl": r["target_url"],
                    "sourceUrl": r["source_url"],
                    "pageNum": r["page_num"],
                    "companyName": r["company_name"],
                    "contactPerson": r["contact_person"],
                    "mobileNo": r["mobile_raw"],
                    "altMobileNo": r["alt_mobile_raw"],
                    "email": r["email_raw"],
                    "city": r["city_raw"],
                    "state": r["state_raw"],
                    "location": r["gps_raw"],
                    "website": r["website_raw"],
                    "status": r["status"],
                    "validationError": r["validation_error"],
                    "crmContactId": r["crm_contact_id"],
                    "createdAt": r["created_at"],
                    "processedAt": r["processed_at"]
                })
            return output
