"""
Unit Tests for Decoupled Crawl Pipeline and Checkpoint Store.
"""

import os
import unittest
import tempfile
import shutil

from staging_store import StagingStore
from fetcher_process import FetcherProcess
from ingest_worker_process import IngestWorkerProcess


class TestCrawlPipeline(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.mkdtemp()
        self.db_path = os.path.join(self.temp_dir, "test_staging.db")
        self.store = StagingStore(db_path=self.db_path)
        self.test_url = "https://transportfamily.com/listing-category/trailer-container-movement/"

    def tearDown(self):
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_checkpoint_initial_state(self):
        """Verify initial checkpoint for unseen URL starts at Page 1."""
        cp = self.store.get_checkpoint(self.test_url)
        self.assertEqual(cp["last_crawled_page"], 0)
        self.assertEqual(cp["next_page_to_crawl"], 1)
        self.assertEqual(cp["has_reached_end"], False)
        self.assertEqual(cp["queue_summary"]["pending"], 0)

    def test_checkpoint_advancement_and_reset(self):
        """Verify advancing checkpoint to Page 1, then Page 2, and resetting."""
        # Crawl Page 1 -> next should be Page 2
        cp1 = self.store.update_checkpoint(self.test_url, last_page=1, new_listings_count=14)
        self.assertEqual(cp1["last_crawled_page"], 1)
        self.assertEqual(cp1["next_page_to_crawl"], 2)
        self.assertEqual(cp1["total_pages_crawled"], 1)
        self.assertEqual(cp1["total_listings_discovered"], 14)

        # Crawl Page 2 -> next should be Page 3
        cp2 = self.store.update_checkpoint(self.test_url, last_page=2, new_listings_count=10)
        self.assertEqual(cp2["last_crawled_page"], 2)
        self.assertEqual(cp2["next_page_to_crawl"], 3)
        self.assertEqual(cp2["total_pages_crawled"], 2)
        self.assertEqual(cp2["total_listings_discovered"], 24)

        # Reset -> should restore next_page to 1
        cp_reset = self.store.reset_checkpoint(self.test_url)
        self.assertEqual(cp_reset["last_crawled_page"], 0)
        self.assertEqual(cp_reset["next_page_to_crawl"], 1)

    def test_enqueue_and_status_transitions(self):
        """Verify adding raw leads to staging buffer and consuming them."""
        lead1 = {
            "companyName": "Sharma Fast Freight",
            "fullName": "Vikram Sharma",
            "mobileNo": "9823012345",
            "sourceUrl": "https://transportfamily.com/listing/sharma-fast-freight/",
            "city": "Pune",
            "state": "Maharashtra"
        }
        lead2 = {
            "companyName": "Fake Logistics 999",
            "fullName": "Demo User",
            "mobileNo": "9999999999",  # Anti-mock should reject
            "sourceUrl": "https://transportfamily.com/listing/fake-logistics/",
            "city": "Mumbai"
        }

        # Enqueue raw leads
        ok1, id1 = self.store.enqueue_raw_lead(self.test_url, page_num=1, lead_data=lead1)
        ok2, id2 = self.store.enqueue_raw_lead(self.test_url, page_num=1, lead_data=lead2)
        self.assertTrue(ok1)
        self.assertTrue(ok2)

        summary = self.store.get_queue_summary(self.test_url)
        self.assertEqual(summary["pending"], 2)
        self.assertEqual(summary["total"], 2)

        # Run Consumer / Ingest Worker (Dry Run)
        worker = IngestWorkerProcess(store=self.store)
        res = worker.run_ingest_batch(target_url=self.test_url, batch_limit=10, dry_run=True)

        self.assertEqual(res["processed"], 2)
        self.assertEqual(res["imported"], 1)  # lead1 is genuine
        self.assertEqual(res["rejected"], 1)  # lead2 rejected because 9999999999 is mock
        self.assertEqual(res["pending_remaining"], 0)

        # Check final queue summary
        final_summary = self.store.get_queue_summary(self.test_url)
        self.assertEqual(final_summary["pending"], 0)
        self.assertEqual(final_summary["rejected"], 1)


if __name__ == "__main__":
    unittest.main()
