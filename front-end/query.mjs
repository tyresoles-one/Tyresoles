import http from 'http';

const data = JSON.stringify({
  query: 'query Test($type: CrmMasterType!) { getCrmMasterItems(type: $type) { id name } }',
  variables: { type: 'CONTACT_TYPE' }
});

const req = http.request('http://localhost:5242/graphql', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': data.length
  }
}, res => {
  let body = '';
  res.on('data', chunk => body += chunk);
  res.on('end', () => console.log(body));
});
req.write(data);
req.end();
