"""
Unit tests for Anti-Fake and Anti-Mock Validator.
"""

from validator import LeadValidator

def test_mobile_validation():
    # Valid Indian mobile numbers
    valid_numbers = [
        "+91 98220 54321",
        "09845123987",
        "91-8765412390",
        "7012345678",
        "6361234567"
    ]
    for num in valid_numbers:
        is_val, res = LeadValidator.validate_indian_mobile(num)
        assert is_val, f"Failed for valid number {num}: {res}"
        assert len(res) == 10 and res[0] in {'6', '7', '8', '9'}

    # Invalid / Fake / Mock numbers
    invalid_numbers = [
        "9999999999",       # repetitive
        "9876543210",       # sequential / fake
        "1234567890",       # invalid start
        "0222456789",       # landline (9 digits after 0)
        "9822011223",       # known old mock
        "9800000012",       # 6 identical consecutive digits
        "555-1234",         # invalid length & start
        "",                 # empty
        None                # null
    ]
    for num in invalid_numbers:
        is_val, reason = LeadValidator.validate_indian_mobile(num)
        assert not is_val, f"Invalid number passed validation: {num} (Reason: {reason})"

def test_company_name_validation():
    # Valid names
    valid_names = [
        "Balaji Roadlines & Transport",
        "Shree Ganesh Logistics Pvt Ltd",
        "Vijay Retreading Hub",
        "MRF Tyre Centre - Belgaum"
    ]
    for name in valid_names:
        is_val, res = LeadValidator.validate_business_name(name)
        assert is_val, f"Valid name failed: {name} ({res})"

    # Fake / Mock / Placeholder names
    invalid_names = [
        "Sample Enterprise for Transporters",
        "Dummy Company",
        "Test Logistics",
        "Demo",
        "N/A",
        "ab",
        "",
        None
    ]
    for name in invalid_names:
        is_val, res = LeadValidator.validate_business_name(name)
        assert not is_val, f"Invalid name passed: {name}"

def test_lead_deduplication():
    leads = [
        {"companyName": "ABC Fleet", "mobileNo": "9845012345"},
        {"companyName": "ABC Fleet", "mobileNo": "+91 98450 12345"}, # Duplicate phone
        {"companyName": "XYZ Fleet", "mobileNo": "9845012346"},
    ]
    unique = LeadValidator.deduplicate(leads)
    assert len(unique) == 2, f"Expected 2 unique leads, got {len(unique)}"

if __name__ == "__main__":
    print("Running validator test suite...")
    test_mobile_validation()
    print("[PASS] Mobile validation tests passed.")
    test_company_name_validation()
    print("[PASS] Business name validation tests passed.")
    test_lead_deduplication()
    print("[PASS] Deduplication tests passed.")
    print("\nALL ANTI-MOCK VALIDATOR TESTS PASSED SUCCESSFULLY!")
