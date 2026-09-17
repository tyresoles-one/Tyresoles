"""
Product Catalog and Lead Harvesting Configuration for Tyresoles & Ecoflex
"""

DIVISIONS = {
    "Tyresoles": {
        "name": "Tyresoles India Pvt. Ltd.",
        "industry": "Commercial Tyre Retreading, Fleet Maintenance & OTR Solutions",
        "products": {
            "Commercial Retreading": {
                "code": "TYRE_RETREAD_COMMERCIAL",
                "name": "Commercial Retreading (Pre-Cure & Mould-Cure)",
                "target_personas": ["Fleet Owners", "Transport Contractors", "Logistics Hubs", "Bus Operators"],
                "query_templates": [
                    "Transporters in {city}",
                    "Logistics fleet operators in {city}",
                    "Transport companies in {city} Transport Nagar",
                    "Private bus travel operators in {city}",
                    "Container trailer transport in {city}"
                ],
                "default_tags": ["Commercial-Retreading", "Fleet-Owner", "Transport-Nagar"]
            },
            "Radial Retreading": {
                "code": "TYRE_RETREAD_RADIAL",
                "name": "Radial Tubeless Retreading (295/80R22.5, 11R22.5)",
                "target_personas": ["Interstate Multi-Axle Fleet Owners", "Express Cargo Operators"],
                "query_templates": [
                    "Express cargo logistics in {city}",
                    "Interstate multi axle transport in {city}",
                    "Cold chain logistics transport in {city}"
                ],
                "default_tags": ["Radial-Retreading", "Tubeless", "Multi-Axle"]
            },
            "OTR & Mining Retreading": {
                "code": "TYRE_RETREAD_OTR",
                "name": "OTR, Earthmover & Mining Retreading",
                "target_personas": ["Quarry Operators", "Mining Contractors", "RMC Suppliers", "Tipper Fleets"],
                "query_templates": [
                    "Stone quarry contractors in {city}",
                    "Ready mix concrete suppliers in {city}",
                    "Earthmoving and tipper contractors in {city}",
                    "Mining transport contractors in {city}"
                ],
                "default_tags": ["OTR-Retreading", "Quarry", "Mining", "Tippers", "RMC"]
            },
            "Ready-Retreaded Tyres (RRT)": {
                "code": "TYRE_RRT_SALE",
                "name": "Ready-Retreaded Tyres (RRT / Direct Units)",
                "target_personas": ["Commercial Tyre Dealers", "Truck Tyre Retailers", "Spare Parts Shops"],
                "query_templates": [
                    "Commercial tyre dealers in {city}",
                    "Truck tyre dealers in {city}",
                    "Tractor tyre dealers in {city}",
                    "Used tyre dealers and retreaders in {city}"
                ],
                "default_tags": ["RRT", "Tyre-Dealer", "Retail-Partner"]
            }
        }
    },
    "Ecoflex": {
        "name": "Ecoflex (Surfaces & Flooring Division)",
        "industry": "Certified Sports, Safety & Specialty Rubber Flooring",
        "products": {
            "Tuffloor": {
                "code": "ECO_TUFFLOOR_GYM",
                "name": "Tuffloor® - Gym & Fitness Rubber Tiles",
                "target_personas": ["Gym Owners", "CrossFit Box Owners", "Fitness Centers", "Hotel Gyms"],
                "query_templates": [
                    "Gym and fitness centers in {city}",
                    "CrossFit gym arena in {city}",
                    "Clubhouse gym facility management in {city}",
                    "Fitness studios in {city}"
                ],
                "default_tags": ["Tuffloor", "Gym-Flooring", "Fitness-Center"]
            },
            "PlaySafe": {
                "code": "ECO_PLAYSAFE_PLAYGROUND",
                "name": "PlaySafe® (EPDM & SBR) - Playground Safety Flooring",
                "target_personas": ["Pre-Schools", "International Schools", "Daycares", "Gated Communities (RWAs)"],
                "query_templates": [
                    "Pre schools and play schools in {city}",
                    "International CBSE IB schools in {city}",
                    "Playground equipment suppliers in {city}",
                    "Children play area and parks in {city}"
                ],
                "default_tags": ["PlaySafe", "Playground-Flooring", "Pre-Schools", "EPDM"]
            },
            "Runathon": {
                "code": "ECO_RUNATHON_TRACKS",
                "name": "Runathon® - Jogging & Walking Tracks",
                "target_personas": ["Township Developers", "Sports Stadiums", "Landscape Architects", "Municipal Parks"],
                "query_templates": [
                    "Residential township developers in {city}",
                    "Sports complexes and stadiums in {city}",
                    "Landscape architects in {city}",
                    "Jogging track contractors in {city}"
                ],
                "default_tags": ["Runathon", "Jogging-Track", "Township", "Landscape"]
            },
            "Herculan Sports": {
                "code": "ECO_HERCULAN_SPORTS",
                "name": "Herculan Sports - Multi-Purpose Sports Surfaces",
                "target_personas": ["Sports Clubs", "Badminton Arenas", "Basketball Academies", "Futsal Turf Owners"],
                "query_templates": [
                    "Indoor badminton arena and clubs in {city}",
                    "Basketball courts and sports academies in {city}",
                    "Turf and sports court contractors in {city}",
                    "Indoor sports clubs in {city}"
                ],
                "default_tags": ["Herculan-Sports", "Badminton", "Basketball", "Multi-Sport"]
            },
            "Herculan IG Decorative": {
                "code": "ECO_HERCULAN_DECORATIVE",
                "name": "Herculan IG Decorative - Commercial & Architectural Flooring",
                "target_personas": ["Commercial Interior Designers", "Corporate Architects", "Shopping Malls"],
                "query_templates": [
                    "Commercial interior designers in {city}",
                    "Corporate architects in {city}",
                    "Shopping mall facility management in {city}"
                ],
                "default_tags": ["Herculan-Decorative", "Architects", "Commercial-Interior"]
            }
        }
    }
}

BRANCH_RESP_CENTERS = {
    "MUMBAI": "MUMBAI",
    "NAVI MUMBAI": "MUMBAI",
    "THANE": "MUMBAI",
    "BHIWANDI": "MUMBAI",
    "PUNE": "PUNE",
    "NASHIK": "PUNE",
    "AURANGABAD": "PUNE",
    "AHMEDABAD": "AHMEDABAD",
    "SURAT": "AHMEDABAD",
    "VADODARA": "AHMEDABAD",
    "RAJKOT": "AHMEDABAD",
    "BELGAUM": "BELGAUM",
    "BELAGAVI": "BELGAUM",
    "HUBLI": "BELGAUM",
    "DHARWAD": "BELGAUM",
    "HYDERABAD": "HYDERABAD",
    "SECUNDERABAD": "HYDERABAD",
    "MANGALORE": "MANGALORE",
    "UDUPI": "MANGALORE",
    "BANGALORE": "BANGALORE",
    "BENGALURU": "BANGALORE",
    "CHENNAI": "CHENNAI"
}

def resolve_resp_center(city: str) -> str:
    if not city:
        return "HO"
    key = city.strip().upper()
    return BRANCH_RESP_CENTERS.get(key, "HO")
