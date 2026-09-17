const { spawn } = require('child_process');
const http = require('http');
const fs = require('fs');
const os = require('os');

const CITY_CENTROIDS = {
    pune: [18.5204, 73.8567],
    mumbai: [19.0760, 72.8777],
    bhiwandi: [19.2967, 73.0631],
    thane: [19.2183, 72.9781],
    'navi mumbai': [19.0330, 73.0297],
    belgaum: [15.8497, 74.4977],
    belagavi: [15.8497, 74.4977],
    bangalore: [12.9716, 77.5946],
    bengaluru: [12.9716, 77.5946],
    hubli: [15.3647, 75.1240],
    dharwad: [15.4589, 75.0078],
    ahmedabad: [23.0225, 72.5714],
    surat: [21.1702, 72.8311],
    vadodara: [22.3072, 73.1812],
    nagpur: [21.1458, 79.0882],
    nashik: [19.9975, 73.7898],
    aurangabad: [19.8762, 75.3433],
    'chhatrapati sambhajinagar': [19.8762, 75.3433],
    solapur: [17.6599, 75.9064],
    kolhapur: [16.7050, 74.2433],
    hyderabad: [17.3850, 78.4867],
    chennai: [13.0827, 80.2707],
    delhi: [28.6139, 77.2090],
    jaipur: [26.9124, 75.7873],
    indore: [22.7196, 75.8577]
};

const SATELLITE_CLUSTERS = {
    pune: [
        { name: "Bhosari MIDC", distKm: 15 },
        { name: "Chakan MIDC", distKm: 30 },
        { name: "Talegaon MIDC", distKm: 35 },
        { name: "Hadapsar", distKm: 12 },
        { name: "Ranjangaon MIDC", distKm: 50 },
        { name: "Shirwal", distKm: 52 },
        { name: "Hinjewadi", distKm: 18 }
    ],
    belgaum: [
        { name: "Udyambag", distKm: 5 },
        { name: "Machhe Industrial Estate", distKm: 10 },
        { name: "Kakati Industrial Area", distKm: 12 },
        { name: "Honaga", distKm: 15 },
        { name: "Desur", distKm: 15 },
        { name: "Bailhongal", distKm: 45 },
        { name: "Gokak", distKm: 55 },
        { name: "Nipani", distKm: 65 }
    ],
    belagavi: [
        { name: "Udyambag", distKm: 5 },
        { name: "Machhe Industrial Estate", distKm: 10 },
        { name: "Kakati Industrial Area", distKm: 12 },
        { name: "Honaga", distKm: 15 },
        { name: "Desur", distKm: 15 },
        { name: "Bailhongal", distKm: 45 },
        { name: "Gokak", distKm: 55 },
        { name: "Nipani", distKm: 65 }
    ],
    mumbai: [
        { name: "Bhiwandi", distKm: 30 },
        { name: "Navi Mumbai Turbhe", distKm: 22 },
        { name: "Taloja MIDC", distKm: 40 },
        { name: "Panvel", distKm: 45 },
        { name: "Vasai", distKm: 45 }
    ],
    bangalore: [
        { name: "Peenya Industrial Area", distKm: 15 },
        { name: "Bommasandra Jigani", distKm: 25 },
        { name: "Bidadi Industrial Area", distKm: 35 },
        { name: "Nelamangala", distKm: 30 },
        { name: "Hosur", distKm: 40 }
    ],
    surat: [
        { name: "Sachin GIDC", distKm: 15 },
        { name: "Hazira", distKm: 25 },
        { name: "Pandesara", distKm: 10 },
        { name: "Ankleshwar GIDC", distKm: 60 }
    ],
    ahmedabad: [
        { name: "Changodar", distKm: 20 },
        { name: "Sanand GIDC", distKm: 25 },
        { name: "Naroda GIDC", distKm: 15 },
        { name: "Vatva GIDC", distKm: 12 }
    ]
};

function getZoomForRadius(radiusKm) {
    if (!radiusKm || radiusKm <= 10) return 12;
    if (radiusKm <= 25) return 11;
    if (radiusKm <= 50) return 10;
    if (radiusKm <= 75) return 9;
    return 8;
}

async function scrapeGoogleMaps(query, maxCount = 60, city = "", radiusKm = 0) {
    const chromePath = 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
    const edgePath = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
    const browserExe = fs.existsSync(chromePath) ? chromePath : edgePath;

    // Detect radius from query or city if not explicitly passed
    if (radiusKm === undefined || radiusKm === null || radiusKm <= 0) {
        const radMatch = (query + ' ' + city).match(/(?:within\s*|radius\s*|\b)(\d+)\s*km/i);
        if (radMatch) radiusKm = parseInt(radMatch[1]);
        else radiusKm = 0; // 0 = disabled (strict city search)
    }

    // Resolve city centroid
    let centerLat = null, centerLng = null;
    const cleanCityKey = city.toLowerCase().replace(/\s*\(.*?\)/g, '').trim();
    if (cleanCityKey && CITY_CENTROIDS[cleanCityKey]) {
        [centerLat, centerLng] = CITY_CENTROIDS[cleanCityKey];
    } else {
        for (const [k, v] of Object.entries(CITY_CENTROIDS)) {
            if (query.toLowerCase().includes(k)) {
                [centerLat, centerLng] = v;
                break;
            }
        }
    }

    const tempDir = os.tmpdir() + '\\ts_gmaps_' + Date.now() + '_' + Math.random().toString(36).substring(2, 7);
    const chrome = spawn(browserExe, [
        '--headless=new',
        '--remote-debugging-port=0',
        '--no-sandbox',
        '--disable-gpu',
        '--window-size=1920,1080',
        '--disable-dev-shm-usage',
        `--user-data-dir=${tempDir}`
    ], { stdio: ['ignore', 'pipe', 'pipe'] });

    let port = null;
    for (let i = 0; i < 50; i++) {
        await new Promise(r => setTimeout(r, 100));
        try {
            const portFile = fs.readFileSync(tempDir + '\\DevToolsActivePort', 'utf8');
            port = parseInt(portFile.split('\n')[0].trim());
            if (port > 0) break;
        } catch { }
    }

    if (!port) {
        chrome.kill();
        try { fs.rmSync(tempDir, { recursive: true, force: true }); } catch { }
        throw new Error("Failed to start browser for Google Maps scraping.");
    }

    try {
        const versionRes = await new Promise((resolve, reject) => {
            http.get(`http://127.0.0.1:${port}/json/version`, res => {
                let d = '';
                res.on('data', c => d += c);
                res.on('end', () => resolve(JSON.parse(d)));
            }).on('error', reject);
        });

        const ws = new WebSocket(versionRes.webSocketDebuggerUrl);
        let msgId = 1;
        const pendingCallbacks = new Map();

        ws.onmessage = (event) => {
            try {
                const msg = JSON.parse(event.data);
                if (msg.id && pendingCallbacks.has(msg.id)) {
                    const cb = pendingCallbacks.get(msg.id);
                    pendingCallbacks.delete(msg.id);
                    cb(msg);
                }
            } catch { }
        };

        function call(method, params = {}, sessionId = null) {
            return new Promise((resolve) => {
                const id = msgId++;
                const payload = { id, method, params };
                if (sessionId) payload.sessionId = sessionId;
                pendingCallbacks.set(id, resolve);
                ws.send(JSON.stringify(payload));
            });
        }

        await new Promise(r => ws.onopen = r);
        const targetRes = await call('Target.createTarget', { url: 'about:blank' });
        const targetId = targetRes.result.targetId;
        const attachRes = await call('Target.attachToTarget', { targetId, flatten: true });
        const sessionId = attachRes.result.sessionId;

        await call('Page.enable', {}, sessionId);
        await call('Runtime.enable', {}, sessionId);
        await call('Emulation.setUserAgentOverride', {
            userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36'
        }, sessionId);

        async function harvestUrl(navUrl, remainingCount, existingResults, seenSet) {
            await call('Page.navigate', { url: navUrl }, sessionId);
            await new Promise(r => setTimeout(r, 4200));

            const maxScrollAttempts = Math.max(15, Math.ceil(remainingCount / 2));
            let lastCount = 0;
            let staleCount = 0;

            for (let s = 0; s < maxScrollAttempts; s++) {
                // 1. Dispatch native CDP mouseWheel event on left sidebar feed
                await call('Input.dispatchMouseEvent', {
                    type: 'mouseWheel',
                    x: 250,
                    y: 500,
                    deltaX: 0,
                    deltaY: 1600
                }, sessionId);

                // 2. Also scroll feed element directly & scroll last item into view
                const countRes = await call('Runtime.evaluate', {
                    expression: `
                    (() => {
                        const feed = document.querySelector('div[role="feed"]') || document.querySelector('div[role="main"]');
                        if (feed) {
                            feed.scrollTop = feed.scrollHeight;
                            const items = feed.querySelectorAll('div[role="article"], a[href*="/maps/place/"]');
                            if (items.length > 0) {
                                items[items.length - 1].scrollIntoView({ behavior: 'auto', block: 'end' });
                            }
                        }
                        return document.querySelectorAll('a[href*="/maps/place/"]').length;
                    })()
                    `,
                    returnByValue: true
                }, sessionId);

                const currentCount = countRes?.result?.result?.value || 0;
                if (currentCount >= remainingCount + seenSet.size) break;

                if (currentCount === lastCount) {
                    staleCount++;
                    if (staleCount >= 5) break;
                } else {
                    staleCount = 0;
                }
                lastCount = currentCount;
                await new Promise(r => setTimeout(r, 1100));
            }

            await new Promise(r => setTimeout(r, 500));

            const evalRes = await call('Runtime.evaluate', {
                expression: `
                (() => {
                    function clean(s) {
                        if (!s) return '';
                        return s.replace(/[\\u0000-\\u001F\\u007F-\\u009F\\uE000-\\uF8FF]/g, ' ')
                                .replace(/[·•|┬ҐÇó]/g, ' ')
                                .replace(/\\s+/g, ' ')
                                .trim();
                    }

                    function sanitizePhone(raw) {
                        if (!raw) return '';
                        let digits = raw.replace(/[^0-9+]/g, '');
                        
                        if (digits.startsWith('+91')) {
                            const local = digits.substring(3);
                            if (local.length === 10) return '+91 ' + local.substring(0, 5) + ' ' + local.substring(5);
                            return '+91 ' + local;
                        }
                        if (digits.startsWith('0') && digits.length === 11) {
                            if (digits.charAt(1) >= '6' && digits.charAt(1) <= '9') {
                                return '+91 ' + digits.substring(1, 6) + ' ' + digits.substring(6);
                            }
                            return digits.substring(0, 4) + ' ' + digits.substring(4, 7) + ' ' + digits.substring(7);
                        }
                        if (digits.length === 10 && digits.charAt(0) >= '6' && digits.charAt(0) <= '9') {
                            return '+91 ' + digits.substring(0, 5) + ' ' + digits.substring(5);
                        }
                        return raw.trim();
                    }

                    const cLat = ${centerLat !== null ? centerLat : 'null'};
                    const cLng = ${centerLng !== null ? centerLng : 'null'};

                    function calcDistance(lat1, lon1, lat2, lon2) {
                        if (lat1 === null || lon1 === null || lat2 === null || lon2 === null) return null;
                        const R = 6371;
                        const dLat = (lat2 - lat1) * Math.PI / 180;
                        const dLon = (lon2 - lon1) * Math.PI / 180;
                        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                                  Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
                                  Math.sin(dLon / 2) * Math.sin(dLon / 2);
                        return Math.round(R * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a)) * 10) / 10;
                    }

                    const anchors = Array.from(document.querySelectorAll('a[href*="/maps/place/"]'));
                    const parsed = [];

                    for (let i = 0; i < anchors.length; i++) {
                        const a = anchors[i];
                        const href = a.href || '';
                        const container = a.closest('div[role="article"]') || a.closest('.Nv2PK') || a.parentElement;
                        if (!container) continue;

                        const titleEl = container.querySelector('.qBF1Pd, .fontHeadlineSmall, [role="heading"], .fontTitleMedium');
                        const title = titleEl ? titleEl.textContent.trim() : (a.getAttribute('aria-label') || '');
                        if (!title) continue;

                        const ratingEl = container.querySelector('.MW4etd, span[aria-label*="stars"], span.ZkP5Je');
                        const rating = ratingEl ? ratingEl.textContent.trim() : '';

                        const reviewEl = container.querySelector('.UY7F9, span[aria-label*="reviews"]');
                        const reviews = reviewEl ? reviewEl.textContent.replace(/[^\\d]/g, '') : '';

                        const allText = container.innerText || '';
                        const rawLines = allText.split('\\n').map(l => clean(l)).filter(Boolean);

                        let phone = '';
                        const phonePatterns = [
                            /\\b0\\d{2,4}\\s*\\d{3,4}\\s*\\d{3,4}\\b/,
                            /(?:\\+91[\\s-]?)?(?:0?[6-9]\\d{4}[\\s-]?\\d{5})\\b/,
                            /(?:\\+91[\\s-]?)?(?:0[1-9]\\d{2,4}[-\\s]?\\d{5,8})\\b/,
                            /\\b0\\d{10}\\b/,
                            /\\b\\d{4}\\s\\d{6}\\b/,
                            /\\b\\d{5}\\s\\d{5}\\b/
                        ];

                        for (const pat of phonePatterns) {
                            const match = allText.match(pat);
                            if (match) {
                                phone = match[0].trim();
                                break;
                            }
                        }

                        let address = '';
                        for (const line of rawLines) {
                            if (line === title || line.includes('Directions') || line.includes('Website') || line.startsWith(rating)) continue;
                            if (line.includes('Open') || line.includes('Closed') || line.includes('Closes') || line.includes('Opens')) continue;
                            if (line.length > 6) {
                                address = line;
                                break;
                            }
                        }

                        let website = '';
                        const webAnchor = container.querySelector('a[data-value="Website"], a[aria-label*="website"]');
                        if (webAnchor && webAnchor.href && !webAnchor.href.includes('google.com')) {
                            website = webAnchor.href;
                        }

                        // Extract coordinates from place URL
                        let lat = null, lng = null;
                        const m3d = href.match(/!3d(-?\\d+\\.\\d+)!4d(-?\\d+\\.\\d+)/);
                        if (m3d) {
                            lat = parseFloat(m3d[1]);
                            lng = parseFloat(m3d[2]);
                        } else {
                            const mat = href.match(/@(-?\\d+\\.\\d+),(-?\\d+\\.\\d+)/);
                            if (mat) {
                                lat = parseFloat(mat[1]);
                                lng = parseFloat(mat[2]);
                            }
                        }

                        const distKm = (lat !== null && lng !== null && cLat !== null && cLng !== null)
                            ? calcDistance(cLat, cLng, lat, lng)
                            : null;

                        const cleanSnippet = rawLines.filter(l => !l.includes('Directions') && !l.includes('Website') && l !== title).slice(0, 3).join(' • ');

                        parsed.push({
                            title: clean(title),
                            phone: sanitizePhone(phone),
                            address: clean(address).replace(/Directions/gi, '').trim(),
                            rating: clean(rating),
                            reviews: reviews,
                            website: website,
                            url: href,
                            location: (lat !== null && lng !== null) ? (lat.toFixed(6) + ', ' + lng.toFixed(6)) : "",
                            distanceKm: distKm,
                            snippet: cleanSnippet
                        });
                    }
                    return parsed;
                })()
                `,
                returnByValue: true
            }, sessionId);

            const items = evalRes?.result?.result?.value || [];
            for (const item of items) {
                const normKey = (item.title + '|' + item.phone).toLowerCase();
                if (!seenSet.has(normKey)) {
                    seenSet.add(normKey);
                    existingResults.push(item);
                    if (existingResults.length >= maxCount) break;
                }
            }
        }

        const results = [];
        const seen = new Set();

        // 1. Primary Query Execution with Viewport Zoom (only if radiusKm > 0)
        const baseSearchUrl = (radiusKm > 0 && centerLat !== null && centerLng !== null)
            ? `https://www.google.com/maps/search/${encodeURIComponent(query)}/@${centerLat},${centerLng},${getZoomForRadius(radiusKm)}z?hl=en`
            : `https://www.google.com/maps/search/${encodeURIComponent(query)}?hl=en`;

        await harvestUrl(baseSearchUrl, maxCount, results, seen);

        // 2. Multi-Cluster Satellite Expansion if quota remains and radius >= 15 km
        if (results.length < maxCount && radiusKm >= 15 && cleanCityKey) {
            const satellites = SATELLITE_CLUSTERS[cleanCityKey] || [];
            const activeSatellites = satellites.filter(s => s.distKm <= radiusKm);

            function simplifyQuery(q) {
                const base = q.replace(/\s*(?:in|near|within|\(.*?\)|at)\s+.*$/i, '').trim() || q;
                const lower = base.toLowerCase();
                if (lower.includes('transporter') || lower.includes('logistics') || lower.includes('fleet')) return 'Transporters';
                if (lower.includes('retread') || lower.includes('tyre')) return 'Tyres';
                if (lower.includes('mining') || lower.includes('quarry')) return 'Mining contractors';
                if (lower.includes('sports') || lower.includes('badminton')) return 'Sports turf';
                if (lower.includes('gym') || lower.includes('fitness')) return 'Fitness gym';
                if (lower.includes('school')) return 'Schools';
                return base.split(' ').slice(0, 2).join(' ');
            }

            const cleanBaseCategory = simplifyQuery(query);

            for (const sat of activeSatellites) {
                if (results.length >= maxCount) break;
                // Formulate concise, high-yield satellite query (e.g. Transporters in Udyambag, Belgaum)
                const clusterQuery = `${cleanBaseCategory} in ${sat.name} ${city}`;
                const clusterUrl = `https://www.google.com/maps/search/${encodeURIComponent(clusterQuery)}?hl=en`;

                await harvestUrl(clusterUrl, maxCount - results.length, results, seen);
            }
        }

        ws.close();
        chrome.kill();
        try { fs.rmSync(tempDir, { recursive: true, force: true }); } catch { }

        return results;
    } catch (err) {
        chrome.kill();
        try { fs.rmSync(tempDir, { recursive: true, force: true }); } catch { }
        throw err;
    }
}

const queryArg = process.argv[2] || "Transporters in Belgaum";
const limitArg = parseInt(process.argv[3] || "60");
const cityArg = process.argv[4] || "";
const radiusArg = parseInt(process.argv[5] || "0");

scrapeGoogleMaps(queryArg, limitArg, cityArg, radiusArg)
    .then(results => {
        process.stdout.write(JSON.stringify(results));
    })
    .catch(err => {
        console.error(JSON.stringify({ error: err.message }));
        process.exit(1);
    });
