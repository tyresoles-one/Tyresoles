"""
Production Anti-Fake & Anti-Mock Validator for Tyresoles & Ecoflex Leads.
Enforces strict checks to guarantee that zero mock, dummy, or fake contacts reach the CRM.
"""

import re
import logging
from typing import Dict, Any, List, Tuple, Optional

logger = logging.getLogger(__name__)

# Known dummy/fake phone patterns
KNOWN_FAKE_PHONES = {
    "9999999999", "8888888888", "7777777777", "6666666666",
    "9876543210", "1234567890", "0123456789", "9812345678",
    "9822011223", "9800000000", "9123456789"
}

# Blacklisted placeholder keywords in company / person names
FORBIDDEN_NAME_KEYWORDS = [
    "sample", "dummy", "test", "demo", "placeholder", "n/a", "na", "null",
    "unknown", "example", "enterprise for", "sample enterprise", "my company",
    "abc transport", "xyz logistics", "fake", "mock", "temp"
]

# Blacklisted placeholder addresses
FORBIDDEN_ADDRESS_KEYWORDS = [
    "local hub", "n/a", "address not found", "sample address", "test address",
    "unknown address", "dummy", "example street"
]

GSTIN_REGEX = re.compile(r'^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$')

class LeadValidator:
    """Strict gatekeeper that filters out fake, incomplete, or mock leads."""

    @staticmethod
    def clean_phone(phone: Optional[str]) -> str:
        """Extracts and normalizes an Indian phone number to 10 digits."""
        if not phone:
            return ""
        # Remove all non-digits
        digits = re.sub(r'[^\d]', '', str(phone))
        # Handle country code prefixes
        if digits.startswith("91") and len(digits) == 12:
            digits = digits[2:]
        elif digits.startswith("0") and len(digits) == 11:
            digits = digits[1:]
        elif len(digits) > 10:
            digits = digits[-10:]
        return digits

    @classmethod
    def validate_indian_mobile(cls, phone: Optional[str]) -> Tuple[bool, str]:
        """
        Validates whether a number is a genuine, active Indian mobile series.
        Valid Indian mobile numbers: exactly 10 digits starting with 6, 7, 8, or 9.
        """
        digits = cls.clean_phone(phone)
        if not digits:
            return False, "Empty or missing phone number"

        if len(digits) != 10:
            return False, f"Invalid length ({len(digits)} digits, expected 10)"

        if digits[0] not in {'6', '7', '8', '9'}:
            return False, f"Invalid Indian mobile starting digit '{digits[0]}'"

        if digits in KNOWN_FAKE_PHONES:
            return False, f"Matches known fake/test number '{digits}'"

        # Check for repetitive identical digits (e.g., 9999999999)
        if len(set(digits)) <= 2:
            return False, "Suspicious repetitive digits (<=2 unique characters)"

        # Check for long runs of the same digit (e.g., 9800000012)
        if re.search(r'(\d)\1{5,}', digits):
            return False, "Contains 6+ identical consecutive digits"

        # Check for simple ascending or descending sequences
        if digits in "01234567890123456789" or digits in "98765432109876543210":
            return False, "Sequential number detected"

        return True, digits

    @classmethod
    def validate_business_name(cls, name: Optional[str]) -> Tuple[bool, str]:
        """Validates business name authenticity."""
        if not name:
            return False, "Empty company name"

        cleaned = name.strip()
        if len(cleaned) < 3:
            return False, f"Company name too short ('{cleaned}')"

        lower_name = cleaned.lower()
        for kw in FORBIDDEN_NAME_KEYWORDS:
            # Word boundary search or exact phrase match
            if re.search(rf'\b{re.escape(kw)}\b', lower_name):
                return False, f"Company name contains forbidden mock keyword '{kw}'"

        return True, cleaned

    @classmethod
    def validate_address(cls, address: Optional[str]) -> Tuple[bool, str]:
        """Ensures address is not a placeholder."""
        if not address:
            return False, "Empty address"

        cleaned = address.strip()
        lower_addr = cleaned.lower()
        for kw in FORBIDDEN_ADDRESS_KEYWORDS:
            if kw == lower_addr or lower_addr.startswith(kw):
                return False, f"Address is a placeholder ('{kw}')"

        return True, cleaned

    @classmethod
    def validate_gstin(cls, gstin: Optional[str]) -> bool:
        """Validates 15-character GSTIN format."""
        if not gstin:
            return False
        return bool(GSTIN_REGEX.match(gstin.strip().upper()))

    @classmethod
    def validate_lead(cls, lead: Dict[str, Any], allow_no_phone: bool = False) -> Tuple[bool, str]:
        """
        Performs full gatekeeper validation on a lead record.
        Returns (True, '') if valid, or (False, rejection_reason).
        """
        # 1. Company / Person Name Check
        company_name = lead.get("companyName")
        full_name = lead.get("fullName")
        valid_name, name_res = cls.validate_business_name(company_name)
        if not valid_name:
            if allow_no_phone and full_name and len(full_name.strip()) >= 3:
                # Valid individual person prospect on LinkedIn
                pass
            else:
                return False, f"Invalid Company Name: {name_res}"

        # 2. Phone Number Check
        phone = lead.get("mobileNo")
        if phone:
            valid_phone, phone_res = cls.validate_indian_mobile(phone)
            if not valid_phone:
                if not allow_no_phone:
                    return False, f"Invalid Mobile Number: {phone_res}"
        elif not allow_no_phone:
            return False, "Invalid Mobile Number: Empty or missing phone number"

        # 3. Address Check
        raw_addr = lead.get("address", "")
        if raw_addr:
            valid_addr, addr_res = cls.validate_address(raw_addr)
            if not valid_addr:
                return False, f"Invalid Address: {addr_res}"

        # 4. Check for mock tags or notes
        notes = (lead.get("notes") or "").lower()
        for kw in ["mock", "sample enterprise", "dummy data"]:
            if kw in notes:
                return False, f"Notes contain mock indicator '{kw}'"

        return True, "Valid"

    @classmethod
    def deduplicate(cls, leads: List[Dict[str, Any]], existing_phones: Optional[set] = None) -> List[Dict[str, Any]]:
        """Deduplicates leads by normalized 10-digit mobile number, source URL, or company + contact name."""
        seen_phones = set(existing_phones or set())
        seen_urls = set()
        seen_people = set()
        unique_leads = []

        for lead in leads:
            phone = cls.clean_phone(lead.get("mobileNo"))
            raw_name = (lead.get("companyName") or "").strip().lower()
            full_name = (lead.get("fullName") or "").strip().lower()
            source_url = (lead.get("sourceUrl") or "").strip().lower()

            if phone:
                if phone in seen_phones:
                    logger.debug(f"Skipping duplicate phone: {phone} ({lead.get('companyName')})")
                    continue
                seen_phones.add(phone)
            else:
                # Deduplicate by LinkedIn/web URL or person + company for leads without phone
                if source_url and source_url in seen_urls:
                    continue
                if full_name and raw_name and (full_name, raw_name) in seen_people:
                    continue

            if source_url:
                seen_urls.add(source_url)
            if full_name and raw_name:
                seen_people.add((full_name, raw_name))

            unique_leads.append(lead)

        return unique_leads
