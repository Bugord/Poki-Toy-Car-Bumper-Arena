// Refreshes the Poki CLI's login before an upload (run by Tools/upload-poki.sh and Tools > Build > Upload to Poki).
// The CLI refreshes an expired access token only once the upload gets a 401 back, but the API sends the 401 and closes
// the connection while the CLI is still sending the build, so the upload fails with ECONNRESET instead, every time,
// once the token has run out (it lasts 2 minutes). So this does the CLI's own refresh (auth.poki.io/auth/refresh) right
// before every upload. If the refresh fails, it removes auth.json, so the CLI signs in in the browser again.
'use strict';
const fs = require('fs');
const https = require('https');
const os = require('os');
const path = require('path');

const configDir = process.platform === 'win32' && process.env.LOCALAPPDATA !== undefined
    ? path.join(process.env.LOCALAPPDATA, 'Poki')
    : process.env.XDG_CONFIG_HOME !== undefined
        ? path.join(process.env.XDG_CONFIG_HOME, 'poki')
        : path.join(os.homedir(), '.config', 'poki');
const configPath = path.join(configDir, 'auth.json');

function log(message) {
    console.log(`poki-auth: ${message}`);
}

function refresh(refreshToken) {
    return new Promise((resolve, reject) => {
        const req = https.request({
            hostname: 'auth.poki.io',
            port: 443,
            path: '/auth/refresh',
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        }, res => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                if (res.statusCode !== 200) {
                    reject(new Error(`HTTP ${res.statusCode}`));
                    return;
                }
                try {
                    resolve(JSON.parse(data).access_token);
                }
                catch (e) {
                    reject(e);
                }
            });
        });
        req.on('error', reject);
        req.write(JSON.stringify({ refresh_token: refreshToken }));
        req.end();
    });
}

async function main() {
    // An upload token (UserSettings/poki-upload-token.txt, see Tools/upload-poki.sh) is taken by the CLI over auth.json.
    if (process.env.POKI_UPLOAD_TOKEN !== undefined) {
        return;
    }

    let config;
    try {
        config = JSON.parse(fs.readFileSync(configPath, 'ascii'));
    }
    catch (e) {
        log('not signed in yet; the CLI opens the browser to sign in');
        return;
    }

    // A token of type Token (an API key) does not expire and is not refreshed.
    if (config.access_type === 'Token' || config.access_token === undefined) {
        return;
    }

    try {
        if (config.refresh_token === undefined) {
            throw new Error('no refresh token');
        }
        config.access_token = await refresh(config.refresh_token);
        fs.writeFileSync(configPath, JSON.stringify(config), 'ascii');
        fs.chmodSync(configPath, 0o600);
    }
    catch (e) {
        fs.rmSync(configPath, { force: true });
        log(`refresh failed (${e.message}); the CLI opens the browser to sign in again`);
    }
}

main().catch(e => {
    console.error(`poki-auth: ${e.message}`);
    process.exit(1);
});
