"""
AI Lead Enrichment & Scoring Module for Tyresoles & Ecoflex.
Extracts structured contact personas, computes quality scores, and enriches B2B context.
Supports:
1. Google Gemini Flash API (if GEMINI_API_KEY is set)
2. Local Ollama LLM (if running)
3. Heuristic NLP Entity Parser (deterministic rule-based intelligence)
"""

import os
import re
import json
import logging
import urllib.request
from typing import Dict, Any, Optional
from validator import LeadValidator

logger = logging.getLogger(__name__)

class AILeadEnricher:
    def __init__(self, ollama_url: str = "http://localhost:11434/api/generate", model_name: str = "llama3.2"):
        self.ollama_url = ollama_url
        self.model_name = model_name
        self.gemini_api_key = os.environ.get("GEMINI_API_KEY", "")
        self._ollama_online = self._check_ollama_health()

    def _check_ollama_health(self) -> bool:
        """Checks if local Ollama daemon is responsive with 500ms timeout."""
        try:
            req = urllib.request.Request(self.ollama_url.replace("/api/generate", "/api/tags"), method="GET")
            with urllib.request.urlopen(req, timeout=0.5) as resp:
                return resp.status == 200
        except Exception:
            return False

    def enrich_and_score(
        self,
        raw_listing: Dict[str, Any],
        division: str,
        target_product: str,
        city: str
    ) -> Dict[str, Any]:
        """
        Enriches a raw listing with standardized contact properties, quality scores, and tags.
        """
        raw_name = raw_listing.get("name", "").strip()
        raw_phone = raw_listing.get("phone", "").strip()
        raw_address = raw_listing.get("address", "").strip()
        raw_snippet = raw_listing.get("raw_snippet", "")
        source_url = raw_listing.get("source_url", "")
        rating = raw_listing.get("rating")
        reviews_count = raw_listing.get("reviews_count", 0)
        website = raw_listing.get("website", "")

        # 1. Phone number cleanup
        phone_digits = LeadValidator.clean_phone(raw_phone)

        # 2. Extract AI data or Heuristic
        ai_data = None
        if self.gemini_api_key:
            ai_data = self._call_gemini_parser(raw_name, raw_snippet, division, target_product)
        elif self._ollama_online:
            ai_data = self._call_ollama_parser(raw_name, raw_snippet, division, target_product)


        if not ai_data:
            ai_data = self._heuristic_parser(
                name=raw_name,
                phone=phone_digits,
                address=raw_address,
                rating=rating,
                reviews_count=reviews_count,
                website=website,
                division=division,
                target_product=target_product,
                city=city
            )

        contact_person = ai_data.get("contact_person") or raw_name or "Commercial Contact"
        quality_score = ai_data.get("quality_score", 0.75)
        notes = ai_data.get("notes", f"Verified lead for {target_product} in {city}.")

        return {
            "fullName": contact_person,
            "companyName": raw_name,
            "mobileNo": phone_digits,
            "mobileNo2": ai_data.get("mobile2", ""),
            "emailIds": ai_data.get("email", ""),
            "address": raw_address,
            "city": city,
            "state": ai_data.get("state", ""),
            "division": division,
            "targetProduct": target_product,
            "leadSourceType": "Automated",
            "leadSourceChannel": raw_listing.get("category", "Directory-Search"),
            "sourceUrl": source_url,
            "website": website,
            "rating": rating,
            "reviewsCount": reviews_count,
            "qualityScore": quality_score,
            "scrapingQuery": raw_listing.get("query", ""),
            "tags": f"Automated, Source:Live-Harvest, Division:{division}, Product:{target_product}, City:{city}",
            "notes": notes,
            "contactType": "Lead",
            "location": raw_listing.get("location", "")
        }

    def _is_ollama_available(self) -> bool:
        try:
            req = urllib.request.Request(self.ollama_url.replace("/api/generate", "/api/tags"), method="GET")
            with urllib.request.urlopen(req, timeout=1) as resp:
                return resp.status == 200
        except Exception:
            return False

    def _call_gemini_parser(self, name: str, snippet: str, division: str, target_product: str) -> Optional[Dict[str, Any]]:
        """Parses snippet using Gemini API."""
        try:
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={self.gemini_api_key}"
            prompt = f"""
You are a B2B sales intelligence agent for {division} in India offering {target_product}.
Extract structured contact information from this text.
Business Name: {name}
Details: {snippet}

Return strict JSON only:
{{
  "contact_person": "Extracted owner/manager name or null",
  "quality_score": 0.85,
  "notes": "Brief one sentence commercial summary",
  "state": "State name if mentioned"
}}
"""
            payload = {
                "contents": [{"parts": [{"text": prompt}]}],
                "generationConfig": {"response_mime_type": "application/json"}
            }
            req = urllib.request.Request(url, data=json.dumps(payload).encode("utf-8"), headers={"Content-Type": "application/json"}, method="POST")
            with urllib.request.urlopen(req, timeout=5) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                text_res = data["candidates"][0]["content"]["parts"][0]["text"]
                return json.loads(text_res)
        except Exception as e:
            logger.debug(f"Gemini API call skipped: {e}")
            return None

    def _call_ollama_parser(self, name: str, snippet: str, division: str, target_product: str) -> Optional[Dict[str, Any]]:
        """Calls local Ollama instance."""
        try:
            prompt = f"""
You are a B2B sales intelligence agent for {division} offering {target_product}.
Extract structured lead info from this text in JSON:
Name/Text: {name} | {snippet}

Return JSON with keys:
"contact_person": string or null,
"quality_score": float between 0.5 and 1.0,
"notes": brief one-sentence reason why this is a good prospective buyer.
"""
            req = urllib.request.Request(
                self.ollama_url,
                data=json.dumps({"model": self.model_name, "prompt": prompt, "format": "json", "stream": False}).encode("utf-8"),
                headers={"Content-Type": "application/json"},
                method="POST"
            )
            with urllib.request.urlopen(req, timeout=4) as resp:
                data = json.loads(resp.read().decode("utf-8"))
                return json.loads(data.get("response", "{}"))
        except Exception:
            return None

    def _heuristic_parser(
        self,
        name: str,
        phone: str,
        address: str,
        rating: Optional[float],
        reviews_count: int,
        website: str,
        division: str,
        target_product: str,
        city: str
    ) -> Dict[str, Any]:
        """Deterministic rule-based NLP parser and commercial intent scoring."""
        score = 0.60

        # Quality scoring based on verified business signals
        if phone and len(phone) == 10:
            score += 0.15
        if address and len(address) > 15:
            score += 0.10
        if rating and rating >= 4.0:
            score += 0.05
        if reviews_count and reviews_count >= 10:
            score += 0.05
        if website:
            score += 0.05

        # Infer contact person name if mentioned in business title (e.g. "Ramesh Patel Transport", "Sharma Logistics")
        contact_person = name
        prop_match = re.search(r'^([A-Z][a-z]+ [A-Z][a-z]+)\s+(Transport|Logistics|Tyres|Enterprises|Roadlines)', name)
        if prop_match:
            contact_person = prop_match.group(1)

        if division == "Ecoflex":
            note = f"Prospective commercial facility in {city} for {target_product}."
        else:
            note = f"Active fleet/commercial operator in {city} for {target_product}."

        return {
            "contact_person": contact_person,
            "quality_score": min(round(score, 2), 1.0),
            "notes": note,
            "state": ""
        }
