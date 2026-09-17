"""
GraphQL CRM API Client for pushing harvested leads to Tyresoles CRM
"""

import os
import json
import logging
import urllib.request
from typing import List, Dict, Any, Optional

logger = logging.getLogger(__name__)

IMPORT_MUTATION = """
mutation ImportHarvestedLeads($leads: [HarvestedLeadInput!]!) {
  importHarvestedLeads(leads: $leads) {
    totalSubmitted
    importedCount
    duplicateCount
    skippedCount
    messages
  }
}
"""

class CrmApiClient:
    def __init__(self, endpoint_url: Optional[str] = None, auth_token: str = ""):
        self.endpoint_url = endpoint_url or os.environ.get("CRM_ENDPOINT", "http://localhost:5001/graphql")
        self.auth_token = auth_token or os.environ.get("CRM_AUTH_TOKEN", "")

    def import_leads(self, leads: List[Dict[str, Any]]) -> Dict[str, Any]:
        """
        Sends a batch of harvested leads to the Tyresoles GraphQL CRM endpoint.
        """
        if not leads:
            return {"totalSubmitted": 0, "importedCount": 0, "duplicateCount": 0, "skippedCount": 0, "messages": ["Empty lead batch."]}

        payload = {
            "query": IMPORT_MUTATION,
            "variables": {
                "leads": leads
            }
        }

        headers = {
            "Content-Type": "application/json"
        }
        if self.auth_token:
            headers["Authorization"] = f"Bearer {self.auth_token}"

        try:
            req = urllib.request.Request(
                self.endpoint_url,
                data=json.dumps(payload).encode("utf-8"),
                headers=headers,
                method="POST"
            )
            with urllib.request.urlopen(req, timeout=30) as response:
                result = json.loads(response.read().decode("utf-8"))
                if "errors" in result:
                    logger.error(f"GraphQL errors: {result['errors']}")
                    return {"success": False, "errors": result["errors"]}
                return result.get("data", {}).get("importHarvestedLeads", {})
        except Exception as e:
            logger.error(f"Failed to post leads to CRM API: {e}")
            return {"success": False, "error": str(e)}
