"""
Test Suite for Universal Web URL Scraper & Lead Harvester.
Tests:
1. URL extraction from category pages
2. Multi-page pagination (with & without next button)
3. Detail page extraction (Company, Contact Person, Phone, Address, Coordinates, Website)
4. Anti-fake validation & deduplication gatekeeper
5. CRM payload formatting
"""

import sys
import os
import unittest
import logging

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(name)s: %(message)s")

from scrapers.web_url_scraper import WebUrlScraper
from validator import LeadValidator

class TestWebUrlScraper(unittest.TestCase):

    def setUp(self):
        self.scraper = WebUrlScraper(timeout=20, delay_between_requests=0.2)
        self.test_category_url = "https://transportfamily.com/listing-category/trailer-container-movement/"
        self.test_single_listing_url = "https://transportfamily.com/listing/karnataka-transport-service-2/"

    def test_single_listing_detection(self):
        self.assertTrue(self.scraper.is_single_listing_url(self.test_single_listing_url))
        self.assertFalse(self.scraper.is_single_listing_url(self.test_category_url))

    def test_extract_single_listing_details(self):
        lead = self.scraper.extract_lead_details(self.test_single_listing_url)
        self.assertIsNotNone(lead)
        self.assertTrue("KARNATAKA TRANSPORT SERVICE" in lead["companyName"].upper())
        self.assertTrue(bool(lead["mobileNo"]))
        self.assertEqual(len(lead["mobileNo"]), 10)
        self.assertTrue(lead["mobileNo"][0] in {'6', '7', '8', '9'})
        self.assertTrue(bool(lead["address"]))
        self.assertTrue("Chennai" in lead["city"] or "Tamil Nadu" in lead["state"])
        self.assertTrue(bool(lead["location"]))  # Lat, lon
        self.assertTrue("kts.transportindia.co.in" in lead["website"])

        # Test validation
        is_valid, reason = LeadValidator.validate_lead(lead)
        self.assertTrue(is_valid, f"Lead failed validation: {reason}")

    def test_multi_page_pagination(self):
        # Request 18 listings (requires at least 2 pages since each page has ~14)
        urls = self.scraper.scrape_listing_urls(self.test_category_url, max_results=18)
        self.assertGreaterEqual(len(urls), 15, "Should successfully paginate beyond page 1")
        # Ensure all URLs are unique
        self.assertEqual(len(urls), len(set(urls)), "Listing URLs must be unique")

    def test_scrape_and_validate_leads(self):
        # Scrape 5 leads end-to-end
        leads = self.scraper.scrape(self.test_category_url, limit=5, division="Tyresoles", product="Commercial Retreading")
        self.assertGreater(len(leads), 0, "Should harvest at least 1 genuine verified lead")

        for lead in leads:
            is_valid, reason = LeadValidator.validate_lead(lead)
            self.assertTrue(is_valid, f"Harvested lead failed gatekeeper: {reason}")
            self.assertEqual(lead["division"], "Tyresoles")
            self.assertEqual(lead["targetProduct"], "Commercial Retreading")
            self.assertTrue(bool(lead["companyName"]))
            self.assertTrue(bool(lead["mobileNo"]))

if __name__ == "__main__":
    unittest.main()
