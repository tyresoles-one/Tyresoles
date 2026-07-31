const https = require('https');

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const makeRequest = (query, variables = {}, token = null) => {
    return new Promise((resolve, reject) => {
        const payload = JSON.stringify({ query, variables });
        const headers = {
            'Content-Type': 'application/json',
            'Content-Length': Buffer.byteLength(payload)
        };
        if (token) {
            headers['Authorization'] = `Bearer ${token}`;
        }
        
        const req = https.request({
            hostname: 'localhost',
            port: 5002,
            path: '/graphql',
            method: 'POST',
            headers: headers
        }, (res) => {
            let data = '';
            res.on('data', (chunk) => data += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(data));
                } catch (e) {
                    reject(new Error(`Failed to parse response: ${data}`));
                }
            });
        });
        
        req.on('error', reject);
        req.write(payload);
        req.end();
    });
};

const run = async () => {
    console.log('Logging in...');
    const loginQuery = `
        mutation Login($username: String!, $password: String!) {
            login(username: $username, password: $password) {
                success
                message
                token
            }
        }
    `;
    
    const loginRes = await makeRequest(loginQuery, {
        username: 'tyresoles\\navservice',
        password: 'passme#3'
    });
    
    console.log('Login response:', JSON.stringify(loginRes, null, 2));
    const token = loginRes?.data?.login?.token;
    if (!token) {
        console.error('Login failed, no token returned!');
        return;
    }
    
    console.log('Executing allocateAgentContacts...');
    const allocateQuery = `
        mutation AllocateAgentContacts {
            allocateAgentContacts {
                success
                message
            }
        }
    `;
    const allocateRes = await makeRequest(allocateQuery, {}, token);
    console.log('Allocation response:', JSON.stringify(allocateRes, null, 2));
    
    console.log('Querying getCrmAgentContacts...');
    const queryAgentContacts = `
        query GetCrmAgentContacts {
            crmAgentContacts: getCrmAgentContacts(take: 5) {
                items {
                    id
                    agentUsername
                    contactId
                    contact {
                        id
                        fullName
                        companyName
                    }
                }
                totalCount
            }
        }
    `;
    const queryRes = await makeRequest(queryAgentContacts, {}, token);
    console.log('Query response:', JSON.stringify(queryRes, null, 2));
};

run().catch(console.error);
